using System.Collections.Generic;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>
/// Feixe do Eco do Farol: uma luz que sai do caçador na direção da mira e puxa para um ponto à frente todo monstro
/// que ela cobre, durante a duração do Eco. Quem chega ao ponto fica lento por um instante.
/// </summary>
public partial class EchoBeam : Node2D
{
    private const float BeamWidth = 80f;
    private const float PullSpeed = 900f;
    private const float Arrived = 36f;

    private readonly LevelWorld _world;
    private readonly Vector2 _from;
    private readonly Vector2 _to;
    private readonly Vector2 _focus;
    private readonly EchoDef _def;
    private readonly float _scale;
    private readonly List<Enemy> _caught = [];
    private float _age;

    public EchoBeam(LevelWorld world, Vector2 from, Vector2 dir, EchoDef def, float scale)
    {
        _world = world;
        _def = def;
        _scale = scale;
        _from = from;
        var length = (float)def.Radius * scale;
        _to = from + (dir.Normalized() * length);
        _focus = from + (dir.Normalized() * length * 0.55f);
        ZIndex = 18;
        Name = "EcoFarol";
        foreach (var enemy in world.Enemies)
        {
            if (!enemy.Dead && Geometry2D.GetClosestPointToSegment(enemy.HitCenter, _from, _to).DistanceTo(enemy.HitCenter) <= (BeamWidth * scale) + enemy.HitRadius)
            {
                _caught.Add(enemy);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age >= _def.Duration)
        {
            foreach (var enemy in _caught)
            {
                if (IsInstanceValid(enemy) && !enemy.Dead)
                {
                    enemy.Slow(1.2f);
                }
            }

            QueueFree();
            return;
        }

        foreach (var enemy in _caught)
        {
            if (!IsInstanceValid(enemy) || enemy.Dead)
            {
                continue;
            }

            var to = _focus - enemy.HitCenter;
            enemy.Pull(to.Length() > Arrived * _scale ? to.Normalized() * PullSpeed * _scale : Vector2.Zero);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / (float)_def.Duration;
        var alpha = 1f - (t * 0.6f);
        var a = ToLocal(_from);
        var b = ToLocal(_to);
        DrawLine(a, b, new Color(0.55f, 0.85f, 1f, 0.3f * alpha), BeamWidth * _scale, true);
        DrawLine(a, b, new Color(0.85f, 0.97f, 1f, 0.85f * alpha), 14f * _scale, true);
        DrawCircle(ToLocal(_focus), (22f + (Mathf.Sin(_age * 30f) * 4f)) * _scale, new Color(1f, 0.95f, 0.7f, 0.8f * alpha));
    }
}
