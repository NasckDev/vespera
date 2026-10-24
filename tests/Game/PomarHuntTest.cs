using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>Caçadas da Brecha do Pomar Podre (Ato II): monstros do pomar, enxame de Ratos, Covil da Macieira Oca e pontos no chão.</summary>
[TestSuite]
public class PomarHuntTest
{
    private static readonly string[] PomarMonsters = ["carrapicho", "rato_lanterna"];

    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_pomar.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    [TestCase]
    public void PomarRunsUseOnlyOrchardGroundsAndMonsters()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 100; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            AssertThat(plan.Rooms[^1].Ground).IsEqual("pomar_arena");
            foreach (var room in plan.Rooms)
            {
                AssertThat(room.Ground.StartsWith("pomar_", StringComparison.Ordinal)).IsTrue();
                foreach (var spawn in room.Waves.SelectMany(w => w))
                {
                    AssertThat(PomarMonsters).Contains(spawn.Monster);
                }
            }
        }
    }

    [TestCase]
    public void FirstRoomHasNoRatsAndRatsArriveInSwarms()
    {
        var planner = new RunPlanner(Config);
        var swarms = 0;
        for (var seed = 1; seed <= 100; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == "carrapicho")).IsTrue();
            foreach (var wave in plan.Rooms.SelectMany(r => r.Waves))
            {
                var rats = wave.Count(s => s.Monster == "rato_lanterna");
                AssertThat(rats % Config.SecondSwarm).IsEqual(0);
                swarms += rats > 0 ? 1 : 0;
            }
        }

        AssertThat(swarms).IsGreater(0);
    }

    [TestCase]
    public void GuardianRunEndsInTheMacieiraLair()
    {
        var plan = new RunPlanner(Config).Plan(5, 2, true);
        AssertThat(plan.HasGuardian).IsTrue();
        AssertThat(plan.Rooms[^1].Ground).IsEqual("pomar_arena");
        AssertThat(plan.Rooms[^1].Kind).IsEqual(RoomKind.Guardian);
    }

    [TestCase]
    public void EsgotoConfigKeepsActOneDefaults()
    {
        var config = HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada.json")));
        AssertThat(config.FinalGround).IsEqual("esgoto_arena");
        AssertThat(config.CommonMonster).IsEqual("lodo");
        AssertThat(config.SecondMonster).IsEqual("espectro");
        AssertThat(config.SecondSwarm).IsEqual(1);
    }

    [TestCase]
    public void EverySpawnPointIsOnWalkableFloor()
    {
        foreach (var (id, ground) in Config.Grounds)
        {
            var level = Level(id);
            var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
            foreach (var p in ground.Spawns.Concat(ground.Breakables))
            {
                AssertThat(grid.IsBlockedAt(p[0], p[1])).OverrideFailureMessage($"{id}: ponto {p[0]},{p[1]} em parede").IsFalse();
            }
        }
    }
}
