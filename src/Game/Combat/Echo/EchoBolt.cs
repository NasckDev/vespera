using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>Espinho da muda do Eco da Semente: voa reto, para na parede e acerta o primeiro monstro no caminho.</summary>
public partial class EchoBolt : Node2D
{
    private const float Speed = 760f;
    private const float Radius = 14f;

    private readonly LevelWorld _world;
    private readonly HitInfo _hit;
    private readonly Vector2 _dir;
    private readonly float _scale;
    private float _left;

    public EchoBolt(LevelWorld world, Vector2 from, Vector2 dir, float range, HitInfo hit, float scale)
    {
        _world = world;
        _dir = dir.Normalized();
        _left = range;
        _hit = hit;
        _scale = scale;
        Position = from;
        Rotation = _dir.Angle();
        ZIndex = 15;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = Mathf.Min(_left, Speed * _scale * (float)delta);
        Position += _dir * step;
        _left -= step;
        foreach (var target in _world.Combat.InCircle(GlobalPosition, Radius * _scale, Team.Hunters))
        {
            if (target.ReceiveHit(_hit).Landed)
            {
                _world.Feel.Spark(GlobalPosition, new Color("c879ef"), 6, 200f, -_dir, 70f);
                QueueFree();
                return;
            }
        }

        if (_left <= 0f)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var s = _scale;
        DrawColoredPolygon([new Vector2(14f * s, 0), new Vector2(-8f * s, -5f * s), new Vector2(-8f * s, 5f * s)], new Color("e9c8ff"));
        DrawLine(new Vector2(-30f * s, 0), new Vector2(-6f * s, 0), new Color(0.78f, 0.47f, 0.94f, 0.55f), 4f * s, true);
    }
}
