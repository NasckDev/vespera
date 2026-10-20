namespace Vespera.Core.World;

/// <summary>Retângulo em células da grade (x, y, largura, altura).</summary>
public readonly record struct CellRect(int X, int Y, int Width, int Height);

/// <summary>
/// Grade de células andáveis/bloqueadas lida da máscara de colisão de um mapa
/// (branco = andável, preto = bloqueado; 1 pixel da máscara = 1 célula).
/// </summary>
public sealed class WalkGrid
{
    private readonly bool[] _walkable;

    public WalkGrid(int width, int height, bool[] walkable)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(walkable);
        if (walkable.Length != width * height)
        {
            throw new ArgumentException($"Esperado {width * height} células, recebido {walkable.Length}.", nameof(walkable));
        }

        Width = width;
        Height = height;
        _walkable = walkable;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Fora da grade conta como bloqueado.</summary>
    public bool IsWalkable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _walkable[(y * Width) + x];

    /// <summary>
    /// Cobre todas as células bloqueadas com poucos retângulos (greedy meshing: estende na horizontal,
    /// depois na vertical). Cada retângulo vira um colisor no Godot; menos colisores = física mais leve.
    /// </summary>
    public IReadOnlyList<CellRect> BlockedRects()
    {
        var used = new bool[_walkable.Length];
        var rects = new List<CellRect>();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (!IsFreeBlocked(x, y, used))
                {
                    continue;
                }

                var width = 1;
                while (IsFreeBlocked(x + width, y, used))
                {
                    width++;
                }

                var height = 1;
                while (RowIsFreeBlocked(x, y + height, width, used))
                {
                    height++;
                }

                for (var dy = 0; dy < height; dy++)
                {
                    for (var dx = 0; dx < width; dx++)
                    {
                        used[((y + dy) * Width) + x + dx] = true;
                    }
                }

                rects.Add(new CellRect(x, y, width, height));
            }
        }

        return rects;
    }

    /// <summary>Célula andável mais próxima (busca em anéis), para corrigir spawns em cima de parede.</summary>
    public (int X, int Y)? NearestWalkable(int x, int y, int maxRadius = 32)
    {
        if (IsWalkable(x, y))
        {
            return (x, y);
        }

        for (var r = 1; r <= maxRadius; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && IsWalkable(x + dx, y + dy))
                    {
                        return (x + dx, y + dy);
                    }
                }
            }
        }

        return null;
    }

    private bool IsFreeBlocked(int x, int y, bool[] used) =>
        x < Width && y < Height && !_walkable[(y * Width) + x] && !used[(y * Width) + x];

    private bool RowIsFreeBlocked(int x, int y, int width, bool[] used)
    {
        if (y >= Height)
        {
            return false;
        }

        for (var dx = 0; dx < width; dx++)
        {
            if (!IsFreeBlocked(x + dx, y, used))
            {
                return false;
            }
        }

        return true;
    }
}
