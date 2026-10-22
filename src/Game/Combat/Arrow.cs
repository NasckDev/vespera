using System.Collections.Generic;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Combat;

/// <summary>
/// Flecha do Arco: voa reta na altura do peito, para na parede (pela grade, medindo no chão abaixo dela) e acerta o
/// primeiro alvo — ou todos no caminho, se for a flecha de luz carregada. Desenhada em código: haste, ponta e rastro.
/// </summary>
public partial class Arrow : Node2D
{
    private const float ChestHeight = 34f;
    private const float TipRadius = 10f;

    private readonly HitInfo _hit;
    private readonly Vector2 _dir;
    private readonly float _speed;
    private readonly bool _pierce;
    private readonly float _scale;
    private readonly HashSet<IHittable> _done = [];
    private float _left;
    private float _fade = -1f;

    public Arrow(HitInfo hit, Vector2 dir, float speed, float range, bool pierce, float scale)
    {
        _hit = hit;
        _dir = dir.Normalized();
        _speed = speed;
        _left = range;
        _pierce = pierce;
        _scale = scale;
        Rotation = _dir.Angle();
        ZIndex = 15;
    }

    public LevelWorld World { get; set; } = null!;

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (_fade >= 0f)
        {
            _fade -= dt;
            Modulate = Modulate with { A = Mathf.Max(0f, _fade / 0.12f) };
            if (_fade <= 0f)
            {
                QueueFree();
            }

            return;
        }

        var step = Mathf.Min(_left, _speed * dt);
        var substeps = Mathf.Max(1, Mathf.CeilToInt(step / (8f * _scale)));
        for (var i = 0; i < substeps && _fade < 0f; i++)
        {
            Position += _dir * (step / substeps);
            Advance();
        }

        _left -= step;
        if (_left <= 0f && _fade < 0f)
        {
            Stop(false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var s = _scale;
        var shaft = 46f * s;
        var glow = _pierce ? new Color(1f, 0.82f, 0.4f, 0.55f) : new Color(1f, 0.9f, 0.7f, 0.22f);
        DrawLine(new Vector2(-shaft * (_pierce ? 2.6f : 1.6f), 0), Vector2.Zero, glow, (_pierce ? 9f : 5f) * s, true);
        DrawLine(new Vector2(-shaft, 0), new Vector2(-4f * s, 0), new Color("6b4a2e"), 3f * s, true);
        DrawLine(new Vector2(-shaft, 0), new Vector2(-shaft + (9f * s), -5f * s), new Color("e64a8e"), 2.5f * s, true);
        DrawLine(new Vector2(-shaft, 0), new Vector2(-shaft + (9f * s), 5f * s), new Color("e64a8e"), 2.5f * s, true);
        DrawColoredPolygon([new Vector2(0, -4.5f * s), new Vector2(11f * s, 0), new Vector2(0, 4.5f * s)],
            _pierce ? new Color("fff1c4") : new Color("d8d2c4"));
    }

    private void Advance()
    {
        var ground = GlobalPosition + new Vector2(0, ChestHeight * _scale);
        if (World.Grid.IsBlockedAt(ground.X, ground.Y))
        {
            Stop(true);
            return;
        }

        foreach (var target in World.Combat.InCircle(GlobalPosition, TipRadius * _scale, Team.Hunters))
        {
            if (!_done.Add(target))
            {
                continue;
            }

            var outcome = target.ReceiveHit(_hit);
            if (!outcome.Landed)
            {
                continue;
            }

            World.Feel.Spark(GlobalPosition, _pierce ? new Color("ffd27a") : new Color("ffe9c2"), _pierce ? 12 : 7, 280f, -_dir, 60f);
            World.Feel.HitStop(_hit.HitStop + (outcome.Killed ? 0.02 : 0));
            World.Feel.Shake(_pierce ? 0.3f : 0.12f);
            CombatSfx.Play(World, CombatSound.ArrowHit, GlobalPosition);
            if (!_pierce)
            {
                Stop(false);
                return;
            }
        }
    }

    private void Stop(bool wall)
    {
        if (wall)
        {
            World.Feel.Spark(GlobalPosition, new Color("c9b89a"), 6, 160f, -_dir, 80f);
            CombatSfx.Play(World, CombatSound.ArrowHit, GlobalPosition, -8f);
        }

        _fade = 0.12f;
    }
}
