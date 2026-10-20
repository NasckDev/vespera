using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
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
/// Caçador controlado pelo jogador (spec 01 §4.3 e §5): 8 direções a 78 u/s sem inércia, colisão círculo r=4 no pé
/// (a origem do nó é o pé, então o y-sort é pelo pé), sprite <c>c_{classe}_{front|back|side|atk}</c> do atlas com
/// sombra e "bob" de caminhada, e interação com o <see cref="Interactable"/> mais próximo.
/// Esquiva, ataques e dano são da Fase B: os ganchos são <see cref="SpeedMultiplier"/>, <see cref="MovementLocked"/>,
/// <see cref="AttackPose"/> e <see cref="Aim"/>.
/// </summary>
public partial class Player : CharacterBody2D
{
    public const float BaseSpeed = 78f; // combat.js:83

    private const float MaxDelta = 0.05f; // main.js:18
    private const float IdleTurnDownSeconds = 0.45f; // combat.js:116
    private const float WalkBobFrequency = 11f; // render.js:161
    private const float WalkBobHeight = 1.6f;
    private const float IdleBobFrequency = 2.4f;
    private const float IdleBobHeight = 0.35f;

    private readonly List<Interactable> _nearby = [];
    private readonly string[] _poseKeys = new string[4];
    private string _classId = "sword";
    private Interactable? _focused;
    private float _time;
    private float _walkT;
    private float _idleT;

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

    /// <summary>Nó que espelha (scale.x = -1) e faz o bob do corpo, em volta do pé.</summary>
    [Export]
    public Node2D Body { get; set; } = null!;

    [Export]
    public AtlasSprite Sprite { get; set; } = null!;

    /// <summary>Área que detecta Interactables (máscara na camada 6). Um ponto no pé.</summary>
    [Export]
    public Area2D InteractionArea { get; set; } = null!;

    /// <summary>Última direção de movimento; usada para animação e direção do ataque.</summary>
    public Vector2 Facing { get; private set; } = Vector2.Down;

    public FacingDirection Direction { get; private set; } = FacingDirection.Down;

    /// <summary>Andou neste frame de física (input de movimento ativo).</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Multiplicador de velocidade (equipamentos, gelo, ataque…). Gancho da Fase B.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Trava o movimento por input (esquiva, golpes). Gancho da Fase B.</summary>
    public bool MovementLocked { get; set; }

    /// <summary>Desenha a pose de ataque olhando para <see cref="Aim"/> (<c>c_*_atk</c>). Gancho da Fase B.</summary>
    public bool AttackPose { get; set; }

    /// <summary>Ângulo de mira em radianos (0 = direita).</summary>
    public float Aim { get; set; } = MathF.PI / 2;

    /// <summary>Interagível em foco (o prompt do HUD), ou null.</summary>
    public Interactable? FocusedInteractable => _focused;

    public override void _Ready()
    {
        InteractionArea.CollisionLayer = 0;
        InteractionArea.CollisionMask = Interactable.InteractablesLayer;
        InteractionArea.AreaEntered += OnAreaEntered;
        InteractionArea.AreaExited += OnAreaExited;
        UpdatePoseKeys();
        UpdateVisual();
    }

    public override void _ExitTree()
    {
        InteractionArea.AreaEntered -= OnAreaEntered;
        InteractionArea.AreaExited -= OnAreaExited;
        SetFocused(null);
    }

    /// <summary>Coloca o jogador num ponto (troca de mapa), parado e sem foco de interação.</summary>
    public void Teleport(Vector2 globalPosition)
    {
        GlobalPosition = globalPosition;
        Velocity = Vector2.Zero;
        IsMoving = false;
        SetFocused(null); // a lista de próximos segue os sinais da física (entrar/sair), não é zerada aqui
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = MathF.Min(MaxDelta, (float)delta);
        _time += dt;

        var locked = GameManager.Instance.IsGameplayBlocked || MovementLocked;
        var input = locked
            ? Vector2.Zero
            : Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);

        // combat.js:78-92 — velocidade instantânea, sem aceleração.
        var speed = MoveSpeed * SpeedMultiplier;
        Velocity = input * speed;
        MoveAndSlide();

        IsMoving = input != Vector2.Zero;
        if (IsMoving)
        {
            Facing = input;
            _walkT += dt * (speed * input.Length() / BaseSpeed);
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

        UpdateVisual();
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

    /// <summary>render.js:157-171 — pose, espelho e bob.</summary>
    private void UpdateVisual()
    {
        if (Sprite is null || Body is null)
        {
            return;
        }

        Sprite.Key = AttackPose ? _poseKeys[3] : Direction switch
        {
            FacingDirection.Up => _poseKeys[1],
            FacingDirection.Down => _poseKeys[0],
            _ => _poseKeys[2],
        };

        var flip = AttackPose ? MathF.Cos(Aim) < 0 : Direction == FacingDirection.Left;
        var bob = IsMoving
            ? -MathF.Abs(MathF.Sin(_walkT * WalkBobFrequency)) * WalkBobHeight
            : MathF.Sin(_time * IdleBobFrequency) * IdleBobHeight;
        Body.Scale = new Vector2(flip ? -1f : 1f, 1f);
        Body.Position = new Vector2(0f, bob);
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
