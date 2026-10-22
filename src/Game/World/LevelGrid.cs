using System;
using System.Collections.Generic;

namespace Vespera.Game.World;

/// <summary>Retângulo em pixels do mundo (lógica pura, sem Godot).</summary>
public readonly record struct RectF(float X, float Y, float W, float H)
{
    public float Right => X + W;

    public float Bottom => Y + H;

    public bool Intersects(RectF other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public RectF Offset(float dx, float dy) => this with { X = X + dx, Y = Y + dy };

    public RectF Shrink(float by) => new(X + by, Y + by, W - (2 * by), H - (2 * by));
}

/// <summary>Uma face entre uma célula bloqueada e uma andável vizinha: o bot de colisão empurra o jogador por ela.</summary>
public readonly record struct GridFace(int Col, int Row, int Dx, int Dy);

/// <summary>
/// Grade de colisão de uma fase: '#' bloqueado, '.' andável. Fora da grade conta como bloqueado (borda do mapa).
/// </summary>
public sealed class LevelGrid
{
    public const char Blocked = '#';
    public const char Walkable = '.';

    private readonly bool[,] _blocked;

    private LevelGrid(int cell, int cols, int rows, bool[,] blocked)
    {
        Cell = cell;
        Cols = cols;
        Rows = rows;
        _blocked = blocked;
    }

    public int Cell { get; }

    public int Cols { get; }

    public int Rows { get; }

    public float Width => Cols * Cell;

    public float Height => Rows * Cell;

    public static LevelGrid Parse(int cell, int cols, int rows, IReadOnlyList<string> lines)
    {
        if (lines.Count != rows)
        {
            throw new FormatException($"grade com {lines.Count} linhas, esperado {rows}");
        }

        var blocked = new bool[cols, rows];
        for (var r = 0; r < rows; r++)
        {
            var line = lines[r];
            if (line.Length != cols)
            {
                throw new FormatException($"linha {r} da grade com {line.Length} colunas, esperado {cols}");
            }

            for (var c = 0; c < cols; c++)
            {
                blocked[c, r] = line[c] switch
                {
                    Blocked => true,
                    Walkable => false,
                    _ => throw new FormatException($"caractere '{line[c]}' inválido na grade ({c},{r})"),
                };
            }
        }

        return new LevelGrid(cell, cols, rows, blocked);
    }

    public bool IsBlocked(int col, int row) => col < 0 || row < 0 || col >= Cols || row >= Rows || _blocked[col, row];

    public bool IsBlockedAt(float x, float y) => IsBlocked((int)MathF.Floor(x / Cell), (int)MathF.Floor(y / Cell));

    public RectF CellRect(int col, int row) => new(col * Cell, row * Cell, Cell, Cell);

    public (float X, float Y) CellCenter(int col, int row) => ((col + 0.5f) * Cell, (row + 0.5f) * Cell);

    public IEnumerable<(int Col, int Row)> BlockedCells()
    {
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                if (_blocked[c, r])
                {
                    yield return (c, r);
                }
            }
        }
    }

    public int WalkableCount()
    {
        var count = 0;
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                count += _blocked[c, r] ? 0 : 1;
            }
        }

        return count;
    }

    /// <summary>Faces bloqueado→andável: (Col,Row) é a célula bloqueada; (Dx,Dy) aponta para a vizinha andável.</summary>
    public IEnumerable<GridFace> BoundaryFaces()
    {
        (int Dx, int Dy)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach (var (c, r) in BlockedCells())
        {
            foreach (var (dx, dy) in dirs)
            {
                if (!IsBlocked(c + dx, r + dy))
                {
                    yield return new GridFace(c, r, dx, dy);
                }
            }
        }
    }

    /// <summary>Verdadeiro se o retângulo cruza alguma célula bloqueada (ou sai da grade).</summary>
    public bool OverlapsBlocked(RectF rect)
    {
        var c0 = (int)MathF.Floor(rect.X / Cell);
        var c1 = (int)MathF.Floor((rect.Right - 0.001f) / Cell);
        var r0 = (int)MathF.Floor(rect.Y / Cell);
        var r1 = (int)MathF.Floor((rect.Bottom - 0.001f) / Cell);
        for (var r = r0; r <= r1; r++)
        {
            for (var c = c0; c <= c1; c++)
            {
                if (IsBlocked(c, r))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
