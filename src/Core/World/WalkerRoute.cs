using Vespera.Core.Common;

namespace Vespera.Core.World;

/// <summary>
/// Percurso de um NPC com rotina, pronto para andar sem atravessar parede (DECISIONS D6/D41). Cada waypoint da v2
/// que cai em área bloqueada é levado ao ponto andável mais próximo; cada trecho entre dois waypoints é uma linha
/// reta quando dá, senão o caminho do <see cref="WalkPathfinder"/> (BFS de 4 u) encurtado por "puxar o fio".
/// Construir custa uma BFS por trecho bloqueado: faça fora do frame (thread) e guarde em cache por mapa.
/// </summary>
public sealed class WalkerRoute
{
    private WalkerRoute(IReadOnlyList<MapPoint> waypoints, IReadOnlyList<IReadOnlyList<MapPoint>?> legs)
    {
        Waypoints = waypoints;
        Legs = legs;
    }

    /// <summary>Waypoints corrigidos (mesma ordem da v2).</summary>
    public IReadOnlyList<MapPoint> Waypoints { get; }

    /// <summary>
    /// <c>Legs[i]</c> = pontos de <c>Waypoints[i]</c> até <c>Waypoints[(i+1) % n]</c> (inclui os dois), ou null se não há
    /// caminho (o NPC espera e segue para o próximo).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<MapPoint>?> Legs { get; }

    public static WalkerRoute Build(IWalkArea area, IReadOnlyList<Point2> path, float radius = WalkMover.NpcRadius)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(path);
        var waypoints = new List<MapPoint>(path.Count);
        foreach (var p in path)
        {
            var fixedPoint = area.NearestWalkable((float)p.X, (float)p.Y, radius, maxDistance: 160f) ?? new MapPoint((float)p.X, (float)p.Y);
            waypoints.Add(fixedPoint);
        }

        var legs = new List<IReadOnlyList<MapPoint>?>(waypoints.Count);
        for (var i = 0; i < waypoints.Count; i++)
        {
            legs.Add(BuildLeg(area, waypoints[i], waypoints[(i + 1) % waypoints.Count], radius));
        }

        return new WalkerRoute(waypoints, legs);
    }

    private static List<MapPoint>? BuildLeg(IWalkArea area, MapPoint from, MapPoint to, float radius)
    {
        if (!area.CanStand(from.X, from.Y, radius) || !area.CanStand(to.X, to.Y, radius))
        {
            return null;
        }

        if (WalkMover.IsClear(area, from, to, radius))
        {
            return [from, to];
        }

        var path = WalkPathfinder.FindPath(area, from, p => WalkMover.Hypot(p.X - to.X, p.Y - to.Y) < 4f, radius: radius);
        if (path is null)
        {
            return null;
        }

        var points = new List<MapPoint>(path);
        if (WalkMover.IsClear(area, points[^1], to, radius))
        {
            points.Add(to);
        }

        return Smooth(area, points, radius);
    }

    /// <summary>
    /// "Puxa o fio": a partir de cada ponto, avança enquanto a linha reta até o próximo ponto do caminho continua livre.
    /// O resultado só tem segmentos livres (<see cref="WalkMover.IsClear"/>).
    /// </summary>
    public static List<MapPoint> Smooth(IWalkArea area, IReadOnlyList<MapPoint> points, float radius = WalkMover.NpcRadius)
    {
        ArgumentNullException.ThrowIfNull(points);
        var result = new List<MapPoint>();
        if (points.Count == 0)
        {
            return result;
        }

        var i = 0;
        result.Add(points[0]);
        while (i < points.Count - 1)
        {
            var next = i + 1;
            while (next + 1 < points.Count && WalkMover.IsClear(area, points[i], points[next + 1], radius))
            {
                next++;
            }

            result.Add(points[next]);
            i = next;
        }

        return result;
    }
}
