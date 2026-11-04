using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Ato VI (ROTEIRO-V2 §13, "A Sopa da Véspera"): Brecha das Ruas Invertidas com as 4 salas da issue #47 e os monstros
/// da #48 (Varal, Despejado, Rasgo) até a arena do Inquilino, hub das Ruas Baixas ligado às Forjas depois do Sineiro
/// Mudo e as falas do ato.
/// </summary>
[TestSuite]
public class RuasHuntTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_ruas.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static LevelGrid Grid(LevelData level) => LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

    private static readonly string[] RuasMonsters = ["varal", "despejado", "rasgo"];

    [TestCase]
    public void RuasRunsUseOnlyStreetRoomsAndMonsters()
    {
        var planner = new RunPlanner(Config);
        var seen = new HashSet<string>();
        for (var seed = 1; seed <= 100; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            AssertThat(plan.Rooms.Count).IsGreaterEqual(Config.RoomsMin);
            AssertThat(plan.Rooms[^1].Ground).IsEqual("ruas_arena");
            AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == "varal")).IsTrue();
            foreach (var room in plan.Rooms)
            {
                AssertThat(room.Ground.StartsWith("ruas_", StringComparison.Ordinal)).IsTrue();
                foreach (var spawn in room.Waves.SelectMany(w => w))
                {
                    AssertThat(RuasMonsters).Contains(spawn.Monster);
                    seen.Add(spawn.Monster);
                }
            }
        }

        AssertThat(seen).ContainsExactlyInAnyOrder(RuasMonsters);
    }

    [TestCase]
    public void InquilinoWaitsAfterOneRunAndTheVisitToRavisMother()
    {
        AssertThat(Config.GuardianOnly).IsFalse();
        AssertThat(Config.GuardianAfterRuns).IsEqual(1);
        AssertThat(Config.GuardianFlag).IsEqual("met_mae_ravi");
        var plan = new RunPlanner(Config).Plan(11, 1, true);
        AssertThat(plan.HasGuardian).IsTrue();
        AssertThat(plan.Rooms[^1].Ground).IsEqual("ruas_arena");
        AssertThat(plan.Rooms[^2].Ground).IsEqual("ruas_cozinha");
        AssertThat(plan.Rooms[^2].Kind).IsEqual(RoomKind.Rest);
    }

    [TestCase]
    public void GuardianOnlyNeedsTheArenaToAcceptTheGuardian()
    {
        var config = Config;
        config.GuardianOnly = true;
        config.Grounds["ruas_arena"].Kinds = [RoomKind.Combat];
        AssertThat(config.Validate()).IsNotEmpty();
    }

    [TestCase]
    public void OtherHuntsKeepTheirRooms()
    {
        var pomar = HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_pomar.json")));
        AssertThat(pomar.GuardianOnly).IsFalse();
        AssertThat(new RunPlanner(pomar).Plan(3, 1, false).Rooms.Count).IsGreaterEqual(pomar.RoomsMin);
    }

    [TestCase]
    public void BrechaRuasIsRankSWithItsRoomsMonstersAndTheInquilino()
    {
        var def = BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), "brecha_ruas.json")));
        AssertThat(def.Id).IsEqual("ruas_invertidas");
        AssertThat(def.Rank).IsEqual("S");
        AssertThat(def.GuardianId).IsEqual("inquilino");
        AssertThat(def.Hub).IsEqual("ruas_baixas");
        AssertThat(def.Rooms).ContainsExactly("ruas_viela", "ruas_largo", "ruas_cozinha", "ruas_volta", "ruas_arena");
        foreach (var id in RuasMonsters.Append("inquilino"))
        {
            // Todo golpe forte tem aviso âmbar de 0,4 a 0,8 s (PLANO §2).
            AssertThat(def.Monsters[id].Telegraph).OverrideFailureMessage(id).IsBetween(0.4, 0.8);
        }

        AssertThat(def.Monsters["inquilino"].Boss).IsTrue();
        AssertThat(def.Boss.Phases).ContainsExactly(0.66, 0.33);
        AssertThat(def.Boss.Cores).IsEqual(4);
    }

    [TestCase]
    public void EveryRuasRoomIsWalkableFromTheEntrance()
    {
        foreach (var (id, ground) in Config.Grounds)
        {
            var level = Level(id);
            var grid = Grid(level);
            var reach = Reachable(grid, level.Spawn);
            var rift = level.Rift!;
            var points = ground.Spawns.Concat(ground.Breakables).ToList();
            if (ground.Rest.Length == 2)
            {
                points.Add(ground.Rest);
            }

            foreach (var p in new[] { rift.Chest, rift.ExitPortal, rift.Boss?.At, rift.Exit is { } e ? new[] { e.Barrier[0] - 8, e.Barrier[1] + (e.Barrier[3] / 2) } : null })
            {
                if (p is not null)
                {
                    points.Add(p);
                }
            }

            foreach (var p in points)
            {
                AssertThat(reach.Contains(Cell(grid, p))).OverrideFailureMessage($"{id}: ponto {p[0]},{p[1]} fora do chão andável a partir da entrada").IsTrue();
            }
        }
    }

    [TestCase]
    public void CozinhaIsTheRestRoomWithTheLanternOnTheFloor()
    {
        var cozinha = Config.Grounds["ruas_cozinha"];
        AssertThat(cozinha.Kinds).Contains(RoomKind.Rest);
        AssertThat(cozinha.Rest).HasSize(2);
    }

    private static (int C, int R) Cell(LevelGrid grid, float[] p) => ((int)(p[0] / grid.Cell), (int)(p[1] / grid.Cell));

    /// <summary>Células andáveis ligadas à entrada (4 vizinhos).</summary>
    internal static HashSet<(int C, int R)> Reachable(LevelGrid grid, float[] from)
    {
        var start = Cell(grid, from);
        var seen = new HashSet<(int C, int R)>();
        var queue = new Queue<(int C, int R)>();
        if (!grid.IsBlocked(start.C, start.R))
        {
            queue.Enqueue(start);
            seen.Add(start);
        }

        while (queue.Count > 0)
        {
            var (c, r) = queue.Dequeue();
            foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (c + dc, r + dr);
                if (!grid.IsBlocked(next.Item1, next.Item2) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    [TestCase]
    public void ArenaHasTheInquilinoOnWalkableFloor()
    {
        var arena = Level("ruas_arena");
        var grid = Grid(arena);
        AssertThat(arena.Rift).IsNotNull();
        AssertThat(arena.Rift!.Boss!.Type).IsEqual("inquilino");
        foreach (var p in new[] { arena.Spawn, arena.Rift.Boss.At, arena.Rift.ExitPortal! })
        {
            AssertThat(grid.IsBlockedAt(p[0], p[1])).OverrideFailureMessage($"ponto {p[0]},{p[1]} em parede").IsFalse();
        }
    }

    [TestCase]
    public void HubIsReachedFromTheForgesAfterTheSineiroMudo()
    {
        var forjas = Level("forjas");
        var exit = forjas.Zones.Single(z => z.Target == "ruas_baixas");
        AssertThat(exit.Requires).Contains("GuardianDefeated:sineiro_mudo");

        var hub = Level("ruas_baixas");
        var grid = Grid(hub);
        AssertThat(grid.IsBlockedAt(exit.Spawn![0], exit.Spawn[1])).IsFalse();
        var back = hub.Zones.Single(z => z.Target == "forjas");
        AssertThat(grid.IsBlockedAt(back.Spawn![0], back.Spawn[1])).IsFalse();
        AssertThat(Grid(forjas).IsBlockedAt(back.Spawn[0], back.Spawn[1])).IsFalse();
    }

    [TestCase]
    public void HubHasTheBrechaAndTheActSixPeopleOnWalkableFloor()
    {
        var hub = Level("ruas_baixas");
        var grid = Grid(hub);
        var brecha = hub.Zones.Single(z => z.Kind == "brecha");
        AssertThat(brecha.Brecha).IsEqual("ruas_invertidas");
        AssertThat(grid.IsBlockedAt(brecha.At[0], brecha.At[1])).IsFalse();
        AssertThat(hub.Npcs.Select(n => n.Id)).Contains("nilza", "mae_ravi", "celeste", "ravi");
        foreach (var npc in hub.Npcs)
        {
            AssertThat(grid.IsBlockedAt(npc.At[0], npc.At[1])).OverrideFailureMessage($"{npc.Id} em parede").IsFalse();
            AssertThat(File.Exists(Path.Combine(Dir("Dialogue"), $"{npc.Dialogue}.json"))).OverrideFailureMessage(npc.Dialogue).IsTrue();
        }
    }

    [TestCase]
    public void ActSixStoryHasItsObjectivesAndSpeakers()
    {
        var script = StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json")));
        foreach (var id in new[] { "ato6", "ato6_mae", "ato6_brecha", "ato6_fecho", "ato7" })
        {
            AssertThat(script.Objective(id)).OverrideFailureMessage(id).IsNotNull();
        }

        AssertThat(script.Speakers.ContainsKey("mae_ravi")).IsTrue();
        AssertThat(script.Speakers.ContainsKey("inquilino")).IsTrue();
    }

    [TestCase]
    public void EachRuasDeathPlaysANewLineOnce()
    {
        var script = StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json")));
        var engine = new StoryEngine(new StoryProgress(), script);
        var file = DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "ato6", "morte.json")));
        var seen = new List<string>();
        for (var n = 1; n <= 3; n++)
        {
            engine.SetFlag($"ruas_invertidas:fell_{n}");
            var runner = new DialogueRunner(file, engine, "Rui");
            AssertThat(runner.Start()).IsTrue();
            seen.Add(runner.CurrentId!);
            while (runner.Advance())
            {
            }

            AssertThat(engine.HasFlag($"morte_ruas_{n}")).IsTrue();
        }

        AssertThat(seen).ContainsExactly("m1", "m2", "m3");
    }
}
