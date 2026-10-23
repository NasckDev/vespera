using System.Collections.Generic;
using Godot;
using Vespera.Game.Components;
using Vespera.Game.World;

namespace Vespera.Game.Actors;

/// <summary>
/// Personagem não jogável: anda quadro a quadro (idle/walk em 4 direções, esquerda espelha o lado), tem colisão no pé,
/// rotina simples (parado ou patrulha entre pontos), vira o rosto para o jogador na conversa e expõe uma
/// <see cref="Interactable"/> do tipo "npc" que a fase liga à fala. Origem no pé, como o jogador (y-sort).
/// </summary>
public partial class Npc : CharacterBody2D
{
    public const uint NpcLayer = Player.WorldLayer;
    private const float FeetRadius = 16f;
    private const float PromptMargin = 14f;
    private const float WalkSpeedFactor = 0.42f;
    private const float ArriveDistance = 4f;
    private const float WaitMin = 2.2f;
    private const float WaitSpread = 2.6f;
    private const float TalkReach = 150f;
    private const float StuckGiveUp = 0.8f;

    private readonly string _id;
    private readonly NpcPlacement _placement;
    private readonly float _scale;
    private readonly float _hunterScale;
    private readonly bool _hasWalk;
    private readonly List<Vector2> _path = [];
    private AnimatedSprite2D _sprite = null!;
    private CollisionShape2D _feet = null!;
    private Facing _facing = Facing.Down;
    private bool _faceLeft;
    private bool _held;
    private bool _active = true;
    private int _target;
    private float _wait;
    private float _stuck;
    private int _patrolStep;

    /// <param name="humanHeight">Altura humana do mapa (0,75 × porta pintada); a escala vem do <see cref="ScaleRegistry"/>.</param>
    public Npc(string id, NpcPlacement placement, float humanHeight)
    {
        _id = id;
        _placement = placement;
        _scale = ScaleRegistry.SpriteScale(id, humanHeight);
        _hunterScale = humanHeight / ScaleRules.ReferenceHuman;
        _hasWalk = NpcSprites.HasWalk(id);
    }

    /// <summary>Escala do sprite (altura-alvo ÷ altura em pé).</summary>
    public float SpriteScale => _scale;

    /// <summary>Altura do corpo em px de mundo (altura em pé x escala).</summary>
    public float BodyHeight => ScaleRegistry.StandingHeight(_id) * _scale;

    private float PromptHeight => BodyHeight + PromptMargin;

    public string NpcId => _id;

    public Interactable Interaction { get; private set; } = null!;

    public Facing Facing => _facing;

    public bool FacingLeft => _faceLeft;

    public string CurrentAnimation => _sprite.Animation;

    public bool IsWalking => _sprite.Animation.ToString().StartsWith("walk", System.StringComparison.Ordinal);

    /// <summary>Retângulo do colisor do pé em coordenadas do mundo.</summary>
    public Rect2 FeetRect => new(GlobalPosition.X - (FeetRadius * _scale), GlobalPosition.Y - (2 * FeetRadius * _scale),
        2 * FeetRadius * _scale, 2 * FeetRadius * _scale);

    public override void _Ready()
    {
        CollisionLayer = NpcLayer;
        CollisionMask = Player.WorldLayer | Player.PlayerLayer;
        MotionMode = MotionModeEnum.Floating;
        SafeMargin = Mathf.Max(0.08f, 0.5f * _scale);

        _feet = new CollisionShape2D
        {
            Name = "Feet",
            Shape = new CircleShape2D { Radius = FeetRadius * _scale },
            Position = new Vector2(0, -FeetRadius * _scale),
        };
        AddChild(_feet);

        _sprite = new AnimatedSprite2D
        {
            Name = "Sprite",
            SpriteFrames = NpcSprites.Build(_id),
            Offset = NpcSprites.FootOffset,
            Scale = new Vector2(_scale, _scale),
        };
        AddChild(_sprite);
        AddChild(ContactShadow.Make(2 * FeetRadius * _scale * 1.5f, 0.42f));

        Interaction = new Interactable("npc", string.IsNullOrEmpty(_placement.Prompt) ? "Conversar" : _placement.Prompt, TalkReach * _scale)
        {
            Name = "Talk",
            Dialogue = _placement.Dialogue.Length > 0 ? _placement.Dialogue : _id,
            OwnerNpc = this,
            PromptAnchor = Vector2.Zero,
        };
        AddChild(Interaction);
        Interaction.PromptAnchor = GlobalPosition + new Vector2(0, -PromptHeight);

        _path.Add(GlobalPosition);
        foreach (var point in _placement.Path)
        {
            _path.Add(new Vector2(point[0], point[1]));
        }

        SetFacingFromName(_placement.Facing);
        _wait = WaitMin + ((_id.GetHashCode() & 7) * 0.2f);
        Play(walking: false);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        Interaction.PromptAnchor = GlobalPosition + new Vector2(0, -PromptHeight);
        if (!_active || _held || _placement.Routine != "patrol" || _path.Count < 2)
        {
            Velocity = Vector2.Zero;
            Play(walking: false);
            return;
        }

        if (_wait > 0f)
        {
            _wait -= dt;
            Velocity = Vector2.Zero;
            Play(walking: false);
            return;
        }

        var goal = _path[_target];
        var to = goal - GlobalPosition;
        if (to.Length() <= ArriveDistance * _scale + 1f)
        {
            NextWaypoint();
            return;
        }

        var dir = to.Normalized();
        Velocity = dir * Player.BaseSpeed * _hunterScale * WalkSpeedFactor;
        MoveAndSlide();
        FaceDirection(dir);
        Play(walking: true);
        _stuck = GetRealVelocity().Length() < 2f * _scale ? _stuck + dt : 0f;
        if (_stuck > StuckGiveUp)
        {
            NextWaypoint();
        }
    }

    /// <summary>Segura a rotina (conversa em andamento): para e, se houver jogador, vira o rosto para ele.</summary>
    public void Hold(bool held, Vector2? lookAt = null)
    {
        _held = held;
        if (held && lookAt is { } point)
        {
            FaceTowards(point);
        }

        if (!held)
        {
            _wait = WaitMin;
            SetFacingFromName(_placement.Facing);
        }

        Play(walking: false);
    }

    /// <summary>Liga/desliga o NPC como obstáculo e como patrulheiro (o bot de colisão testa a fase sem eles).</summary>
    public void SetActive(bool active)
    {
        _active = active;
        _feet.SetDeferred(CollisionShape2D.PropertyName.Disabled, !active);
        Interaction.Enabled = active;
    }

    public void FaceTowards(Vector2 point) => FaceDirection(point - GlobalPosition);

    private void NextWaypoint()
    {
        _target = (_target + 1) % _path.Count;
        _patrolStep++;
        _wait = WaitMin + ((_patrolStep * 37 % 5) * WaitSpread / 5f);
        _stuck = 0f;
        Velocity = Vector2.Zero;
    }

    private void FaceDirection(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y) * 1.15f)
        {
            _facing = Facing.Side;
            _faceLeft = direction.X < 0;
        }
        else
        {
            _facing = direction.Y < 0 ? Facing.Up : Facing.Down;
        }

        _sprite.FlipH = _facing == Facing.Side && _faceLeft;
    }

    private void SetFacingFromName(string name)
    {
        switch (name)
        {
            case "up":
                FaceDirection(Vector2.Up);
                break;
            case "left":
                FaceDirection(Vector2.Left);
                break;
            case "right":
                FaceDirection(Vector2.Right);
                break;
            default:
                FaceDirection(Vector2.Down);
                break;
        }
    }

    private void Play(bool walking)
    {
        var animation = NpcSprites.Animation(walking && _hasWalk, _facing);
        _sprite.FlipH = _facing == Facing.Side && _faceLeft;
        if (_sprite.Animation != animation)
        {
            _sprite.Play(animation);
        }
        else if (!_sprite.IsPlaying())
        {
            _sprite.Play();
        }
    }
}
