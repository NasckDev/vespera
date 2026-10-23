using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Combat;

/// <summary>
/// Poça do Eco Viscoso: mancha de lodo verde no chão, deixada pelo avanço do Q, que lentifica os monstros que pisam
/// nela enquanto dura (4 s, some aos poucos no último segundo).
/// </summary>
public partial class SlimeTrail : Node2D
{
    public const float Seconds = 4f;
    public const float SlowSeconds = 0.35f;
    private const float Radius = 40f;
    private static readonly Color Body = new(0.42f, 0.78f, 0.36f, 0.62f);
    private static readonly Color Shine = new(0.78f, 1f, 0.7f, 0.55f);

    private readonly LevelWorld _world;
    private readonly float _radius;
    private readonly float _wobble;
    private float _age;

    public SlimeTrail(LevelWorld world, Vector2 at)
    {
        _world = world;
        _radius = Radius * world.Data.ActorScale;
        _wobble = GD.Randf() * Mathf.Tau;
        Position = at;
        Name = "Slime";
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age >= Seconds)
        {
            QueueFree();
            return;
        }

        foreach (var enemy in _world.Enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.Dead && enemy.GlobalPosition.DistanceTo(GlobalPosition) < _radius + (enemy.HitRadius * 0.5f))
            {
                enemy.Slow(SlowSeconds);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var fade = Mathf.Clamp(Seconds - _age, 0f, 1f);
        var grow = Mathf.Min(1f, _age / 0.15f);
        var r = _radius * grow;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.55f));
        DrawCircle(Vector2.Zero, r, Body with { A = Body.A * fade });
        DrawCircle(new Vector2(r * 0.25f * Mathf.Cos(_wobble), -r * 0.2f), r * 0.35f, Shine with { A = Shine.A * fade });
    }
}
