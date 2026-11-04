namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Story;
using static GdUnit4.Assertions;

/// <summary>
/// Atos IV e V (ROTEIRO-V2 §13, §8.1): o Vigia só à noite, uma pergunta por noite com as respostas em flags, o primeiro
/// slot de Eco, e as duas rotas do Ravi (Brecha B das Ruas Baixas: três avisos, ruptura, Lanterna recuperada).
/// </summary>
[TestSuite]
public class Ato45StoryTest
{
    private static string DialogueDir => Path.Combine(AppContext.BaseDirectory, "Dialogue");

    private static StoryScript LoadScript() => StoryScript.Parse(File.ReadAllText(Path.Combine(DialogueDir, "story.json")));

    private static DialogueFile LoadDialogue(string id) => DialogueFile.Parse(File.ReadAllText(Path.Combine(DialogueDir, $"{id}.json")));

    private static StoryEngine NewEngine() => new(new StoryProgress(), LoadScript());

    private static DialogueRunner Start(string id, StoryEngine engine)
    {
        var runner = new DialogueRunner(LoadDialogue(id), engine, "Rui");
        AssertThat(runner.Start()).IsTrue();
        return runner;
    }

    /// <summary>Anda até a primeira caixa com respostas (ou até o fim); devolve verdadeiro se parou numa pergunta.</summary>
    private static bool RunToChoices(DialogueRunner runner)
    {
        for (var i = 0; i < 40; i++)
        {
            if (runner.Choices.Count > 0)
            {
                return true;
            }

            if (!runner.Advance())
            {
                return false;
            }
        }

        throw new InvalidOperationException("conversa sem fim");
    }

    private static void Finish(DialogueRunner runner, int choice = 0)
    {
        var steps = 0;
        while (runner.Advance(choice) && steps++ < 40)
        {
            choice = 0;
        }
    }

    [TestCase]
    public void Night_FallsOnVesperaAndPassesAtMorning()
    {
        var engine = NewEngine();
        AssertThat(engine.HasFlag(NightFlags.Night)).IsFalse();

        engine.NightFalls(ruasCanRupture: false);
        AssertThat(engine.HasFlag(NightFlags.Night)).IsTrue();
        AssertThat(engine.Progress.Night.Night).IsEqual(1);

        engine.Dawn();
        AssertThat(engine.HasFlag(NightFlags.Night)).IsFalse();
        AssertThat(engine.Progress.Night.Night).IsEqual(1);
    }

    [TestCase]
    public void ComputedFlags_CannotBeStoredByDialogue()
    {
        var engine = NewEngine();
        AssertThat(engine.SetFlag(NightFlags.Night)).IsFalse();
        AssertThat(engine.Progress.Flags).NotContains(NightFlags.Night);
        AssertThat(engine.HasFlag(NightFlags.Night)).IsFalse();
    }

    [TestCase]
    public void Vigia_FirstVisitAsksQuestionOneWithThreeAnswersAndUnlocksEchoSlot()
    {
        var engine = NewEngine();
        var events = new List<string>();
        engine.EventRaised += events.Add;
        engine.NightFalls(false);

        var runner = Start("ato4/vigia", engine);
        AssertThat(runner.Text).IsEqual("Você carrega uma lanterna que já atravessou.");
        AssertThat(RunToChoices(runner)).IsTrue();
        AssertThat(runner.Text).IsEqual("Pergunta um: você sabia disso?");
        AssertThat(runner.Choices.Select(c => c.Text).ToList()).ContainsExactly("Sim.", "Não.", "Não entendi.");

        Finish(runner, choice: 2);

        AssertThat(engine.HasFlag("vigia_p1_naoentendi")).IsTrue();
        AssertThat(engine.HasFlag("vigia_p1_sim")).IsFalse();
        AssertThat(events).Contains("UnlockEchoSlot:1");
        AssertThat(engine.ObjectiveId).IsEqual("ato4_brecha");
        AssertThat(engine.HasFlag(NightFlags.VigiaAskedTonight)).IsTrue();
    }

    [TestCase]
    public void Vigia_AsksOnlyOneQuestionPerNightAndRemembersEachAnswer()
    {
        var engine = NewEngine();
        engine.NightFalls(false);
        var first = Start("ato4/vigia", engine);
        RunToChoices(first);
        Finish(first, choice: 0);
        AssertThat(engine.HasFlag("vigia_p1_sim")).IsTrue();

        // Mesma noite: ele não pergunta de novo.
        var again = Start("ato4/vigia", engine);
        AssertThat(again.CurrentId).IsEqual("h1");

        // Noite seguinte: pergunta dois.
        engine.NightFalls(false);
        AssertThat(engine.HasFlag(NightFlags.VigiaAskedTonight)).IsFalse();
        var second = Start("ato4/vigia", engine);
        AssertThat(second.CurrentId).IsEqual("s1");
        AssertThat(second.Choices.Count).IsEqual(3);
        Finish(second, choice: 1);
        AssertThat(engine.HasFlag("vigia_p2_naosei")).IsTrue();
        AssertThat(engine.HasFlag("vigia_p1_sim")).IsTrue();

        // Sem pergunta nova no Ato IV: só a conversa de sempre.
        engine.NightFalls(false);
        AssertThat(Start("ato4/vigia", engine).CurrentId).IsEqual("h1");
    }

    [TestCase]
    public void Vigia_AfterVeloraRecognisesTheLanternAndSaysHerName()
    {
        var engine = NewEngine();
        engine.NightFalls(false);
        var first = Start("ato4/vigia", engine);
        RunToChoices(first);
        Finish(first);
        engine.SetFlag("GuardianDefeated:velora");
        engine.NightFalls(false);

        var runner = Start("ato4/vigia", engine);
        AssertThat(runner.CurrentId).IsEqual("r1");
        AssertThat(RunToChoices(runner)).IsTrue();
        runner.Advance(0);
        AssertThat(runner.Text).IsEqual("Eu era a Marisa.");
        Finish(runner);
        AssertThat(engine.HasFlag("vigia_e_marisa")).IsTrue();
    }

    [TestCase]
    public void NightState_SanitizeClampsLoadedValues()
    {
        var night = new NightState { Night = 2, VigiaAskedOn = 9, RuasOpenedOn = -5 };
        night.Sanitize();
        AssertThat(night.VigiaAskedOn).IsEqual(2);
        AssertThat(night.RuasOpenedOn).IsEqual(-1);
    }

    [TestCase]
    public void RuasStage_ThreeWarningsThenRuptureOnlyWhenTheBrechaExists()
    {
        AssertThat(RaviRescue.Stage(-1, 5, false, true)).IsEqual(RuasStage.NotOpen);
        AssertThat(RaviRescue.Stage(2, 2, false, true)).IsEqual(RuasStage.Warning1);
        AssertThat(RaviRescue.Stage(2, 3, false, true)).IsEqual(RuasStage.Warning2);
        AssertThat(RaviRescue.Stage(2, 4, false, true)).IsEqual(RuasStage.Warning3);
        AssertThat(RaviRescue.Stage(2, 5, false, true)).IsEqual(RuasStage.Ruptured);
        AssertThat(RaviRescue.Stage(2, 9, false, false)).IsEqual(RuasStage.Warning3);
        AssertThat(RaviRescue.Stage(2, 9, true, true)).IsEqual(RuasStage.ClosedInTime);
        AssertThat(RaviRescue.FlagsFor(RuasStage.Warning3)).ContainsExactly(RaviRescue.Opened, RaviRescue.Warning2Flag, RaviRescue.Warning3Flag);
    }

    [TestCase]
    public void RaviRouteB_WarningsLisNilzaRaviThenRuptureAndLanternBringsHimBack()
    {
        var engine = NewEngine();
        var events = new List<string>();
        engine.EventRaised += events.Add;
        engine.NightFalls(true);
        engine.SetFlag(NightFlags.MetVigia);
        AssertThat(engine.HasFlag(RaviRescue.Opened)).IsTrue();
        AssertThat(Start("ato4/aviso_ruas", engine).Current!.Speaker).IsEqual("lis");

        AssertThat(engine.NightFalls(true)).IsEqual(RuasStage.Warning2);
        AssertThat(Start("ato4/aviso_ruas", engine).Current!.Speaker).IsEqual("nilza");

        AssertThat(engine.NightFalls(true)).IsEqual(RuasStage.Warning3);
        var ravi = Start("ato4/aviso_ruas", engine);
        AssertThat(ravi.Current!.Speaker).IsEqual("ravi");
        AssertThat(ravi.Text).IsEqual("Se você não for, eu vou.");

        AssertThat(engine.NightFalls(true)).IsEqual(RuasStage.Ruptured);
        AssertThat(engine.HasFlag(RaviRescue.Ruptured)).IsTrue();
        AssertThat(engine.HasFlag(NightFlags.RaviMissing)).IsTrue();

        // Fechar depois de romper não vira rota A.
        RaviRescue.Close(engine);
        AssertThat(engine.HasFlag(RaviRescue.ClosedInTime)).IsFalse();

        engine.SetFlag(RaviRescue.LanternRecovered);
        AssertThat(engine.HasFlag(NightFlags.RaviMissing)).IsFalse();
        var back = Start("ato4/ravi", engine);
        AssertThat(back.Text).IsEqual("Eu entrei.");
        Finish(back);
        AssertThat(engine.HasFlag(RaviRescue.RouteB)).IsTrue();
        AssertThat(events).Contains("HideRivalScore");
        AssertThat(RaviRescue.Route(engine) == 'B').IsTrue();
        AssertThat(Start("ato5/ravi", engine).CurrentId).IsEqual("b1");
    }

    [TestCase]
    public void RaviRouteA_ClosedInTimeNeverRupturesAndKeepsTheBet()
    {
        var engine = NewEngine();
        engine.NightFalls(true);
        engine.SetFlag(NightFlags.MetVigia);
        engine.NightFalls(true);
        RaviRescue.Close(engine);
        for (var i = 0; i < 5; i++)
        {
            AssertThat(engine.NightFalls(true)).IsEqual(RuasStage.ClosedInTime);
        }

        AssertThat(engine.HasFlag(RaviRescue.Ruptured)).IsFalse();
        var scene = Start("ato4/ravi", engine);
        AssertThat(scene.Text).IsEqual("Eu ia entrar. Juro que ia.");
        Finish(scene);
        AssertThat(engine.HasFlag(RaviRescue.RouteA)).IsTrue();
        AssertThat(RaviRescue.Route(engine) == 'A').IsTrue();
        AssertThat(Start("ato5/ravi", engine).CurrentId).IsEqual("a1");
    }

    [TestCase]
    public void RaviCountdown_WaitsAtThirdWarningWhileTheRuasBrechaIsNotPlayable()
    {
        var engine = NewEngine();
        engine.SetFlag(NightFlags.MetVigia);
        for (var i = 0; i < 6; i++)
        {
            engine.NightFalls(false);
        }

        AssertThat(engine.HasFlag(RaviRescue.Warning3Flag)).IsTrue();
        AssertThat(engine.HasFlag(RaviRescue.Ruptured)).IsFalse();
        AssertThat(engine.HasFlag(NightFlags.RaviMissing)).IsFalse();
    }

    [TestCase]
    public void Ato5_BenicioDoorThenConfessionThenQuestions()
    {
        var engine = NewEngine();
        var door = Start("ato5/benicio", engine);
        AssertThat(door.Text).IsEqual("Esta é a última Brecha que a Ordem controla. Abre dentro da minha casa.");
        Finish(door);

        var confession = Start("ato5/benicio", engine);
        AssertThat(confession.Text).IsEqual("Fui eu que toquei. Faz vinte anos.");
        AssertThat(confession.Current!.Look).IsEqual("sino");
        Finish(confession);
        AssertThat(engine.HasFlag("ato5_confissao")).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("ato5_brecha");
        AssertThat(Start("ato5/benicio", engine).CurrentId).IsEqual("h1");
    }

    [TestCase]
    public void Story_Ato45AutoplaysPointToExistingDialoguesAndObjectives()
    {
        var script = LoadScript();
        foreach (var id in new[] { "ato4", "ato4_brecha", "ato4_fecho", "ato5", "ato5_brecha", "ato5_fecho", "ato4_brecha" })
        {
            AssertThat(script.Objective(id)).IsNotNull();
        }

        foreach (var auto in script.Autoplay.Where(a => a.Dialogue.StartsWith("ato4/", StringComparison.Ordinal) || a.Dialogue.StartsWith("ato5/", StringComparison.Ordinal)))
        {
            AssertThat(File.Exists(Path.Combine(DialogueDir, $"{auto.Dialogue}.json"))).IsTrue();
        }

        AssertThat(script.Speakers.ContainsKey("vigia")).IsTrue();
    }

    [TestCase]
    public void Banco_OffersToWaitForNightOnlyDuringTheDay()
    {
        var engine = NewEngine();
        var events = new List<string>();
        engine.EventRaised += events.Add;
        var day = Start("ato4/banco", engine);
        AssertThat(day.Choices.Count).IsEqual(2);
        Finish(day, choice: 0);
        AssertThat(events).Contains("WaitNight");

        engine.NightFalls(false);
        var night = Start("ato4/banco", engine);
        AssertThat(night.CurrentId).IsEqual("n");
        AssertThat(night.Choices.Count).IsEqual(0);
    }
}
