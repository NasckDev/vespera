namespace Vespera.Core.World;

/// <summary>
/// Área andável de um mapa hd a partir da máscara (<see cref="WalkGrid"/>): célula = <c>(floor(x/8), floor(y/8))</c>,
/// fora da grade = bloqueado (<c>core.js:241</c>, DECISIONS D1).
/// </summary>
public sealed class MaskWalkArea : IWalkArea
{
    public const int DefaultCellSize = 8;

    private IReadOnlyList<MapRect>? _blocked;

    public MaskWalkArea(WalkGrid grid, int cellSize = DefaultCellSize)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSize);
        Grid = grid;
        CellSize = cellSize;
    }

    public WalkGrid Grid { get; }

    public int CellSize { get; }

    public float Width => Grid.Width * CellSize;

    public float Height => Grid.Height * CellSize;

    public bool IsWalkable(float x, float y) =>
        Grid.IsWalkable((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));

    /// <summary>Células bloqueadas mescladas (<see cref="WalkGrid.BlockedRects"/>) em u, mais a moldura em volta da grade.</summary>
    public IReadOnlyList<MapRect> BlockedRects()
    {
        if (_blocked is not null)
        {
            return _blocked;
        }

        var rects = new List<MapRect>();
        foreach (var r in Grid.BlockedRects())
        {
            rects.Add(new MapRect(r.X * CellSize, r.Y * CellSize, r.Width * CellSize, r.Height * CellSize));
        }

        AddFrame(rects, Width, Height);
        _blocked = rects;
        return rects;
    }

    /// <summary>Quatro faixas de <see cref="WalkAreaExtensions.FrameMargin"/> u em volta de [0, w] × [0, h].</summary>
    internal static void AddFrame(List<MapRect> rects, float width, float height)
    {
        const float m = WalkAreaExtensions.FrameMargin;
        rects.Add(new MapRect(-m, -m, width + (2 * m), m)); // topo
        rects.Add(new MapRect(-m, height, width + (2 * m), m)); // base
        rects.Add(new MapRect(-m, 0, m, height)); // esquerda
        rects.Add(new MapRect(width, 0, m, height)); // direita
    }
}
