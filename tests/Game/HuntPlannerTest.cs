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
            AssertThat(plan.Rooms[^1].MonsterCount).IsLess(plan.MonsterCount / 2);
            foreach (var room in plan.Rooms)
            {
                AssertThat(Config.Grounds[room.Ground].Kinds).Contains(room.Kind);
            }
        }

        AssertThat(counts).Contains(5, 6, 7);
    }

    [TestCase]
    public void NoTwoRoomsInARowShareTheSameGround()
    {
        var planner = new RunPlanner(Config);
        foreach (var guardian in new[] { false, true })
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var plan = planner.Plan(seed, seed % 4, guardian);
                for (var i = 1; i < plan.Rooms.Count; i++)
                {
                    AssertThat(plan.Rooms[i].Ground).OverrideFailureMessage($"semente {seed}: salas {i} e {i + 1} iguais ({plan.Rooms[i].Ground})")
                        .IsNotEqual(plan.Rooms[i - 1].Ground);
                }
            }
        }
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
        AssertThat(unlocked.Rooms.Count).IsEqual(Config.GuardianRunRooms);
        AssertThat(unlocked.Rooms[^2].Kind).IsEqual(RoomKind.Rest);
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
    public void SevenContractsTwoOfferedGuardianOnesOnlyWithTheGuardian()
    {
        var board = new ContractBoard(Config, id => id);
        AssertThat(Config.Contracts.Kinds.Count).IsEqual(7);
        for (var seed = 1; seed <= 60; seed++)
        {
            var plain = board.Roll(new SeededRandom(seed));
            AssertThat(plain.Count).IsEqual(2);
            AssertThat(plain.Select(c => c.Kind).Distinct().Count()).IsEqual(2);
            AssertThat(plain.Any(c => Config.Contracts.Kinds[c.Kind].Guardian)).IsFalse();
        }

        AssertThat(Enumerable.Range(1, 60).Any(seed => board.Roll(new SeededRandom(seed), guardian: true).Any(c => Config.Contracts.Kinds[c.Kind].Guardian))).IsTrue();
    }

    [TestCase]
    public void ContractsEvaluateAgainstTheRun()
    {
        var board = new ContractBoard(Config, id => id);
        var report = new RunReport
        {
            Survived = true, Completed = true, Breaks = 6, EspectroKills = 3, TreasureFound = true, FirstRoomFlawless = true,
            EndHpFraction = 0.95, GuardianDefeated = true, GuardianSeconds = 90, GuardianCores = 3, GuardianCoresBroken = 3,
        };
        foreach (var kind in Config.Contracts.Kinds.Keys)
        {
            var c = new Contract { Kind = kind, Target = Math.Max(1, Config.Contracts.Kinds[kind].Min) };
            AssertThat(board.Evaluate(c, report).Done).OverrideFailureMessage($"{kind} deveria valer").IsTrue();
            AssertThat(board.Evaluate(c, null).Done).IsFalse();
        }

        var slow = new RunReport { Survived = true, Completed = true, GuardianDefeated = true, GuardianSeconds = 150, EndHpFraction = 0.5 };
        AssertThat(board.Evaluate(new Contract { Kind = "guardiao_rapido" }, slow).Done).IsFalse();
        AssertThat(board.Evaluate(new Contract { Kind = "vida_cheia" }, slow).Done).IsFalse();
        AssertThat(board.Describe(new Contract { Kind = "quebrar", Target = 5 })).Contains("5 barris");
    }
}
