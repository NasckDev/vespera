using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Fundo do título: key art com paralaxe e zoom bem lentos, sombra lateral atrás do menu, vinheta e brasas âmbar subindo.
/// Sem entrada de mouse; só desenha.
/// </summary>
public partial class TitleBackground : Control
{
    private const float BaseZoom = 1.95f;
    private const float ZoomBreath = 0.04f;
    private const float DriftX = 46f;
    private const float DriftY = 22f;
    private const int EmberCount = 70;
    private const float EmberLifetime = 11f;

    private double _time;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(BuildEmbers());
    }

    public override void _Process(double delta)
    {
        _time += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = (float)_time;
        var zoom = BaseZoom + (ZoomBreath * (0.5f + (0.5f * Mathf.Sin(t * 0.12f))));
        var shift = new Vector2(Mathf.Sin(t * 0.09f) * DriftX, Mathf.Cos(t * 0.07f) * DriftY);
        ArtAssets.DrawKeyArt(this, zoom, shift);

        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, new Color(0.05f, 0.06f, 0.16f, 0.30f));
        DrawSideShade();
        DrawTextureRect(ArtAssets.Vignette, rect, false);
    }

    /// <summary>Sombra em degradê da direita para a esquerda, para o painel do menu e o letreiro lerem bem.</summary>
    private void DrawSideShade()
    {
        var clear = new Color(0.04f, 0.05f, 0.13f, 0f);
        var dark = new Color(0.04f, 0.05f, 0.13f, 0.82f);
        DrawPolygon(
            [new Vector2(980, 0), new Vector2(Size.X, 0), new Vector2(Size.X, Size.Y), new Vector2(980, Size.Y)],
            [clear, dark, dark, clear]);
    }

    private static CpuParticles2D BuildEmbers()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.72f, 0.28f, 0f));
        ramp.SetColor(1, new Color(1f, 0.45f, 0.2f, 0f));
        ramp.AddPoint(0.15f, new Color(1f, 0.75f, 0.3f, 0.9f));
        ramp.AddPoint(0.7f, new Color(1f, 0.55f, 0.22f, 0.55f));

        return new CpuParticles2D
        {
            Position = new Vector2(ArtAssets.ScreenWidth / 2f, ArtAssets.ScreenHeight + 20f),
            Amount = EmberCount,
            Lifetime = EmberLifetime,
            Preprocess = EmberLifetime,
            Texture = ArtAssets.SoftCircle,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(ArtAssets.ScreenWidth / 2f, 8f),
            Direction = new Vector2(0.15f, -1f),
            Spread = 22f,
            InitialVelocityMin = 40f,
            InitialVelocityMax = 120f,
            Gravity = new Vector2(10f, -4f),
            ScaleAmountMin = 0.07f,
            ScaleAmountMax = 0.24f,
            ColorRamp = ramp,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }
}
