using Godot;
using Vespera.Game.Combat;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Fragmento caído (moeda do Avesso): salta do monstro num arco, quica, brilha pulsando e, quando o caçador chega
/// perto, é puxado como ímã. Cada um vale 1 na bolsa da caçada. Desenhado em código: cristal facetado magenta-âmbar.
/// </summary>
public partial class FragmentPickup : Node2D
{
    private const float PopSeconds = 0.45f;
    private const float MagnetRadius = 150f;
    private const float CollectRadius = 14f;
    private const float MagnetAccel = 2600f;

    private readonly LevelWorld _world;
    private readonly Vector2 _landAt;
    private readonly float _a;
    private Vector2 _start;
    private Vector2 _velocity;
    private float _t;
    private bool _magnet;

    public FragmentPickup(LevelWorld world, Vector2 from, Vector2 landAt)
    {
        _world = world;
        _start = from;
        _landAt = landAt;
        _a = world.Data.ActorScale;
        Position = from;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        var player = _world.Player;
        if (_t < PopSeconds)
        {
            var k = _t / PopSeconds;
            Position = _start.Lerp(_landAt, k);
            QueueRedraw();
            return;
        }

        var feet = player.GlobalPosition;
        var alive = !player.Combat.IsDead;
        if (alive && (_magnet || feet.DistanceTo(GlobalPosition) < MagnetRadius * _a))
        {
            _magnet = true;
            var to = (feet - (Vector2.Up * 6f * _a)) - GlobalPosition;
            _velocity = _velocity.MoveToward(to.Normalized() * 900f * _a, MagnetAccel * _a * dt);
            GlobalPosition += _velocity * dt;
            if (to.Length() < CollectRadius * _a)
            {
                Collect();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var a = _a;
        var hop = _t < PopSeconds ? Mathf.Sin(Mathf.Pi * (_t / PopSeconds)) * 26f * a : (2f + (2f * Mathf.Sin(_t * 4f))) * a;
        var beat = 0.6f + (0.4f * Mathf.Sin(_t * 6f));
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 7f * a, new Color(0, 0, 0, 0.3f));
        DrawSetTransform(new Vector2(0, -hop - (9f * a)), 0f, Vector2.One);
        DrawCircle(Vector2.Zero, 13f * a * beat, new Color(0.9f, 0.29f, 0.56f, 0.18f));
        var s = 7f * a;
        Vector2[] gem = [new(0, -s * 1.4f), new(s * 0.8f, -s * 0.2f), new(0, s * 1.1f), new(-s * 0.8f, -s * 0.2f)];
        DrawColoredPolygon(gem, new Color("c2367a"));
        DrawColoredPolygon([gem[0], gem[1], new Vector2(0, -s * 0.1f)], new Color("ff8cc4"));
        DrawColoredPolygon([gem[0], new Vector2(0, -s * 0.1f), gem[3]], new Color("f2a541"));
        DrawPolyline([gem[0], gem[1], gem[2], gem[3], gem[0]], new Color("2a1030"), 1.2f, true);
        DrawCircle(new Vector2(-s * 0.25f, -s * 0.75f), s * 0.18f, new Color(1f, 1f, 1f, 0.9f * beat));
    }

    private void Collect()
    {
        RiftSession.Rules.AddFragments(RiftSession.Bag, 1);
        CombatSfx.Play(_world, CombatSound.Pickup, GlobalPosition, -6f);
        _world.Feel.Spark(GlobalPosition - new Vector2(0, 10f * _a), new Color("ff8cc4"), 5, 120f);
        QueueFree();
    }
}
