using Godot;

namespace Vespera.Game.UI;

/// <summary>Raios girando e anéis de luz atrás do selo da celebração de rank, mais fagulhas na cor do rank.</summary>
public partial class RankBurst : Control
{
    private const int Rays = 18;
    private readonly Color _color;
    private float _intensity;
    private float _spin;

    public RankBurst(Color color)
    {
        _color = color;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public float Intensity
    {
        get => _intensity;
        set
        {
            _intensity = value;
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        _spin += (float)delta * 0.35f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2f + new Vector2(0, -10);
        var reach = Mathf.Min(Size.X, Size.Y) * 0.62f;
        for (var i = 6; i >= 1; i--)
        {
            DrawCircle(center, reach * i / 6f * _intensity, new Color(_color, 0.045f * _intensity));
        }

        for (var i = 0; i < Rays; i++)
        {
            var angle = _spin + (Mathf.Tau * i / Rays);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var length = reach * (i % 2 == 0 ? 1f : 0.78f) * _intensity;
            DrawLine(center + (dir * reach * 0.42f), center + (dir * length), new Color(_color.Lerp(Palette.Cream, 0.4f), 0.5f * _intensity), i % 2 == 0 ? 5f : 3f, antialiased: true);
        }
    }

    public void Sparks()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, _color.Lerp(Palette.Cream, 0.6f));
        gradient.SetColor(1, new Color(_color, 0f));
        var particles = new CpuParticles2D
        {
            Position = Size / 2f + new Vector2(0, -10),
            Amount = 90,
            Lifetime = 1.8,
            OneShot = true,
            Explosiveness = 0.9f,
            Spread = 180f,
            Direction = new Vector2(0, -1),
            InitialVelocityMin = 220f,
            InitialVelocityMax = 620f,
            Gravity = new Vector2(0, 260),
            ScaleAmountMin = 5f,
            ScaleAmountMax = 12f,
            ColorRamp = gradient,
            Emitting = true,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(particles);
        GetTree().CreateTimer(2.4, processAlways: true).Timeout += particles.QueueFree;
    }
}
