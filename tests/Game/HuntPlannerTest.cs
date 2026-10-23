using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Common;
using Vespera.Core.Hunt;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>Caçadas montadas por semente (playtest 23/10): salas, ondas, elites, descanso, Guardião e contratos.</summary>
[TestSuite]
public class HuntPlannerTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static bool FeetFit(LevelData level, float x, float y, float factor = 1f)
    {
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
        var s = level.ActorScale * factor;
        var feet = CollisionMath.FeetCapsule(x, y, 22f * s, 11f * s);
        return !CollisionMath.CapsuleHitsGrid(grid, feet, 0f);
    }

    [TestCase]
    public void SameSeedSameRun()
    {
        var planner = new RunPlanner(Config);
        var a = planner.Plan(77, 1, false);
        var b = planner.Plan(77, 1, false);
        AssertThat(a.Rooms.Count).IsEqual(b.Rooms.Count);
        AssertThat(string.Join(",", a.Rooms.Select(r => $"{r.Ground}/{r.Kind}/{r.MonsterCount}")))
            .IsEqual(string.Join(",", b.Rooms.Select(r => $"{r.Ground}/{r.Kind}/{r.MonsterCount}")));
    }

    [TestCase]
    public void EveryRunHasFiveToSevenRoomsRestBeforeTheEndAndRealWaves()
    {
        var planner = new RunPlanner(Config);
        var counts = new HashSet<int>();
        for (var seed = 1; seed <= 200; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            counts.Add(plan.Rooms.Count);
            AssertThat(plan.Rooms.Count).IsBetween(5, 7);
            AssertThat(plan.Rooms[0].Kind).IsEqual(RoomKind.Combat);
            AssertThat(plan.Rooms[^2].Kind).IsEqual(RoomKind.Rest);
            AssertThat(plan.Rooms[^1].Kind).IsEqual(RoomKind.Elite);
            AssertThat(plan.Rooms.Count(r => r.Kind == RoomKind.Treasure)).IsLessEqual(1);
            AssertThat(plan.MonsterCount).IsGreaterEqual(20);
            AssertThat(plan.Rooms[^1].EliteCount).IsGreaterEqual(2);
            foreach (var room in plan.Rooms)
            {
                AssertThat(Config.Grounds[room.Ground].Kinds).Contains(room.Kind);
            }
        }

        AssertThat(counts).Contains(5, 6, 7);
    }

    [TestCase]
    public void GuardianOnlyWhenTheGateIsOpen()
    {
        var planner = new RunPlanner(Config);
        AssertThat(planner.Plan(5, 0, false).HasGuardian).IsFalse();
        AssertThat(planner.Plan(5, 9, false).HasGuardian).IsFalse();
        var unlocked = planner.Plan(5, 2, true);
        AssertThat(unlocked.HasGuardian).IsTrue();
        AssertThat(unlocked.Rooms[^1].Ground).IsEqual("esgoto_arena");
        AssertThat(unlocked.Rooms[^1].Waves.Count).IsEqual(0);
    }

    [TestCase]
    public void FirstRunIsShortAndStartsGentle()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 50; seed++)
        {
            var plan = planner.Plan(seed, 0, false);
            AssertThat(plan.Rooms.Count).IsEqual(Config.FirstRunRooms);
            AssertThat(plan.Rooms[0].Waves.Max(w => w.Count)).IsLessEqual(Config.FirstWaveSize);
            AssertThat(plan.Rooms[0].EliteCount).IsEqual(0);
            AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == "lodo")).IsTrue();
        }
    }

    [TestCase]
    public void LaterRunsHaveMoreMonsters()
    {
        var planner = new RunPlanner(Config);
        double Average(int done) => Enumerable.Range(1, 100).Average(seed =>
        {
            var plan = planner.Plan(seed, done, false);
            return plan.MonsterCount / (double)plan.Rooms.Count;
        });

        AssertThat(Average(2)).IsGreater(Average(1));
        AssertThat(Average(1)).IsGreater(Average(0));
    }

    [TestCase]
    public void SpawnRestAndBreakablePointsAreOnTheFloor()
    {
        foreach (var (id, ground) in Config.Grounds)
        {
            var level = Level(id);
            foreach (var p in ground.Spawns.Concat(ground.Breakables))
            {
                AssertThat(FeetFit(level, p[0], p[1], 1.6f)).OverrideFailureMessage($"{id}: ponto [{p[0]}, {p[1]}] fora do chão").IsTrue();
            }

            if (ground.Rest.Length == 2)
            {
                AssertThat(FeetFit(level, ground.Rest[0], ground.Rest[1], 1.6f)).OverrideFailureMessage($"{id}: lanterna fora do chão").IsTrue();
            }
        }
    }

    [TestCase]
    public void ContractsAreThreeDistinctAndEvaluateAgainstTheRun()
    {
        var board = new ContractBoard(Config, id => id == "gel_de_lodo" ? "Gel de Lodo" : "Véu de Espectro");
        var contracts = board.Roll(new SeededRandom(3));
        AssertThat(contracts.Count).IsEqual(3);
        AssertThat(contracts.Select(c => c.Kind).Distinct().Count()).IsEqual(3);

        var report = new RunReport { Survived = true, Completed = true, WavesSurvived = 9, FlawlessRooms = 1 };
        report.Materials["gel_de_lodo"] = 6;
        report.Materials["veu_de_espectro"] = 6;
        foreach (var kind in Config.Contracts.Kinds.Keys)
        {
            var c = new Contract { Kind = kind, Target = 5, Material = "gel_de_lodo" };
            AssertThat(board.Evaluate(c, report).Done).IsTrue();
            AssertThat(board.Evaluate(c, null).Done).IsFalse();
        }

        var died = new RunReport { Survived = false, Completed = false, WavesSurvived = 2 };
        AssertThat(board.Evaluate(new Contract { Kind = "cacada" }, died).Done).IsFalse();
        AssertThat(board.Describe(new Contract { Kind = "coleta", Target = 4, Material = "gel_de_lodo" })).Contains("4 Gel de Lodo");
    }
}
