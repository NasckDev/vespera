using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>Abrigo do Eco do Lar: uma casinha de luz no chão; quem estiver dentro dele fica invulnerável enquanto durar.</summary>
public partial class EchoShelter : Node2D
{
    private readonly LevelWorld _world;
    private readonly EchoDef _def;
    private readonly float _radius;
    private float _age;

    public EchoShelter(LevelWorld world, Vector2 at, EchoDef def, float scale)
    {
        _world = world;
        _def = def;
        _radius = (float)def.Radius * scale;
        Position = at;
        Name = "EcoLar";
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age >= _def.Duration)
        {
            QueueFree();
            return;
        }

        foreach (var player in _world.Players)
        {
            if (IsInstanceValid(player) && player.GlobalPosition.DistanceTo(GlobalPosition) <= _radius)
            {
                player.Combat.Shelter(0.1f);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var open = Mathf.Min(1f, _age / 0.2f);
        var fade = Mathf.Clamp(((float)_def.Duration - _age) / 0.5f, 0f, 1f);
        var warm = new Color(1f, 0.86f, 0.5f);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.55f));
        DrawCircle(Vector2.Zero, _radius * open, warm with { A = 0.22f * fade });
        DrawArc(Vector2.Zero, _radius * open, 0f, Mathf.Tau, 48, warm with { A = 0.9f * fade }, 4f, true);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        var h = _radius * 0.9f * open;
        var w = _radius * 0.55f;
        DrawRect(new Rect2(-w, -h * 0.55f, w * 2f, h * 0.55f), warm with { A = 0.35f * fade });
        DrawColoredPolygon([new Vector2(-w * 1.25f, -h * 0.55f), new Vector2(0, -h * 1.1f), new Vector2(w * 1.25f, -h * 0.55f)], warm with { A = 0.55f * fade });
        DrawRect(new Rect2(-w * 0.25f, -h * 0.3f, w * 0.5f, h * 0.3f), new Color(1f, 0.97f, 0.8f, 0.8f * fade));
    }
}
