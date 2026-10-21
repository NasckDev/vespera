using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Player;

/// <summary>Direção visual da v2 (<c>p.dir</c>).</summary>
public enum FacingDirection
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>
/// Caçador controlado pelo jogador (spec 01 §4.3 e §5): 8 direções a 78 u/s, colisão círculo no pé (a origem do nó é o
/// pé, então o y-sort é pelo pé) e interação com o <see cref="Interactable"/> mais próximo.
/// Movimento (DECISIONS D42): aceleração e frenagem muito curtas (<c>rules.world.movement</c>), correção de quina, e o
/// corpo é desenhado na posição interpolada entre ticks de física (<c>physics_interpolation</c>), que a câmera também
/// segue (<see cref="VisualPosition"/>). Visual em <see cref="PlayerAnimator"/> (quadros do kit para a Lâmina, caminhada
/// procedural, poeira e passos). Ganchos do combate: <see cref="SpeedMultiplier"/>, <see cref="MovementLocked"/>,
/// <see cref="AttackPose"/>, <see cref="Aim"/>, <see cref="PlayAttack"/>, <see cref="PlayHurt"/> e <see cref="PlayDeath"/>.
/// </summary>
public partial class Player : CharacterBody2D, ICameraTarget
{
    public const float BaseSpeed = 78f; // combat.js:83

    private const float MaxDelta = 0.05f; // main.js:18
    private const float IdleTurnDownSeconds = 0.45f; // combat.js:116
    private const float DefaultAccelSeconds = 0.06f;
    private const float DefaultDecelSeconds = 0.05f;

    private readonly List<Interactable> _nearby = [];
    private readonly string[] _poseKeys = new string[4];
    private string _classId = "sword";
    private Interactable? _focused;
    private float _idleT;
    private Vector2 _previousPhysicsPosition;
    private Vector2 _currentPhysicsPosition;
    private float _accelSeconds = DefaultAccelSeconds;
    private float _decelSeconds = DefaultDecelSeconds;

    [Export]
    public float MoveSpeed { get; set; } = BaseSpeed;

    /// <summary>
    /// Classe/arma que define o visual (<c>s.cur</c> da v2: sword, bow, staff, hammer, dag). Novo save da v2 começa
    /// em <c>sword</c> (<c>core.js:211</c>).
    /// </summary>
    [Export]
    public string ClassId
    {
        get => _classId;
        set
        {
            _classId = value;
            UpdatePoseKeys();
        }
    }

    /// <summary>Nó que espelha (scale.x = -1) e recebe a pose do corpo, em volta do pé.</summary>
    [Export]
    public Node2D Body { get; set; } = null!;

    [Export]
    public AtlasSprite Sprite { get; set; } = null!;

    /// <summary>Área que detecta Interactables (máscara na camada 6). Um ponto no pé.</summary>
    [Export]
    public Area2D InteractionArea { get; set; } = null!;

    [Export]
    public PlayerAnimator Animator { get; set; } = null!;

    /// <summary>Última direção de movimento; usada para animação e direção do ataque.</summary>
    public Vector2 Facing { get; private set; } = Vector2.Down;

    public FacingDirection Direction { get; private set; } = FacingDirection.Down;

    /// <summary>Andou neste frame de física (input de movimento ativo).</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Multiplicador de velocidade (equipamentos, gelo, ataque…). Gancho da Fase B.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Trava o movimento por input (esquiva, golpes). Gancho da Fase B.</summary>
    public bool MovementLocked { get; set; }

    /// <summary>Desenha a pose de ataque olhando para <see cref="Aim"/> (<c>c_*_atk</c>; Lâmina: quadros do golpe). Gancho da Fase B.</summary>
    public bool AttackPose { get; set; }

    /// <summary>Ângulo de mira em radianos (0 = direita).</summary>
    public float Aim { get; set; } = MathF.PI / 2;

    /// <summary>Interagível em foco (o prompt do HUD), ou null.</summary>
    public Interactable? FocusedInteractable => _focused;

    /// <summary>Pé como desenhado neste quadro: interpolado entre os dois últimos ticks de física.</summary>
    public Vector2 VisualPosition =>
        IsInsideTree() && GetTree().PhysicsInterpolation
            ? _previousPhysicsPosition.Lerp(_currentPhysicsPosition, (float)Engine.GetPhysicsInterpolationFraction())
            : GlobalPosition;

    /// <summary>Altura do corpo em u (32 u do atlas × <c>characterScale</c>).</summary>
    public float VisualHeight => WorldScale.CharacterHeight;

    public bool IsDead => Animator.IsDead;

    public override void _Ready()
    {
        InteractionArea.CollisionLayer = 0;
        InteractionArea.CollisionMask = Interactable.InteractablesLayer;
        InteractionArea.AreaEntered += OnAreaEntered;
        InteractionArea.AreaExited += OnAreaExited;
        if (WorldScale.Rules is { } rules)
        {
            _accelSeconds = (float)rules.Movement.AccelSeconds;
            _decelSeconds = (float)rules.Movement.DecelSeconds;
        }

        UpdatePoseKeys();
        Animator.Setup(this, Body, Sprite);
        _previousPhysicsPosition = _currentPhysicsPosition = GlobalPosition;
    }

    public override void _ExitTree()
    {
        InteractionArea.AreaEntered -= OnAreaEntered;
        InteractionArea.AreaExited -= OnAreaExited;
        SetFocused(null);
    }

    /// <summary>Coloca o jogador num ponto (troca de mapa), parado, sem rastro de interpolação e sem foco de interação.</summary>
    public void Teleport(Vector2 globalPosition)
    {
        GlobalPosition = globalPosition;
        Velocity = Vector2.Zero;
        IsMoving = false;
        _previousPhysicsPosition = _currentPhysicsPosition = globalPosition;
        ResetPhysicsInterpolation();
        Animator?.ResetMotion();
        SetFocused(null); // a lista de próximos segue os sinais da física (entrar/sair), não é zerada aqui
    }

    /// <summary>Golpe (combate chama em cada ataque). A Lâmina toca os quadros do kit; as outras classes, <c>c_*_atk</c>.</summary>
    public void PlayAttack(float duration = 0.3f) => Animator.PlayAttack(duration);

    /// <summary>Levou dano: clarão branco e tremor curto.</summary>
    public void PlayHurt() => Animator.PlayHurt();

    /// <summary>Morte: ajoelha e cai (Lâmina) ou gira até −90° (v2). Fica assim até <see cref="Revive"/>.</summary>
    public void PlayDeath() => Animator.PlayDeath();

    public void Revive() => Animator.Revive();

    /// <summary>Chave de pose do atlas: 0 frente, 1 costas, 2 lado, 3 ataque.</summary>
    public string PoseKey(int index) => _poseKeys[index];

    /// <summary>Até quantos px o jogador é deslizado de lado para contornar a quina de uma passagem.</summary>
    private const float CornerNudgeMax = 5f;

    /// <summary>
    /// Correção de quina (estilo Zelda/Hades): andando reto contra a borda de uma passagem, se um pequeno
    /// deslocamento lateral libera o caminho, desliza o jogador para lá em vez de travar na face da parede.
    /// Não aloca: só <see cref="PhysicsBody2D.TestMove"/> com structs.
    /// </summary>
    private void ApplyCornerCorrection(Vector2 input, float step)
    {
        if (input == Vector2.Zero || step <= 0.05f)
        {
            return;
        }

        // Só em movimento quase reto num eixo; na diagonal o MoveAndSlide já desliza.
        Vector2 forward;
        if (Mathf.Abs(input.Y) > 0.9f && Mathf.Abs(input.X) < 0.4f)
        {
            forward = new Vector2(0, Mathf.Sign(input.Y));
        }
        else if (Mathf.Abs(input.X) > 0.9f && Mathf.Abs(input.Y) < 0.4f)
        {
            forward = new Vector2(Mathf.Sign(input.X), 0);
        }
        else
        {
            return;
        }

        var probe = forward * Mathf.Max(step, 1f);
        if (!TestMove(GlobalTransform, probe))
        {
            return; // não está bloqueado
        }

        var side = new Vector2(-forward.Y, forward.X);
        for (var offset = 1f; offset <= CornerNudgeMax; offset += 1f)
        {
            foreach (var sign in CornerSigns)
            {
                var shifted = GlobalTransform.Translated(side * (offset * sign));
                if (!TestMove(GlobalTransform, side * (offset * sign)) && !TestMove(shifted, probe))
                {
                    // desliza na velocidade normal, sem teleporte
                    MoveAndCollide(side * (sign * Mathf.Min(offset, step)));
                    return;
                }
            }
        }
    }

    private static readonly float[] CornerSigns = [1f, -1f];

    public override void _PhysicsProcess(double delta)
    {
        var dt = MathF.Min(MaxDelta, (float)delta);
        _previousPhysicsPosition = _currentPhysicsPosition;

        var locked = GameManager.Instance.IsGameplayBlocked || MovementLocked || IsDead;
        var input = locked
            ? Vector2.Zero
            : Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);

        // D42: aceleração/frenagem curtas sobre a velocidade da v2 (combat.js:78-92), nunca acima da máxima.
        var speed = MoveSpeed * SpeedMultiplier;
        var v = Locomotion.Step(new MapPoint(Velocity.X, Velocity.Y), new MapPoint(input.X, input.Y), speed, _accelSeconds, _decelSeconds, dt);
        Velocity = new Vector2(v.X, v.Y);
        var desiredStep = Velocity.Length() * dt;
        var before = GlobalPosition;
        MoveAndSlide();
        // O deslize de quina usa só o que sobrou do passo do frame: nunca anda mais rápido que a velocidade atual.
        ApplyCornerCorrection(input, desiredStep - GlobalPosition.DistanceTo(before));

        IsMoving = input != Vector2.Zero;
        if (IsMoving)
        {
            Facing = input;
            _idleT = 0f;
            if (!AttackPose)
            {
                Direction = MathF.Abs(input.X) > MathF.Abs(input.Y) * 0.9f
                    ? (input.X < 0 ? FacingDirection.Left : FacingDirection.Right)
                    : (input.Y < 0 ? FacingDirection.Up : FacingDirection.Down);
            }
        }
        else if (!AttackPose)
        {
            _idleT += dt;
            if (_idleT > IdleTurnDownSeconds)
            {
                Direction = FacingDirection.Down; // olha para a câmera
            }
        }

        _currentPhysicsPosition = GlobalPosition;
        SetFocused(GameManager.Instance.IsGameplayBlocked ? null : FindClosestInteractable());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_focused is not null && !GameManager.Instance.IsGameplayBlocked && @event.IsActionPressed(InputActions.Interact))
        {
            GetViewport().SetInputAsHandled();
            _focused.Interact(this);
        }
    }

    private void UpdatePoseKeys()
    {
        _poseKeys[0] = $"c_{_classId}_front";
        _poseKeys[1] = $"c_{_classId}_back";
        _poseKeys[2] = $"c_{_classId}_side";
        _poseKeys[3] = $"c_{_classId}_atk";
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is Interactable it && !_nearby.Contains(it))
        {
            _nearby.Add(it);
        }
    }

    private void OnAreaExited(Area2D area)
    {
        if (area is Interactable it)
        {
            _nearby.Remove(it);
        }
    }

    /// <summary>world.js:198 — o mais próximo com distância &lt; r do pé.</summary>
    private Interactable? FindClosestInteractable()
    {
        Interactable? closest = null;
        var bestDistance = float.MaxValue;
        for (var i = _nearby.Count - 1; i >= 0; i--)
        {
            var candidate = _nearby[i];
            if (!IsInstanceValid(candidate) || !candidate.IsInsideTree())
            {
                _nearby.RemoveAt(i); // liberado na troca de mapa
                continue;
            }

            if (!candidate.Enabled)
            {
                continue;
            }

            var distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
            if (candidate.Radius > 0 && distance >= candidate.Radius * candidate.Radius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void SetFocused(Interactable? interactable)
    {
        if (interactable == _focused)
        {
            return;
        }

        _focused = interactable;
        EventBus.Instance?.EmitSignal(EventBus.SignalName.InteractionPromptChanged, interactable?.Prompt ?? string.Empty);
    }
}
