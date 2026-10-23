namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Save;
using Vespera.Core.Story;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class StoryTest
{
    private static string DialogueDir => Path.Combine(AppContext.BaseDirectory, "Dialogue");

    private static StoryScript LoadScript() => StoryScript.Parse(File.ReadAllText(Path.Combine(DialogueDir, "story.json")));

    private static DialogueFile LoadDialogue(string id) => DialogueFile.Parse(File.ReadAllText(Path.Combine(DialogueDir, $"{id}.json")));

    private static StoryEngine NewEngine(int rank = 0) => new(new StoryProgress(), LoadScript(), () => rank);

    private static DialogueRunner Runner(string id, StoryEngine engine) => new(LoadDialogue(id), engine, "Rui");

    /// <summary>Anda pela conversa escolhendo sempre a primeira resposta; devolve quantas caixas passou.</summary>
    private static int RunToEnd(DialogueRunner runner)
    {
        var boxes = 0;
        AssertThat(runner.Start()).IsTrue();
        do
        {
            boxes++;
            AssertThat(boxes).IsLess(60);
        }
        while (runner.Advance(0));
        return boxes;
    }

    [TestCase]
    public void Conditions_FlagsNegationObjectiveAndRank()
    {
        var engine = NewEngine(rank: 1);
        engine.SetFlag("a");
        engine.SetObjective("ravi");

        AssertThat(StoryConditions.Evaluate(["a"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["a", "!b"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["a", "b"], engine)).IsFalse();
        AssertThat(StoryConditions.Evaluate(["objective:ravi"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["!objective:ravi"], engine)).IsFalse();
        AssertThat(StoryConditions.Evaluate(["rank>=D"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["rank>=C"], engine)).IsFalse();
        AssertThat(StoryConditions.Evaluate(["rank<C"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["rank==D"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(null, engine)).IsTrue();
    }

    [TestCase]
    [ThrowsException(typeof(FormatException))]
    public void Conditions_UnknownRankIsAnError() => StoryConditions.Evaluate(["rank>=Z"], NewEngine());

    [TestCase]
    public void Engine_SetFlagIsIdempotentAndFiresTrigger()
    {
        var engine = NewEngine();
        var changes = 0;
        engine.Changed += () => changes++;

        AssertThat(engine.SetFlag(StoryFlags.GuardianDefeatedLodoMae)).IsTrue();
        AssertThat(engine.SetFlag(StoryFlags.GuardianDefeatedLodoMae)).IsFalse();

        AssertThat(engine.ObjectiveId).IsEqual("ato2");
        AssertThat(engine.HasFlag("act1_done")).IsTrue();
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void Engine_UnknownObjectiveIsRejected() => NewEngine().SetObjective("nao_existe");

    [TestCase]
    public void Engine_NewCampaignStartsAtFirstObjectiveOnlyOnce()
    {
        var engine = NewEngine();
        engine.BeginCampaignIfNew();
        AssertThat(engine.ObjectiveId).IsEqual("quadro");

        engine.SetObjective("odete");
        engine.BeginCampaignIfNew();
        AssertThat(engine.ObjectiveId).IsEqual("odete");
    }

    [TestCase]
    public void Autoplay_ArrivalPlaysOnceOnPraca()
    {
        var engine = NewEngine();
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("chegada");
        AssertThat(engine.AutoplayFor("forjas")).IsNull();

        RunToEnd(Runner("chegada", engine));

        AssertThat(engine.AutoplayFor("praca")).IsNull();
    }

    [TestCase]
    public void Benicio_RegistersAndMovesObjectiveToRavi()
    {
        var engine = NewEngine();
        engine.BeginCampaignIfNew();

        RunToEnd(Runner("benicio_quadro", engine));

        AssertThat(engine.HasFlag(StoryFlags.Registered)).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("ravi");
    }

    [TestCase]
    public void Benicio_SecondVisitOnlyRemindsAndKeepsObjective()
    {
        var engine = NewEngine();
        RunToEnd(Runner("benicio_quadro", engine));
        var reminder = Runner("benicio_quadro", engine);
        reminder.Start();

        AssertThat(reminder.CurrentId).IsEqual("lembra_ravi");
        AssertThat(engine.ObjectiveId).IsEqual("ravi");
    }

    [TestCase]
    public void Ravi_BeforeBoardSendsYouBack_AfterBoardSendsYouToOdete()
    {
        var engine = NewEngine();
        var early = Runner("ravi", engine);
        early.Start();
        AssertThat(early.CurrentId).IsEqual("rp1");
        AssertThat(engine.HasFlag(StoryFlags.MetRavi)).IsFalse();

        RunToEnd(Runner("benicio_quadro", engine));
        RunToEnd(Runner("ravi", engine));

        AssertThat(engine.HasFlag(StoryFlags.MetRavi)).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("odete");
    }

    [TestCase]
    public void Odete_FirstTalkGivesBladeAndRaisesEvent()
    {
        var engine = NewEngine();
        var events = new List<string>();
        engine.EventRaised += events.Add;

        RunToEnd(Runner("odete", engine));

        AssertThat(engine.HasFlag(StoryFlags.StarterBlade)).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("brecha");
        AssertThat(events).Contains(StoryFlags.EventStarterBladeGiven);
    }

    [TestCase]
    public void Odete_ForgeChoiceRaisesOpenForgeEvent()
    {
        var engine = NewEngine();
        engine.SetFlag(StoryFlags.StarterBlade);
        var events = new List<string>();
        engine.EventRaised += events.Add;
        var runner = Runner("odete", engine);
        runner.Start();

        AssertThat(runner.CurrentId).IsEqual("ob_hub");
        AssertThat(runner.Choices.Count).IsEqual(2);
        runner.Advance(0);

        AssertThat(events).ContainsExactly(StoryFlags.EventOpenForge);
    }

    [TestCase]
    public void Odete_AfterGuardianLineChangesAndObjectivePointsToAct2()
    {
        var engine = NewEngine();
        engine.SetFlag(StoryFlags.StarterBlade);
        var before = Runner("odete", engine);
        before.Start();
        AssertThat(before.CurrentId).IsEqual("ob_hub");

        engine.SetFlag(StoryFlags.GuardianDefeatedLodoMae);
        var after = Runner("odete", engine);
        after.Start();

        AssertThat(after.CurrentId).IsEqual("og1");
        AssertThat(engine.ObjectiveId).IsEqual("ato2");
        AssertThat(engine.Script.Objective("ato2")!.Locked).IsTrue();
    }

    [TestCase]
    public void Runner_FiltersChoicesByCondition()
    {
        var file = DialogueFile.Parse("""
            {"id":"t","entries":[{"node":"a"}],"nodes":{
              "a":{"speaker":"odete","text":"x","choices":[
                {"text":"sempre","next":""},
                {"text":"so com flag","if":["f"],"next":""},
                {"text":"so rank C","if":["rank>=C"],"next":""}]}}}
            """);
        var engine = NewEngine(rank: 0);
        var runner = new DialogueRunner(file, engine, "Rui");
        runner.Start();
        AssertThat(runner.Choices.Count).IsEqual(1);

        engine.SetFlag("f");
        var again = new DialogueRunner(file, engine, "Rui");
        again.Start();
        AssertThat(again.Choices.Count).IsEqual(2);
    }

    [TestCase]
    public void Runner_ChoiceSetsFlagAndSubstitutesPlayerName()
    {
        var file = DialogueFile.Parse("""
            {"id":"t","entries":[{"node":"a"}],"nodes":{
              "a":{"speaker":"odete","text":"Oi, {nome}.","choices":[{"text":"ok","set":["escolheu"],"next":"b"}]},
              "b":{"speaker":"odete","text":"fim"}}}
            """);
        var engine = NewEngine();
        var runner = new DialogueRunner(file, engine, "Rui");
        runner.Start();
        AssertThat(runner.Text).IsEqual("Oi, Rui.");

        AssertThat(runner.Advance(0)).IsTrue();
        AssertThat(engine.HasFlag("escolheu")).IsTrue();
        AssertThat(runner.Advance()).IsFalse();
        AssertThat(runner.Finished).IsTrue();
    }

    [TestCase]
    public void Runner_NoMatchingEntryMeansNothingToSay()
    {
        var file = DialogueFile.Parse("""{"id":"t","entries":[{"if":["x"],"node":"a"}],"nodes":{"a":{"text":"."}}}""");
        AssertThat(new DialogueRunner(file, NewEngine(), "Rui").Start()).IsFalse();
    }

    [TestCase]
    [ThrowsException(typeof(FormatException))]
    public void Dialogue_DanglingNextIsRejected() =>
        DialogueFile.Parse("""{"id":"t","entries":[{"node":"a"}],"nodes":{"a":{"text":"x","next":"fantasma"}}}""");

    [TestCase]
    public void AllShippedDialoguesParseAndUseKnownSpeakers()
    {
        var script = LoadScript();
        foreach (var path in Directory.GetFiles(DialogueDir, "*.json").Where(p => !p.EndsWith("story.json", StringComparison.Ordinal)))
        {
            var file = DialogueFile.Parse(File.ReadAllText(path));
            foreach (var node in file.Nodes.Values.Where(n => n.Speaker.Length > 0))
            {
                AssertThat(script.Speakers.ContainsKey(node.Speaker)).IsTrue();
            }
        }
    }

    [TestCase]
    public void Save_StoryProgressRoundTripsAndOldSaveLoadsWithEmptyStory()
    {
        var data = SaveData.NewGame(TestDb.Instance, "Ana", "sword");
        data.Story.Add("met_ravi");
        data.Story.ObjectiveId = "odete";
        data.Story.LevelId = "forjas";

        var back = SaveSerializer.FromJson(SaveSerializer.ToJson(data), TestDb.Instance);
        AssertThat(back.Story.Flags).ContainsExactly("met_ravi");
        AssertThat(back.Story.ObjectiveId).IsEqual("odete");
        AssertThat(back.Story.LevelId).IsEqual("forjas");

        var withoutStory = System.Text.RegularExpressions.Regex.Replace(SaveSerializer.ToJson(data), "\"story\":\\s*\\{.*?\\n  \\},?", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        AssertThat(withoutStory.Contains("\"story\"")).IsFalse();
        var old = SaveSerializer.FromJson(withoutStory, TestDb.Instance);
        AssertThat(old.Story.Flags).IsEmpty();
        AssertThat(old.Story.ObjectiveId).IsEqual(string.Empty);
    }

    [TestCase]
    public void Save_DuplicateAndBlankFlagsAreCleaned()
    {
        var data = SaveData.NewGame(TestDb.Instance, "Ana", "sword");
        data.Story.Flags = ["a", "a", " ", "b"];

        var back = SaveSerializer.FromJson(SaveSerializer.ToJson(data), TestDb.Instance);

        AssertThat(back.Story.Flags).ContainsExactly("a", "b");
    }
}
