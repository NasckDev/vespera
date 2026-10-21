namespace Vespera.Core.World;

/// <summary>
/// A mesma grade que <c>tools/maps/layout.py build</c> grava em <c>walk_&lt;id&gt;.png</c> (DECISIONS D40): células de
/// <c>cell</c> u, base (máscara da v2, formas do maps.json ou nada) amostrada no centro de cada célula, depois as formas
/// <c>walk</c> do layout (passam a andáveis) e por último as <c>block</c>. O jogo usa o PNG quando ele existe; esta conta
/// serve para layouts sem PNG gerado (fixtures do smoke) e para conferir o PNG nos testes.
/// </summary>
public static class LayoutGrid
{
    /// <summary>
    /// Monta a grade de um mapa de <paramref name="width"/>×<paramref name="height"/> u. <paramref name="baseArea"/> é a área da
    /// base (máscara ou formas); null = tudo bloqueado (<c>"base": "none"</c>).
    /// </summary>
    public static WalkGrid Build(int width, int height, IWalkArea? baseArea, MapLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var cell = Math.Max(1, layout.Cell);
        var gw = (width + cell - 1) / cell;
        var gh = (height + cell - 1) / cell;
        var cells = new bool[gw * gh];
        for (var y = 0; y < gh; y++)
        {
            for (var x = 0; x < gw; x++)
            {
                var cx = (x + 0.5f) * cell;
                var cy = (y + 0.5f) * cell;
                var walk = baseArea is not null && baseArea.IsWalkable(cx, cy);
                walk = Apply(layout.Walk, cx, cy, walk, value: true);
                walk = Apply(layout.Block, cx, cy, walk, value: false);
                cells[(y * gw) + x] = walk;
            }
        }

        return new WalkGrid(gw, gh, cells);
    }

    private static bool Apply(IReadOnlyList<LayoutShape> shapes, float x, float y, bool current, bool value)
    {
        for (var i = 0; i < shapes.Count; i++)
        {
            var s = shapes[i];
            if (s.Bounds.Contains(x, y) && PolygonMath.Contains(s.Polygon, x, y))
            {
                current = value;
            }
        }

        return current;
    }
}
