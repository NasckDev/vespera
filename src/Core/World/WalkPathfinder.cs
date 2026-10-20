namespace Vespera.Core.World;

/// <summary>
/// Busca em largura numa malha de <c>step</c> u ancorada no ponto de partida, andando só por pontos onde
/// <see cref="WalkAreaExtensions.CanStand"/> vale (e pelo ponto médio de cada passo). Serve para verificar que
/// chegadas, saídas e interagíveis são alcançáveis e para o playtest automático andar até eles.
/// </summary>
public static class WalkPathfinder
{
    private static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// Caminho (inclui início e fim) até o primeiro ponto que satisfaz <paramref name="goal"/>, ou null se não houver.
    /// O início precisa ser um ponto onde dá para ficar de pé.
    /// </summary>
    public static IReadOnlyList<MapPoint>? FindPath(IWalkArea area, MapPoint start, Func<MapPoint, bool> goal, float step = 4f, float radius = WalkAreaExtensions.PlayerRadius)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        if (!area.CanStand(start.X, start.Y, radius))
        {
            return null;
        }

        MapPoint At((int I, int J) n) => new(start.X + (n.I * step), start.Y + (n.J * step));

        var cameFrom = new Dictionary<(int, int), (int, int)> { [(0, 0)] = (0, 0) };
        var queue = new Queue<(int I, int J)>();
        queue.Enqueue((0, 0));
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var p = At(node);
            if (goal(p))
            {
                return Rebuild(cameFrom, node, At);
            }

            foreach (var (dx, dy) in Neighbours)
            {
                var next = (node.I + dx, node.J + dy);
                if (cameFrom.ContainsKey(next))
                {
                    continue;
                }

                var q = At(next);
                if (q.X < 0 || q.Y < 0 || q.X > area.Width || q.Y > area.Height
                    || !area.CanStand(q.X, q.Y, radius)
                    || !area.CanStand((p.X + q.X) / 2, (p.Y + q.Y) / 2, radius))
                {
                    continue;
                }

                cameFrom[next] = node;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    /// <summary>Atalho: caminho até qualquer ponto dentro do retângulo.</summary>
    public static IReadOnlyList<MapPoint>? FindPathInto(IWalkArea area, MapPoint start, MapRect target, float step = 4f) =>
        FindPath(area, start, p => target.Contains(p.X, p.Y), step);

    /// <summary>Atalho: caminho até ficar a menos de <paramref name="distance"/> u de um ponto.</summary>
    public static IReadOnlyList<MapPoint>? FindPathNear(IWalkArea area, MapPoint start, MapPoint target, float distance, float step = 4f) =>
        FindPath(area, start, p => ((p.X - target.X) * (p.X - target.X)) + ((p.Y - target.Y) * (p.Y - target.Y)) < distance * distance, step);

    private static List<MapPoint> Rebuild(Dictionary<(int, int), (int, int)> cameFrom, (int, int) end, Func<(int, int), MapPoint> at)
    {
        var path = new List<MapPoint>();
        var node = end;
        while (true)
        {
            path.Add(at(node));
            var previous = cameFrom[node];
            if (previous == node)
            {
                break;
            }

            node = previous;
        }

        path.Reverse();
        return path;
    }
}
