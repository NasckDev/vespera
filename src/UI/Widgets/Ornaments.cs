using Godot;
using Vespera.UI.Common;

namespace Vespera.UI.Widgets;

/// <summary>
/// Painel do guia visual (hud-kit / <c>panel()</c> da v2, <c>core.js:112</c>): o StyleBox do tema (fundo escuro, borda
/// dourada, contorno escuro) mais a borda interna fraca e os losangos dourados nos cantos. Só redesenha ao mudar de tamanho.
/// </summary>
[GlobalClass]
public partial class OrnatePanel : PanelContainer
{
    private static readonly Color InnerBorder = new(201 / 255f, 150 / 255f, 62 / 255f, 0.22f);

    [Export]
    public bool Corners { get; set; } = true;

    [Export]
    public bool InnerLine { get; set; } = true;

    /// <summary>Cor dos losangos (padrão: dourado; painéis de perigo usam magenta).</summary>
    [Export]
    public Color CornerColor { get; set; } = UiPalette.Gold;

    [Export]
    public float CornerSize { get; set; } = 3f;

    public OrnatePanel()
    {
        ThemeTypeVariation = "OrnatePanel";
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Draw()
    {
        var size = Size;
        if (InnerLine && size.X > 12 && size.Y > 12)
        {
            DrawRect(new Rect2(2.5f, 2.5f, size.X - 5f, size.Y - 5f), InnerBorder, filled: false, width: 1f);
        }

        if (Corners)
        {
            Diamond.Paint(this, Vector2.Zero, CornerSize, CornerColor);
            Diamond.Paint(this, new Vector2(size.X, 0), CornerSize, CornerColor);
            Diamond.Paint(this, new Vector2(0, size.Y), CornerSize, CornerColor);
            Diamond.Paint(this, size, CornerSize, CornerColor);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }
}

/// <summary>Losango (marcador de lista, cantos, título de seção). Também desenha em qualquer CanvasItem.</summary>
[GlobalClass]
public partial class Diamond : Control
{
    private static readonly Vector2[] Points = new Vector2[4];
    private Color _color = UiPalette.Ambar;

    [Export]
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            QueueRedraw();
        }
    }

    /// <summary>Losango vazado (contorno) em vez de cheio.</summary>
    [Export]
    public bool Hollow { get; set; }

    public Diamond()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public static Diamond Create(float size, Color color, bool hollow = false) => new()
    {
        CustomMinimumSize = new Vector2(size, size),
        Color = color,
        Hollow = hollow,
    };

    public override void _Draw()
    {
        var r = Mathf.Min(Size.X, Size.Y) / 2f;
        if (Hollow)
        {
            Paint(this, Size / 2f, r, _color);
            Paint(this, Size / 2f, r * 0.45f, UiPalette.Panel);
            return;
        }

        Paint(this, Size / 2f, r, _color);
    }

    /// <summary>Desenha um losango de raio <paramref name="radius"/> centrado em <paramref name="center"/>.</summary>
    public static void Paint(CanvasItem canvas, Vector2 center, float radius, Color color)
    {
        if (radius < 0.25f)
        {
            return;
        }

        Points[0] = center + new Vector2(0, -radius);
        Points[1] = center + new Vector2(radius, 0);
        Points[2] = center + new Vector2(0, radius);
        Points[3] = center + new Vector2(-radius, 0);
        canvas.DrawColoredPolygon(Points, color);
    }
}
