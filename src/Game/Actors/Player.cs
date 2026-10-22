using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Components;

namespace Vespera.Game.Actors;

/// <summary>
/// Caçador controlado por uma <see cref="IInputSource"/> (nunca por <c>Input</c> direto). Anda em 8 direções com
/// aceleração e frenagem, colide só pelo pé (cápsula deitada) e anima quadro a quadro em 4 direções.
/// A cadência da animação acompanha a velocidade real para o pé não deslizar no chão.
/// </summary>
public partial class Player : CharacterBody2D
{
    public const string Group = "players";
    public const uint WorldLayer = 1;
    public const uint PlayerLayer = 2;

    /// <summary>Velocidade máxima na escala 1 (px/s). Casada com a passada: 6 quadros a 12 fps cobrem 2 passos de ~52 px.</summary>
    public const float BaseSpeed = 210f;
    public const float FeetHalfWidth = 22f;
    public const float FeetHalfHeight = 11f;

    private const float Acceleration = 1500f;
    private const float Deceleration = 2100f;
    private const float FacingHysteresis = 1.15f;
    private const float BreathPeriod = 2.6f;
    private const float BreathAmount = 0.012f;

    private readonly List<Interactable> _nearby = [];
    private AnimatedSprite2D _sprite = null!;
    private Facing _facing = Facing.Down;
    private bool _faceLeft;
    private float _breath;
    private float _scale = 1f;

    public Player(IInputSource input, HunterClass hunter, float actorScale)
    {
        Input = input;
        Hunter = hunter;
        _scale = actorScale;
    }

    public IInputSource Input { get; set; }

    public HunterClass Hunter { get; }

    public float MaxSpeed => BaseSpeed * _scale;

    /// <summary>Movimento travado (painel de leitura aberto, cutscene).</summary>
    public bool Frozen { get; set; }

    public Facing Facing => _facing;

    public bool FacingLeft => _faceLeft;

    /// <summary>Retângulo do colisor do pé em coordenadas do mundo.</summary>
    public Rect2 FeetRect => new(GlobalPosition.X - (FeetHalfWidth * _scale), GlobalPosition.Y - (2 * FeetHalfHeight * _scale),
        2 * FeetHalfWidth * _scale, 2 * FeetHalfHeight * _scale);

    /// <summary>Retângulo aproximado do corpo desenhado (para a oclusão).</summary>
    public Rect2 BodyRect => new(GlobalPosition.X - (40 * _scale), GlobalPosition.Y - (124 * _scale), 80 * _scale, 124 * _scale);

    public string CurrentAnimation => _sprite.Animation;

    public int CurrentFrame => _sprite.Frame;

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

        _sprite = new AnimatedSprite2D
        {
            Name = "Sprite",
            SpriteFrames = HunterSprites.Build(Hunter),
            Offset = HunterSprites.FootOffset,
            Scale = new Vector2(_scale, _scale),
        };
        AddChild(_sprite);
        AddChild(ContactShadow.Make(2 * FeetHalfWidth * _scale * 1.25f, 0.42f));
        PlayIdle();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var move = Frozen ? Vector2.Zero : Input.Move;
        var target = move * MaxSpeed;
        var rate = move.LengthSquared() > 0.0001f ? Acceleration : Deceleration;
        Velocity = Velocity.MoveToward(target, rate * _scale * dt);
        MoveAndSlide();
        UpdateAnimation(move, dt);
        UpdateFocus();

        // A borda de "interagir" é consultada todo quadro; travado (lendo), o aperto serve para fechar.
        var pressed = Input.IsJustPressed(InputActions.Interact);
        if (pressed && (Focus is not null || Frozen))
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

    public void EnterRange(Interactable item)
    {
        if (!_nearby.Contains(item))
        {
            _nearby.Add(item);
        }
    }

    public void ExitRange(Interactable item) => _nearby.Remove(item);

    private void UpdateAnimation(Vector2 move, float dt)
    {
        var speed = GetRealVelocity().Length();
        var walking = move.LengthSquared() > 0.0001f && speed > 8f * _scale;
        if (move.LengthSquared() > 0.0001f)
        {
            ChooseFacing(move);
        }

        _sprite.FlipH = _facing == Facing.Side && _faceLeft;
        if (walking)
        {
            var walk = HunterSprites.WalkAnimation(_facing);
            if (_sprite.Animation != walk)
            {
                _sprite.Play(walk);
            }

            _sprite.SpeedScale = Mathf.Clamp(speed / MaxSpeed, 0.35f, 1.2f);
            _sprite.Scale = new Vector2(_scale, _scale);
            _breath = 0f;
            return;
        }

        PlayIdle();
        _breath += dt;
        var rise = 1f + (BreathAmount * (0.5f - (0.5f * Mathf.Cos(_breath * Mathf.Tau / BreathPeriod))));
        _sprite.Scale = new Vector2(_scale, _scale * rise);
    }

    private void PlayIdle()
    {
        var idle = HunterSprites.IdleAnimation(_facing);
        if (_sprite.Animation != idle)
        {
            _sprite.Play(idle);
        }
    }

    /// <summary>Lado quando o eixo X domina com folga; senão cima/baixo. A folga evita trocar de pose nas diagonais.</summary>
    private void ChooseFacing(Vector2 move)
    {
        var ax = Mathf.Abs(move.X);
        var ay = Mathf.Abs(move.Y);
        var current = _facing;
        if (current == Facing.Side ? ax * FacingHysteresis >= ay : ax > ay * FacingHysteresis)
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
