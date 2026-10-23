using Godot;
using Vespera.Core.Ranks;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>Cores de rank do jogo (as mesmas do catálogo de progressão) e variações para fundo e brilho.</summary>
public static class RankColors
{
    public static Color Of(Rank rank) => new(Progression.Catalog.Step(rank).Color);

    /// <summary>Versão escurecida, para fundos de selo.</summary>
    public static Color Deep(Rank rank) => Of(rank).Darkened(0.62f);

    /// <summary>BBCode da cor do rank, para usar em RichTextLabel.</summary>
    public static string Hex(Rank rank) => "#" + Of(rank).ToHtml(false);
}

/// <summary>
/// Selo de rank desenhado em código: losango com degradê na cor do rank, borda dupla e a letra no centro (o Alvor leva um sol).
/// Serve de "ícone" de item e de rank onde não há arte pintada. O brilho (0 a 1) é animável.
/// </summary>
public partial class RankBadge : Control
{
    private const float InnerScale = 0.72f;
    private Rank _rank;
    private float _glow;
    private bool _empty;

    public RankBadge(Rank rank, int size)
    {
        _rank = rank;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public Rank Rank
    {
        get => _rank;
        set
        {
            _rank = value;
            QueueRedraw();
        }
    }

    /// <summary>Slot vazio: só o contorno do losango, apagado.</summary>
    public bool Empty
    {
        get => _empty;
        set
        {
            _empty = value;
            QueueRedraw();
        }
    }

    /// <summary>Intensidade do halo ao redor do selo (0 a 1).</summary>
    public float Glow
    {
        get => _glow;
        set
        {
            _glow = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.47f;
        if (_empty)
        {
            DrawDiamondOutline(center, radius * 0.8f, new Color(Palette.CreamDim, 0.45f), 2f);
            DrawDiamondOutline(center, radius * 0.5f, new Color(Palette.CreamDim, 0.25f), 2f);
            return;
        }

        var color = RankColors.Of(_rank);
        DrawHalo(center, radius, color);

        DrawDiamond(center, radius, new Color("0F1230"), color.Lerp(Palette.Cream, 0.35f), 3f);
        DrawDiamondGradient(center, radius * 0.9f, color.Lightened(0.25f), color.Darkened(0.35f));
        DrawDiamondOutline(center, radius * 0.9f * InnerScale, new Color(Palette.Cream, 0.55f), 2f);

        if (_rank == Rank.Alvor)
        {
            DrawSun(center, radius * 0.5f);
        }
        else
        {
            DrawLetter(center, radius);
        }
    }

    private void DrawHalo(Vector2 center, float radius, Color color)
    {
        if (_glow <= 0.01f)
        {
            return;
        }

        for (var i = 4; i >= 1; i--)
        {
            var grow = i * radius * 0.11f;
            DrawDiamondOutline(center, radius + grow, new Color(color, 0.16f * _glow * (5 - i) / 4f), 4f);
        }
    }

    private void DrawDiamond(Vector2 center, float radius, Color fill, Color edge, float width)
    {
        var points = Points(center, radius);
        DrawColoredPolygon(points, fill);
        DrawPolyline([.. points, points[0]], edge, width, antialiased: true);
    }

    private void DrawDiamondOutline(Vector2 center, float radius, Color edge, float width)
    {
        var points = Points(center, radius);
        DrawPolyline([.. points, points[0]], edge, width, antialiased: true);
    }

    private void DrawDiamondGradient(Vector2 center, float radius, Color top, Color bottom)
    {
        var points = Points(center, radius);
        DrawPolygon(points, [top, top.Lerp(bottom, 0.5f), bottom, top.Lerp(bottom, 0.5f)]);
    }

    private static Vector2[] Points(Vector2 center, float radius) =>
    [
        center + new Vector2(0, -radius),
        center + new Vector2(radius, 0),
        center + new Vector2(0, radius),
        center + new Vector2(-radius, 0),
    ];

    private void DrawLetter(Vector2 center, float radius)
    {
        var text = _rank.ToString();
        var font = GetThemeFont("font", "HeadingLabel") ?? ThemeDB.FallbackFont;
        var size = (int)(radius * (text.Length > 2 ? 0.78f : text.Length > 1 ? 0.95f : 1.2f));
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var origin = new Vector2(center.X - (width / 2f), center.Y + (size * 0.3f));
        DrawString(font, origin + new Vector2(0, 2), text, HorizontalAlignment.Left, -1, size, new Color(0.05f, 0.06f, 0.18f, 0.85f));
        DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, Palette.Cream);
    }

    private void DrawSun(Vector2 center, float radius)
    {
        DrawCircle(center, radius * 0.5f, Palette.Cream);
        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Tau * i / 8f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(center + (dir * radius * 0.72f), center + (dir * radius * (i % 2 == 0 ? 1.15f : 0.95f)), Palette.Cream, 3f, antialiased: true);
        }
    }
}
