namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class WalkPathfinderTest
{
    private static readonly Lazy<MapCatalog> Real = new(() =>
        MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json"))));

    // Sala de cima e sala de baixo ligadas por um corredor estreito à direita.
    private static ShapeWalkArea TwoRooms() => new(
        120,
        120,
        [
            WalkShape.FromRect(new MapRect(0, 0, 120, 40)),
            WalkShape.FromRect(new MapRect(100, 40, 20, 40)),
            WalkShape.FromRect(new MapRect(0, 80, 120, 40)),
        ],
        []);

    [TestCase]
    public void FindsPathAroundWalls()
    {
        var area = TwoRooms();
        var path = WalkPathfinder.FindPathInto(area, new MapPoint(20, 20), new MapRect(10, 95, 20, 20));
        AssertThat(path).IsNotNull();
        AssertThat(path![0]).IsEqual(new MapPoint(20, 20));
        foreach (var p in path)
        {
            AssertThat(area.CanStand(p.X, p.Y)).IsTrue();
        }

        // Precisa passar pelo corredor (x >= 104).
        AssertThat(path.Any(p => p.Y > 45 && p.Y < 75 && p.X >= 104)).IsTrue();
    }

    [TestCase]
    public void ReturnsNullWhenUnreachable()
    {
        var area = new ShapeWalkArea(100, 100, [WalkShape.FromRect(new MapRect(0, 0, 40, 100)), WalkShape.FromRect(new MapRect(60, 0, 40, 100))], []);
        AssertThat(WalkPathfinder.FindPathInto(area, new MapPoint(20, 50), new MapRect(70, 40, 20, 20))).IsNull();
    }

    [TestCase]
    public void ReturnsNullFromABlockedStart()
        => AssertThat(WalkPathfinder.FindPathInto(TwoRooms(), new MapPoint(50, 60), new MapRect(0, 0, 10, 10))).IsNull();

    [TestCase]
    public void StartInsideGoalIsAOnePointPath()
        => AssertThat(WalkPathfinder.FindPathInto(TwoRooms(), new MapPoint(20, 20), new MapRect(0, 0, 40, 40))!.Count).IsEqual(1);

    /// <summary>Em todo mapa por formas, spawn e chegadas alcançam todas as saídas, a fuga e os interagíveis.</summary>
    [TestCase]
    public void ShapeMapsAreConnected()
    {
        var failures = new List<string>();
        foreach (var map in Real.Value.Maps.Where(m => !m.HasMask))
        {
            var area = map.CreateShapeWalkArea();
            var starts = new List<(string, MapPoint)> { ("spawn", map.Spawn) };
            starts.AddRange(map.Arrivals.Select(a => (a.Key, a.Value)));
            foreach (var (name, start) in starts)
            {
                foreach (var exit in map.Exits)
                {
                    if (WalkPathfinder.FindPathInto(area, start, exit.Area) is null)
                    {
                        failures.Add($"{map.Id}: {name} não chega na saída para {exit.To}");
                    }
                }

                if (map.Back is { } back && WalkPathfinder.FindPathInto(area, start, back) is null)
                {
                    failures.Add($"{map.Id}: {name} não chega na fuga");
                }

                foreach (var it in map.Interactables)
                {
                    if (WalkPathfinder.FindPathNear(area, start, new MapPoint(it.X, it.Y), it.Radius) is null)
                    {
                        failures.Add($"{map.Id}: {name} não alcança '{it.Label}'");
                    }
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void CoreBossAndExitPortalAreReachable()
    {
        var core = Real.Value["core"];
        var area = core.CreateShapeWalkArea();
        AssertThat(WalkPathfinder.FindPathNear(area, core.Spawn, core.BossDefault!.Value, 26)).IsNotNull();
        AssertThat(WalkPathfinder.FindPathNear(area, core.Spawn, core.ExitDefault!.Value, 26)).IsNotNull(); // r do portal de saída
    }
}
