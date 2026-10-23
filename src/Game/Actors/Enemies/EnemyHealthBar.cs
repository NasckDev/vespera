using Godot;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Barra de vida sobre a cabeça do monstro: só aparece depois do primeiro golpe (CLAUDE.md §5) e mostra o dano
/// recente em creme antes de baixar. Desenhada em código no mundo.
/// </summary>
public partial class EnemyHealthBar : Node2D
{
    private static readonly Color Back = new(0.07f, 0.06f, 0.12f, 0.85f);
    private static readonly Color Fill = new("e85d5d");
    private static readonly Color Trail = new("f4e7c6");

    private readonly float _width;
    private readonly float _height;
    private float _trail = 1f;
    private float _value = 1f;
    private float _alpha;

    public EnemyHealthBar(float width, float height)
    {
        _width = width;
        _height = height;
        ZIndex = 30;
        ZAsRelative = false;
    }

    public bool Revealed { get; private set; }

    public void Set(float fraction)
    {
        _value = Mathf.Clamp(fraction, 0f, 1f);
        Revealed = true;
    }

    public void Conceal() => Revealed = false;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _alpha = Mathf.MoveToward(_alpha, Revealed ? 1f : 0f, 6f * dt);
        _trail = _trail > _value ? Mathf.MoveToward(_trail, _value, 0.9f * dt) : _value;
        if (_alpha > 0f)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_alpha <= 0.01f)
        {
            return;
        }

        var x = -_width / 2f;
        var pad = Mathf.Max(1f, _height * 0.25f);
        DrawRect(new Rect2(x - pad, -pad, _width + (2 * pad), _height + (2 * pad)), Back with { A = Back.A * _alpha });
        DrawRect(new Rect2(x, 0, _width * _trail, _height), Trail with { A = 0.9f * _alpha });
        DrawRect(new Rect2(x, 0, _width * _value, _height), Fill with { A = _alpha });
        DrawRect(new Rect2(x, 0, _width * _value, _height * 0.35f), new Color(1f, 1f, 1f, 0.25f * _alpha));
    }
}
