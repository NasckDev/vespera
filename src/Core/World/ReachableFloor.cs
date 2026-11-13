namespace Vespera.Core.World;

/// <summary>
/// Chão alcançável a pé numa sala (grade de células): as células andáveis ligadas (4 vizinhos) à célula de partida.
/// Uma sala de Brecha só acaba quando todos os monstros da onda caem; um monstro numa célula fora desta região
/// (atrás do selo da saída, num bolsão murado) nunca seria alcançado pelo caçador. O diretor da sala usa isto para
/// fazer os monstros nascerem e ficarem no chão alcançável.
/// </summary>
public sealed class ReachableFloor
{
    private static readonly (int Dc, int Dr)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly bool[,] _reach;

    /// <param name="cols">Colunas da grade.</param>
    /// <param name="rows">Linhas da grade.</param>
    /// <param name="blocked">Célula bloqueada (parede, selo, barreira fechada). Fora da grade conta como bloqueada.</param>
    /// <param name="startCol">Coluna de partida (onde o caçador entra).</param>
    /// <param name="startRow">Linha de partida.</param>
    public ReachableFloor(int cols, int rows, Func<int, int, bool> blocked, int startCol, int startRow)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        Cols = Math.Max(0, cols);
        Rows = Math.Max(0, rows);
        _reach = new bool[Cols, Rows];
        Count = Flood(blocked, startCol, startRow);
    }

    public int Cols { get; }

    public int Rows { get; }

    /// <summary>Quantas células são alcançáveis (0 se a partida estava bloqueada).</summary>
    public int Count { get; }

    public bool IsReachable(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows && _reach[col, row];

    /// <summary>
    /// A célula alcançável mais perto (distância euclidiana entre centros) de (<paramref name="col"/>,
    /// <paramref name="row"/>); a própria célula se já for alcançável; null se nada é alcançável.
    /// </summary>
    public (int Col, int Row)? Nearest(int col, int row)
    {
        if (IsReachable(col, row))
        {
            return (col, row);
        }

        if (Count == 0)
        {
            return null;
        }

        (int Col, int Row)? best = null;
        var bestDistance = long.MaxValue;
        var maxRing = Math.Max(Cols, Rows) + Math.Max(Math.Abs(col), Math.Abs(row));
        for (var ring = 1; ring <= maxRing; ring++)
        {
            for (var dr = -ring; dr <= ring; dr++)
            {
                for (var dc = -ring; dc <= ring; dc++)
                {
                    if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != ring || !IsReachable(col + dc, row + dr))
                    {
                        continue;
                    }

                    var d = ((long)dc * dc) + ((long)dr * dr);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = (col + dc, row + dr);
                    }
                }
            }

            // Um anel mais externo nunca fica mais perto que ring² (o lado do quadrado); dá para parar.
            if (best is not null && bestDistance <= (long)ring * ring)
            {
                return best;
            }
        }

        return best;
    }

    private int Flood(Func<int, int, bool> blocked, int startCol, int startRow)
    {
        if (!Open(blocked, startCol, startRow))
        {
            return 0;
        }

        var queue = new Queue<(int Col, int Row)>();
        _reach[startCol, startRow] = true;
        queue.Enqueue((startCol, startRow));
        var count = 1;
        while (queue.Count > 0)
        {
            var (c, r) = queue.Dequeue();
            foreach (var (dc, dr) in Neighbours)
            {
                var nc = c + dc;
                var nr = r + dr;
                if (!Open(blocked, nc, nr) || _reach[nc, nr])
                {
                    continue;
                }

                _reach[nc, nr] = true;
                count++;
                queue.Enqueue((nc, nr));
            }
        }

        return count;
    }

    private bool Open(Func<int, int, bool> blocked, int col, int row) =>
        col >= 0 && row >= 0 && col < Cols && row < Rows && !blocked(col, row);
}
