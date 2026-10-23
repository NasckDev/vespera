using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.World;

namespace Vespera.Game.Actors;

/// <summary>
/// Caçador controlado por uma <see cref="IInputSource"/> (nunca por <c>Input</c> direto). Anda em 8 direções com
/// aceleração e frenagem, colide só pelo pé (cápsula deitada) e anima quadro a quadro em 4 direções.
/// A cadência da animação acompanha a velocidade real para o pé não deslizar no chão. O combate
/// (<see cref="HunterCombat"/>) roda antes do movimento e pode tomar a animação e a velocidade (golpe, esquiva, dano).
/// </summary>
public partial class Player : CharacterBody2D
{
    public const string Group = "players";
    public const uint WorldLayer = 1;
    public const uint PlayerLayer = 2;

    /// <summary>
    /// Velocidade máxima na escala 1 (px/s). Playtest 23/10 ("mapas enormes para andar"): era 210; com 360 a Praça
    /// (x 240→1432, entre as cabeceiras das pontes) se atravessa em menos de 6 s (medido por <c>--escala</c>).
    /// </summary>
    public const float BaseSpeed = 360f;

    /// <summary>Velocidade em que a passada da folha de andar casa com o chão (a animação acelera na mesma razão).</summary>
    public const float StrideSpeed = 210f;
    public const float FeetHalfWidth = 22f;
    public const float FeetHalfHeight = 11f;

    // Arranque e frenagem curtos (~0,08 s até a velocidade máxima): o caçador responde na hora.
    private const float Acceleration = 4800f;
    private const float Deceleration = 6000f;
    private const float FacingHysteresis = 1.15f;

    private readonly List<Interactable> _nearby = [];
    private Facing _facing = Facing.Down;
    private bool _faceLeft;
    private Vector2 _aim = Vector2.Down;
    private readonly float _scale;

    public Player(IInputSource input, HunterClass hunter, float actorScale)
    {
        Input = input;
        Hunter = hunter;
        _scale = actorScale;
    }

    public IInputSource Input { get; set; }

    public HunterClass Hunter { get; private set; }

    public float ActorScale => _scale;

    public float MaxSpeed => BaseSpeed * _scale;

    /// <summary>Movimento travado (painel de leitura aberto, cutscene).</summary>
    public bool Frozen { get; set; }

    public Facing Facing => _facing;

    public bool FacingLeft => _faceLeft;

    /// <summary>Direção da mira (último movimento ou golpe), unitária.</summary>
    public Vector2 AimDirection => _aim;

    public AnimatedSprite2D Sprite { get; private set; } = null!;

    public HitFlash Flash { get; private set; } = null!;

    public HunterCombat Combat { get; private set; } = null!;

    /// <summary>Mundo da sala (posto por <see cref="LevelWorld.SpawnPlayer"/>).</summary>
    public LevelWorld? World { get; set; }

    public CombatFeel? Feel => World?.Feel;

    /// <summary>Retângulo do colisor do pé em coordenadas do mundo.</summary>
    public Rect2 FeetRect => new(GlobalPosition.X - (FeetHalfWidth * _scale), GlobalPosition.Y - (2 * FeetHalfHeight * _scale),
        2 * FeetHalfWidth * _scale, 2 * FeetHalfHeight * _scale);

    /// <summary>Retângulo aproximado do corpo desenhado (para a oclusão).</summary>
    public Rect2 BodyRect => new(GlobalPosition.X - (40 * _scale), GlobalPosition.Y - (124 * _scale), 80 * _scale, 124 * _scale);

    public string CurrentAnimation => Sprite.Animation;

    public int CurrentFrame => Sprite.Frame;

    /// <summary>Interativo mais próximo no alcance, ou null.</summary>
    public Interactable? Focus { get; private set; }

    public event Action<Player, Interactable?>? InteractRequested;

    public override void _Ready()
    {
        AddToGroup(Group);
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = PlayerLayer;
        CollisionMask = WorldLayer;
        SafeMargin = Mathf.Max(0.08f, 0.5f * _scale);

        var feet = new CollisionShape2D
        {
            Name = "Feet",
            Shape = new CapsuleShape2D { Radius = FeetHalfHeight * _scale, Height = 2 * FeetHalfWidth * _scale },
            Rotation = Mathf.Pi / 2f,
            Position = new Vector2(0, -FeetHalfHeight * _scale),
        };
        AddChild(feet);

        var spriteScale = SpriteScaleFor(Hunter);
        Sprite = new AnimatedSprite2D
        {
            Name = "Sprite",
            SpriteFrames = HunterSprites.Build(Hunter),
            Offset = HunterSprites.FootOffset,
            Scale = new Vector2(spriteScale, spriteScale),
        };
        AddChild(Sprite);
        Flash = new HitFlash(Sprite);
        AddChild(ContactShadow.Make(2 * FeetHalfWidth * _scale * 1.25f, 0.42f));
        Combat = new HunterCombat(this);
        AddChild(Combat);
        PlayIdle();
        World?.Combat.Register(Combat);
    }

    public override void _ExitTree() => World?.Combat.Unregister(Combat);

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var move = Frozen ? Vector2.Zero : Input.Move;
        var order = Combat.Step(dt, move);
        if (order.Velocity is { } forced)
        {
            Velocity = forced;
        }
        else
        {
            var target = move * MaxSpeed * order.SpeedScale;
            var rate = target.LengthSquared() > 0.0001f ? Acceleration : Deceleration;
            Velocity = Velocity.MoveToward(target, rate * _scale * dt);
        }

        MoveAndSlide();
        if (!order.OwnsAnimation)
        {
            UpdateAnimation(order.SpeedScale > 0f ? move : Vector2.Zero);
        }

        Flash.Update(dt);
        UpdateFocus();

        // A borda de "interagir" é consultada todo quadro; travado (lendo), o aperto serve para fechar.
        var pressed = Input.IsJustPressed(InputActions.Interact);
        if (pressed && (Focus is not null || Frozen) && Combat.State == HunterState.Free && !Combat.Moves.Busy)
        {
            InteractRequested?.Invoke(this, Focus);
        }
    }

    public void Teleport(Vector2 feet)
    {
        GlobalPosition = feet;
        Velocity = Vector2.Zero;
        ResetPhysicsInterpolation();
    }

    /// <summary>Troca de classe (seletor de arma): outra folha e outro conjunto de golpes.</summary>
    public void SetHunter(HunterClass hunter)
    {
        Hunter = hunter;
        Sprite.SpriteFrames = HunterSprites.Build(hunter);
        Sprite.Scale = Vector2.One * SpriteScaleFor(hunter);
        Combat.ChangeWeapon();
        PlayIdle(force: true);
    }

    /// <summary>Escala da folha pelo contrato de escala (altura humana do mapa = actorScale × 125).</summary>
    private float SpriteScaleFor(HunterClass hunter) =>
        ScaleRegistry.SpriteScale(HunterSprites.ClassFolder(hunter), _scale * ScaleRules.ReferenceHuman);

    /// <summary>Vira para a direção (8 direções → baixo/cima/lado + espelho) e guarda a mira.</summary>
    public void Face(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.0001f)
        {
            return;
        }

        _aim = dir.Normalized();
        var ax = Mathf.Abs(dir.X);
        var ay = Mathf.Abs(dir.Y);
        _facing = ax >= ay * 0.85f ? Facing.Side : dir.Y < 0 ? Facing.Up : Facing.Down;
        if (ax > 0.05f)
        {
            _faceLeft = dir.X < 0;
        }
    }

    /// <summary>
    /// Toca a animação do verbo na direção atual (cai para o lado quando a folha não tem a direção) e devolve o nome.
    /// <paramref name="fitSeconds"/> &gt; 0 ajusta a velocidade para a animação durar esse tempo.
    /// </summary>
    public string PlayAnimation(string verb, bool restart, float fitSeconds = 0f)
    {
        var frames = Sprite.SpriteFrames;
        var name = HunterSprites.Resolve(frames, verb, _facing);
        var side = name.EndsWith("_lado", StringComparison.Ordinal);
        Sprite.FlipH = side && _faceLeft;
        Sprite.SpeedScale = 1f;
        if (fitSeconds > 0f)
        {
            var natural = frames.GetFrameCount(name) / (float)frames.GetAnimationSpeed(name);
            Sprite.SpeedScale = natural / fitSeconds;
        }

        if (restart || Sprite.Animation != name)
        {
            Sprite.Play(name);
            Sprite.Frame = 0;
        }

        return name;
    }

    public void EnterRange(Interactable item)
    {
        if (!_nearby.Contains(item))
        {
            _nearby.Add(item);
        }
    }

    public void ExitRange(Interactable item) => _nearby.Remove(item);

    private void UpdateAnimation(Vector2 move)
    {
        var speed = GetRealVelocity().Length();
        var walking = move.LengthSquared() > 0.0001f && speed > 8f * _scale;
        if (move.LengthSquared() > 0.0001f)
        {
            ChooseFacing(move);
            _aim = move.Normalized();
        }

        Sprite.FlipH = _facing == Facing.Side && _faceLeft;
        if (walking)
        {
            var walk = HunterSprites.WalkAnimation(_facing);
            if (Sprite.Animation != walk)
            {
                Sprite.Play(walk);
            }

            Sprite.SpeedScale = Mathf.Clamp(speed / MaxSpeed, 0.35f, 1.2f) * (BaseSpeed / StrideSpeed);
            return;
        }

        PlayIdle();
    }

    private void PlayIdle(bool force = false)
    {
        var idle = HunterSprites.IdleAnimation(_facing);
        if (force || Sprite.Animation != idle)
        {
            Sprite.Play(idle);
        }

        Sprite.SpeedScale = 1f;
    }

    /// <summary>Lado quando o eixo X domina com folga; senão cima/baixo. A folga evita trocar de pose nas diagonais.</summary>
    private void ChooseFacing(Vector2 move)
    {
        var ax = Mathf.Abs(move.X);
        var ay = Mathf.Abs(move.Y);
        if (_facing == Facing.Side ? ax * FacingHysteresis >= ay : ax > ay * FacingHysteresis)
        {
            _facing = Facing.Side;
            if (ax > 0.05f)
            {
                _faceLeft = move.X < 0;
            }

            return;
        }

        _facing = move.Y < 0 ? Facing.Up : Facing.Down;
    }

    private void UpdateFocus()
    {
        Interactable? best = null;
        var bestDistance = float.MaxValue;
        foreach (var item in _nearby)
        {
            if (!IsInstanceValid(item) || !item.Enabled)
            {
                continue;
            }

            var distance = item.GlobalPosition.DistanceSquaredTo(GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = item;
            }
        }

        Focus = best;
    }
}
