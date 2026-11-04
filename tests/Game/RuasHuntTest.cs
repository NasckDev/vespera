using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Ato VI (ROTEIRO-V2 §13, "A Sopa da Véspera"): Brecha das Ruas Invertidas direto à arena do Inquilino (as salas
/// esperam a issue #47), hub das Ruas Baixas ligado às Forjas depois do Sineiro Mudo e as falas do ato.
/// </summary>
[TestSuite]
public class RuasHuntTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_ruas.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static LevelGrid Grid(LevelData level) => LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

    [TestCase]
    public void EveryRuasRunGoesStraightToTheInquilino()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 50; seed++)
        {
            var plan = planner.Plan(seed, seed % 3, seed % 2 == 0);
            AssertThat(plan.Rooms.Count).IsEqual(1);
            AssertThat(plan.Rooms[0].Ground).IsEqual("ruas_arena");
            AssertThat(plan.Rooms[0].Kind).IsEqual(RoomKind.Guardian);
            AssertThat(plan.HasGuardian).IsTrue();
            AssertThat(plan.MonsterCount).IsEqual(0);
        }
    }

    [TestCase]
    public void GuardianOnlyNeedsTheArenaToAcceptTheGuardian()
    {
        var config = Config;
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
    public void BrechaRuasIsRankSWithTheInquilinoInThreePhases()
    {
        var def = BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), "brecha_ruas.json")));
        AssertThat(def.Id).IsEqual("ruas_invertidas");
        AssertThat(def.Rank).IsEqual("S");
        AssertThat(def.GuardianId).IsEqual("inquilino");
        AssertThat(def.Hub).IsEqual("ruas_baixas");
        AssertThat(def.Rooms).ContainsExactly("ruas_arena");
        AssertThat(def.Monsters["inquilino"].Boss).IsTrue();
        AssertThat(def.Monsters["inquilino"].Telegraph).IsBetween(0.4, 0.8);
        AssertThat(def.Boss.Phases).ContainsExactly(0.66, 0.33);
        AssertThat(def.Boss.Cores).IsEqual(4);
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

        foreach (var p in Config.Grounds["ruas_arena"].Spawns)
        {
            AssertThat(grid.IsBlockedAt(p[0], p[1])).IsFalse();
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
