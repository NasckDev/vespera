namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Story;
using static GdUnit4.Assertions;

/// <summary>
/// Regressão das issues #76 (fecho do Ato II sem o C), #77 (perguntas e revelação do Vigia) e #81 (falas inacessíveis,
/// genéricas datadas e incoerências do Final).
/// </summary>
[TestSuite]
public class HistoriaDialoguesTest
{
    private const int RankD = 1;
    private const int RankC = 2;

    private static string DialogueDir => Path.Combine(AppContext.BaseDirectory, "Dialogue");

    private static StoryScript LoadScript() => StoryScript.Parse(File.ReadAllText(Path.Combine(DialogueDir, "story.json")));

    private static DialogueFile LoadDialogue(string id) => DialogueFile.Parse(File.ReadAllText(Path.Combine(DialogueDir, $"{id}.json")));

    private static StoryEngine NewEngine(Func<int>? rank = null, params string[] flags)
    {
        var engine = new StoryEngine(new StoryProgress(), LoadScript(), rank ?? (() => 0));
        foreach (var flag in flags)
        {
            engine.SetFlag(flag);
        }

        return engine;
    }

    /// <summary>Toca a conversa até o fim; <paramref name="pick"/> escolhe a resposta pelo texto (sem pick, para na primeira escolha).</summary>
    private static List<string> Play(string id, StoryEngine engine, string? pick = null)
    {
        var runner = new DialogueRunner(LoadDialogue(id), engine, "Rui");
        AssertThat(runner.Start()).IsTrue();
        var texts = new List<string>();
        var picked = false;
        for (var i = 0; i < 80; i++)
        {
            texts.Add(runner.Text);
            var index = 0;
            if (runner.Choices.Count > 0)
            {
                index = pick is null || picked ? -1 : runner.Choices.ToList().FindIndex(c => c.Text == pick);
                if (index < 0)
                {
                    return texts;
                }

                picked = true;
            }

            if (!runner.Advance(index))
            {
                return texts;
            }
        }

        throw new InvalidOperationException("conversa sem fim");
    }

    private static List<string> ChoiceTextsOnStart(string id, StoryEngine engine)
    {
        var runner = new DialogueRunner(LoadDialogue(id), engine, "Rui");
        AssertThat(runner.Start()).IsTrue();
        while (runner.Choices.Count == 0 && runner.Advance())
        {
        }

        return [.. runner.Choices.Select(c => c.Text)];
    }

    // ---------------------------------------------------------------- #76

    [TestCase]
    public void Ato2Fim_WithoutRankC_BenicioSaysOnlyKeepHuntingAndPlaysOnce()
    {
        var rank = RankD;
        var engine = NewEngine(() => rank, "act2_done", "arrival_done", "met_ravi");

        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("ato2_fim");
        var texts = Play("ato2_fim", engine);

        AssertThat(texts.Count).IsEqual(1);
        AssertThat(texts[0]).Contains("Falta pouco para o C");
        AssertThat(engine.HasFlag("ato2_fim_done")).IsFalse();
        AssertThat(engine.HasFlag("ato2_fim_falta_c")).IsTrue();
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsNotEqual("ato2_fim");
    }

    [TestCase]
    public void Ato2Fim_WhenRankCArrivesLater_PlaysFullSceneWithRaviCelebratingOnce()
    {
        var rank = RankD;
        var engine = NewEngine(() => rank, "act2_done", "arrival_done", "met_ravi");
        Play("ato2_fim", engine);

        rank = RankC;
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsEqual("ato2_fim");
        var texts = Play("ato2_fim", engine);

        AssertThat(texts).Contains("C. Agora você pode entrar onde a maré sobe.");
        AssertThat(texts).Contains("C! Eu tô em D ainda.");
        AssertThat(engine.HasFlag("ato2_fim_done")).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("ato3");
        AssertThat(engine.AutoplayFor("praca")?.Dialogue).IsNotEqual("ato2_fim");
    }

    [TestCase]
    public void Ato2Fim_WithRankCAlready_NeverShowsTheDeniedLine()
    {
        var engine = NewEngine(() => RankC, "act2_done");
        var texts = Play("ato2_fim", engine);

        AssertThat(texts).Contains("C! Eu tô em D ainda.");
        AssertThat(texts.Any(t => t.Contains("Falta pouco", StringComparison.Ordinal))).IsFalse();
        AssertThat(engine.HasFlag("ato2_fim_falta_c")).IsFalse();
    }

    // ---------------------------------------------------------------- #77

    [TestCase]
    public void Vigia_SilenceDoesNotHideTheMarisaReveal()
    {
        var engine = NewEngine(null, "met_vigia", "vigia_p2_sim", "GuardianDefeated:velora");
        engine.NightFalls(false);

        var texts = Play("ato4/vigia", engine, pick: "(Ficar em silêncio)");

        AssertThat(texts).Contains("Eu era a Marisa.");
        AssertThat(engine.HasFlag("vigia_e_marisa")).IsTrue();
    }

    [TestCase]
    public void Vigia_OldSaveThatSkippedTheNameCanAskLater()
    {
        var engine = NewEngine(null, "met_vigia", "vigia_p2_sim", "GuardianDefeated:velora", "vigia_lanterna");
        engine.NightFalls(false);

        AssertThat(ChoiceTextsOnStart("ato4/vigia", engine)).Contains("Qual é o seu nome?");
        var texts = Play("ato4/vigia", engine, pick: "Qual é o seu nome?");

        AssertThat(texts).Contains("Eu era a Marisa.");
        AssertThat(engine.HasFlag("vigia_e_marisa")).IsTrue();
        AssertThat(ChoiceTextsOnStart("ato4/vigia", engine)).NotContains("Qual é o seu nome?");
    }

    [TestCase]
    public void Vigia_QuestionTwoIsNotSkippedWhenVeloriaFallsFirst()
    {
        var engine = NewEngine(null, "met_vigia", "vigia_p1_sim", "GuardianDefeated:velora");
        engine.NightFalls(false);
        Play("ato4/vigia", engine, pick: "(Ficar em silêncio)");
        AssertThat(engine.HasFlag("vigia_e_marisa")).IsTrue();

        engine.NightFalls(false);
        var texts = Play("ato4/vigia", engine);
        AssertThat(texts[0]).StartsWith("Pergunta dois");
    }

    [TestCase]
    public void Vigia_AllFiveQuestionsAreReachableOnNormalNightlyVisitsWithoutDying()
    {
        var engine = NewEngine(null, "exame_b_visto", "ato4_fecho_benicio");

        engine.NightFalls(false);
        Play("ato4/vigia", engine, pick: "Sim.");
        AssertThat(engine.HasFlag("vigia_p1_sim")).IsTrue();

        engine.NightFalls(false);
        Play("ato4/vigia", engine, pick: "Aceitava.");
        AssertThat(engine.HasFlag("vigia_p2_sim")).IsTrue();

        engine.SetFlag("GuardianDefeated:velora");
        engine.NightFalls(false);
        Play("ato4/vigia", engine, pick: "(Ficar em silêncio)");
        AssertThat(engine.HasFlag("vigia_e_marisa")).IsTrue();

        engine.SetFlag("ato5_confissao");
        engine.NightFalls(false);
        AssertThat(engine.AutoplayFor("cemiterio")?.Dialogue).IsEqual("ato4/vigia");
        Play("ato4/vigia", engine, pick: "Toco.");
        AssertThat(engine.HasFlag("vigia_p3_toco")).IsTrue();
        AssertThat(engine.HasFlag("vigia_perguntou_hoje")).IsTrue();
        AssertThat(Play("ato4/vigia", engine)[0]).StartsWith("Volte à noite");

        engine.SetFlag("act5_done");
        engine.NightFalls(false);
        AssertThat(engine.AutoplayFor("cemiterio")?.Dialogue).IsEqual("ato4/vigia");
        Play("ato4/vigia", engine, pick: "Fechava.");
        AssertThat(engine.HasFlag("vigia_p4_fechava")).IsTrue();

        engine.SetFlag("ato7_benicio");
        engine.NightFalls(false);
        AssertThat(engine.AutoplayFor("cemiterio")?.Dialogue).IsEqual("ato7/vigia");
        Play("ato7/vigia", engine, pick: "Volto.");
        AssertThat(engine.HasFlag("vigia_p5_volto")).IsTrue();
        AssertThat(ChoiceTextsOnStart("ato4/vigia", engine)).Contains("E a Odete?");
    }

    [TestCase]
    public void Vigia_DeathScenesDoNotAskAnAnsweredQuestionAgain()
    {
        var engine = NewEngine(null, "ato5_morte_3", "vigia_p3_toco");
        AssertThat(Play("ato5/morte", engine)).Contains("Pergunta três você já respondeu. Eu guardei.");

        var ruas = NewEngine(null, "ato6_morte_5", "vigia_p4_nao");
        AssertThat(Play("ato6/morte", ruas)).Contains("Pergunta quatro você já respondeu. Eu guardei.");
        AssertThat(ruas.HasFlag("morte_ruas_5")).IsTrue();

        var fresh = NewEngine(null, "ato5_morte_3");
        var texts = Play("ato5/morte", fresh, pick: "Não sei.");
        AssertThat(texts).Contains("Pergunta três: você toca?");
        AssertThat(fresh.HasFlag("vigia_p3_naosei")).IsTrue();
    }

    [TestCase]
    public void Vigia_FirstFallPromisesTheQuestionThatIsReallyNext()
    {
        var before = NewEngine(null, "ato4_morte_1");
        AssertThat(Play("ato4/morte", before)).Contains("Pergunta dois fica pra próxima.");

        var after = NewEngine(null, "ato4_morte_1", "vigia_p2_nao");
        AssertThat(Play("ato4/morte", after)).Contains("Pergunta três fica pra próxima.");
    }

    [TestCase]
    public void Vigia_FifthAnswerAppearsInTheFinalFallLine()
    {
        var back = NewEngine(null, "final_morte_1", "vigia_p5_volto");
        AssertThat(Play("final/morte", back)).Contains("Você me disse que voltava. Eu anotei.");

        var stay = NewEngine(null, "final_morte_1", "vigia_p5_fico");
        AssertThat(Play("final/morte", stay)).Contains("Você me disse que ficava. Eu anotei também.");

        var none = NewEngine(null, "final_morte_1");
        AssertThat(Play("final/morte", none).Count).IsEqual(1);
    }

    // ---------------------------------------------------------------- #81

    [TestCase]
    public void Generic_RaviCelebratesSealDOnlyOnceAndOnlyWithSealD()
    {
        var engine = NewEngine(() => RankD, "met_ravi", "ato1_fim_done");
        AssertThat(Play("ravi", engine)).Contains("Selo D! E eu aqui no E.");
        AssertThat(Play("ravi", engine)).NotContains("Selo D! E eu aqui no E.");

        var later = NewEngine(() => RankC, "met_ravi", "ato1_fim_done");
        AssertThat(Play("ravi", later)).NotContains("Selo D! E eu aqui no E.");
    }

    [TestCase]
    public void Generic_RaviOnRouteBStopsTheBet()
    {
        var engine = NewEngine(() => 5, "met_ravi", "ato1_fim_done", "ravi_rota_b");
        var texts = Play("ravi", engine);
        AssertThat(texts.Any(t => t.Contains("A aposta continua", StringComparison.Ordinal))).IsFalse();
    }

    [TestCase]
    public void Generic_CelesteStopsSendingYouToBenicioAfterTheSealScene()
    {
        var before = NewEngine(() => RankC, "met_celeste", "act2_done");
        AssertThat(Play("celeste", before)).Contains("O Benício quer te ver na praça. Desce a escada, ao sul.");

        var after = NewEngine(() => RankC, "met_celeste", "act2_done", "ato2_fim_done");
        var texts = Play("celeste", after);
        AssertThat(texts.Any(t => t.Contains("Benício", StringComparison.Ordinal))).IsFalse();
    }

    [TestCase]
    public void Generic_Act7LinesOfCelesteLisNilzaBenicioOdeteAndSeveroAreReachable()
    {
        var celeste = NewEngine(null, "met_celeste", "act2_done", "ato2_fim_done", "lis_revelada");
        AssertThat(Play("celeste", celeste, pick: "Se fechar tudo, você some?"))
            .Contains("Se fechar tudo, eu sumo? Talvez. Eu plantei mesmo assim.");

        var lis = NewEngine(null, "met_lis", "ato7_lis_noite");
        AssertThat(Play("lis", lis)).Contains("Eu lembro do som. Juro.");
        AssertThat(Play("lis", lis)).NotContains("Eu lembro do som. Juro.");

        var nilza = NewEngine(null, "met_nilza", "ato7_lis_dia");
        AssertThat(Play("nilza", nilza, pick: "Perguntar mais"))
            .Contains("Se o sol parar de nascer de novo, eu abro a taverna no escuro. Já fiz.");

        var benicio = NewEngine(null, "ato7_badalo");
        AssertThat(Play("benicio_quadro", benicio, pick: "Por que deu o badalo ao Severo?"))
            .Contains("Eu passei o badalo pro Severo porque, se fosse eu de novo, eu não puxava.");

        var odete = NewEngine(null, "met_odete", "arma_duas_vesperas", "vigia_e_marisa");
        AssertThat(Play("odete", odete, pick: "E se a Marisa voltar diferente?"))
            .Contains("Se a Marisa voltar diferente? Eu aceito.");

        var severo = NewEngine(null, "met_severo", "ato5_confissao");
        AssertThat(Play("ato3/severo", severo)).Contains("Então não foi o farol.");
        AssertThat(Play("ato3/severo", severo)).NotContains("Então não foi o farol.");
    }

    [TestCase]
    public void Final_MothersLineIsRouteBOnlyAndRaviOnRouteAKeepsHisPromise()
    {
        var b = NewEngine(null, "met_mae_ravi", "ravi_rota_b");
        var bChoices = ChoiceTextsOnStart("final/antes", b);
        AssertThat(bChoices).Contains("Mãe do Ravi");
        AssertThat(bChoices).NotContains("Ravi");
        AssertThat(Play("final/antes", b, pick: "Mãe do Ravi"))
            .Contains("Eu dei a lanterna dele pra ele de novo. Ele é homem, decide.");

        var a = NewEngine(null, "met_mae_ravi", "ravi_rota_a");
        var aTexts = Play("final/antes", a, pick: "Mãe do Ravi");
        AssertThat(aTexts.Any(t => t.Contains("lanterna dele", StringComparison.Ordinal))).IsFalse();
        AssertThat(Play("final/antes", a, pick: "Ravi")).Contains("Eu disse que ia junto, e vou: até a porta.");
    }

    [TestCase]
    public void Ato6Nilza_ThanksTheGlassEyedFishOnlyIfThePlayerFished()
    {
        var fished = NewEngine(null, "pescou");
        AssertThat(Play("ato6/nilza", fished)).Contains("Lembrei quando você trouxe o peixe de olho de vidro.");

        var skipped = NewEngine(null);
        var texts = Play("ato6/nilza", skipped);
        AssertThat(texts.Any(t => t.Contains("peixe", StringComparison.Ordinal))).IsFalse();
        AssertThat(texts).Contains("Toma antes de entrar. Se cair lá dentro, levanta uma vez.");
    }
}
