using System.Collections.Generic;
using Godot;
using Vespera.Core.Ranks;

namespace Vespera.UI;

/// <summary>Selo de rank desenhado em código: forma e cor da seção 6 do CLAUDE.md.</summary>
[GlobalClass]
public partial class RankSeal : Control
{
    private enum SealShape
    {
        Octagon,
        Diamond,
        Hexagon,
        Star,
        Sun,
    }

    private static readonly Color Outline = new("#F4E7C6");
    private static readonly Color LabelColor = new("#1B1F3B");

    private static readonly Dictionary<Rank, (SealShape Shape, Color Color)> Seals = new()
    {
        [Rank.E] = (SealShape.Octagon, new Color("#8A8FA8")),
        [Rank.D] = (SealShape.Octagon, new Color("#B4C2D4")),
        [Rank.C] = (SealShape.Diamond, new Color("#52E0C4")),
        [Rank.B] = (SealShape.Diamond, new Color("#5B8DEF")),
        [Rank.A] = (SealShape.Hexagon, new Color("#9B6BD6")),
        [Rank.S] = (SealShape.Hexagon, new Color("#E64A8E")),
        [Rank.SS] = (SealShape.Star, new Color("#E64A8E")),
        [Rank.SSS] = (SealShape.Star, new Color("#F2A541")),
        [Rank.SSSS] = (SealShape.Star, new Color("#E64A8E")),
        [Rank.Alvor] = (SealShape.Sun, new Color("#F2A541")),
    };

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
        var (shape, color) = Seals[Rank];
        var center = Size / 2;
        var radius = Mathf.Min(Size.X, Size.Y) / 2 - 2;

        if (shape == SealShape.Sun)
        {
            DrawSunRays(center, radius, color);
            radius *= 0.6f;
        }

        var points = ShapePoints(shape, center, radius);
        DrawColoredPolygon(points, color);
        DrawPolyline([.. points, points[0]], Outline, 2f);

        var font = LabelFont ?? GetThemeDefaultFont();
        var label = Rank == Rank.Alvor ? string.Empty : Rank.ToString();
        var fontSize = label.Length > 1 ? LabelFontSize * 2 / 3 : LabelFontSize;
        var baseline = new Vector2(0, center.Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2);
        DrawString(font, baseline, label, HorizontalAlignment.Center, Size.X, fontSize, LabelColor);
    }

    private static Vector2[] ShapePoints(SealShape shape, Vector2 center, float radius) => shape switch
    {
        SealShape.Octagon => RegularPolygon(center, radius, 8, Mathf.Pi / 8),
        SealShape.Diamond => RegularPolygon(center, radius, 4, -Mathf.Pi / 2),
        SealShape.Hexagon => RegularPolygon(center, radius, 6, -Mathf.Pi / 2),
        SealShape.Star => StarPolygon(center, radius, radius * 0.45f, 5),
        _ => RegularPolygon(center, radius, 16, 0),
    };

    private static Vector2[] RegularPolygon(Vector2 center, float radius, int sides, float rotation)
    {
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
        {
            points[i] = center + Vector2.FromAngle(rotation + (Mathf.Tau * i / sides)) * radius;
        }

        return points;
    }

    private static Vector2[] StarPolygon(Vector2 center, float outer, float inner, int tips)
    {
        var points = new Vector2[tips * 2];
        for (var i = 0; i < points.Length; i++)
        {
            var radius = i % 2 == 0 ? outer : inner;
            points[i] = center + Vector2.FromAngle(-Mathf.Pi / 2 + (Mathf.Pi * i / tips)) * radius;
        }

        return points;
    }

    private void DrawSunRays(Vector2 center, float radius, Color color)
    {
        for (var i = 0; i < 8; i++)
        {
            var direction = Vector2.FromAngle(Mathf.Tau * i / 8);
            DrawLine(center + direction * radius * 0.7f, center + direction * radius, color, 3f);
        }
    }
}
