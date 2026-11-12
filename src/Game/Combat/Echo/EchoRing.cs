using Godot;

namespace Vespera.Game.Combat.Echo;

/// <summary>Onda que se abre e some (Eco do Sino): anel dourado do centro do caçador até o alcance do Eco.</summary>
public partial class EchoRing : Node2D
{
    private const float Seconds = 0.55f;

    private readonly float _radius;
    private readonly Color _color;
    private float _age;

    public EchoRing(Vector2 at, float radius, Color color)
    {
        Position = at;
        _radius = radius;
        _color = color;
        ZIndex = 20;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Seconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / Seconds;
        var r = _radius * (1f - Mathf.Pow(1f - t, 3f));
        var alpha = 1f - t;
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 64, _color with { A = 0.9f * alpha }, (8f * alpha) + 2f, true);
        DrawArc(Vector2.Zero, r * 0.82f, 0f, Mathf.Tau, 64, _color with { A = 0.45f * alpha }, 4f, true);
    }
}
