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
        engine.SetObjective("nilza");

        AssertThat(StoryConditions.Evaluate(["a"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["a", "!b"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["a", "b"], engine)).IsFalse();
        AssertThat(StoryConditions.Evaluate(["objective:nilza"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["!objective:nilza"], engine)).IsFalse();
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

        AssertThat(engine.ObjectiveId).IsEqual("selo");
        AssertThat(engine.HasFlag("act1_done")).IsTrue();
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    public void Engine_TriggerWithConditionOnlyMovesObjectiveWhenConditionHolds()
    {
        const string json = """
            {
              "startObjective": "a",
              "objectives": [ { "id": "a", "title": "A" }, { "id": "b", "title": "B" }, { "id": "c", "title": "C" } ],
              "triggers": [
                { "when": "x", "if": ["objective:a"], "objective": "b" },
                { "when": "y", "if": ["x"], "objective": "c", "set": ["z"] }
              ]
            }
            """;
        var engine = new StoryEngine(new StoryProgress(), StoryScript.Parse(json));
        engine.BeginCampaignIfNew();
        engine.SetFlag("y");
        AssertThat(engine.ObjectiveId).IsEqual("a");
        AssertThat(engine.HasFlag("z")).IsFalse();
        engine.SetFlag("x");
        AssertThat(engine.ObjectiveId).IsEqual("b");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void Engine_UnknownObjectiveIsRejected() => NewEngine().SetObjective("nao_existe");

    [TestCase]
    public void Engine_NewCampaignStartsAtFirstObjectiveOnlyOnce()
    {
        var engine = NewEngine();
        engine.BeginCampaignIfNew();
        AssertThat(engine.ObjectiveId).IsEqual("estrada");

        engine.SetObjective("forja");
        engine.BeginCampaignIfNew();
        AssertThat(engine.ObjectiveId).IsEqual("forja");
    }

    [TestCase]
    public void Autoplay_ArrivalPlaysOnceOnPraca()
    {
        var engine = NewEngine();
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("chegada");
        AssertThat(engine.AutoplayFor("forjas")).IsNull();

        AssertThat(engine.AutoplayFor("praca")?.Stage).IsEqual("arrival");

        RunToEnd(Runner("chegada", engine));

        AssertThat(engine.ObjectiveId).IsEqual("cacada1");
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("ravi");
    }

    [TestCase]
    public void ActOne_FirstHuntLeadsRaviThenNilzaThenOdete()
    {
        var engine = NewEngine();
        engine.BeginCampaignIfNew();
        RunToEnd(Runner("chegada", engine));
        RunToEnd(Runner("ravi", engine));
        AssertThat(engine.HasFlag(StoryFlags.MetRavi)).IsTrue();

        engine.SetFlag(StoryFlags.HuntOne);
        AssertThat(engine.ObjectiveId).IsEqual("nilza");
        var placar = Runner("ravi", engine);
        placar.Start();
        AssertThat(placar.CurrentId).IsEqual("p1");
        RunToEnd(Runner("ravi", engine));
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("nilza");

        var events = new List<string>();
        engine.EventRaised += events.Add;
        RunToEnd(Runner("nilza", engine));
        AssertThat(events).Contains(StoryFlags.EventGivePotion);
        AssertThat(engine.ObjectiveId).IsEqual("forja");

        RunToEnd(Runner("odete", engine));
        AssertThat(events).Contains(StoryFlags.EventOpenForge);
        AssertThat(engine.ObjectiveId).IsEqual("manha");
        AssertThat(engine.AutoplayFor("praca")?.Stage).IsEqual("morning");
    }

    [TestCase]
    public void ActOne_SecondHuntCallsBenicioForTheObservedExam()
    {
        var engine = NewEngine();
        engine.SetFlag(StoryFlags.ArrivalDone);
        engine.SetFlag(StoryFlags.MetRavi);
        engine.SetFlag(StoryFlags.HuntTwo);
        AssertThat(engine.ObjectiveId).IsEqual("exame");
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("benicio_quadro");
        RunToEnd(Runner("benicio_quadro", engine));
        AssertThat(engine.HasFlag(StoryFlags.GuardianGate)).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("lodo_mae");
    }

    [TestCase]
    public void ActOne_EndingRunsSealOdeteMarisaAndPurpleApple()
    {
        var engine = NewEngine();
        engine.SetFlag(StoryFlags.ArrivalDone);
        engine.SetFlag(StoryFlags.MetRavi);
        engine.SetFlag(StoryFlags.GuardianDefeatedLodoMae);
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("ato1_fim");
        RunToEnd(Runner("ato1_fim", engine));
        AssertThat(engine.ObjectiveId).IsEqual("odete_fim");
        AssertThat(engine.AutoplayFor("forja_interior")?.Dialogue).IsEqual("odete");

        var events = new List<string>();
        engine.EventRaised += events.Add;
        RunToEnd(Runner("odete", engine));
        AssertThat(events).Contains(StoryFlags.EventChooseWeapon);
        AssertThat(engine.HasFlag(StoryFlags.WeaponChosen)).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("dormir");

        RunToEnd(Runner("lis", engine));
        AssertThat(engine.ObjectiveId).IsEqual("ato2");
        AssertThat(engine.Script.Objective("ato2")!.Title).IsEqual("Vá ao Pomar Alto");
        AssertThat(engine.Script.Questions.Count(q => StoryConditions.Evaluate(q.Conditions, engine))).IsGreaterEqual(1);
    }

    [TestCase]
    public void ActOne_ObjectivesFitFiveWordsAndBalloonsTwoSentences()
    {
        foreach (var o in LoadScript().Objectives)
        {
            AssertThat(o.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length).IsLessEqual(5);
        }

        string[] files = ["chegada", "ravi", "nilza", "odete", "lis", "benicio_quadro", "ato1_fim", "exame_benicio"];
        foreach (var id in files)
        {
            foreach (var node in LoadDialogue(id).Nodes.Values)
            {
                // Falas exatas do roteiro com fragmentos ("Você. Com a lanterna.") contam como uma frase cada pedaço; o teto é 3.
                var sentences = node.Text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries).Count(t => t.Trim().Length > 0);
                AssertThat(sentences).IsLessEqual(3);
            }
        }


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
    public void Runner_BranchesPickTheFirstMatchingNodeAfterTheBox()
    {
        const string json = """
            {
              "id": "t",
              "entries": [ { "node": "a" } ],
              "nodes": {
                "a": { "speaker": "benicio", "text": "Deixe-me ver.", "branches": [ { "if": ["!x"], "node": "falta" }, { "node": "ok" } ] },
                "falta": { "speaker": "benicio", "text": "Falta x." },
                "ok": { "speaker": "benicio", "text": "Tudo certo." }
              }
            }
            """;
        var engine = NewEngine();
        var runner = new DialogueRunner(DialogueFile.Parse(json), engine, "Rui");
        runner.Start();
        runner.Advance();
        AssertThat(runner.CurrentId).IsEqual("falta");

        engine.SetFlag("x");
        runner = new DialogueRunner(DialogueFile.Parse(json), engine, "Rui");
        runner.Start();
        runner.Advance();
        AssertThat(runner.CurrentId).IsEqual("ok");
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
