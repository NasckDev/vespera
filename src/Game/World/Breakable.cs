using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.Hunter;

namespace Vespera.Game.World;

/// <summary>
/// Barril, caixote ou jarro que quebra com um golpe (playtest 23/10: "pouca interação na cidade"): solta lascas e
/// poeira, faz barulho de madeira e deixa alguns Lumes (às vezes um Fragmento, na Brecha). Na cidade fica quebrado
/// até a próxima volta da Brecha (<see cref="HuntService.IsBroken"/>). Sólido pelo pé como um objeto comum.
/// </summary>
public partial class Breakable : Node2D, IHittable
{
    private static readonly Color Wood = new("8a5a34");
    private static readonly Color Dust = new("cdb89a");
    private static readonly Color LumeColor = new("ffd27a");

    private readonly PropDef _def;
    private readonly LevelWorld _world;
    private Sprite2D _sprite = null!;
    private StaticBody2D? _body;
    private bool _broken;

    public Breakable(string type, PropDef def, LevelWorld world, string key)
    {
        Type = type;
        _def = def;
        _world = world;
        Key = key;
        Name = $"breakable_{type}";
    }

    public string Type { get; }

    /// <summary>Id no "dia" (mapa + índice) para a cidade lembrar o que já quebrou.</summary>
    public string Key { get; }

    public bool Broken => _broken;

    public Team Team => Team.Monsters;

    public Vector2 HitCenter => GlobalPosition - new Vector2(0, _def.Anchor[1] * _def.Scale * 0.45f);

    public float HitRadius => Mathf.Max(_def.WorldFootprint.W * 0.5f, 18f * _world.Data.ActorScale);

    public bool CanBeHit => !_broken && IsInsideTree();

    /// <summary>Pegada sólida no mundo (vazia depois de quebrar).</summary>
    public Rect2 FootprintRect
    {
        get
        {
            if (_broken)
            {
                return new Rect2();
            }

            var f = _def.WorldFootprint;
            return new Rect2(GlobalPosition.X + f.X, GlobalPosition.Y + f.Y, f.W, f.H);
        }
    }

    public override void _Ready()
    {
        _sprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = GD.Load<Texture2D>(_def.Texture),
            Centered = false,
            Offset = -new Vector2(_def.Anchor[0], _def.Anchor[1]),
            Scale = new Vector2(_def.Scale, _def.Scale),
        };
        AddChild(_sprite);
        var f = _def.WorldFootprint;
        AddChild(ContactShadow.Make(f.W * 1.15f, 0.38f));
        _body = new StaticBody2D { Name = "Footprint", CollisionLayer = Player.WorldLayer, CollisionMask = 0 };
        _body.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(f.W, f.H) },
            Position = new Vector2(f.X + (f.W / 2f), f.Y + (f.H / 2f)),
        });
        AddChild(_body);
        _world.Combat.Register(this);
    }

    public override void _ExitTree() => _world.Combat.Unregister(this);

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!CanBeHit)
        {
            return HitOutcome.Miss;
        }

        _broken = true;
        _world.Combat.Unregister(this);
        _body?.QueueFree();
        _body = null;
        var center = HitCenter;
        var push = hit.Push.LengthSquared() > 1f ? hit.Push.Normalized() : Vector2.Up;
        _world.Feel.Spark(center, Wood, 22, 360f, push, 120f);
        _world.Feel.Spark(center, Dust, 14, 180f, Vector2.Up, 160f);
        _world.Feel.Shake(0.15f);
        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -4f);
        WorldSfx.Play(_world, WorldSound.DoorLocked, GlobalPosition);
        var (lumes, fragments) = HuntService.BreakableLoot(Key);
        var human = _world.Data.HumanHeight;
        _world.Feel.Number(center - new Vector2(0, 0.3f * human), $"+{lumes} Lumes", LumeColor, 0.85f);
        if (fragments > 0)
        {
            _world.Feel.Number(center - new Vector2(0, 0.62f * human), "+Fragmento", new Color("e64a8e"), 0.8f);
        }

        var tween = CreateTween();
        tween.TweenProperty(_sprite, "scale", new Vector2(_def.Scale * 1.25f, _def.Scale * 0.35f), 0.12f);
        tween.Parallel().TweenProperty(_sprite, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(QueueFree));
        return new HitOutcome(1, true, false, true);
    }
}
