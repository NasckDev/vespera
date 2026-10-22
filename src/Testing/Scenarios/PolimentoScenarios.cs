using System;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Rules;
using Vespera.UI;

namespace Vespera.Testing;

/// <summary>
/// Frente C1 (polimento da v0.1): cenários de cada correção jogada pelo integrador e das opções de acessibilidade.
/// Prints com <c>--smoke-filter=polimento --smoke-shots=…</c> (sem --headless).
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("polimento: o tutorial da Odete cumpre 'Primeiros passos'", order: 960)]
    private async Task PolishTutorialCountsOdete()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        Require(state.CurrentQuest?.Id == "primeiros_passos", $"jogo novo começa em 'Primeiros passos', está em {state.CurrentQuest?.Id}");
        Require(state.Save.Quests.Counter(QuestCounters.Odete) == 0, "contador da Odete zerado no jogo novo");
        var auto = GameUi.AutoCompleteQuests;
        GameUi.AutoCompleteQuests = true;
        try
        {
            ui.StartTutorial();
            await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Odete", 3, "tutorial da Odete abrir");
            for (var line = 0; line < 6 && ui.Dialog.IsOpen && state.Save.Quests.Counter(QuestCounters.Odete) == 0; line++)
            {
                await SkipTyping(ui);
                await UiPress("interact");
                await WaitSeconds(0.2);
            }

            Require(state.Save.Quests.Counter(QuestCounters.Odete) == 1, "o tutorial deveria contar como falar com a Odete");
            await WaitUntil(() => state.CurrentQuest?.Id != "primeiros_passos", 2, "'Primeiros passos' cumprida depois do tutorial");
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "fala de fim de missão");
            Require(ui.Dialog.Text.StartsWith("Seu Selo é E.", StringComparison.Ordinal), $"fala final da missão: {ui.Dialog.Text}");
            await WaitSeconds(0.3);
            await Shot("polimento-tutorial-missao");
        }
        finally
        {
            GameUi.AutoCompleteQuests = auto;
            ui.CloseAll();
        }

        Require(world.MapId == "guild", "continua na Guilda");
    }
}
