using System;
using Godot;
using Vespera.Game.Combat;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Uma das cinco velas da fase 3 da Velória ("Uma vela", CAMPANHA Ato IV). Leva golpe como um alvo: o primeiro golpe
/// apaga. As erradas <b>tremem</b> quando a lanterna do caçador passa perto; a certa (a Velória escondida) não treme.
/// Quem decide o que acontece ao apagar é a <see cref="Velora"/> (evento <see cref="Snuffed"/>).
/// </summary>
public partial class VeloraCandle : Node2D, IHittable
{
    private static readonly Color Wax = new("f4e7c6");
    private static readonly Color Flame = new("f2a541");
    private static readonly Color Ember = new("e64a8e");

    private readonly float _height;
    private readonly float _notice;
    private float _flicker;
    private float _shake;

    public VeloraCandle(bool trueOne, float height, float noticeRadius)
    {
        IsTrue = trueOne;
        _height = height;
        _notice = noticeRadius;
        ZIndex = 1;
    }

    /// <summary>A vela certa: é a Velória escondida.</summary>
    public bool IsTrue { get; }

    public bool Lit { get; private set; } = true;

    /// <summary>Está tremendo agora (a lanterna passou perto de uma vela errada).</summary>
    public bool Trembling => Lit && !IsTrue && _shake > 0f;

    public Team Team => Team.Monsters;

    public Vector2 HitCenter => GlobalPosition - new Vector2(0, _height * 0.5f);

    public float HitRadius => _height * 0.45f;

    public bool CanBeHit => Lit;

    /// <summary>Posição do caçador (para tremer quando ele passa perto); a Velória atualiza a cada quadro.</summary>
    public Vector2? Lantern { get; set; }

    public event Action<VeloraCandle>? Snuffed;

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!Lit)
        {
            return HitOutcome.Miss;
        }

        Lit = false;
        QueueRedraw();
        Snuffed?.Invoke(this);
        return new HitOutcome(0, true, false, true);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _flicker += dt * 9f;
        var near = Lantern is { } at && at.DistanceTo(GlobalPosition) <= _notice;
        _shake = near && !IsTrue ? 0.25f : Mathf.Max(0f, _shake - dt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var w = _height * 0.22f;
        var jitter = Trembling ? Mathf.Sin(_flicker * 3.1f) * w * 0.5f : 0f;
        DrawRect(new Rect2(-w * 0.5f, -_height * 0.7f, w, _height * 0.7f), Wax);
        DrawRect(new Rect2(-w * 0.5f, -_height * 0.7f, w, _height * 0.08f), new Color(0.85f, 0.8f, 0.65f));
        DrawLine(new Vector2(0, -_height * 0.7f), new Vector2(jitter * 0.3f, -_height * 0.8f), new Color(0.1f, 0.08f, 0.08f), 2f);
        if (!Lit)
        {
            DrawCircle(new Vector2(0, -_height * 0.85f), w * 0.25f, new Color(0.5f, 0.5f, 0.55f, 0.5f));
            return;
        }

        var tip = new Vector2(jitter, -_height * (0.98f + (0.04f * Mathf.Sin(_flicker))));
        DrawCircle(new Vector2(jitter * 0.6f, -_height * 0.86f), w * 0.9f, Flame with { A = 0.35f });
        DrawColoredPolygon([new Vector2(-w * 0.35f, -_height * 0.8f), tip, new Vector2(w * 0.35f, -_height * 0.8f)], Flame);
        DrawCircle(new Vector2(0, -_height * 0.82f), w * 0.22f, Ember);
    }
}
