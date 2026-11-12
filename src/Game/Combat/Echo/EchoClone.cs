using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Actors;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>
/// Clone do Eco do Avesso: uma cópia magenta do caçador que segue ele, vai atrás do monstro mais perto e bate com o
/// ataque do dono por <see cref="EchoDef.Duration"/> segundos.
/// </summary>
public partial class EchoClone : Node2D
{
    private const float Speed = 330f;
    private const float StrikeRange = 95f;
    private const float StrikeAnimSeconds = 0.35f;

    private readonly LevelWorld _world;
    private readonly Player _owner;
    private readonly EchoDef _def;
    private readonly double _attack;
    private readonly AnimatedSprite2D _sprite;
    private float _age;
    private float _strikeCooldown;
    private float _strikeAnim;
    private Facing _facing = Facing.Down;

    public EchoClone(LevelWorld world, Player owner, EchoDef def, double attack)
    {
        _world = world;
        _owner = owner;
        _def = def;
        _attack = attack;
        Name = "EcoAvesso";
        Position = owner.GlobalPosition + new Vector2(-70f * owner.ActorScale, 8f);
        ZIndex = 4;
        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = owner.Sprite.SpriteFrames,
            Offset = owner.Sprite.Offset,
            Scale = owner.Sprite.Scale,
            Modulate = new Color(0.95f, 0.45f, 0.85f, 0.7f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        AddChild(_sprite);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _age += dt;
        if (_age >= _def.Duration || !IsInstanceValid(_owner))
        {
            QueueFree();
            return;
        }

        _strikeCooldown -= dt;
        _strikeAnim -= dt;
        var scale = _owner.ActorScale;
        var target = _world.Combat.Nearest(_owner.GlobalPosition, Team.Hunters, (float)_def.Radius * scale);
        var goal = target is null ? _owner.GlobalPosition + new Vector2(-70f * scale, 8f) : target.HitCenter + new Vector2(0, 40f * scale);
        var to = goal - GlobalPosition;
        var strike = target is not null && target.HitCenter.DistanceTo(GlobalPosition) <= (StrikeRange * scale) + target.HitRadius;
        var moving = !strike && to.Length() > 12f;
        if (moving)
        {
            GlobalPosition = _world.ClampToFloor(GlobalPosition + (to.Normalized() * Speed * scale * dt));
        }

        if (strike && _strikeCooldown <= 0f)
        {
            Strike(target!);
        }

        var look = target is null ? to : target.HitCenter - GlobalPosition;
        Animate(look, moving);
    }

    private void Strike(IHittable target)
    {
        _strikeCooldown = (float)EchoRules.CloneInterval;
        _strikeAnim = StrikeAnimSeconds;
        var dir = (target.HitCenter - GlobalPosition).Normalized();
        var hit = new HitInfo(EchoRules.Damage(_def, _attack), GlobalPosition, dir * 260f * _owner.ActorScale, 0.04, 1, false, 0);
        if (target.ReceiveHit(hit).Landed)
        {
            _world.Feel.Spark(target.HitCenter, new Color(0.95f, 0.45f, 0.85f), 9, 260f, dir, 90f);
        }
    }

    private void Animate(Vector2 look, bool moving)
    {
        if (look.LengthSquared() > 1f)
        {
            var ax = Mathf.Abs(look.X);
            var ay = Mathf.Abs(look.Y);
            _facing = ax >= ay * 0.85f ? Facing.Side : look.Y < 0 ? Facing.Up : Facing.Down;
            _sprite.FlipH = _facing == Facing.Side && look.X < 0;
        }

        var verb = _strikeAnim > 0f ? "ataque1" : moving ? "andar" : "parado";
        var name = HunterSprites.Anim(verb, _facing);
        if (!_sprite.SpriteFrames.HasAnimation(name))
        {
            name = HunterSprites.Anim(moving ? "andar" : "parado", _facing);
        }

        if (_sprite.SpriteFrames.HasAnimation(name) && (_sprite.Animation != name || !_sprite.IsPlaying()))
        {
            _sprite.Play(name);
        }

        var fade = Mathf.Clamp(((float)_def.Duration - _age) / 0.8f, 0f, 1f);
        _sprite.Modulate = _sprite.Modulate with { A = 0.7f * fade };
    }
}
