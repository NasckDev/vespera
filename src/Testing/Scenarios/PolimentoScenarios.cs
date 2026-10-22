using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Rules;
using Vespera.UI;
using Vespera.World.City;

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

    [SmokeScenario("polimento: as primeiras Brechas respeitam o Selo do novato", order: 961)]
    private async Task PolishNoviceBrechas()
    {
        await FreshGame("sword");
        var state = GameManager.Instance.State;
        var city = CityLife.Instance ?? throw new SmokeFailure("World sem CityLife");
        state.Save.FirstBrechaDone = true; // a primeira (Mercado, rank do Selo) já passou: daqui em diante a v2 sorteia
        var ranks = new List<string>();
        try
        {
            for (var round = 0; round < 10; round++)
            {
                city.Brechas.Reset();
                for (var i = 0; i < 2; i++)
                {
                    city.Brechas.OpenSoon(0);
                    city.Brechas.Step(0.01);
                }

                Require(city.Brechas.Schedule.Novice, "jogo novo (0 Brechas fechadas) deveria ser novato");
                foreach (var b in city.Brechas.Brechas)
                {
                    ranks.Add($"{b.ThemeId}/r{b.Rank}");
                    Require(state.Brechas.CanEnter(b.Rank, state.RankIndex), $"Brecha {b.ThemeId} rank {b.Rank} abriu acima do que o Selo {state.RankDef.Id} entra");
                }
            }

            Log.Info($"polimento: Brechas do novato (Selo {state.RankDef.Id}): {string.Join(", ", ranks)}");
            state.Save.BrechasClosed = state.Brechas.Def.NoviceBrechas;
            city.Brechas.Step(0.01);
            Require(!city.Brechas.Schedule.Novice, "com 3 Brechas fechadas o sorteio volta ao da v2");
        }
        finally
        {
            state.Save.BrechasClosed = 0;
            city.Brechas.Reset();
            Hud.Current?.Toasts.Clear();
            await WaitSeconds(0.1);
        }
    }

    [SmokeScenario("polimento: saída da Brecha só com intenção e longe do spawn", order: 962)]
    private async Task PolishEscapeNeedsIntent()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var flee = new List<string>();
        void OnChoice(string id, string speaker, string text, string[] options)
        {
            if (id == "flee")
            {
                flee.Add(text);
                GameUi.Current?.CloseAll();
                EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceMade, "flee", 1); // "Continuar lutando"
            }
        }

        EventBus.Instance.ChoiceRequested += OnChoice;
        try
        {
            // Toda chegada numa sala com borda de fuga fica a >= 40 u dela (o Mercado nascia a 20 u).
            foreach (var (theme, _) in GameManager.Db.Themes)
            {
                await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
                var back = world.Map.Back ?? throw new SmokeFailure($"{world.MapId} sem borda de fuga");
                var gap = back.Y - player.GlobalPosition.Y;
                Log.Info($"polimento: {world.MapId} nasce em ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0}), {gap:0} u acima da borda de fuga (v2: {back.Y - world.Map.Spawn.Y:0} u)");
                Require(gap >= 40f, $"{world.MapId}: nasce a {gap:0} u da borda de fuga");
                await FleeRun(world, combat);
            }

            await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            var edge = world.Map.Back!.Value;
            await WaitSeconds(0.3);
            await Shot("polimento-fuga-spawn");

            // Um passo para baixo na beira da borda (0,2 s dentro dela) não pergunta nada.
            player.Teleport(new Vector2(edge.Center.X, edge.Y - 3));
            await Frames(2);
            PressDirection(Vector2.Down);
            await WaitSeconds(0.25);
            PressDirection(Vector2.Zero);
            await WaitSeconds(0.3);
            Require(flee.Count == 0, $"um passo na borda abriu a fuga ({flee.Count}x)");
            Require(player.FocusedInteractable?.Id == World.World.EscapeInteractableId, $"na borda o prompt deveria ser 'Sair da Brecha' (foco: {player.FocusedInteractable?.Id})");
            await Shot("polimento-fuga-prompt");

            // Insistir (segurar ~0,5 s para fora) pergunta uma vez.
            PressDirection(Vector2.Down);
            await WaitUntil(() => flee.Count > 0, 2, "fuga depois de insistir na borda");
            PressDirection(Vector2.Zero);
            Require(flee.Count == 1, $"insistir pergunta uma vez ({flee.Count}x)");
            Require(combat.Brecha.Run is not null && world.Map.Dungeon, "'Continuar lutando' fica na Brecha");

            // Interagir com a saída pergunta na hora.
            await WaitSeconds(1.1); // recarga de 1 s entre pedidos
            player.Teleport(new Vector2(edge.Center.X, edge.Y - 2));
            await Frames(3);
            await JourneyPress(InputActions.Interact);
            await WaitUntil(() => flee.Count > 1, 2, "fuga ao interagir com a saída");
        }
        finally
        {
            PressDirection(Vector2.Zero);
            EventBus.Instance.ChoiceRequested -= OnChoice;
            await FleeRun(world, combat);
        }
    }
}
