namespace Vespera.Core.World;

/// <summary>Geometria de polígonos em u, sem alocação (usada por frame na oclusão e na água).</summary>
public static class PolygonMath
{
    /// <summary>Retângulo que envolve os vértices.</summary>
    public static MapRect Bounds(IReadOnlyList<MapPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return default;
        }

        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            x0 = MathF.Min(x0, p.X);
            y0 = MathF.Min(y0, p.Y);
            x1 = MathF.Max(x1, p.X);
            y1 = MathF.Max(y1, p.Y);
        }

        return new MapRect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Ponto dentro do polígono (regra par-ímpar; borda conta como dentro para pontos exatamente nela só às vezes).</summary>
    public static bool Contains(IReadOnlyList<MapPoint> polygon, float x, float y)
    {
        var inside = false;
        var n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>Dois retângulos se sobrepõem (bordas encostadas não contam).</summary>
    public static bool Overlaps(MapRect a, MapRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>Área (sempre positiva) do polígono.</summary>
    public static float Area(IReadOnlyList<MapPoint> polygon)
    {
        var sum = 0f;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            sum += (polygon[j].X * polygon[i].Y) - (polygon[i].X * polygon[j].Y);
        }

        return MathF.Abs(sum) / 2f;
    }
}
