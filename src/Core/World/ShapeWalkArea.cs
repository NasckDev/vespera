namespace Vespera.Core.World;

/// <summary>
/// Área andável de mapas sem máscara (Guilda, Estalagem, Núcleo, lojas): <c>walk</c> = união de retângulos/círculos,
/// <c>block</c> = retângulos subtraídos (<c>core.js:240-247</c>, spec 01 §2.4 e §9.2 item 3). As formas são usadas
/// como estão, sem rasterizar em células (os retângulos não são múltiplos de 8).
/// </summary>
public sealed class ShapeWalkArea : IWalkArea
{
    /// <summary>Altura das faixas usadas para aproximar círculos nos colisores (u).</summary>
    public const float DefaultCircleStep = 2f;

    private readonly WalkShape[] _walk;
    private readonly MapRect[] _block;
    private readonly float _circleStep;
    private IReadOnlyList<MapRect>? _blocked;

    public ShapeWalkArea(float width, float height, IReadOnlyList<WalkShape> walk, IReadOnlyList<MapRect> block, float circleStep = DefaultCircleStep)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(walk);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(circleStep);
        Width = width;
        Height = height;
        _walk = [.. walk];
        _block = [.. block];
        _circleStep = circleStep;
    }

    public float Width { get; }

    public float Height { get; }

    public IReadOnlyList<WalkShape> Walk => _walk;

    public IReadOnlyList<MapRect> Block => _block;

    public bool IsWalkable(float x, float y)
    {
        var inWalk = false;
        foreach (var shape in _walk)
        {
            if (shape.Contains(x, y))
            {
                inWalk = true;
                break;
            }
        }

        if (!inWalk)
        {
            return false;
        }

        foreach (var rect in _block)
        {
            if (rect.Contains(x, y))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Complemento exato da área andável dentro de [0, Width] × [0, Height] mais a moldura, em retângulos.
    /// Faixas horizontais nas bordas de todos os retângulos; círculos viram faixas de <c>circleStep</c> u usando a
    /// corda mais estreita de cada faixa (o colisor nunca é menor que a área bloqueada). Faixas vizinhas com o mesmo
    /// intervalo bloqueado são mescladas.
    /// </summary>
    public IReadOnlyList<MapRect> BlockedRects() => _blocked ??= ComputeBlockedRects();

    private List<MapRect> ComputeBlockedRects()
    {
        const float m = WalkAreaExtensions.FrameMargin;
        var left = -m;
        var right = Width + m;
        var top = -m;
        var bottom = Height + m;

        var ys = new SortedSet<float> { top, bottom };
        foreach (var shape in _walk)
        {
            if (shape.Circle is { } c)
            {
                for (var y = c.Y - c.Radius; y < c.Y + c.Radius; y += _circleStep)
                {
                    ys.Add(y);
                }

                ys.Add(c.Y + c.Radius);
            }
            else
            {
                ys.Add(shape.Rect.Y);
                ys.Add(shape.Rect.Bottom);
            }
        }

        foreach (var rect in _block)
        {
            ys.Add(rect.Y);
            ys.Add(rect.Bottom);
        }

        var breaks = ys.Where(y => y >= top && y <= bottom).ToArray();
        var result = new List<MapRect>();
        var open = new Dictionary<(float X0, float X1), (float Y0, float Y1)>();
        var walkIntervals = new List<(float A, float B)>();

        for (var i = 0; i < breaks.Length - 1; i++)
        {
            var y0 = breaks[i];
            var y1 = breaks[i + 1];
            if (y1 - y0 <= 1e-4f)
            {
                continue;
            }

            CollectWalkIntervals(y0, y1, walkIntervals);
            var blocked = Complement(walkIntervals, left, right);

            // Fecha retângulos que não continuam nesta faixa; estende os que continuam.
            var next = new Dictionary<(float X0, float X1), (float Y0, float Y1)>();
            foreach (var interval in blocked)
            {
                next[interval] = open.TryGetValue(interval, out var span) && span.Y1 == y0 ? (span.Y0, y1) : (y0, y1);
            }

            foreach (var (key, span) in open)
            {
                if (!next.TryGetValue(key, out var continued) || continued.Y0 != span.Y0)
                {
                    result.Add(new MapRect(key.X0, span.Y0, key.X1 - key.X0, span.Y1 - span.Y0));
                }
            }

            open = next;
        }

        foreach (var (key, span) in open)
        {
            result.Add(new MapRect(key.X0, span.Y0, key.X1 - key.X0, span.Y1 - span.Y0));
        }

        return result;
    }

    /// <summary>Intervalos X andáveis na faixa [y0, y1] (walk unido, block subtraído), ordenados e disjuntos.</summary>
    private void CollectWalkIntervals(float y0, float y1, List<(float A, float B)> output)
    {
        output.Clear();
        foreach (var shape in _walk)
        {
            if (shape.Circle is { } c)
            {
                if (c.Y - c.Radius > y0 || c.Y + c.Radius < y1)
                {
                    continue;
                }

                var dy = MathF.Max(MathF.Abs(y0 - c.Y), MathF.Abs(y1 - c.Y));
                if (dy >= c.Radius)
                {
                    continue;
                }

                var half = MathF.Sqrt((c.Radius * c.Radius) - (dy * dy));
                output.Add((c.X - half, c.X + half));
            }
            else if (shape.Rect.Y <= y0 && shape.Rect.Bottom >= y1)
            {
                output.Add((shape.Rect.X, shape.Rect.Right));
            }
        }

        Merge(output);

        foreach (var rect in _block)
        {
            if (rect.Y < y1 && rect.Bottom > y0)
            {
                Subtract(output, rect.X, rect.Right);
            }
        }
    }

    private static void Merge(List<(float A, float B)> intervals)
    {
        if (intervals.Count < 2)
        {
            return;
        }

        intervals.Sort((p, q) => p.A.CompareTo(q.A));
        var merged = 0;
        for (var i = 1; i < intervals.Count; i++)
        {
            if (intervals[i].A <= intervals[merged].B)
            {
                intervals[merged] = (intervals[merged].A, MathF.Max(intervals[merged].B, intervals[i].B));
            }
            else
            {
                intervals[++merged] = intervals[i];
            }
        }

        intervals.RemoveRange(merged + 1, intervals.Count - merged - 1);
    }

    private static void Subtract(List<(float A, float B)> intervals, float a, float b)
    {
        for (var i = intervals.Count - 1; i >= 0; i--)
        {
            var (ia, ib) = intervals[i];
            if (ib <= a || ia >= b)
            {
                continue;
            }

            intervals.RemoveAt(i);
            if (ib > b)
            {
                intervals.Insert(i, (b, ib));
            }

            if (ia < a)
            {
                intervals.Insert(i, (ia, a));
            }
        }
    }

    private static List<(float X0, float X1)> Complement(List<(float A, float B)> walk, float left, float right)
    {
        var blocked = new List<(float, float)>();
        var cursor = left;
        foreach (var (a, b) in walk)
        {
            if (a > cursor)
            {
                blocked.Add((cursor, MathF.Min(a, right)));
            }

            cursor = MathF.Max(cursor, b);
        }

        if (cursor < right)
        {
            blocked.Add((cursor, right));
        }

        return blocked;
    }
}
