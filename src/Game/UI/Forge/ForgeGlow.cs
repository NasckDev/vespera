using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Brilho âmbar da forja: anéis concêntricos e raios que acendem quando a arma sobe de nível, e uma chuva de fagulhas.
/// Tudo desenhado em código; <see cref="Intensity"/> vai de 0 (apagado) a 1 (aceso).
/// </summary>
public partial class ForgeGlow : Control
{
    private const int Rings = 7;
    private const int Rays = 14;
    private float _intensity;
    private float _spin;

    public float Intensity
    {
        get => _intensity;
        set
        {
            _intensity = value;
            QueueRedraw();
        }
    }

    public ForgeGlow()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (_intensity <= 0.01f)
        {
            return;
        }

        _spin += (float)delta * 0.6f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_intensity <= 0.01f)
        {
            return;
        }

        var center = Size / 2f;
        var reach = Mathf.Min(Size.X, Size.Y) * 0.5f;
        for (var i = Rings; i >= 1; i--)
        {
            var t = i / (float)Rings;
            DrawCircle(center, reach * t, new Color(Palette.Amber, 0.07f * _intensity * (1.1f - t)));
        }

        for (var i = 0; i < Rays; i++)
        {
            var angle = _spin + (Mathf.Tau * i / Rays);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var inner = reach * (0.32f + (0.06f * (i % 3)));
            var outer = reach * (0.7f + (0.25f * _intensity) + (0.08f * (i % 2)));
            DrawLine(center + (dir * inner), center + (dir * outer), new Color(Palette.Amber.Lerp(Palette.Cream, 0.4f), 0.55f * _intensity), 3f, antialiased: true);
        }
    }

    /// <summary>Estouro de fagulhas a partir do centro.</summary>
    public void Burst()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Palette.Cream);
        gradient.SetColor(1, new Color(Palette.Amber, 0f));
        var sparks = new CpuParticles2D
        {
            Position = Size / 2f,
            Amount = 56,
            Lifetime = 1.1,
            OneShot = true,
            Explosiveness = 0.95f,
            Direction = new Vector2(0, -1),
            Spread = 180f,
            InitialVelocityMin = 190f,
            InitialVelocityMax = 460f,
            Gravity = new Vector2(0, 520),
            ScaleAmountMin = 4f,
            ScaleAmountMax = 10f,
            ColorRamp = gradient,
            Emitting = true,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(sparks);
        GetTree().CreateTimer(1.6, processAlways: true).Timeout += sparks.QueueFree;
    }
}
