using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Final;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Final (ROTEIRO-V2 §13 FINAL, "A Primeira Brecha"): a Brecha SSS leva direto à praça de baixo, o Reflexo nasce em
/// chão andável, o sino fica ao alcance, e a história do Final (objetivos, falas antes de entrar, mortes, epílogo) está
/// ligada no roteiro.
/// </summary>
[TestSuite]
public class FinalHuntTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_final.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static LevelGrid Grid(LevelData level) => LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

    private static StoryScript Script => StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json")));

    /// <summary>Motor com a chegada e o Ravi já vistos (senão as falas do começo da Praça tocariam antes).</summary>
    private static StoryEngine Engine()
    {
        var engine = new StoryEngine(new StoryProgress(), Script);
        engine.SetFlag("arrival_done");
        engine.SetFlag("met_ravi");
        return engine;
    }

    private static DialogueFile Dialogue(string id) => DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), $"{id}.json")));

    [TestCase]
    public void EveryFinalRunGoesStraightToTheLowerPlaza()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 30; seed++)
        {
            var plan = planner.Plan(seed, seed % 3, seed % 2 == 0);
            AssertThat(plan.Rooms.Count).IsEqual(1);
            AssertThat(plan.Rooms[0].Ground).IsEqual(FinalStory.ArenaLevel);
            AssertThat(plan.Rooms[0].Kind).IsEqual(RoomKind.Guardian);
        }
    }

    [TestCase]
    public void FinalBrechaIsRankSssWithTheReflexo()
    {
        var def = BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), "brecha_final.json")));
        AssertThat(def.Id).IsEqual(FinalStory.BrechaId);
        AssertThat(def.Rank).IsEqual("SSS");
        AssertThat(def.GuardianId).IsEqual("reflexo");
        AssertThat(def.Hub).IsEqual(FinalStory.HubLevel);
        AssertThat(def.Rooms).ContainsExactly(FinalStory.ArenaLevel);
        AssertThat(def.Monsters["reflexo"].Boss).IsTrue();
        AssertThat(def.Boss.Phases).ContainsExactly(0.66, 0.33);
        AssertThat(def.Moves.Keys).Contains("sword", "bow", "hammer");
    }

    [TestCase]
    public void LowerPlazaHasTheReflexoTheSpawnAndTheBellOnWalkableFloor()
    {
        var arena = Level(FinalStory.ArenaLevel);
        var grid = Grid(arena);
        AssertThat(arena.Rift!.Boss!.Type).IsEqual("reflexo");
        AssertThat(arena.Rift.ExitPortal).IsNull();
        foreach (var p in new[] { arena.Spawn, arena.Rift.Boss.At, [838f, 196f] })
        {
            AssertThat(grid.IsBlockedAt(p[0], p[1])).OverrideFailureMessage($"ponto {p[0]},{p[1]} em parede").IsFalse();
        }

        foreach (var p in Config.Grounds[FinalStory.ArenaLevel].Spawns)
        {
            AssertThat(grid.IsBlockedAt(p[0], p[1])).IsFalse();
        }

        // O poste da grande lanterna apagada é obstáculo.
        AssertThat(grid.IsBlockedAt(838, 300)).IsTrue();
    }

    [TestCase]
    public void FinalStoryHasItsObjectivesAndOpensAfterActSeven()
    {
        var script = Script;
        foreach (var id in new[] { "final", "final_reflexo", "final_sino", "final_fim" })
        {
            AssertThat(script.Objective(id)).OverrideFailureMessage(id).IsNotNull();
        }

        var engine = Engine();
        engine.SetFlag(FinalStory.UnlockFlag);
        AssertThat(engine.ObjectiveId).IsEqual("final");
        AssertThat(engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue).IsEqual("final/praca");
    }

    [TestCase]
    public void TalkingBeforeEnteringEndsWithTheEnterEvent()
    {
        var engine = Engine();
        var runner = new DialogueRunner(Dialogue("final/antes"), engine, "Rui");
        var events = new List<string>();
        engine.EventRaised += events.Add;
        AssertThat(runner.Start()).IsTrue();
        var enter = runner.Choices.ToList().FindIndex(c => c.Text == "Entrar.");
        AssertThat(enter).IsGreaterEqual(0);
        runner.Advance(enter);
        while (runner.Advance())
        {
        }

        AssertThat(events).Contains("EnterBreach:" + FinalStory.BrechaId);
    }

    [TestCase]
    public void EachFinalDeathPlaysANewLineOnce()
    {
        var engine = Engine();
        var file = Dialogue("final/morte");
        var seen = new List<string>();
        for (var n = 1; n <= 3; n++)
        {
            engine.SetFlag($"{FinalStory.BrechaId}:fell_{n}");
            AssertThat(engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue).IsEqual("final/morte");
            var runner = new DialogueRunner(file, engine, "Rui");
            AssertThat(runner.Start()).IsTrue();
            seen.Add(runner.CurrentId!);
            while (runner.Advance())
            {
            }

            AssertThat(engine.HasFlag($"morte_final_{n}")).IsTrue();
        }

        AssertThat(seen).ContainsExactly("m1", "m2", "m3");
    }

    [TestCase]
    public void AfterTheBellTheMorningPlaysTheRightEpilogue()
    {
        var engine = Engine();
        engine.SetFlag(FinalStory.UnlockFlag);
        engine.SetFlag("final_praca_visto");
        engine.SetFlag(FinalStory.AlvorFlag);
        AssertThat(engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue).IsEqual("final/epilogo");
        engine.SetFlag("final_benicio");
        AssertThat(engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue).IsEqual("final/epilogo_sem_benicio");
        engine.SetFlag(FinalStory.PartOneDoneFlag);
        AssertThat(engine.AutoplayFor(FinalStory.HubLevel)).IsNull();
        AssertThat(engine.ObjectiveId).IsEqual("final_fim");
    }
}
