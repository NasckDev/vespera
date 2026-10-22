using Godot;

namespace Vespera.Game.Combat;

/// <summary>
/// Aviso no chão (GDD §4: 0,6 s de âmbar antes de todo golpe forte de chefe): área preenchida que pulsa e um contorno
/// que se fecha até a borda no instante do golpe. Círculo (pouso) ou faixa (investida). Achatado pela perspectiva 3/4.
/// </summary>
public partial class TelegraphMarker : Node2D
{
    private static readonly Color Amber = new("f2a541");
    private static readonly Color Hot = new("ffdd8a");

    private readonly float _seconds;
    private readonly float _radius;
    private readonly float _length;
    private readonly float _width;
    private float _t;
    private float _fadeOut = -1f;

    private TelegraphMarker(float seconds, float radius, float length, float width)
    {
        _seconds = seconds;
        _radius = radius;
        _length = length;
        _width = width;
        ZIndex = -5;
    }

    public static TelegraphMarker Circle(Vector2 at, float radius, float seconds) => new(seconds, radius, 0f, 0f) { Position = at };

    /// <summary>Faixa de <paramref name="from"/> na direção do ângulo, com comprimento e largura dados.</summary>
    public static TelegraphMarker Lane(Vector2 from, float angle, float length, float width, float seconds) =>
        new(seconds, 0f, length, width) { Position = from, Rotation = angle };

    /// <summary>0 no início, 1 no instante do golpe.</summary>
    public float Progress => Mathf.Clamp(_t / _seconds, 0f, 1f);

    public void Release() => _fadeOut = 0.18f;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_fadeOut >= 0f)
        {
            _fadeOut -= dt;
            Modulate = Modulate with { A = Mathf.Max(0f, _fadeOut / 0.18f) };
            if (_fadeOut <= 0f)
            {
                QueueFree();
            }
        }
        else
        {
            _t += dt;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var p = Progress;
        var pulse = 0.5f + (0.5f * Mathf.Sin(_t * 26f));
        var fill = Amber with { A = 0.16f + (0.18f * p) + (0.06f * pulse) };
        var edge = (p >= 1f ? Hot : Amber) with { A = 0.95f };
        if (_length > 0f)
        {
            DrawLane(p, fill, edge);
            return;
        }

        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, _radius, fill);
        DrawCircle(Vector2.Zero, _radius * p, Amber with { A = 0.22f });
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 64, edge, Mathf.Max(2f, _radius * 0.05f), true);
        DrawArc(Vector2.Zero, _radius * p, 0f, Mathf.Tau, 64, Hot with { A = 0.8f }, Mathf.Max(1.5f, _radius * 0.03f), true);
    }

    private void DrawLane(float p, Color fill, Color edge)
    {
        var h = _width / 2f;
        var rect = new Rect2(0, -h, _length, _width);
        DrawRect(rect, fill);
        DrawRect(new Rect2(0, -h, _length * p, _width), Amber with { A = 0.25f });
        DrawRect(rect, edge, false, Mathf.Max(2f, _width * 0.06f));
        for (var i = 1; i <= 3; i++)
        {
            var x = _length * ((i / 4f) + (0.25f * ((_t * 2f) % 1f))) % _length;
            DrawLine(new Vector2(x - (h * 0.6f), -h * 0.6f), new Vector2(x, 0), Hot with { A = 0.7f }, 3f, true);
            DrawLine(new Vector2(x - (h * 0.6f), h * 0.6f), new Vector2(x, 0), Hot with { A = 0.7f }, 3f, true);
        }
    }
}
