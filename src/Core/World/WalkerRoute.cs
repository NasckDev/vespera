using Vespera.Core.Common;

namespace Vespera.Core.World;

/// <summary>
/// Percurso de um NPC com rotina, pronto para andar sem atravessar parede (DECISIONS D6/B2-D41/B2-D48), calculado em tempo de
/// execução sobre a área do mapa carregado (vale para qualquer máscara). Cada waypoint da v2 vai para o ponto "firme" mais
/// próximo da região ligada comum (<see cref="WalkPlanner.NearestPerRegion"/>); cada trecho entre dois waypoints é uma reta livre ou o A* do
/// <see cref="WalkPlanner"/> encurtado. Construir custa uma busca por trecho bloqueado: faça fora do frame (thread) e
/// guarde em cache por mapa. O <see cref="TownWalker"/> replaneja sozinho se travar no caminho.
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
    /// caminho (o NPC tenta replanejar dali e, se nem assim, espera e segue para o próximo).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<MapPoint>?> Legs { get; }

    public static WalkerRoute Build(IWalkArea area, IReadOnlyList<Point2> path, float radius = WalkMover.NpcRadius) =>
        Build(new WalkPlanner(area, radius), path);

    /// <summary>Até onde um waypoint pode ser levado para chegar a um ponto andável (u).</summary>
    public const float SettleDistance = 160f;

    /// <summary>
    /// Mesma coisa com um planejador já criado para a área (uma instância por thread). Os waypoints vão todos para a mesma
    /// região ligada da malha: a que tem ponto livre perto do maior número deles (empate: a maior). Assim um waypoint da v2
    /// dentro de uma parede não cai num bolsão fechado (um quintal, o miolo de um prédio) de onde o NPC não sai.
    /// </summary>
    public static WalkerRoute Build(WalkPlanner planner, IReadOnlyList<Point2> path)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(path);
        planner.Prepare();
        var near = new List<Dictionary<int, (float DistanceSq, MapPoint Point)>>(path.Count);
        var votes = new Dictionary<int, int>();
        foreach (var p in path)
        {
            var candidates = new Dictionary<int, (float DistanceSq, MapPoint Point)>();
            planner.NearestPerRegion(new MapPoint((float)p.X, (float)p.Y), SettleDistance, candidates);
            near.Add(candidates);
            foreach (var region in candidates.Keys)
            {
                votes[region] = votes.GetValueOrDefault(region) + 1;
            }
        }

        var chosen = 0;
        foreach (var (region, count) in votes)
        {
            if (region == 0)
            {
                continue;
            }

            if (chosen == 0 || count > votes[chosen] || (count == votes[chosen] && planner.RegionSize(region) > planner.RegionSize(chosen)))
            {
                chosen = region;
            }
        }

        var waypoints = new List<MapPoint>(path.Count);
        for (var i = 0; i < path.Count; i++)
        {
            var raw = new MapPoint((float)path[i].X, (float)path[i].Y);
            if (planner.CanStandFirmly(raw) && planner.RegionOf(raw) == chosen)
            {
                waypoints.Add(raw); // o ponto da v2 serve como está
            }
            else if (near[i].TryGetValue(chosen, out var candidate))
            {
                waypoints.Add(candidate.Point);
            }
            else
            {
                waypoints.Add(planner.Settle(raw, SettleDistance) ?? raw);
            }
        }

        var legs = new List<IReadOnlyList<MapPoint>?>(waypoints.Count);
        var buffer = new List<MapPoint>();
        for (var i = 0; i < waypoints.Count; i++)
        {
            var ok = planner.TryPlan(waypoints[i], waypoints[(i + 1) % waypoints.Count], buffer);
            legs.Add(ok ? buffer.ToArray() : null);
        }

        return new WalkerRoute(waypoints, legs);
    }
}
