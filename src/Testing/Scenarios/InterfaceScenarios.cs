using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.State;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.UI.Screens;
using Vespera.UI.Title;

namespace Vespera.Testing;

/// <summary>
/// Cenários da interface (frente B3): título → classe → mundo, tutorial, HUD, diálogos com escolha, todas as
/// interações da cidade, forja e lojas com preços, inventário, pausa/opções, morte e portal. Navegam com input
/// simulado (eventos de ação que passam pela GUI) e tiram print de cada tela.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("interface: título → história → classe → mundo", order: 5)]
    private async Task TitleToWorld()
    {
        await WaitForScene("World", 10); // boot do smoke vai para o World; o título abre daqui
        DirAccess.RemoveAbsolute(SaveService.Instance.SavePath);
        GameManager.Instance.ChangeScene(TitleScreen.ScenePath);
        await WaitForScene("Title", 10);
        try
        {
            await TitleFlow((TitleScreen)GetTree().CurrentScene);
        }
        finally
        {
            // Falhou no meio do título: volta ao World para os próximos cenários não ficarem presos no menu.
            if (GetTree().CurrentScene?.Name != "World")
            {
                GameManager.Instance.ChangeScene(TitleScreen.WorldScenePath);
                await WaitForScene("World", 10);
            }
        }
    }

    private string TitleState(TitleScreen title) =>
        $"topo={title.Stack.Top?.GetType().Name ?? "menu"}, foco={GetViewport().GuiGetFocusOwner()?.GetType().Name ?? "nenhum"}, save={SaveService.Instance.HasSave()}";

    private async Task TitleFlow(TitleScreen title)
    {
        Require(title.ContinueRow.Disabled, "sem save, Continuar deveria estar desabilitado");
        Require(title.NewGameRow.HasFocus(), "sem save, o foco deveria começar em Novo jogo");
        await Shot("ui-titulo");

        // Opções pelo título (volta com Esc).
        await UiPress("ui_down");
        await UiPress("ui_down");
        await UiPress("ui_down");
        await UiPress("ui_accept");
        await WaitUntil(() => title.Stack.Top == title.Options, 2, $"opções do título ({TitleState(title)})");
        await WaitSeconds(0.2);
        await Shot("ui-titulo-opcoes");
        await UiPress("ui_cancel");
        await WaitUntil(() => title.Stack.Count == 0, 2, "voltar ao menu do título");
        title.NewGameRow.GrabFocus();
        await WaitSeconds(0.2);

        await UiPress("ui_accept");
        await WaitUntil(() => title.Stack.Top == title.Story, 2, $"história abrir ({TitleState(title)})");
        await WaitSeconds(0.2);
        await Shot("ui-historia");
        for (var i = 0; i < 5; i++)
        {
            await UiPress("ui_accept");
            await WaitSeconds(0.05);
        }

        await WaitUntil(() => title.Stack.Top == title.ClassSelect, 2, "escolha de classe abrir");
        await WaitSeconds(0.2);
        await UiKey(Key.Backspace);
        await UiKey(Key.Backspace);
        await UiKey(Key.Backspace);
        title.ClassSelect.Type("Lia");
        await UiPress("ui_right");
        await UiPress("ui_right");
        Require(title.ClassSelect.SelectedClass == "staff", $"duas vezes → deveria escolher o Cajado, está {title.ClassSelect.SelectedClass}");
        Require(title.ClassSelect.PlayerName == "Lia", $"nome digitado: '{title.ClassSelect.PlayerName}'");
        await Shot("ui-classe");

        await UiPress("ui_accept");
        await WaitForScene("World", 10);
        var state = GameManager.Instance.State;
        Require(state.Save.Name == "Lia" && state.Save.ClassId == "staff", $"jogo novo: {state.Save.Name} ({state.Save.ClassId})");
        Require(SaveService.Instance.HasSave(), "o jogo novo deveria ser salvo no início (spec 01 §1.11)");
        var world = FindWorld();
        Require(world.MapId == "guild", $"jogo novo deveria começar na Guilda, está em {world.MapId}");
        Require(FindPlayer()!.GlobalPosition.DistanceTo(new Vector2(250, 560)) < 2, $"posição inicial {FindPlayer()!.GlobalPosition}");
        Require(FindPlayer()!.ClassId == "staff", $"o jogador deveria aparecer com o visual do Cajado, está {FindPlayer()!.ClassId}");
        await WaitSeconds(0.3);
        await Shot("ui-mundo-inicio");

        // Tutorial da Odete: 3 falas depois de 0,9 s (extra.js:129-136).
        var ui = UiOrFail();
        await WaitUntil(() => ui.Dialog.IsOpen, 3, "tutorial da Odete abrir");
        Require(ui.Dialog.Speaker == "Odete" && ui.Dialog.Text.StartsWith("Então você é Lia.", StringComparison.Ordinal), $"1ª fala do tutorial: {ui.Dialog.Speaker}: {ui.Dialog.Text}");
        Require(GameManager.Instance.IsGameplayBlocked, "diálogo deveria travar o jogador");
        await WaitSeconds(0.4);
        await Shot("ui-dialogo-digitando");
        for (var line = 0; line < 3; line++)
        {
            await WaitSeconds(0.2);
            await UiPress("ui_accept"); // completa o texto
            await WaitSeconds(0.2);
            if (line == 1)
            {
                Require(ui.Dialog.Text.Contains("Cajado", StringComparison.Ordinal), $"2ª fala deveria citar a classe: {ui.Dialog.Text}");
                await Shot("ui-dialogo-tutorial");
            }

            await UiPress("interact"); // avança com E/A
        }

        await WaitUntil(() => !ui.Dialog.IsOpen, 2, "tutorial fechar");
        Require(!GameManager.Instance.IsGameplayBlocked, "depois do tutorial o jogador volta a andar");
    }

    [SmokeScenario("interface: HUD com energia, Essência, recargas, onda e chefe", order: 30)]
    private async Task HudShowsCombatSignals()
    {
        var world = await FreshGame("sword");
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        var bus = EventBus.Instance;
        bus.EmitSignal(EventBus.SignalName.StaminaChanged, 40f, 100f);
        bus.EmitSignal(EventBus.SignalName.EssenceChanged, 30f, 100f);
        bus.EmitSignal(EventBus.SignalName.AbilityCooldownChanged, "ultimate", 40f, 40f);
        bus.EmitSignal(EventBus.SignalName.AbilityCooldownChanged, "potion", 1f, 1f);
        bus.EmitSignal(EventBus.SignalName.WaveStarted, 2, 3);
        bus.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
        bus.EmitSignal(EventBus.SignalName.PortalOpened, 1, "crypt", 1);
        GameManager.Instance.DamagePlayer(30);
        await NextPhysicsFrame();
        await NextPhysicsFrame();
        Require(Mathf.IsEqualApprox(hud.EnergyBar.Ratio, 0.4f), $"barra de energia {hud.EnergyBar.Ratio}");
        Require(Mathf.IsEqualApprox(hud.EssenceBar.Ratio, 0.3f), $"barra de Essência {hud.EssenceBar.Ratio}");
        Require(!hud.Ultimate.IsReady, "Supremo deveria estar recarregando");
        Require(hud.WaveLabel.Visible && hud.WaveLabel.Text == "Onda 2 de 3", $"onda: '{hud.WaveLabel.Text}'");
        Require(hud.BrechaList.GetChildCount() == 2, $"2 Brechas abertas na lista do HUD, há {hud.BrechaList.GetChildCount()}");
        Require(hud.HpBar.Ratio < 1f && hud.HpBar.LagRatio > hud.HpBar.Ratio, "vida com lag depois do dano");
        Require(hud.WeaponSlots[0].Selected && hud.WeaponSlots[1].Locked, "hotbar: Lâmina selecionada, Arco com cadeado");
        await WaitSeconds(0.25);
        await Shot("ui-hud-combate");

        bus.EmitSignal(EventBus.SignalName.BossSpawned, "O Abissal do Mercado", 520);
        bus.EmitSignal(EventBus.SignalName.BossHealthChanged, 300, 520);
        await WaitSeconds(0.6);
        Require(hud.Boss.Visible && hud.Boss.BossName == "O Abissal do Mercado", "barra do chefe");
        Require(hud.BossAlert.Visible, "alerta de chefe");
        await Shot("ui-hud-chefe");
        bus.EmitSignal(EventBus.SignalName.BossHealthChanged, 0, 520);
        await NextPhysicsFrame();
        Require(!hud.Boss.Visible, "barra do chefe some quando ele cai");

        // Rank-up, toast e caixa de loot.
        GameManager.Instance.AddXp(100);
        GameManager.Instance.State.AddMaterial("presa", 2);
        GameManager.Instance.State.AddMaterial("presa", 1);
        bus.EmitSignal(EventBus.SignalName.ToastRequested, "Brecha aberta\nMercado de Peixes");
        await WaitSeconds(0.5);
        Require(hud.RankUp.Showing, "overlay de Novo Selo");
        Require(hud.Loot.VisibleTexts.Any(t => t.StartsWith("Presa Rubra", StringComparison.Ordinal) && t.EndsWith("×3", StringComparison.Ordinal)), $"caixa de loot deveria somar Presa Rubra ×3: {string.Join(" | ", hud.Loot.VisibleTexts)}");
        Require(hud.Toasts.Pending > 0, "toast na fila");
        await Shot("ui-hud-rankup-loot");
        _ = world;
    }

    [SmokeScenario("interface: diálogo com escolhas, falas e ChoiceMade", order: 31)]
    private async Task DialogueAndChoices()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var bus = EventBus.Instance;

        var finished = 0;
        void OnFinished() => finished++;
        bus.DialogueFinished += OnFinished;
        (string Id, int Index)? made = null;
        void OnChoice(string id, int index) => made = (id, index);
        bus.ChoiceMade += OnChoice;
        try
        {
            bus.EmitSignal(EventBus.SignalName.DialogueRequested, "Sargento Lauro", new[] { "A Guarda segura a rua.", "Já fui caçador." });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "fala do Lauro");
            Require(ui.Dialog.Speaker == "Sargento Lauro", "nome do NPC no diálogo");
            await AdvanceDialog();
            Require(ui.Dialog.Text == "Já fui caçador.", $"2ª linha: '{ui.Dialog.Text}'");
            await AdvanceDialog();
            Require(finished == 1 && !ui.Dialog.IsOpen, $"DialogueFinished deveria sair uma vez ({finished})");

            bus.EmitSignal(EventBus.SignalName.ChoiceRequested, "flee", "Saída da Brecha", "Fugir para Lumeira? Você leva a bolsa; a Brecha continua aberta.", new[] { "Fugir", "Ficar" });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "escolha de fuga");
            await SkipTyping(ui);
            Require(ui.Dialog.ChoicesVisible && ui.Dialog.Rows[0].HasFocus(), "opções visíveis com foco na primeira");
            await UiPress("move_down"); // WASD também navega
            await NextPhysicsFrame();
            Require(ui.Dialog.Rows[1].HasFocus(), "S/↓ deveria focar 'Ficar'");
            await Shot("ui-escolha");
            await UiPress("ui_accept");
            await WaitUntil(() => made is not null, 2, "ChoiceMade");
            Require(made == ("flee", 1), $"ChoiceMade {made}");

            made = null;
            bus.EmitSignal(EventBus.SignalName.ChoiceRequested, "flee", "Saída da Brecha", "Fugir?", new[] { "Fugir", "Ficar" });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "escolha de novo");
            await SkipTyping(ui);
            await UiPress("ui_cancel");
            await WaitUntil(() => made is not null, 2, "ChoiceMade ao cancelar");
            Require(made == ("flee", -1), $"Esc deveria responder -1 ({made})");
        }
        finally
        {
            bus.DialogueFinished -= OnFinished;
            bus.ChoiceMade -= OnChoice;
        }
    }

    [SmokeScenario("interface: todas as interações da cidade abrem a fala certa", order: 32)]
    private async Task AllInteractionsOpenDialogs()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var expected = new (string Kind, string Id, string Speaker)[]
        {
            ("talkClerk", "odete", "Odete"), ("talkSmith", "bras", "Brás"), ("oficiosDialog", "oficios", "Rua dos Ofícios"),
            ("portalDialog", "portal0", "Brecha"), ("talkInn", "lucia", "Lúcia"), ("restDialog", "rest", "Repouso"),
            ("talkGaspar", "gaspar", "Gaspar"), ("talkVesna", "vesna", "Vesna"), ("talkIvo", "ivo", "Ivo"), ("talkAbel", "abel", "Abel"),
        };
        foreach (var (kind, id, speaker) in expected)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, kind, id);
            await WaitUntil(() => ui.Dialog.IsOpen, 2, $"{kind} abrir diálogo");
            Require(ui.Dialog.Speaker.StartsWith(speaker, StringComparison.Ordinal), $"{kind}: esperava {speaker}, veio {ui.Dialog.Speaker}");
            var dialogue = Database.Db.Npcs.Dialogue;
            var verbatim = kind switch
            {
                "talkIvo" => dialogue["ivo"]["greetings"][0],
                "talkAbel" => dialogue["abel"]["greetings"][0],
                "talkGaspar" => dialogue["gaspar"]["greetings"][0],
                "talkVesna" => dialogue["vesna"]["noEcos"][0],
                _ => null,
            };
            Require(verbatim is null || ui.Dialog.Text == verbatim, $"{kind}: fala verbatim errada: '{ui.Dialog.Text}'");
            await SkipTyping(ui);
            await Shot($"ui-fala-{kind}");
            await DismissUi();
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "openBoard", "board");
        await WaitUntil(() => ui.Board.IsOpen, 2, "mural abrir");
        await DismissUi();
        Require(GameManager.Instance.State.Save.Quests.Counter(QuestCounters.Odete) == 1, "falar com a Odete conta para a missão");
    }

    [SmokeScenario("interface: forja do Brás compra com Lumes e materiais", order: 33)]
    private async Task ForgeUpgradeAndRecipes()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        state.AddLumes(500);
        state.AddMaterial("presa", 6);
        state.AddMaterial("minerio", 4);
        var lumesBefore = state.Lumes;

        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás abrir");
        await SkipTyping(ui);
        await Shot("ui-forja");
        await ChooseOption(ui, "Melhorar armas");
        await WaitUntil(() => ui.Dialog.Text.StartsWith("Cada nível", StringComparison.Ordinal), 2, "lista de melhorias");
        await SkipTyping(ui);
        var upgrade = ui.Dialog.Options[0];
        Require(upgrade.Label == "Lâmina da Lumeira  +1 → +2" && upgrade.Sub == "36 Lumes · 1× Presa Rubra (6)", $"opção de upgrade: '{upgrade.Label}' / '{upgrade.Sub}'");
        await Shot("ui-forja-melhorar");
        await ChooseOption(ui, "Lâmina da Lumeira  +1 → +2");
        await WaitUntil(() => state.Save.WeaponLevel("sword") == 2, 2, "Lâmina subir para +2");
        Require(state.Lumes == lumesBefore - 36, $"Lumes deveriam cair 36 (de {lumesBefore} para {state.Lumes})");
        Require(Hud.Current!.LumesLabel.Text == Ui.Thousands(state.Lumes), "HUD acompanha os Lumes");

        // Voltar → equipamento (receita com tooltip).
        await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Text.StartsWith("Cada nível", StringComparison.Ordinal), 2, "Brás reabrir a lista");
        await SkipTyping(ui);
        await ChooseOption(ui, "Voltar");
        await WaitUntil(() => ui.Dialog.Options.Any(o => o.Label == "Forjar equipamento"), 2, "voltar ao Brás");
        await SkipTyping(ui);
        await ChooseOption(ui, "Forjar equipamento");
        await WaitUntil(() => ui.Dialog.Text.StartsWith("Equipamento bom", StringComparison.Ordinal), 2, "receitas");
        await SkipTyping(ui);
        Require(ui.Dialog.Tooltip.Visible && ui.Dialog.Tooltip.ItemName == "Manoplas Rúnicas", $"tooltip da 1ª receita: '{ui.Dialog.Tooltip.ItemName}'");
        await Shot("ui-forja-receitas-tooltip");
        var lumes = state.Lumes;
        await ChooseOption(ui, "Manoplas Rúnicas");
        await WaitUntil(() => state.Save.Backpack.Contains("luva2"), 2, "forjar Manoplas Rúnicas");
        Require(state.Lumes == lumes - 54 && state.MaterialCount("minerio") == 0 && state.MaterialCount("presa") == 3, $"receita: {state.Lumes} Lumes, minério {state.MaterialCount("minerio")}, presa {state.MaterialCount("presa")}");
        await WaitSeconds(0.3);
        await Shot("ui-forja-forjado");
        await DismissUi();

        // Arma de outra classe: recusa efetiva (extra.js:94).
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás de novo");
        await SkipTyping(ui);
        await ChooseOption(ui, "Forjar nova arma");
        await WaitUntil(() => ui.Dialog.Text.StartsWith("Não forjo arma de outra classe", StringComparison.Ordinal), 2, "recusa de arma nova");
        Require(ui.Dialog.Text.Contains("Lâmina", StringComparison.Ordinal), $"recusa cita a classe: {ui.Dialog.Text}");
        await DismissUi();

        // Vender sobras e Estilhaços.
        state.AddMaterial("lapide", 3); // fora de uso: 4 cada
        state.AddMaterial("estilhaco", 5);
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás para vender");
        await SkipTyping(ui);
        var sell = ui.Dialog.Options.First(o => o.Label == "Vender sobras de Brecha");
        Require(sell.Sub == "3 itens sem uso em receitas · 12 Lumes", $"cotação das sobras: '{sell.Sub}'");
        lumes = state.Lumes;
        await ChooseOption(ui, "Vender sobras de Brecha");
        await WaitUntil(() => state.Lumes == lumes + 12, 2, "venda de sobras");
        await SkipTyping(ui);
        await ChooseOption(ui, "Vender Estilhaços");
        await WaitUntil(() => state.Lumes == lumes + 12 + 20 && state.Shards == 0, 2, "venda de Estilhaços (4 cada)");
        await DismissUi();

        // Invasão: Brás fecha e o preço da Lúcia vira ×1,35 (30 → 41, JS).
        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, true);
        await NextPhysicsFrame();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás na invasão");
        Require(ui.Dialog.Text == Database.Db.Npcs.Dialogue["bras"]["invasion"][0], $"Brás deveria recusar: {ui.Dialog.Text}");
        await DismissUi();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkInn", "lucia");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Lúcia na invasão");
        await SkipTyping(ui);
        Require(ui.Dialog.Options[0].Sub!.StartsWith("41 Lumes", StringComparison.Ordinal), $"poção em invasão deveria custar 41: '{ui.Dialog.Options[0].Sub}'");
        await DismissUi();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
    }

    [SmokeScenario("interface: lojas, serviços e descanso", order: 34)]
    private async Task ShopsAndServices()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        state.AddLumes(600);
        state.AddMaterial("halo", 1);
        state.AddMaterial("estilhaco", 3);
        state.AddMaterial("erva", 4);

        // Abel: relíquia com tooltip e "Você já tem".
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkAbel", "abel");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Abel");
        await SkipTyping(ui);
        await ChooseOption(ui, "Lanterna Solar");
        await WaitUntil(() => state.Save.Backpack.Contains("ln_solar"), 2, "comprar Lanterna Solar");
        await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.ChoicesVisible || ui.Dialog.IsTyping, 2, "Abel reabrir");
        await SkipTyping(ui);
        Require(ui.Dialog.Options.First(o => o.Label == "Lanterna Solar") is { Disabled: true, Sub: "Você já tem" }, "Lanterna Solar já comprada");
        await FocusOption(ui, "Égide Solar");
        await Shot("ui-loja-abel");
        await DismissUi();

        // Ivo: poções.
        var potions = state.Save.Potions;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkIvo", "ivo");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Ivo");
        await SkipTyping(ui);
        await ChooseOption(ui, "Destilar Erva-de-Véspera");
        await WaitUntil(() => state.Save.Potions == potions + 2, 2, "destilar 2 poções");
        await SkipTyping(ui);
        await Shot("ui-loja-ivo");
        await DismissUi();

        // Gaspar: infusão Solar na Lâmina (80 L + 3 Estilhaços + 2 Erva).
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkGaspar", "gaspar");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Gaspar");
        await SkipTyping(ui);
        await ChooseOption(ui, "Lâmina da Lumeira");
        await WaitUntil(() => ui.Dialog.Text.StartsWith("Lobos e espectros", StringComparison.Ordinal), 2, "infusões da Lâmina");
        await SkipTyping(ui);
        await ChooseOption(ui, "Solar");
        await WaitUntil(() => state.Save.ActiveInfusion.GetValueOrDefault("sword") == "solar", 2, "infusão Solar ativa");
        await SkipTyping(ui);
        Require(ui.Dialog.Options[0].Label == "Solar  (ativa)", $"Solar deveria aparecer ativa: '{ui.Dialog.Options[0].Label}'");
        await Shot("ui-loja-gaspar");
        await DismissUi();

        // Vesna: Diário da Brecha.
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkVesna", "vesna");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Vesna");
        await SkipTyping(ui);
        await ChooseOption(ui, "Comprar Diário da Brecha");
        await WaitUntil(() => state.Save.OwnsGear("livro"), 2, "comprar o Diário");
        await DismissUi();

        // Lúcia → descansar: dia novo, HP cheio.
        GameManager.Instance.DamagePlayer(20);
        var day = state.Save.Day;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkInn", "lucia");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Lúcia");
        await SkipTyping(ui);
        await ChooseOption(ui, "Descansar até a próxima véspera");
        await WaitUntil(() => ui.Dialog.Speaker == "Repouso", 2, "diálogo do repouso");
        await SkipTyping(ui);
        await ChooseOption(ui, "Dormir");
        await WaitUntil(() => state.Save.Day == day + 1, 3, "dormir avança o dia");
        Require(state.Hp == state.MaxHp, "dormir cura tudo");
        await WaitSeconds(0.6);
        await Shot("ui-dormiu");
        await DismissUi();
    }

    [SmokeScenario("interface: inventário equipa, bloqueia e desmonta", order: 35)]
    private async Task InventoryEquip()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        state.AddGear("gibao"); // dag/bow: bloqueado para a Lâmina
        state.AddMaterial("presa", 2);

        await UiPress("inventory");
        await WaitUntil(() => ui.Inventory.IsOpen, 2, "inventário abrir");
        Require(GameManager.Instance.IsGameplayBlocked, "inventário trava o jogador");
        await WaitSeconds(0.2);
        var capuz = ui.Inventory.Cells.First(c => c.Item.Id == "capuz");
        capuz.Row.GrabFocus();
        await NextPhysicsFrame();
        Require(ui.Inventory.Detail.ItemName == "Capuz do Vigia", $"detalhe: '{ui.Inventory.Detail.ItemName}'");
        await Shot("ui-inventario");
        var hpBefore = state.MaxHp;
        await UiPress("ui_accept");
        await WaitUntil(() => state.Save.Equipment.GetValueOrDefault("cabeca") == "capuz", 2, "equipar o Capuz");
        Require(state.MaxHp == hpBefore + 10, $"Capuz dá +10 de vida ({hpBefore} → {state.MaxHp})");

        var gibao = ui.Inventory.Cells.First(c => c.Item.Id == "gibao");
        gibao.Row.GrabFocus();
        await NextPhysicsFrame();
        Require(ui.Inventory.Detail.LockText == "Exclusivo de Adagas / Arco. Sua classe é Lâmina.", $"trava por classe: '{ui.Inventory.Detail.LockText}'");
        await Shot("ui-inventario-bloqueado");
        var shards = state.Shards;
        await UiKey(Key.X);
        await WaitUntil(() => !state.Save.Backpack.Contains("gibao"), 2, "desmontar o Gibão");
        Require(state.Shards == shards + 2, "desmontar dá +2 Estilhaços");

        // Desequipar pelo slot do palco.
        ui.Inventory.SlotRow("cabeca")!.GrabFocus();
        await NextPhysicsFrame();
        await UiPress("ui_accept");
        await WaitUntil(() => !state.Save.Equipment.ContainsKey("cabeca"), 2, "desequipar a cabeça");

        await UiKey(Key.Tab);
        await WaitUntil(() => ui.Inventory.Tab == 1, 2, "aba Ecos");
        await WaitSeconds(0.2);
        await Shot("ui-inventario-ecos");
        await UiKey(Key.Tab);
        await WaitUntil(() => ui.Inventory.Tab == 2, 2, "aba Selos");
        await WaitSeconds(0.2);
        await Shot("ui-inventario-selos");
        await UiPress("ui_cancel");
        await WaitUntil(() => !ui.Inventory.IsOpen && !GameManager.Instance.IsGameplayBlocked, 2, "inventário fechar");
    }

    [SmokeScenario("interface: pausa, opções de volume, diário e mural", order: 36)]
    private async Task PauseAndOptions()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var paused = new List<bool>();
        void OnPause(bool value) => paused.Add(value);
        EventBus.Instance.PauseChanged += OnPause;
        try
        {
            await UiPress("pause");
            await WaitUntil(() => ui.Pause.IsOpen, 2, "pausa abrir");
            Require(paused.SequenceEqual([true]) && GameManager.Instance.IsGameplayBlocked, $"PauseChanged(true) e jogo travado ({string.Join(",", paused)})");
            await WaitSeconds(0.2);
            await Shot("ui-pausa");

            // Opções: 5ª linha.
            for (var i = 0; i < 4; i++)
            {
                await UiPress("ui_down");
            }

            await UiPress("ui_accept");
            await WaitUntil(() => ui.Options.IsOpen, 2, "opções abrir");
            await WaitSeconds(0.2);
            var music = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
            await UiPress("ui_down"); // Música
            await UiPress("ui_left");
            await UiPress("ui_left");
            var after = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
            Require(after < music - 0.15f, $"← duas vezes deveria baixar a música 20% ({music} → {after})");
            await Shot("ui-opcoes");
            await UiPress("ui_cancel");
            await WaitUntil(() => ui.Stack.Top == ui.Pause, 2, "voltar à pausa");
            await WaitSeconds(0.2);
            var saved = new ConfigFile();
            saved.Load(AudioManager.Instance.SettingsFile);
            Require(Math.Abs((float)saved.GetValue("audio", "music", 1f) - after) < 0.01f, "fechar as opções grava o volume");

            // Diário.
            await UiPress("ui_up");
            await UiPress("ui_up");
            await UiPress("ui_up");
            await UiPress("ui_accept");
            await WaitUntil(() => ui.Journal.IsOpen, 2, "diário abrir");
            await WaitSeconds(0.2);
            await Shot("ui-diario");
            await UiPress("ui_cancel");
            await WaitUntil(() => ui.Stack.Top == ui.Pause, 2, "voltar à pausa do diário");
            await WaitSeconds(0.2);
            await UiPress("ui_cancel");
            await WaitUntil(() => ui.Stack.Count == 0, 2, "fechar a pausa");
            Require(paused.SequenceEqual([true, false]) && !GameManager.Instance.IsGameplayBlocked, $"PauseChanged(false) ao fechar ({string.Join(",", paused)})");

            // Mural pelo ScreenRequested, com Brechas da Cidade.
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 1, "crypt", 1);
            EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "board");
            await WaitUntil(() => ui.Board.IsOpen, 2, "mural abrir");
            await WaitSeconds(0.2);
            Require(ui.Board.Count == 2, $"mural com 2 Brechas, tem {ui.Board.Count}");
            await UiPress("ui_accept"); // rastrear
            await NextPhysicsFrame();
            Require(ui.Brechas.TrackedId is not null, "Enter rastreia a Brecha");
            await Shot("ui-mural");
            await DismissUi();
        }
        finally
        {
            EventBus.Instance.PauseChanged -= OnPause;
        }
    }

    [SmokeScenario("interface: portal pede BrechaStartRequested e respeita o Selo", order: 37)]
    private async Task PortalDialogStartsBrecha()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        int? requested = null;
        void OnStart(int spot) => requested = spot;
        EventBus.Instance.BrechaStartRequested += OnStart;
        try
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 1, "fish", 1);
            EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "portalDialog", "portal1");
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "diálogo do portal");
            Require(ui.Dialog.Speaker == "Brecha · Mercado de Peixes", $"portal: '{ui.Dialog.Speaker}'");
            await SkipTyping(ui);
            Require(ui.Dialog.Text.Contains("acima do seu Selo", StringComparison.Ordinal), $"aviso de rank acima: {ui.Dialog.Text}");
            await Shot("ui-portal");
            await ChooseOption(ui, "Entrar na Brecha");
            await WaitUntil(() => requested is not null, 2, "BrechaStartRequested");
            Require(requested == 1, $"spot pedido: {requested}");
            await DismissUi();

            // Dois Selos acima: o Código da Véspera proíbe (extra.js:97).
            requested = null;
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "cathedral", 4);
            EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "portalDialog", "portal0");
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "portal proibido");
            Require(ui.Dialog.Text.StartsWith("O Código da Véspera proíbe", StringComparison.Ordinal) && ui.Dialog.Options.Count == 0, $"recusa: {ui.Dialog.Text}");
            await DismissUi();
            Require(requested is null, "Brecha proibida não pode ser pedida");
        }
        finally
        {
            EventBus.Instance.BrechaStartRequested -= OnStart;
        }
    }

    [SmokeScenario("interface: morte mostra a tela da v2 e pede respawn", order: 38)]
    private async Task DeathAndRespawn()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var respawns = 0;
        void OnRespawn()
        {
            // Stub do Combate: devolve o jogador na estalagem (LanternRules.DieInCity).
            respawns++;
            GameManager.Instance.State.DieInCity();
        }

        EventBus.Instance.RespawnRequested += OnRespawn;
        try
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStartRequested, 0);
            EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStarted, "crypt", 1);
            GameManager.Instance.DamagePlayer(9999);
            await WaitUntil(() => ui.Death.IsOpen, 4, "tela de morte (2,4 s)");
            Require(ui.Death.InBrecha && ui.Death.Title == "Sua Lanterna se apagou", $"morte na Brecha: '{ui.Death.Title}'");
            await UiPress("ui_accept"); // antes de 1,2 s não responde
            Require(respawns == 0, "confirmar antes de 1,2 s não deveria voltar");
            await WaitSeconds(1.3);
            await Shot("ui-morte");
            await UiPress("ui_accept");
            await WaitUntil(() => respawns == 1 && !ui.Death.IsOpen, 2, "RespawnRequested");
            Require(!GameManager.Instance.IsGameplayBlocked, "depois do respawn o jogo volta");
            EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaEnded, "crypt", false);
        }
        finally
        {
            EventBus.Instance.RespawnRequested -= OnRespawn;
        }
    }

    // --- utilidades ---

    private GameUi UiOrFail() => GameUi.Current ?? throw new SmokeFailure("sem GameUi no World");

    /// <summary>Jogo novo limpo na Guilda, sem telas abertas.</summary>
    private async Task<World.World> FreshGame(string classId)
    {
        await WaitForScene("World", 10);
        var world = FindWorld();
        GameUi.Current?.CloseAll();
        GameUi.Current?.Brechas.Clear();
        GameManager.Instance.NewGame("Rui", classId);
        world.GoMap("guild", 250, 330, instant: true);
        await WaitSeconds(0.45);
        await DismissUi();
        return world;
    }

    /// <summary>Fecha qualquer tela (e a que abrir logo depois, como o diálogo de fim de missão).</summary>
    private async Task DismissUi()
    {
        for (var i = 0; i < 2; i++)
        {
            GameUi.Current?.CloseAll();
            await WaitSeconds(0.35);
        }

        GameUi.Current?.CloseAll();
        await NextPhysicsFrame();
    }

    private async Task SkipTyping(GameUi ui)
    {
        await WaitSeconds(0.22);
        // Só aperta com folga: perto do fim o texto completa sozinho e o Enter cairia na 1ª opção.
        if (ui.Dialog.TypingRemaining > 12)
        {
            await UiPress("ui_accept");
        }

        await WaitUntil(() => !ui.Dialog.IsTyping, 3, "texto do diálogo completo");
        await WaitSeconds(0.17);
    }

    /// <summary>Completa o texto (se digitando) e avança a fala.</summary>
    private async Task AdvanceDialog()
    {
        await SkipTyping(UiOrFail());
        await UiPress("ui_accept");
        await WaitSeconds(0.25);
    }

    /// <summary>Navega com ↓ até a opção (pelo rótulo) e confirma com Enter.</summary>
    private async Task ChooseOption(GameUi ui, string label)
    {
        await FocusOption(ui, label);
        await UiPress("ui_accept");
        await WaitSeconds(0.25);
    }

    private async Task FocusOption(GameUi ui, string label)
    {
        await WaitUntil(() => ui.Dialog.ChoicesVisible, 2, $"opções para '{label}'");
        var target = -1;
        for (var i = 0; i < ui.Dialog.Options.Count; i++)
        {
            if (ui.Dialog.Options[i].Label == label)
            {
                target = i;
                break;
            }
        }

        Require(target >= 0, $"opção '{label}' não existe: {string.Join(" | ", ui.Dialog.Options.Select(o => o.Label))}");
        for (var guard = 0; guard < 20 && !ui.Dialog.Rows[target].HasFocus(); guard++)
        {
            await UiPress("ui_down");
        }

        Require(ui.Dialog.Rows[target].HasFocus(), $"não consegui focar '{label}' com ↓");
    }

    /// <summary>Aperta e solta uma ação como evento (passa pela GUI: foco, botões, telas).</summary>
    private async Task UiPress(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await NextPhysicsFrame();
    }

    private async Task UiKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await NextPhysicsFrame();
    }
}
