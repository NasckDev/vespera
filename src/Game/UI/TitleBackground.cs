using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Fundo do título: o último quadro da abertura (cidade e logo pintados) com zoom e paralaxe bem lentos, sombra lateral
/// atrás do menu, vinheta e brasas âmbar subindo. A sombra e a vinheta entram aos poucos, para o fundido vindo da
/// abertura começar exatamente na imagem do vídeo. Sem entrada de mouse; só desenha.
/// </summary>
public partial class TitleBackground : Control
{
    private const float ZoomGain = 0.035f;
    private const float ZoomRate = 0.1f;
    private const float DriftX = 18f;
    private const float DriftY = 9f;
    private const float RampSeconds = 1.2f;
    private const float ShadeStartX = 1250f;
    private const float ShadeAlpha = 0.80f;
    private const int EmberCount = 70;
    private const float EmberLifetime = 11f;

    private double _time;
    private CpuParticles2D _embers = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _embers = BuildEmbers();
        _embers.Modulate = Colors.Transparent;
        AddChild(_embers);
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _embers.Modulate = new Color(1f, 1f, 1f, Ramp());
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = (float)_time;
        var zoom = 1f + (ZoomGain * (0.5f - (0.5f * Mathf.Cos(t * ZoomRate))));
        var shift = new Vector2(Mathf.Sin(t * 0.09f) * DriftX, Mathf.Cos(t * 0.07f) * DriftY);
        ArtAssets.DrawIntroStill(this, zoom, shift);

        var ramp = Ramp();
        var rect = new Rect2(Vector2.Zero, Size);
        DrawSideShade(ramp);
        DrawTextureRect(ArtAssets.Vignette, rect, false, new Color(1f, 1f, 1f, ramp * 0.8f));
    }

    private float Ramp()
    {
        var x = Mathf.Clamp((float)_time / RampSeconds, 0f, 1f);
        return x * x * (3f - (2f * x));
    }

    /// <summary>Sombra em degradê da direita para a esquerda, para o painel do menu ler bem sem apagar o logo.</summary>
    private void DrawSideShade(float ramp)
    {
        var clear = new Color(0.04f, 0.05f, 0.13f, 0f);
        var dark = new Color(0.04f, 0.05f, 0.13f, ShadeAlpha * ramp);
        DrawPolygon(
            [new Vector2(ShadeStartX, 0), new Vector2(Size.X, 0), new Vector2(Size.X, Size.Y), new Vector2(ShadeStartX, Size.Y)],
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
