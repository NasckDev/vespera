using Godot;
using Vespera.Autoload;
using Vespera.Core.Ranks;

namespace Vespera.UI;

/// <summary>
/// Selo desenhado em código. Forma por Selo (CLAUDE.md §6): E/D octógono, C/B losango, A/S hexágono, SS/SSS estrela,
/// SSSS estrela de oito pontas (entre a estrela do SSS e o sol do Alvor; DECISIONS D4) e Alvor sol.
/// Cor de cada Selo vem de <c>ranks.json</c> (DECISIONS D5).
/// </summary>
[GlobalClass]
public partial class RankSeal : Control
{
    private enum SealShape
    {
        Octagon,
        Diamond,
        Hexagon,
        Star,
        EightPointStar,
        Sun,
    }

    /// <summary>Forma por índice do <see cref="Rank"/>.</summary>
    private static readonly SealShape[] Shapes =
    [
        SealShape.Octagon, SealShape.Octagon, SealShape.Diamond, SealShape.Diamond, SealShape.Hexagon,
        SealShape.Hexagon, SealShape.Star, SealShape.Star, SealShape.EightPointStar, SealShape.Sun,
    ];

    private static readonly Color Outline = new("#F4E7C6");
    private static readonly Color LabelColor = new("#1B1F3B");
    private static readonly Color FallbackColor = new("#a39fae");

    private static Color[]? _colors;
    private static string[]? _labels;

    private Rank _rank = Rank.E;

    [Export]
    public Rank Rank
    {
        get => _rank;
        set
        {
            _rank = value;
            QueueRedraw();
        }
    }

    [Export]
    public Font? LabelFont { get; set; }

    [Export]
    public int LabelFontSize { get; set; } = 16;

    public override void _Draw()
    {
        var index = Mathf.Clamp((int)Rank, 0, Shapes.Length - 1);
        var shape = Shapes[index];
        var color = ColorOf(index);
        var center = Size / 2;
        var radius = (Mathf.Min(Size.X, Size.Y) / 2) - 2;

        if (shape == SealShape.Sun)
        {
            DrawSunRays(center, radius, color);
            radius *= 0.6f;
        }

        var points = ShapePoints(shape, center, radius);
        DrawColoredPolygon(points, color);
        DrawPolyline([.. points, points[0]], Outline, 2f);
        if (shape == SealShape.EightPointStar)
        {
            DrawArc(center, radius * 0.38f, 0, Mathf.Tau, 16, Outline, 1f);
        }

        var label = shape == SealShape.Sun ? string.Empty : LabelOf(index);
        var font = LabelFont ?? GetThemeDefaultFont();
        var fontSize = label.Length switch
        {
            <= 1 => LabelFontSize,
            2 => LabelFontSize * 2 / 3,
            3 => LabelFontSize / 2,
            _ => LabelFontSize * 2 / 5,
        };
        var baseline = new Vector2(0, center.Y + ((font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2));
        DrawString(font, baseline, label, HorizontalAlignment.Center, Size.X, fontSize, LabelColor);
    }

    /// <summary>Cores e ids dos Selos lidos uma vez dos dados (fallback se o Database não carregou).</summary>
    private static Color ColorOf(int index)
    {
        if (_colors is null && Database.IsLoaded)
        {
            var ranks = Database.Db.Ranks;
            _colors = new Color[ranks.Count];
            _labels = new string[ranks.Count];
            for (var i = 0; i < ranks.Count; i++)
            {
                _colors[i] = new Color(ranks[i].Color);
                _labels[i] = ranks[i].Id;
            }
        }

        return _colors is not null && index < _colors.Length ? _colors[index] : FallbackColor;
    }

    private static string LabelOf(int index) => _labels is not null && index < _labels.Length ? _labels[index] : ((Rank)index).ToString();

    private static Vector2[] ShapePoints(SealShape shape, Vector2 center, float radius) => shape switch
    {
        SealShape.Octagon => RegularPolygon(center, radius, 8, Mathf.Pi / 8),
        SealShape.Diamond => RegularPolygon(center, radius, 4, -Mathf.Pi / 2),
        SealShape.Hexagon => RegularPolygon(center, radius, 6, -Mathf.Pi / 2),
        SealShape.Star => StarPolygon(center, radius, radius * 0.45f, 5),
        SealShape.EightPointStar => StarPolygon(center, radius, radius * 0.58f, 8),
        _ => RegularPolygon(center, radius, 16, 0),
    };

    private static Vector2[] RegularPolygon(Vector2 center, float radius, int sides, float rotation)
    {
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
        {
            points[i] = center + (Vector2.FromAngle(rotation + (Mathf.Tau * i / sides)) * radius);
        }

        return points;
    }

    private static Vector2[] StarPolygon(Vector2 center, float outer, float inner, int tips)
    {
        var points = new Vector2[tips * 2];
        for (var i = 0; i < points.Length; i++)
        {
            var radius = i % 2 == 0 ? outer : inner;
            points[i] = center + (Vector2.FromAngle((-Mathf.Pi / 2) + (Mathf.Pi * i / tips)) * radius);
        }

        return points;
    }

    private void DrawSunRays(Vector2 center, float radius, Color color)
    {
        for (var i = 0; i < 8; i++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * i / 8);
            DrawLine(center + (direction * radius * 0.7f), center + (direction * radius), color, 3f);
        }
    }
}
