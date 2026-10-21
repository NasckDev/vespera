using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.UI;
using Vespera.UI.Screens;
using Vespera.UI.Title;

namespace Vespera.Testing;

/// <summary>
/// Cenários do tester de experiência da frente B3 (Interface e economia), rodada 1. Jogam como um jogador:
/// andam até cada NPC com input, apertam E/A, navegam as telas só com teclas reais (InputEventKey) ou só com
/// botões de gamepad (InputEventJoypadButton), conferem que a pausa congela o jogador sem vazar ataque/interação,
/// que o preço mostrado é o cobrado, que Continuar volta ao jogo salvo e que as telas não alocam por frame.
/// Os "estresse:" (spam de botões, 50 trocas de mapa, bot de 5 min pela cidade, tempo de frame) só rodam com
/// <c>--smoke-stress</c>.
/// </summary>
public partial class SmokeTest
{
    private static readonly (string Map, string Id, string Speaker)[] QaTownTargets =
    [
        ("guild", "odete", "Odete"), ("guild", "board", "board"), ("forge", "bras", "Brás"), ("forge", "oficios", "Rua dos Ofícios"),
        ("forge", "portal0", "Brecha"), ("inn", "lucia", "Lúcia"), ("inn", "rest", "Repouso"), ("shop_arm", "gaspar", "Gaspar"),
        ("shop_mag", "vesna", "Vesna"), ("shop_alq", "ivo", "Ivo"), ("shop_rel", "abel", "Abel"),
    ];

    private static readonly string[] QaTownMaps = ["guild", "forge", "inn", "shop_arm", "shop_mag", "shop_alq", "shop_rel"];

    private static readonly Regex QaPriceRegex = new(@"^(\d+) Lumes", RegexOptions.CultureInvariant);

    [SmokeScenario("interface (QA): andar até cada NPC com input e apertar E abre a fala certa", order: 40)]
    private async Task QaWalkToEveryNpc()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        var player = FindPlayer()!;
        var failures = new List<string>();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
        foreach (var (mapId, id, speaker) in QaTownTargets)
        {
            var map = World.World.Catalog[mapId];
            world.GoMap(mapId, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            if (id.StartsWith("portal", StringComparison.Ordinal))
            {
                world.SetInteractableEnabled(id, true, "Brecha · Mercado de Peixes"); // a Cidade liga o portal ao abrir a Brecha
            }

            var it = world.GetInteractable(id);
            if (it is null)
            {
                failures.Add($"{mapId}/{id}: interagível não existe");
                continue;
            }

            var walkError = await QaWalkNear(world, new MapPoint(it.Position.X, it.Position.Y), it.Radius - 2, () => player.FocusedInteractable == it);
            if (walkError is not null)
            {
                failures.Add($"{mapId}/{id}: não cheguei andando ({walkError})");
                continue;
            }

            await Frames(2);
            if (!hud.PromptVisible || hud.PromptLabel.Text != it.Prompt)
            {
                failures.Add($"{mapId}/{id}: prompt do HUD '{hud.PromptLabel.Text}' (visível={hud.PromptVisible}), esperava '{it.Prompt}'");
            }

            await Shot($"qa-prompt-{id}");
            await QaKeyTap(Key.E);
            var opened = await QaWaitFor(() => ui.Dialog.IsOpen || ui.Board.IsOpen, 2);
            if (!opened)
            {
                failures.Add($"{mapId}/{id}: E não abriu nada");
                continue;
            }

            if (speaker == "board" ? !ui.Board.IsOpen : !ui.Dialog.Speaker.StartsWith(speaker, StringComparison.Ordinal))
            {
                failures.Add($"{mapId}/{id}: abriu '{ui.Dialog.Speaker}' (mural={ui.Board.IsOpen}), esperava {speaker}");
            }

            if (ui.Dialog.IsOpen)
            {
                await SkipTyping(ui);

                // Fala verbatim: a primeira fala de quem tem falas no npcs.json tem de ser uma delas (com {name}/{class}).
                if (Database.Db.Npcs.Dialogue.TryGetValue(id, out var lines))
                {
                    var known = lines.Values.SelectMany(l => l).Select(UI.Town.TownServices.Fill).ToHashSet();
                    if (!known.Contains(ui.Dialog.Text))
                    {
                        failures.Add($"{mapId}/{id}: fala fora do npcs.json: '{ui.Dialog.Text}'");
                    }
                }
            }

            await Shot($"qa-npc-{id}");

            // Fecha só com Esc, como o jogador (o fim de missão pode abrir outra fala logo depois).
            for (var round = 0; round < 3; round++)
            {
                for (var i = 0; i < 6 && ui.Stack.Count > 0; i++)
                {
                    await QaKeyTap(Key.Escape);
                    await WaitSeconds(0.2);
                }

                await WaitSeconds(0.35);
            }

            if (ui.Stack.Count > 0 || GameManager.Instance.IsGameplayBlocked)
            {
                failures.Add($"{mapId}/{id}: Esc não devolveu o jogo (topo={ui.Stack.Top?.GetType().Name}, travado={GameManager.Instance.IsGameplayBlocked})");
                await DismissUi();
                continue;
            }

            if (!await QaCanStillWalk(player))
            {
                failures.Add($"{mapId}/{id}: depois de fechar a fala o jogador não anda em nenhuma direção ({player.GlobalPosition})");
            }

            Log.Info($"qa: {mapId}/{id} ok ({ui.Dialog.Speaker})");
        }

        Require(failures.Count == 0, $"{failures.Count} NPC(s) com problema:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("interface (QA): só teclado — pausa, opções, inventário e forja do Brás", order: 41)]
    private async Task QaKeyboardOnly()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        state.AddLumes(300);
        state.AddMaterial("presa", 3);

        // Pausa → Opções (5ª linha) → Música −10% → Painel de missão alterna → volta.
        await QaKeyTap(Key.Escape);
        await WaitUntil(() => ui.Pause.IsOpen, 2, "Esc abrir a pausa");
        await WaitSeconds(0.2);
        for (var i = 0; i < 4; i++)
        {
            await QaKeyTap(Key.Down);
        }

        await QaKeyTap(Key.Enter);
        await WaitUntil(() => ui.Options.IsOpen, 2, "Enter abrir as opções");
        await WaitSeconds(0.2);
        await QaKeyTap(Key.Down);
        var music = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
        await QaKeyTap(Key.Left);
        var lowered = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
        Require(music <= 0.01f || lowered < music - 0.05f, $"← na Música deveria baixar 10% ({music} → {lowered})");
        await QaKeyTap(Key.Right);
        for (var i = 0; i < 5; i++)
        {
            await QaKeyTap(Key.Down);
        }

        var hints = GameSettings.QuestHints;
        await QaKeyTap(Key.Enter); // Painel de missão no HUD
        Require(GameSettings.QuestHints != hints, "Enter no 'Painel de missão' deveria alternar");
        await Shot("qa-teclado-opcoes");
        await QaKeyTap(Key.Enter); // volta ao normal
        Require(GameSettings.QuestHints == hints, "Enter de novo deveria voltar o painel");
        await QaKeyTap(Key.Escape);
        await WaitUntil(() => ui.Stack.Top == ui.Pause, 2, "Esc voltar à pausa");
        await WaitSeconds(0.2);
        await QaKeyTap(Key.Escape);
        await WaitUntil(() => ui.Stack.Count == 0 && !GameManager.Instance.IsGameplayBlocked, 2, "Esc fechar a pausa");

        // Inventário: Tab abre, setas movem o foco, Tab troca de aba, Esc fecha.
        await QaKeyTap(Key.Tab);
        await WaitUntil(() => ui.Inventory.IsOpen, 2, "Tab abrir o inventário");
        await WaitSeconds(0.2);
        var focus = GetViewport().GuiGetFocusOwner();
        Require(focus is not null, "inventário abriu sem foco visível");
        await QaKeyTap(Key.Right);
        Require(GetViewport().GuiGetFocusOwner() != focus, "→ deveria mover o foco no inventário");
        await QaKeyTap(Key.Tab);
        await WaitUntil(() => ui.Inventory.Tab == 1, 2, "Tab ir para Ecos");
        await QaKeyTap(Key.Tab);
        await WaitUntil(() => ui.Inventory.Tab == 2, 2, "Tab ir para Selos");
        await QaKeyTap(Key.Escape);
        await WaitUntil(() => !ui.Inventory.IsOpen && !GameManager.Instance.IsGameplayBlocked, 2, "Esc fechar o inventário");

        // Brás: anda com WASD (digital, 8 direções), E, Enter e setas.
        world.GoMap("forge", 1500, 250, instant: true);
        await WaitMapSettled(world);
        await DismissUi();
        var bras = world.GetInteractable("bras")!;
        var player = FindPlayer()!;
        var error = await QaWalkNearWithKeys(world, new MapPoint(bras.Position.X, bras.Position.Y), bras.Radius - 2, () => player.FocusedInteractable == bras);
        Require(error is null, $"andar com WASD até o Brás: {error}");
        await QaKeyTap(Key.E);
        await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Brás", 2, "E falar com o Brás");
        await QaSkipTypingWithKey(ui, Key.Enter);
        Require(ui.Dialog.Rows.Count > 0 && ui.Dialog.Rows[0].HasFocus(), "1ª opção do Brás deveria ter o foco");
        await QaKeyTap(Key.Enter); // Melhorar armas
        await WaitUntil(() => ui.Dialog.Text.StartsWith("Cada nível", StringComparison.Ordinal), 2, "Enter em 'Melhorar armas'");
        await QaSkipTypingWithKey(ui, Key.Enter);
        var price = QaPrice(ui.Dialog.Options[0].Sub);
        Require(price > 0, $"preço da melhoria ilegível: '{ui.Dialog.Options[0].Sub}'");
        var lumes = state.Lumes;
        await QaKeyTap(Key.Enter);
        await WaitUntil(() => state.Save.WeaponLevel("sword") == 2, 2, "Enter comprar a melhoria");
        Require(lumes - state.Lumes == price, $"mostrou {price} Lumes, cobrou {lumes - state.Lumes}");
        await Shot("qa-teclado-forja");
        for (var i = 0; i < 6 && ui.Stack.Count > 0; i++)
        {
            await QaKeyTap(Key.Escape);
            await WaitSeconds(0.2);
        }

        Require(ui.Stack.Count == 0 && !GameManager.Instance.IsGameplayBlocked, "Esc deveria fechar a forja");
    }

    [SmokeScenario("interface (QA): só gamepad — pausa, opções, inventário, escolha e Odete", order: 42)]
    private async Task QaGamepadOnly()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var failures = new List<string>();
        foreach (var (action, button) in new (string, JoyButton)[] { ("ui_accept", JoyButton.A), ("ui_cancel", JoyButton.B), ("ui_down", JoyButton.DpadDown), ("ui_up", JoyButton.DpadUp) })
        {
            if (!InputMap.EventIsAction(new InputEventJoypadButton { ButtonIndex = button, Pressed = true }, action))
            {
                failures.Add($"InputMap: o botão {button} não dispara {action} (eventos: {string.Join(", ", InputMap.ActionGetEvents(action).Select(e => e.AsText()))})");
            }
        }

        await QaPadTap(JoyButton.Start);
        if (!await QaWaitFor(() => ui.Pause.IsOpen, 2))
        {
            throw new SmokeFailure("Start não abriu a pausa");
        }

        await WaitSeconds(0.2);
        for (var i = 0; i < 4; i++)
        {
            await QaPadTap(JoyButton.DpadDown);
        }

        if (!ui.Pause.Rows[4].HasFocus())
        {
            failures.Add($"d-pad ↓ ×4 deveria focar 'Opções' (foco: {GetViewport().GuiGetFocusOwner()?.GetType().Name})");
        }

        await QaPadTap(JoyButton.A);
        if (await QaWaitFor(() => ui.Options.IsOpen, 2))
        {
            await WaitSeconds(0.2);
            await QaPadTap(JoyButton.DpadDown);
            var music = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
            await QaPadTap(JoyButton.DpadLeft);
            var lowered = AudioManager.Instance.GetBusVolume(AudioManager.MusicBus);
            if (music > 0.01f && lowered >= music - 0.05f)
            {
                failures.Add($"d-pad ← na Música não baixou ({music} → {lowered})");
            }

            await QaPadTap(JoyButton.DpadRight);
            await QaPadTap(JoyButton.B);
            if (!await QaWaitFor(() => ui.Stack.Top == ui.Pause, 2))
            {
                // Sem B, o jogador de gamepad tenta Start e Back: algum deles tira das Opções?
                await QaPadTap(JoyButton.Start);
                await QaPadTap(JoyButton.Back);
                var escaped = await QaWaitFor(() => ui.Stack.Top != ui.Options, 1);
                failures.Add($"B não voltou das opções para a pausa (Start/Back {(escaped ? "tiram" : "também não tiram")} das Opções: jogador de gamepad preso)");
            }
        }
        else
        {
            failures.Add("A não abriu as opções");
        }

        await WaitSeconds(0.2);
        await QaPadTap(JoyButton.B);
        if (!await QaWaitFor(() => ui.Stack.Count == 0, 2))
        {
            failures.Add($"B não fechou a pausa (topo {ui.Stack.Top?.GetType().Name})");
            await DismissUi();
        }

        // Inventário pelo Back, abas com LB/RB.
        await WaitSeconds(0.2);
        await QaPadTap(JoyButton.Back);
        if (await QaWaitFor(() => ui.Inventory.IsOpen, 2))
        {
            await WaitSeconds(0.2);
            var focus = GetViewport().GuiGetFocusOwner();
            await QaPadTap(JoyButton.DpadRight);
            if (focus is null || GetViewport().GuiGetFocusOwner() == focus)
            {
                failures.Add("d-pad → não move o foco no inventário");
            }

            await QaPadTap(JoyButton.RightShoulder);
            if (!await QaWaitFor(() => ui.Inventory.Tab == 1, 1))
            {
                failures.Add("RB não foi para a aba Ecos");
            }

            await QaPadTap(JoyButton.LeftShoulder);
            if (!await QaWaitFor(() => ui.Inventory.Tab == 0, 1))
            {
                failures.Add("LB não voltou para a aba Personagem");
            }

            await QaPadTap(JoyButton.B);
            if (!await QaWaitFor(() => !ui.Inventory.IsOpen, 2))
            {
                failures.Add("B não fechou o inventário");
                await DismissUi();
            }
        }
        else
        {
            failures.Add("Back não abriu o inventário");
        }

        // Escolha (ChoiceRequested) só com d-pad e A.
        (string Id, int Index)? made = null;
        void OnChoice(string id, int index) => made = (id, index);
        EventBus.Instance.ChoiceMade += OnChoice;
        try
        {
            await WaitSeconds(0.2);
            EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceRequested, "qa", "Saída da Brecha", "Fugir para Lumeira?", new[] { "Fugir", "Ficar" });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "escolha abrir");
            await QaSkipTypingWithPad(ui);
            await QaPadTap(JoyButton.DpadDown);
            await QaPadTap(JoyButton.A);
            if (!await QaWaitFor(() => made is not null, 2) || made != ("qa", 1))
            {
                failures.Add($"d-pad ↓ + A deveria escolher 'Ficar' (veio {made})");
            }
        }
        finally
        {
            EventBus.Instance.ChoiceMade -= OnChoice;
        }

        await DismissUi();

        // Odete com o stick (ações analógicas) e A; B fecha.
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        await DismissUi();
        var odete = world.GetInteractable("odete")!;
        var player = FindPlayer()!;
        var walk = await QaWalkNear(world, new MapPoint(odete.Position.X, odete.Position.Y), odete.Radius - 2, () => player.FocusedInteractable == odete);
        if (walk is not null)
        {
            failures.Add($"stick até a Odete: {walk}");
        }
        else
        {
            await QaPadTap(JoyButton.A);
            if (!await QaWaitFor(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Odete", 2))
            {
                failures.Add("A não falou com a Odete");
            }

            await QaSkipTypingWithPad(ui);
            await Shot("qa-gamepad-odete");
            for (var i = 0; i < 6 && ui.Stack.Count > 0; i++)
            {
                await QaPadTap(JoyButton.B);
                await WaitSeconds(0.2);
            }

            if (ui.Stack.Count > 0)
            {
                failures.Add($"B não fechou a fala da Odete (topo {ui.Stack.Top?.GetType().Name})");
            }
        }

        await DismissUi();
        Require(failures.Count == 0, $"{failures.Count} falha(s) no gamepad:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("interface (QA): pausa congela o jogador e não vaza ataque nem interação", order: 43)]
    private async Task QaPauseFreezes()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var player = FindPlayer()!;
        var interactions = 0;
        void OnInteraction(string kind, string id) => interactions++;
        EventBus.Instance.InteractionRequested += OnInteraction;
        try
        {
            // Andando para a direita com D segurado, Esc pausa: o jogador para na hora.
            await QaKey(Key.D, true);
            await Frames(15);
            var walking = player.GlobalPosition;
            await Frames(10);
            Require(player.GlobalPosition.DistanceTo(walking) > 2f, "D segurado deveria andar antes da pausa");
            await QaKeyTap(Key.Escape);
            await WaitUntil(() => ui.Pause.IsOpen, 2, "Esc pausar andando");
            await Frames(2);
            var frozen = player.GlobalPosition;
            await WaitSeconds(1.0);
            Require(player.GlobalPosition.DistanceTo(frozen) < 0.5f, $"com a pausa aberta o jogador andou {player.GlobalPosition.DistanceTo(frozen):0.0} u");

            // Ataque, golpe pesado e Lanterna não chegam ao jogo.
            foreach (var key in new[] { Key.J, Key.K, Key.F })
            {
                await QaKeyTap(key);
            }

            Require(!player.AttackPose, "J/K com a pausa aberta não pode atacar");
            Require(ui.Pause.IsOpen, "J/K/F não deveriam fechar a pausa");
            await QaKeyTap(Key.Escape);
            await WaitUntil(() => ui.Stack.Count == 0, 2, "Esc despausar");
            await Frames(10);
            Require(player.GlobalPosition.DistanceTo(frozen) > 1f, "com D ainda segurado, o jogador deveria voltar a andar ao despausar");
        }
        finally
        {
            await QaKey(Key.D, false);
        }

        try
        {
            // Perto da Odete: o E que confirma "Continuar" na pausa não pode também falar com ela.
            world.GoMap("guild", 250, 330, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            var odete = world.GetInteractable("odete")!;
            var walk = await QaWalkNear(world, new MapPoint(odete.Position.X, odete.Position.Y), odete.Radius - 2, () => player.FocusedInteractable == odete);
            Require(walk is null, $"andar até a Odete: {walk}");
            interactions = 0;
            await QaKeyTap(Key.Escape);
            await WaitUntil(() => ui.Pause.IsOpen, 2, "pausa perto da Odete");
            await WaitSeconds(0.2);
            await QaKeyTap(Key.E);
            await WaitSeconds(0.3);
            Require(interactions == 0, $"o E usado no menu vazou para o mundo ({interactions} InteractionRequested)");
            Require(!ui.Dialog.IsOpen, "o E da pausa abriu a fala da Odete por baixo");
            await DismissUi();

            // Diálogo aberto também trava: andar com D não move.
            EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueRequested, "Sargento Lauro", new[] { "A Guarda segura a rua." });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, "fala do Lauro");
            var before = player.GlobalPosition;
            await QaKey(Key.D, true);
            await Frames(20);
            await QaKey(Key.D, false);
            Require(player.GlobalPosition.DistanceTo(before) < 0.5f, "com diálogo aberto o jogador não pode andar");
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnInteraction;
            await DismissUi();
        }
    }

    [SmokeScenario("interface (QA): voltar ao menu pela pausa e Continuar carrega o jogo salvo", order: 44)]
    private async Task QaQuitAndContinue()
    {
        await WaitForScene("World", 10);
        var world = FindWorld();
        GameUi.Current?.CloseAll();
        GameManager.Instance.NewGame("Teo", "bow");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await DismissUi();
        var state = GameManager.Instance.State;
        state.AddLumes(77);
        state.AddMaterial("presa", 2);
        var lumes = state.Lumes;
        var ui = UiOrFail();
        try
        {
            await QaKeyTap(Key.Escape);
            await WaitUntil(() => ui.Pause.IsOpen, 2, "pausa");
            await WaitSeconds(0.2);
            for (var i = 0; i < 6; i++)
            {
                await QaKeyTap(Key.Down);
            }

            Require(ui.Pause.Rows[6].HasFocus(), "↓ ×6 deveria focar 'Voltar ao menu principal'");
            await QaKeyTap(Key.Enter);
            await WaitForScene("Title", 10);
            var title = (TitleScreen)GetTree().CurrentScene;
            Require(!title.ContinueRow.Disabled, "depois de sair pela pausa, Continuar deveria estar habilitado (a pausa salva)");
            Require(title.ContinueRow.HasFocus(), "com save, o foco deveria começar em Continuar");
            await WaitSeconds(0.3);
            await Shot("qa-titulo-continuar");
            await QaKeyTap(Key.Enter);
            await WaitForScene("World", 10);
            state = GameManager.Instance.State;
            Require(state.Save.Name == "Teo" && state.Save.ClassId == "bow", $"Continuar carregou {state.Save.Name} ({state.Save.ClassId})");
            Require(state.Lumes == lumes && state.MaterialCount("presa") == 2, $"Continuar perdeu a carteira: {state.Lumes} Lumes (esperava {lumes}), presa {state.MaterialCount("presa")}");
            var w = FindWorld();
            Require(w.MapId == "guild" && FindPlayer()!.GlobalPosition.DistanceTo(new Vector2(250, 330)) < 2, $"Continuar deveria abrir a Guilda em (250,330): {w.MapId} {FindPlayer()!.GlobalPosition}");
            Require(FindPlayer()!.ClassId == "bow", "o jogador deveria aparecer com o Arco");
            Require(Hud.Current is { } hud && hud.LumesLabel.Text == UI.Common.Ui.Thousands(lumes), "HUD deveria mostrar os Lumes do save");
            await WaitSeconds(0.3);
            await Shot("qa-continuar-mundo");
        }
        finally
        {
            if (GetTree().CurrentScene?.Name != "World")
            {
                GameManager.Instance.ChangeScene(TitleScreen.WorldScenePath);
                await WaitForScene("World", 10);
            }
        }
    }

    [SmokeScenario("interface (QA): preço mostrado nas lojas é o cobrado, normal e em invasão", order: 45)]
    private async Task QaShownPriceIsCharged()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        var failures = new List<string>();
        var services = Database.Db.Services;
        foreach (var invasion in new[] { false, true })
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, invasion);
            await NextPhysicsFrame();
            state.AddLumes(2000);
            foreach (var material in Database.Db.Materials.Keys)
            {
                state.AddMaterial(material, 6); // o que interessa aqui é o preço em Lumes, não faltar material
            }
            var cases = new List<(string Kind, string[] Path)>
            {
                ("talkInn", [services["inn_potion"].Label]),
                ("talkIvo", [services["ivo_potion"].Label]),
                ("talkIvo", [services["ivo_box"].Label]),
                ("talkAbel", [invasion ? "Escudo Lunar" : "Lanterna Lunar"]),
                ("talkGaspar", ["Lâmina da Lumeira", invasion ? "Lunar" : "Solar"]),
            };
            if (!invasion)
            {
                cases.Add(("talkSmith", ["Melhorar armas", $"Lâmina da Lumeira  +{state.Save.WeaponLevel("sword")} → +{state.Save.WeaponLevel("sword") + 1}"]));
                cases.Add(("talkVesna", [services["vesna_book"].Label]));
            }

            foreach (var (kind, path) in cases)
            {
                var error = await QaBuyAndCompare(ui, kind, path, invasion);
                if (error is not null)
                {
                    failures.Add($"{kind} › {string.Join(" › ", path)} (invasão={invasion}): {error}");
                }
            }
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
        await DismissUi();
        Require(failures.Count == 0, $"{failures.Count} preço(s) errado(s):\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("interface (QA): HUD e telas abertas não alocam por frame", order: 46)]
    private async Task QaScreensDoNotAllocate()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var bus = EventBus.Instance;
        var report = new List<string>();
        var worst = 0L;

        async Task Measure(string label)
        {
            await WaitSeconds(0.4);
            var frames0 = Engine.GetProcessFrames();
            var bytes0 = GC.GetTotalAllocatedBytes(precise: true);
            await WaitSeconds(1.5);
            var bytes = GC.GetTotalAllocatedBytes(precise: true) - bytes0;
            var frames = Math.Max(1UL, Engine.GetProcessFrames() - frames0);
            var perFrame = bytes / (long)frames;
            worst = Math.Max(worst, perFrame);
            report.Add($"{label}: {bytes} B em {frames} frames ({perFrame} B/frame)");
        }

        await Measure("HUD parado");
        bus.EmitSignal(EventBus.SignalName.AbilityCooldownChanged, "ultimate", 30f, 40f);
        bus.EmitSignal(EventBus.SignalName.AbilityCooldownChanged, "potion", 5f, 6f);
        bus.EmitSignal(EventBus.SignalName.BossSpawned, "O Abissal do Mercado", 520);
        bus.EmitSignal(EventBus.SignalName.BossHealthChanged, 300, 520);
        GameManager.Instance.DamagePlayer(20);
        await Measure("HUD em luta (recargas, chefe, lag da vida)");
        bus.EmitSignal(EventBus.SignalName.BossHealthChanged, 0, 520);
        bus.EmitSignal(EventBus.SignalName.DialogueRequested, "Odete", new[] { string.Join(" ", Enumerable.Repeat("Lanternas pequenas marcam cada porta da rua.", 5)) });
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "fala longa");
        await Measure("diálogo digitando");
        await DismissUi();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás");
        await SkipTyping(ui);
        await ChooseOption(ui, "Forjar equipamento");
        await SkipTyping(ui);
        await Measure("forja com opções e tooltip");
        await DismissUi();
        ui.OpenInventory();
        await Measure("inventário");
        await DismissUi();
        ui.OpenPause();
        await Measure("pausa");
        await DismissUi();
        ui.OpenBoard();
        await Measure("mural");
        await DismissUi();
        _ = world;
        foreach (var line in report)
        {
            Log.Info($"qa-alloc: {line}");
        }

        Require(worst < 512, $"tela aloca {worst} B por frame (esperado ~0):\n  " + string.Join("\n  ", report));
    }

    [SmokeScenario("interface (QA): tour visual de todas as telas, textos dentro de 640×360", order: 47)]
    private async Task QaVisualTour()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        var failures = new List<string>();
        state.AddLumes(1234);
        state.AddMaterial("presa", 5);
        state.AddMaterial("minerio", 4);
        state.AddGear("gibao");
        var bus = EventBus.Instance;
        bus.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
        bus.EmitSignal(EventBus.SignalName.PortalOpened, 1, "crypt", 1);
        bus.EmitSignal(EventBus.SignalName.StaminaChanged, 55f, 100f);
        bus.EmitSignal(EventBus.SignalName.EssenceChanged, 70f, 100f);
        bus.EmitSignal(EventBus.SignalName.WaveStarted, 1, 3);

        async Task Check(string name)
        {
            await WaitSeconds(0.3);
            failures.AddRange(QaOffscreenLabels(name));
            await Shot($"qa-tour-{name}");
        }

        await WaitSeconds(3.5); // banners somem
        await Check("hud");
        bus.EmitSignal(EventBus.SignalName.InteractionRequested, "talkClerk", "odete");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Odete");
        await SkipTyping(ui);
        await Check("dialogo-retrato");
        await DismissUi();
        bus.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás");
        await SkipTyping(ui);
        await ChooseOption(ui, "Forjar equipamento");
        await SkipTyping(ui);
        await FocusOption(ui, "Grevas do Alvor");
        await Check("forja-ultima-receita");
        await DismissUi();
        bus.EmitSignal(EventBus.SignalName.InteractionRequested, "talkAbel", "abel");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Abel");
        await SkipTyping(ui);
        await FocusOption(ui, "Sair");
        await Check("abel-fim-da-lista");
        await DismissUi();
        ui.OpenBoard();
        await Check("mural");
        await DismissUi();
        ui.OpenInventory();
        await Check("inventario");
        ui.Inventory.Cells.First(c => c.Item.Id == "gibao").Row.GrabFocus();
        await Check("inventario-comparacao");
        await DismissUi();
        ui.OpenPause();
        await Check("pausa");
        foreach (var (screen, name) in new (Screen, string)[] { (ui.Options, "opcoes"), (ui.Journal, "diario"), (ui.Help, "como-jogar"), (ui.Controls, "controles") })
        {
            ui.Stack.Push(screen);
            await Check(name);
            screen.Close();
        }

        await DismissUi();
        bus.EmitSignal(EventBus.SignalName.ScreenRequested, "death");
        await WaitUntil(() => ui.Death.IsOpen, 2, "morte");
        await WaitSeconds(1.3);
        await Check("morte");
        await DismissUi();
        _ = world;
        Require(failures.Count == 0, $"{failures.Count} texto(s) fora da tela:\n  " + string.Join("\n  ", failures));
    }

    // --- estresse ---

    [SmokeScenario("estresse: interface (QA) spam de interagir, esquivar, atacar, pausa e inventário", order: 90)]
    private async Task QaStressButtonSpam()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        world.GoMap("guild", 250, 300, instant: true); // perto da Odete e do Mural
        await WaitMapSettled(world);
        await DismissUi();
        var rng = new RandomNumberGenerator { Seed = 1234 };
        Key[] keys = [Key.E, Key.Space, Key.J, Key.K, Key.Escape, Key.Tab, Key.Enter, Key.Up, Key.Down, Key.Left, Key.Right, Key.W, Key.A, Key.S, Key.D, Key.I, Key.X, Key.Q, Key.R];
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var maxStack = 0;
        var titleVisits = 0;
        for (var i = 0; i < 800; i++)
        {
            var key = keys[rng.RandiRange(0, keys.Length - 1)];
            await QaKey(key, true);
            if (rng.Randf() < 0.3f)
            {
                await NextPhysicsFrame();
            }

            await QaKey(key, false);
            if (GetTree().CurrentScene?.Name != "World")
            {
                // ↓ + Enter/E na pausa cai em "Voltar ao menu principal" (sem confirmação, mas salva): volta ao jogo.
                titleVisits++;
                await WaitUntil(() => GetTree().CurrentScene?.Name.ToString() is "Title" or "World", 10, "troca de cena no spam");
                if (GetTree().CurrentScene?.Name == "Title")
                {
                    await WaitForScene("Title", 10); // espera o fade da troca terminar antes de pedir outra
                    await WaitSeconds(0.5);
                    GameManager.Instance.ChangeScene(TitleScreen.WorldScenePath);
                }

                await WaitForScene("World", 10);
                world = FindWorld();
                ui = UiOrFail();
                world.GoMap("guild", 250, 300, instant: true);
                await WaitMapSettled(world);
                await DismissUi();
                continue;
            }

            maxStack = Math.Max(maxStack, ui.Stack.Count);
            if (key == Key.X && ui.Inventory.IsOpen)
            {
                await QaKeyTap(Key.Escape); // não desmonta a mochila inteira
            }
        }

        for (var i = 0; i < 12 && ui.Stack.Count > 0; i++)
        {
            await QaKeyTap(Key.Escape);
            await WaitSeconds(0.2);
        }

        await WaitSeconds(0.5);
        for (var i = 0; i < 6 && ui.Stack.Count > 0; i++)
        {
            await QaKeyTap(Key.Escape);
            await WaitSeconds(0.2);
        }

        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        Log.Info($"qa-spam: 800 teclas, pilha máx {maxStack}, voltas ao título {titleVisits}, nós {nodes0} → {nodes1}, órfãos {Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)}");
        if (ui.Stack.Count > 0)
        {
            // Diagnóstico: o que está aberto, quem tem o foco e se a tela aceita entrada.
            var top = ui.Stack.Top!;
            var detail = $"topo {top.GetType().Name} (pilha {ui.Stack.Count}, idade {top.Age:0.00} s, pronta {top.InputReady}, visível {top.Visible}, foco {GetViewport().GuiGetFocusOwner()?.GetType().Name ?? "nenhum"}, foco recursivo {top.FocusBehaviorRecursive}, travado {GameManager.Instance.IsGameplayBlocked})";
            var tries = new List<string>();
            foreach (var key in new[] { Key.Escape, Key.Escape, Key.Enter })
            {
                await WaitSeconds(0.5);
                await QaKeyTap(key);
                await WaitSeconds(0.3);
                tries.Add($"{key}→{ui.Stack.Top?.GetType().Name ?? "vazia"}");
            }

            throw new SmokeFailure($"depois do spam, Esc não esvaziou as telas: {detail}; novas tentativas: {string.Join(", ", tries)}");
        }

        Require(!GameManager.Instance.IsGameplayBlocked, "depois do spam o jogo ficou travado");
        Require(maxStack <= 4, $"spam empilhou {maxStack} telas");
        Require(nodes1 - nodes0 < 200, $"nós cresceram {nodes1 - nodes0} com o spam");
        Require(await QaCanStillWalk(FindPlayer()!), "depois do spam o jogador não anda");
    }

    [SmokeScenario("estresse: interface (QA) 50 trocas de mapa com HUD, local certo e nós estáveis", order: 91)]
    private async Task QaStressMapChanges()
    {
        var world = await FreshGame("sword");
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        var failures = new List<string>();
        var samples = new List<double>();
        var perMap = new Dictionary<string, List<double>>();
        double nodesAt10 = 0;
        for (var i = 0; i < 50; i++)
        {
            var id = QaTownMaps[((i * 3) + 1) % QaTownMaps.Length];
            if (id == world.MapId)
            {
                id = QaTownMaps[(i + 2) % QaTownMaps.Length];
            }

            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y); // com fade, como uma saída de verdade
            await WaitMapSettled(world);
            if (!string.Equals(hud.LocationLabel.Text, map.Name, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"troca {i}: HUD diz '{hud.LocationLabel.Text}', mapa é '{map.Name}'");
            }

            var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            samples.Add(nodes);
            if (!perMap.TryGetValue(id, out var list))
            {
                perMap[id] = list = [];
            }

            list.Add(nodes);
            if (i == 10)
            {
                nodesAt10 = nodes;
            }

            if (i % 10 == 0 || i == 49)
            {
                Log.Info($"qa-mapas: troca {i} ({id}): {QaNodeBreakdown()}");
            }
        }

        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        Log.Info($"qa-mapas: nós min {samples.Min()} máx {samples.Max()} (troca 10: {nodesAt10}, fim: {samples[^1]}), órfãos {orphans}, memória {Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024):0.0} MB");
        Require(failures.Count == 0, string.Join("\n  ", failures));
        Require(orphans == 0, $"{orphans} nós órfãos depois de 50 trocas");
        // Mesmo mapa, visita 2 contra a última: a contagem tem de voltar ao mesmo patamar.
        var growth = perMap.Where(kv => kv.Value.Count >= 3).Select(kv => (Map: kv.Key, Delta: kv.Value[^1] - kv.Value[1], Trail: string.Join("→", kv.Value))).ToList();
        foreach (var (map, delta, trail) in growth)
        {
            Log.Info($"qa-mapas: {map}: {trail}");
        }

        var worst = growth.OrderByDescending(g => g.Delta).First();
        Require(worst.Delta < 40, $"nós crescem a cada volta ao mesmo mapa: {worst.Map} {worst.Trail} (+{worst.Delta})");
    }

    [SmokeScenario("estresse: interface (QA) bot de 5 min pela cidade: passeio aleatório e todos os pontos de interesse", order: 92)]
    private async Task QaStressTownBot()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var player = FindPlayer()!;
        var rng = new RandomNumberGenerator { Seed = 777 };
        var stuck = new List<string>();
        var unreachable = new List<string>();
        var visited = 0;
        var physics0 = Engine.GetPhysicsFrames();
        var baseTicks = Engine.PhysicsTicksPerSecond;
        Vector2[] dirs =
        [
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new Vector2(1, 1).Normalized(), new Vector2(-1, 1).Normalized(), new Vector2(1, -1).Normalized(), new Vector2(-1, -1).Normalized(),
        ];

        using var speed = new SpeedUp(TesterSpeedUp);
        double GameSeconds() => (Engine.GetPhysicsFrames() - physics0) / (double)(baseTicks * TesterSpeedUp) * TesterSpeedUp;
        while (GameSeconds() < 300)
        {
            foreach (var mapId in QaTownMaps)
            {
                var map = World.World.Catalog[mapId];
                world.GoMap(mapId, map.Spawn.X, map.Spawn.Y, instant: true);
                await WaitMapSettled(world);
                await DismissUi();

                // Passeio aleatório (~25 s de jogo) com detecção de travamento real (nenhuma direção anda).
                var until = GameSeconds() + 25;
                while (GameSeconds() < until)
                {
                    var d = dirs[rng.RandiRange(0, dirs.Length - 1)];
                    var frames = rng.RandiRange(120, 360); // 0,5–1,5 s de jogo (física ×4)
                    PressDirection(d);
                    var start = player.GlobalPosition;
                    for (var f = 0; f < frames; f++)
                    {
                        await NextPhysicsFrame();
                        if (world.IsTransitioning || world.MapId != mapId || ui.Stack.Count > 0)
                        {
                            break;
                        }

                        var p = player.GlobalPosition;
                        if (!world.WalkArea.IsWalkable(p.X, p.Y))
                        {
                            stuck.Add($"{mapId}: pé em área bloqueada em ({p.X:0},{p.Y:0})");
                            break;
                        }
                    }

                    PressDirection(Vector2.Zero);
                    if (ui.Stack.Count > 0)
                    {
                        await DismissUi();
                    }

                    if (world.IsTransitioning || world.MapId != mapId)
                    {
                        await WaitMapSettled(world);
                        world.GoMap(mapId, map.Spawn.X, map.Spawn.Y, instant: true); // saiu por uma saída: volta
                        await WaitMapSettled(world);
                        continue;
                    }

                    if (player.GlobalPosition.DistanceTo(start) < 1f && !await QaCanStillWalk(player))
                    {
                        stuck.Add($"{mapId}: preso em ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0}) — nenhuma das 4 direções anda");
                        player.Teleport(new Vector2(map.Spawn.X, map.Spawn.Y));
                    }
                }

                // Todos os pontos de interesse: interagíveis e saídas, a partir de onde o passeio parou.
                var targets = new List<(string Label, string? ItId, MapRect? Area, MapPoint Point, float Radius)>();
                foreach (var it in map.Interactables)
                {
                    targets.Add(($"interagível {it.Id}", it.Id, null, new MapPoint(it.X, it.Y), it.Radius - 2));
                }

                foreach (var exit in map.Exits)
                {
                    targets.Add(($"saída→{exit.To}", null, exit.Area, default, 0));
                }

                foreach (var (label, itId, area, point, radius) in targets)
                {
                    if (world.MapId != mapId)
                    {
                        world.GoMap(mapId, map.Spawn.X, map.Spawn.Y, instant: true);
                        await WaitMapSettled(world);
                    }

                    await DismissUi();
                    var interactable = itId is null ? null : world.GetInteractable(itId);
                    if (interactable is { Enabled: false })
                    {
                        continue; // portal sem Brecha aberta
                    }

                    // Encostado na parede o pé não "cabe" (CanStand) e o Core não acha caminho: parte do ponto livre mais perto.
                    var p = player.GlobalPosition;
                    var from = QaNearestStandable(world, p);
                    var path = area is { } rect ? WalkPathfinder.FindPathInto(world.WalkArea, from, rect) : WalkPathfinder.FindPathNear(world.WalkArea, from, point, radius);
                    if (path is null)
                    {
                        // Daqui o Core não acha caminho: registra e tenta do spawn (inalcançável de verdade só se falhar de lá também).
                        Log.Info($"qa-bot: {mapId} {label}: sem caminho a partir de ({p.X:0},{p.Y:0}); tentando do spawn");
                        player.Teleport(new Vector2(map.Spawn.X, map.Spawn.Y));
                        await NextPhysicsFrame();
                        from = new MapPoint(map.Spawn.X, map.Spawn.Y);
                        path = area is { } rect2 ? WalkPathfinder.FindPathInto(world.WalkArea, from, rect2) : WalkPathfinder.FindPathNear(world.WalkArea, from, point, radius);
                        if (path is null)
                        {
                            unreachable.Add($"{mapId} {label} (nem a partir do spawn {map.Spawn.X:0},{map.Spawn.Y:0})");
                            continue;
                        }
                    }

                    var loads = world.MapLoads;
                    Func<bool> done = area is null ? () => interactable is null || player.FocusedInteractable == interactable : () => world.MapLoads > loads;
                    var error = await TesterWalkPath(world, path, done, (path.Count * 4f / 78f * 1.6f) + 3f);
                    if (error is not null)
                    {
                        stuck.Add($"{mapId} {label}: {error}");
                    }
                    else
                    {
                        visited++;
                    }

                    if (world.MapLoads > loads)
                    {
                        await WaitMapSettled(world);
                    }
                }

                if (GameSeconds() >= 300)
                {
                    break;
                }
            }
        }

        PressDirection(Vector2.Zero);
        Log.Info($"qa-bot: {GameSeconds():0} s de jogo, {visited} pontos alcançados, {stuck.Count} travamento(s), {unreachable.Count} inalcançável(is)");
        foreach (var line in stuck.Concat(unreachable))
        {
            Log.Info($"qa-bot: {line}");
        }

        Require(stuck.Count == 0 && unreachable.Count == 0, $"{stuck.Count} travamento(s) e {unreachable.Count} ponto(s) inalcançável(is):\n  " + string.Join("\n  ", stuck.Concat(unreachable).Take(30)));
    }

    [SmokeScenario("estresse: interface (QA) tempo de frame com HUD e telas abertas (p95 ≤ 20 ms)", order: 93)]
    private async Task QaStressFrameTime()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var report = new List<string>();
        var worstP95 = 0.0;
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);

        async Task Sample(string label, int frames = 240)
        {
            await WaitSeconds(0.3);
            var deltas = new double[frames];
            var last = Time.GetTicksUsec();
            for (var i = 0; i < frames; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksUsec();
                deltas[i] = (now - last) / 1000.0;
                last = now;
            }

            Array.Sort(deltas);
            var p95 = deltas[(int)(frames * 0.95)];
            worstP95 = Math.Max(worstP95, p95);
            report.Add($"{label}: média {deltas.Average():0.00} ms, p95 {p95:0.00} ms, máx {deltas[^1]:0.00} ms");
        }

        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await DismissUi();
        await Sample("Rua da Forja + HUD");
        Input.ActionPress(InputActions.MoveRight);
        await Sample("andando na Rua da Forja");
        Input.ActionRelease(InputActions.MoveRight);
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras");
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "Brás");
        await Sample("diálogo do Brás");
        await DismissUi();
        ui.OpenInventory();
        await Sample("inventário");
        await DismissUi();
        ui.OpenPause();
        await Sample("pausa");
        await DismissUi();
        for (var i = 0; i < 10; i++)
        {
            ui.OpenInventory();
            await Frames(3);
            await DismissUi();
            ui.OpenPause();
            await Frames(3);
            await DismissUi();
        }

        await Sample("depois de abrir/fechar 20 telas");
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        report.Add($"memória {mem0 / (1024 * 1024):0.0} → {mem1 / (1024 * 1024):0.0} MB; nós {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
        foreach (var line in report)
        {
            Log.Info($"qa-frame: {line}");
        }

        Require(worstP95 <= 20.0, $"p95 de {worstP95:0.0} ms > 20 ms:\n  " + string.Join("\n  ", report));
    }

    // --- utilidades do QA ---

    /// <summary>Ponto onde o pé cabe mais perto de <paramref name="p"/> (até 12 u), ou o próprio ponto.</summary>
    private static MapPoint QaNearestStandable(World.World world, Vector2 p)
    {
        for (var r = 0f; r <= 12f; r += 2f)
        {
            for (var a = 0; a < 16; a++)
            {
                var q = p + (Vector2.Right.Rotated(a * Mathf.Tau / 16f) * r);
                if (world.WalkArea.CanStand(q.X, q.Y))
                {
                    return new MapPoint(q.X, q.Y);
                }

                if (r == 0f)
                {
                    break;
                }
            }
        }

        return new MapPoint(p.X, p.Y);
    }

    /// <summary>Nós por sub-árvore (HUD, GameUi, World sem a UI) para achar onde a contagem cresce.</summary>
    private string QaNodeBreakdown()
    {
        static int Count(Node n)
        {
            var total = 1;
            foreach (var child in n.GetChildren())
            {
                total += Count(child);
            }

            return total;
        }

        var hud = Hud.Current is { } h ? Count(h) : 0;
        var gameUi = GameUi.Current is { } g ? Count(g) : 0;
        var scene = GetTree().CurrentScene is { } s ? Count(s) : 0;
        var root = Count(GetTree().Root);
        return $"HUD {hud}, GameUi {gameUi}, cena {scene} (resto {scene - hud - gameUi}), árvore {root}, objetos {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}";
    }

    private static int QaPrice(string? sub)
    {
        var match = sub is null ? null : QaPriceRegex.Match(sub);
        return match is { Success: true } ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    /// <summary>Abre a loja, segue as opções do caminho, lê o preço da última e confere o desconto real.</summary>
    private async Task<string?> QaBuyAndCompare(GameUi ui, string kind, string[] path, bool invasion)
    {
        await DismissUi();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, kind, kind);
        if (!await QaWaitFor(() => ui.Dialog.IsOpen, 2))
        {
            return "a loja não abriu";
        }

        for (var i = 0; i < path.Length; i++)
        {
            await SkipTyping(ui);
            if (!await QaWaitFor(() => ui.Dialog.ChoicesVisible, 2))
            {
                return $"sem opções antes de '{path[i]}' (fala: {ui.Dialog.Text})";
            }

            // A arma com infusão ganha sufixo ("Lâmina da Lumeira · Solar"): basta o começo do rótulo.
            var option = ui.Dialog.Options.FirstOrDefault(o => o.Label == path[i]) ?? ui.Dialog.Options.FirstOrDefault(o => o.Label.StartsWith(path[i], StringComparison.Ordinal));
            if (option is null)
            {
                return $"opção '{path[i]}' não existe: {string.Join(" | ", ui.Dialog.Options.Select(o => o.Label))}";
            }

            if (i < path.Length - 1)
            {
                await ChooseOption(ui, option.Label);
                continue;
            }

            var shown = QaPrice(option.Sub);
            if (shown < 0)
            {
                return $"sem preço em Lumes legível: '{option.Sub}'";
            }

            if (option.Disabled)
            {
                return $"opção desabilitada com {GameManager.Instance.State.Lumes} Lumes ('{option.Sub}')";
            }

            var lumes = GameManager.Instance.State.Lumes;
            await ChooseOption(ui, option.Label);
            await WaitSeconds(0.2);
            var charged = lumes - GameManager.Instance.State.Lumes;
            Log.Info($"qa-preço: {kind} › {path[i]} (invasão={invasion}): mostrou {shown}, cobrou {charged}");
            if (charged != shown)
            {
                return $"mostrou {shown} Lumes, cobrou {charged}";
            }
        }

        await DismissUi();
        return null;
    }

    /// <summary>Textos visíveis da UI (HUD e telas) que saem da tela base 640×360.</summary>
    private static List<string> QaOffscreenLabels(string screen)
    {
        var problems = new List<string>();
        var bounds = new Rect2(-0.5f, -0.5f, 641f, 361f);
        void Visit(Node node)
        {
            if (node is CanvasItem { Visible: false })
            {
                return;
            }

            if (node is Label { Text.Length: > 0 } label && label.IsVisibleInTree() && label.Modulate.A > 0.05f)
            {
                var rect = label.GetGlobalRect();
                if (rect.Size.X > 0 && !bounds.Encloses(rect))
                {
                    problems.Add($"{screen}: '{Trim(label.Text)}' em {rect}");
                }
            }

            foreach (var child in node.GetChildren())
            {
                Visit(child);
            }
        }

        if (GameUi.Current is { } ui)
        {
            Visit(ui);
        }

        if (Hud.Current is { } hud)
        {
            Visit(hud);
        }

        return problems;

        static string Trim(string text) => text.Length > 40 ? text[..40] + "…" : text;
    }

    /// <summary>Anda (ações analógicas, como o stick) do ponto atual até perto do alvo. Devolve o erro ou null.</summary>
    private async Task<string?> QaWalkNear(World.World world, MapPoint target, float radius, Func<bool> done)
    {
        var p = FindPlayer()!.GlobalPosition;
        var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(p.X, p.Y), target, radius);
        if (path is null)
        {
            return $"sem caminho de ({p.X:0},{p.Y:0}) até ({target.X:0},{target.Y:0})";
        }

        using var speed = new SpeedUp(TesterSpeedUp);
        return await TesterWalkPath(world, path, done, (path.Count * 4f / 78f * 1.6f) + 3f);
    }

    /// <summary>Anda com WASD de verdade (digital: só 8 direções), como no teclado. Devolve o erro ou null.</summary>
    private async Task<string?> QaWalkNearWithKeys(World.World world, MapPoint target, float radius, Func<bool> done)
    {
        var player = FindPlayer()!;
        var p0 = player.GlobalPosition;
        var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(p0.X, p0.Y), target, radius);
        if (path is null)
        {
            return $"sem caminho de ({p0.X:0},{p0.Y:0}) até ({target.X:0},{target.Y:0})";
        }

        var held = new HashSet<Key>();
        async Task HoldKey(Key key, bool on)
        {
            if (on == held.Contains(key))
            {
                return;
            }

            if (on)
            {
                held.Add(key);
            }
            else
            {
                held.Remove(key);
            }

            await QaKey(key, on, waitFrame: false);
        }

        var next = 1;
        var stuckFrames = 0;
        var previous = player.GlobalPosition;
        var maxFrames = (int)(((path.Count * 4f / 78f * 2f) + 4f) * Engine.PhysicsTicksPerSecond);
        try
        {
            for (var frame = 0; !done(); frame++)
            {
                var p = player.GlobalPosition;
                if (frame > maxFrames)
                {
                    return $"timeout em ({p.X:0},{p.Y:0})";
                }

                stuckFrames = p.DistanceTo(previous) < 0.05f ? stuckFrames + 1 : 0;
                if (stuckFrames > 45)
                {
                    var aimAt = path[Math.Min(next, path.Count - 1)];
                    return $"travado com WASD em ({p.X:0},{p.Y:0}) indo para ({aimAt.X:0},{aimAt.Y:0})";
                }

                previous = p;
                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                var aim = next < path.Count ? new Vector2(path[next].X, path[next].Y) : new Vector2(target.X, target.Y);
                var d = aim - p;
                await HoldKey(Key.D, d.X > 1.5f);
                await HoldKey(Key.A, d.X < -1.5f);
                await HoldKey(Key.S, d.Y > 1.5f);
                await HoldKey(Key.W, d.Y < -1.5f);
                await NextPhysicsFrame();
            }
        }
        finally
        {
            foreach (var key in held.ToArray())
            {
                await QaKey(key, false, waitFrame: false);
            }
        }

        return null;
    }

    /// <summary>Tenta andar nas 4 direções; true se alguma move o jogador mais de 1 u.</summary>
    private async Task<bool> QaCanStillWalk(Player.Player player)
    {
        foreach (var d in new[] { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up })
        {
            var before = player.GlobalPosition;
            PressDirection(d);
            await Frames(12);
            PressDirection(Vector2.Zero);
            await NextPhysicsFrame();
            if (player.GlobalPosition.DistanceTo(before) > 1f)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> QaWaitFor(Func<bool> condition, double timeoutSeconds)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (!condition())
        {
            if (Time.GetTicksMsec() >= deadline)
            {
                return false;
            }

            await NextPhysicsFrame();
        }

        return true;
    }

    private async Task QaSkipTypingWithKey(GameUi ui, Key key)
    {
        await WaitSeconds(0.22);
        if (ui.Dialog.TypingRemaining > 12)
        {
            await QaKeyTap(key);
        }

        await WaitUntil(() => !ui.Dialog.IsTyping, 3, "texto do diálogo completo");
        await WaitSeconds(0.17);
    }

    private async Task QaSkipTypingWithPad(GameUi ui)
    {
        await WaitSeconds(0.22);
        if (ui.Dialog.TypingRemaining > 12)
        {
            await QaPadTap(JoyButton.A);
        }

        await WaitUntil(() => !ui.Dialog.IsTyping, 3, "texto do diálogo completo");
        await WaitSeconds(0.17);
    }

    /// <summary>Tecla física real (keycode + physical keycode), como o SO entrega.</summary>
    private async Task QaKey(Key key, bool pressed, bool waitFrame = true)
    {
        var unicode = key is >= Key.A and <= Key.Z ? (long)char.ToLowerInvariant((char)key) : 0;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed, Unicode = pressed ? unicode : 0 });
        if (waitFrame)
        {
            await NextPhysicsFrame();
        }
    }

    private async Task QaKeyTap(Key key)
    {
        await QaKey(key, true);
        await QaKey(key, false);
    }

    private async Task QaPadTap(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = true });
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = false });
        await NextPhysicsFrame();
    }
}
