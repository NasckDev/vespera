using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.UI.HudKit;
using Vespera.UI.Screens;
using Vespera.UI.Widgets;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// QA de experiência da frente C1 (rodada 1): um amigo que nunca viu o jogo, só no teclado e só no controle, com input
/// de evento de verdade (<c>Input.ParseInputEvent</c>, o mesmo caminho do teclado e do controle), HUD sem sobreposição
/// em todas as escalas, remapeamento que vale no jogo e nas telas de ajuda, persistência das opções e estresse
/// (opções 100×, trocar teclado↔controle em plena luta). Prints com <c>--smoke-shots</c> (sem --headless).
/// </summary>
public partial class SmokeTest
{
    // --- entrada "de verdade" (evento, não ActionPress): passa pelo Input Map e pela GUI como o teclado e o controle ---

    private static void PqKeyDown(Key key) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });

    private static void PqKeyUp(Key key) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });

    /// <summary>Aperta por tempo de relógio (não por quadros de física): com a máquina cheia o quadro de desenho demora e um toque de 3 quadros de física passa batido.</summary>
    private async Task PqKeyTap(Key key, int frames = 3)
    {
        PqKeyDown(key);
        await WaitSeconds(0.2);
        PqKeyUp(key);
        await Frames(2);
    }

    private static void PqPadDown(JoyButton button) =>
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });

    private static void PqPadUp(JoyButton button) =>
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });

    private async Task PqPadTap(JoyButton button, int frames = 3)
    {
        PqPadDown(button);
        await WaitSeconds(0.2);
        PqPadUp(button);
        await Frames(2);
    }

    private static void PqStick(float x, float y)
    {
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = x });
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = y });
    }

    /// <summary>Um evento de teclado solto (tecla sem uso) avisa a UI que o jogador voltou ao teclado.</summary>
    private async Task PqUseKeyboard()
    {
        PqKeyUp(Key.Shift);
        await Frames(2);
        Require(!InputHints.UsingGamepad, "deveria estar no teclado");
    }

    private async Task PqUseGamepad()
    {
        PqPadUp(JoyButton.LeftShoulder);
        await Frames(2);
        Require(InputHints.UsingGamepad, "deveria estar no controle");
    }

    /// <summary>Recarrega a cena do mundo: HUD, dicas e UI novos, como num jogo recém-aberto.</summary>
    private async Task PqFreshWorld()
    {
        await WaitForScene("World", 10);
        GameManager.Instance.ChangeScene(Vespera.UI.Title.TitleScreen.WorldScenePath);
        await WaitSeconds(0.3);
        await WaitForScene("World", 10);
        await WaitUntil(() => Hud.Current is not null && GameUi.Current is not null, 5, "HUD e UI do mundo novo");
    }

    /// <summary>Solta tudo (o evento de soltar tem de chegar com o mapa de teclas ainda igual) e volta às teclas padrão.</summary>
    private async Task PqRestoreKeys()
    {
        await Frames(3);
        ReleaseAllActions();
        await Frames(2);
        InputBindings.RestoreDefaults();
    }

    private static void PqResetPlayerSettings()
    {
        GameSettings.ResetAccessibility();
        InputBindings.RestoreDefaults();
        OnboardingHints.ResetProgress();
    }

    /// <summary>Anda para uma direção com o dispositivo ativo (WASD ou analógico) por alguns segundos.</summary>
    private async Task PqWalk(bool pad, Vector2 dir, double seconds)
    {
        if (pad)
        {
            PqStick(dir.X, dir.Y);
        }
        else
        {
            if (dir.X > 0) { PqKeyDown(Key.D); } else if (dir.X < 0) { PqKeyDown(Key.A); }
            if (dir.Y > 0) { PqKeyDown(Key.S); } else if (dir.Y < 0) { PqKeyDown(Key.W); }
        }

        await WaitSeconds(seconds);
        if (pad)
        {
            PqStick(0, 0);
        }
        else
        {
            PqKeyUp(Key.D); PqKeyUp(Key.A); PqKeyUp(Key.S); PqKeyUp(Key.W);
        }

        await Frames(2);
    }

    /// <summary>Joga os primeiros minutos como alguém que nunca viu o jogo, num dispositivo só.</summary>
    private async Task PqFriendJourney(bool pad)
    {
        var tag = pad ? "controle" : "teclado";
        await PqFreshWorld(); // jogo aberto agora: HUD e dicas nascem do zero (o HUD guarda estado de dicas entre cenários)
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        var state = GameManager.Instance.State;
        var combat = CombatDirector.Instance ?? throw new SmokeFailure("sem CombatDirector");
        PqResetPlayerSettings();
        var auto = GameUi.AutoCompleteQuests;
        GameUi.AutoCompleteQuests = true;
        try
        {
            if (pad) { await PqUseGamepad(); } else { await PqUseKeyboard(); }

            // 1. Tutorial da Odete: nenhuma dica de controle por cima do diálogo; avança com A/Enter de verdade.
            ui.StartTutorial();
            await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Odete", 4, "tutorial abrir");
            await WaitSeconds(0.5);
            Require(!hud.Onboarding.Visible, "dica de controle apareceu por cima do diálogo do tutorial");
            await Shot($"qa-amigo-{tag}-01-tutorial");
            for (var line = 0; line < 8 && ui.Dialog.IsOpen && state.Save.Quests.Counter(QuestCounters.Odete) == 0; line++)
            {
                await WaitSeconds(0.25);
                var guard = 0;
                while (ui.Dialog.IsTyping && guard++ < 60)
                {
                    await WaitSeconds(0.1); // lê a fala inteira, sem pressa (velocidade normal)
                }

                if (pad) { await PqPadTap(JoyButton.A); } else { await PqKeyTap(Key.Enter); }
            }

            Require(state.Save.Quests.Counter(QuestCounters.Odete) == 1, $"{tag}: o tutorial inteiro não contou como falar com a Odete");
            await WaitUntil(() => state.CurrentQuest?.Id != "primeiros_passos", 3, "missão 'Primeiros passos' cumprida");
            await WaitUntil(() => ui.Dialog.IsOpen, 3, "fala de fim de missão");
            await WaitSeconds(0.6);
            await Shot($"qa-amigo-{tag}-02-missao-cumprida");
            for (var i = 0; i < 4 && ui.Dialog.IsOpen; i++)
            {
                await SkipTyping(ui);
                if (pad) { await PqPadTap(JoyButton.A); } else { await PqKeyTap(Key.Enter); }
                await WaitSeconds(0.3);
            }

            Require(!ui.Dialog.IsOpen, "fala de fim de missão não fechou");

            // 2. Andar: a dica mostra a tecla certa e some depois de andar de verdade.
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Move, 3, "dica de andar");
            await Frames(2);
            Require(hud.Onboarding.KeyText == (pad ? UiText.HintStick : "WASD"), $"{tag}: tecla da dica de andar '{hud.Onboarding.KeyText}'");
            await Shot($"qa-amigo-{tag}-03-dica-andar");
            var from = FindPlayer()!.GlobalPosition;
            await PqWalk(pad, Vector2.Right, 1.2);
            var walked = FindPlayer()!.GlobalPosition.DistanceTo(from);
            Require(walked > 8f, $"{tag}: andou só {walked:0.0} u em 1,2 s de input");
            Require(OnboardingHints.IsDone(OnboardingStep.Move), "andar não concluiu a dica");

            // 3. Atacar nos bonecos (a missão seguinte é "Bonecos de treino": a dica de atacar vem antes da de interagir): a dica aparece com a tecla certa, o golpe acerta e a dica some.
            var player = FindPlayer()!;
            var dummy = combat.Enemies.FirstOrDefault(e => e.Body.IsDummy) ?? throw new SmokeFailure("sem boneco na Guilda");
            player.Teleport(world.CorrectToWalkable(new Vector2((float)dummy.Body.X - 18, (float)dummy.Body.Y)));
            await Frames(4);
            SetAim(Vector2.Right);
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Attack, 4, "dica de atacar");
            Require(hud.Onboarding.KeyText == (pad ? "X" : "J"), $"{tag}: tecla de atacar '{hud.Onboarding.KeyText}'");
            await Shot($"qa-amigo-{tag}-06-dica-atacar");
            var hpBefore = dummy.Body.Hp;
            var hitstopFrames = 0;
            var flash = false;
            if (pad) { PqPadDown(JoyButton.X); } else { PqKeyDown(Key.J); }
            for (var f = 0; f < 80; f++)
            {
                await NextPhysicsFrame();
                hitstopFrames += combat.Clock.Hitstop > 0 ? 1 : 0;
                flash |= dummy.Body.Flash > 0;
                if (f == 4) { await Shot($"qa-amigo-{tag}-07-golpe"); }
            }

            if (pad) { PqPadUp(JoyButton.X); } else { PqKeyUp(Key.J); }
            SetAim(Vector2.Zero);
            Require(flash, $"{tag}: golpe não fez o boneco piscar");
            Require(hitstopFrames >= 3, $"{tag}: golpe sem hitstop ({hitstopFrames} quadros)");
            Require(OnboardingHints.IsDone(OnboardingStep.Attack), "atacar não concluiu a dica");
            Log.Info($"qa-amigo({tag}): 1,3 s de golpes: hitstop {hitstopFrames} quadros, boneco hp {hpBefore:0}→{dummy.Body.Hp:0}");

            // 4. Interagir: chega no balcão, a dica pede a tecla de interagir e o jogador conversa de verdade.
            var odete = world.GetInteractable("odete") ?? throw new SmokeFailure("sem a Odete");
            player.Teleport(odete.Position + new Vector2(0, 20));
            await WaitSeconds(0.8);
            Log.Info($"qa-amigo({tag}): perto da Odete em {player.GlobalPosition}, foco={player.FocusedInteractable?.Id}, prompt='{hud.PromptLabel.Text}', dica={hud.Onboarding.Current}, bloqueado={GameManager.Instance.IsGameplayBlocked}");
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Interact, 4, "dica de interagir");
            Require(hud.Onboarding.KeyText == (pad ? "A" : "E"), $"{tag}: tecla de interagir '{hud.Onboarding.KeyText}'");
            Require(hud.PromptVisible || hud.PromptLabel.Text.Length > 0, "prompt de interação visível");
            await Shot($"qa-amigo-{tag}-04-dica-interagir");
            if (pad) { await PqPadTap(JoyButton.A); } else { await PqKeyTap(Key.E); }
            await WaitUntil(() => ui.Dialog.IsOpen, 3, "conversa abrir ao interagir");
            Require(OnboardingHints.IsDone(OnboardingStep.Interact), "interagir não concluiu a dica");
            await WaitSeconds(0.5);
            await Shot($"qa-amigo-{tag}-05-conversa");
            ui.CloseAll();
            await WaitSeconds(0.4);

            // 5. Esquivar: leva um golpe (de verdade) → dica; esquiva de verdade → invulnerável e dica some.
            combat.HurtPlayer(6, new Vector2(player.GlobalPosition.X + 30, player.GlobalPosition.Y));
            await WaitSeconds(0.7);
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Dodge, 3, "dica de esquivar");
            Require(hud.Onboarding.KeyText == (pad ? "B" : "Espaço"), $"{tag}: tecla de esquivar '{hud.Onboarding.KeyText}'");
            await Shot($"qa-amigo-{tag}-08-dica-esquivar");
            if (pad) { await PqPadTap(JoyButton.B, 2); } else { await PqKeyTap(Key.Space, 2); }
            await Frames(2);
            Require(combat.PlayerDashing || OnboardingHints.IsDone(OnboardingStep.Dodge), "esquiva não aconteceu");
            Require(OnboardingHints.IsDone(OnboardingStep.Dodge), "esquivar não concluiu a dica");
            await WaitSeconds(0.6);

            // 6. Poção: vida abaixo de 50% → dica; beber de verdade cura.
            state.Damage(Math.Max(1, (int)(state.Hp - (state.MaxHp * 0.4))));
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Potion, 3, "dica de poção");
            Require(hud.Onboarding.KeyText == (pad ? "LB" : "R"), $"{tag}: tecla da poção '{hud.Onboarding.KeyText}'");
            await Shot($"qa-amigo-{tag}-09-dica-pocao");
            var hp0 = state.Hp;
            var potions0 = state.Save.Potions;
            if (pad) { await PqPadTap(JoyButton.LeftShoulder, 2); } else { await PqKeyTap(Key.R, 2); }
            await WaitSeconds(0.4);
            Require(state.Hp > hp0 && state.Save.Potions == potions0 - 1, $"{tag}: poção não curou (hp {hp0}→{state.Hp}, poções {potions0}→{state.Save.Potions})");
            Require(OnboardingHints.IsDone(OnboardingStep.Potion), "beber não concluiu a dica");
            await Shot($"qa-amigo-{tag}-10-depois");
        }
        finally
        {
            PqStick(0, 0);
            ReleaseAllActions();
            SetAim(Vector2.Zero);
            GameUi.AutoCompleteQuests = auto;
            PqResetPlayerSettings();
            ui.CloseAll();
        }
    }

    [SmokeScenario("polimento (qa): amigo novo só no teclado percorre tutorial, dicas, andar, interagir, atacar, esquivar e poção", order: 1000)]
    private async Task PqFriendKeyboard() => await PqFriendJourney(pad: false);

    [SmokeScenario("polimento (qa): amigo novo só no controle percorre tutorial, dicas, andar, interagir, atacar, esquivar e poção", order: 1001)]
    private async Task PqFriendGamepad() => await PqFriendJourney(pad: true);

    // --- HUD: nada por cima de nada, em nenhuma escala ---

    /// <summary>Retângulos (coordenadas da tela) de tudo que o HUD desenha agora: zonas publicadas + toast, dica, legendas, letreiro, prompt.</summary>
    private static List<(string Name, Rect2 Rect)> PqHudBlocks(Hud hud)
    {
        var list = new List<(string, Rect2)>();
        for (var i = 0; i < HudZones.Count; i++)
        {
            var r = HudZones.Get(i).GrowIndividual(-2, -2, -2, -2);
            list.Add(($"zona{i}", r));
        }

        void AddVisible(string name, Control c)
        {
            if (c.IsVisibleInTree())
            {
                list.Add((name, c.GetGlobalRect()));
            }
        }

        foreach (var p in hud.Toasts.FindChildren("*", nameof(PanelContainer), true, false).OfType<PanelContainer>().Where(p => p.IsVisibleInTree()).Take(1))
        {
            list.Add(("toast", p.GetGlobalRect()));
        }

        AddVisible("dica", hud.Onboarding);
        foreach (var p in hud.Captions.GetChildren().OfType<PanelContainer>().Where(p => p.IsVisibleInTree()))
        {
            list.Add(("legenda", p.GetGlobalRect()));
        }

        if (hud.BossAlert.IsVisibleInTree())
        {
            foreach (var l in hud.BossAlert.GetChildren().OfType<Label>().Where(l => l.Text.Length > 0 && l.IsVisibleInTree()))
            {
                list.Add(("letreiro", PqTextRect(l)));
            }

            foreach (var band in hud.BossAlert.GetChildren().OfType<ColorRect>().Where(c => c.IsVisibleInTree()))
            {
                list.Add(("faixa-letreiro", band.GetGlobalRect()));
            }
        }

        return list;
    }

    private static string PqOverlaps(List<(string Name, Rect2 Rect)> blocks, Rect2 view)
    {
        var problems = new List<string>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (!view.Grow(1f).Encloses(blocks[i].Rect))
            {
                problems.Add($"{blocks[i].Name} {blocks[i].Rect} fora da tela {view}");
            }

            for (var j = i + 1; j < blocks.Count; j++)
            {
                var cut = blocks[i].Rect.Intersection(blocks[j].Rect);
                if (cut.Size.X > 1f && cut.Size.Y > 1f)
                {
                    problems.Add($"{blocks[i].Name} {blocks[i].Rect} x {blocks[j].Name} {blocks[j].Rect} (interseção {cut.Size.X:0}x{cut.Size.Y:0})");
                }
            }
        }

        return string.Join("\n  ", problems);
    }

    /// <summary>Rótulo com o retângulo só do texto (o rótulo do letreiro ocupa a largura toda, mas o texto é centrado).</summary>
    private static Rect2 PqTextRect(Label l)
    {
        var font = l.GetThemeFont("font");
        var size = l.GetThemeFontSize("font_size");
        var w = font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, size).X;
        var r = l.GetGlobalRect();
        var scale = l.GetGlobalTransform().Scale.X;
        w *= scale;
        return new Rect2(r.Position.X + ((r.Size.X - w) / 2f), r.Position.Y, w, r.Size.Y);
    }

    private async Task PqHudScaleCase(string tag, Func<Hud, Task> setup, Func<Task> teardown)
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current!;
        PqResetPlayerSettings();
        var bad = new List<string>();
        try
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            OnboardingHints.ResetProgress();
            foreach (var scale in GameSettings.UiScales)
            {
                GameSettings.UiScale = scale;
                await setup(hud);
                await WaitSeconds(0.45);
                var view = hud.GetViewport().GetVisibleRect();
                var blocks = PqHudBlocks(hud);
                var issues = PqOverlaps(blocks, view);
                Log.Info($"qa-hud: {tag} {scale * 100:0}%: {blocks.Count} blocos, problemas: {(issues.Length == 0 ? "nenhum" : issues)}");
                await Shot($"qa-hud-{tag}-{scale * 100:0}");
                if (issues.Length > 0)
                {
                    bad.Add($"{tag} {scale * 100:0}%:" + "\n  " + issues);
                }

                await teardown();
            }
        }
        finally
        {
            await teardown();
            ui.Brechas.Clear();
            PqResetPlayerSettings();
            hud.Toasts.Clear();
        }

        Require(bad.Count == 0, "HUD com blocos sobrepostos ou fora da tela:\n" + string.Join("\n", bad));
    }

    [SmokeScenario("polimento (qa): HUD na luta contra o chefe não cobre o painel do jogador em 100/125/150%", order: 1002)]
    private async Task PqHudBossNoOverlap() => await PqHudScaleCase("chefe",
        async hud =>
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.WaveStarted, 2, 3);
            EventBus.Instance.EmitSignal(EventBus.SignalName.BossSpawned, "O Abissal do Mercado", 500);
            await Task.CompletedTask;
        },
        async () =>
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.BossHealthChanged, 0, 500);
            await WaitSeconds(2.3);
        });

    [SmokeScenario("polimento (qa): HUD da cidade com 2 Brechas e loot não empilha blocos em 100/125/150%", order: 1003)]
    private async Task PqHudCityNoOverlap() => await PqHudScaleCase("cidade",
        async hud =>
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 1, "crypt", 1);
            for (var i = 0; i < 4; i++)
            {
                hud.Note($"Estilhaço de Brecha {i}", "i_coin");
            }

            hud.ExpandQuest();
            await Task.CompletedTask;
        },
        async () =>
        {
            UiOrFail().Brechas.Clear();
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalClosed, 0);
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalClosed, 1);
            await WaitSeconds(0.2);
        });

    [SmokeScenario("polimento (qa): legendas ligadas não cobrem a dica de controle nem o nome da arma", order: 1004)]
    private async Task PqCaptionsVsHint()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var hud = Hud.Current!;
        PqResetPlayerSettings();
        try
        {
            GameSettings.Captions = true;
            OnboardingHints.ResetProgress();
            await WaitSeconds(0.5);
            hud.Caption(UiText.CaptionPortal);
            hud.Caption(UiText.CaptionBoss);
            hud.Caption("[Perigo ←]");
            await WaitSeconds(0.45);
            var blocks = PqHudBlocks(hud).Where(b => b.Name is "dica" or "legenda").ToList();
            Log.Info($"qa-hud: legendas+dica: {string.Join("; ", blocks.Select(b => $"{b.Name} {b.Rect}"))}");
            await Shot("qa-hud-legendas-dica");
            Require(blocks.Any(b => b.Name == "dica") && blocks.Count(b => b.Name == "legenda") == 3, "dica e 3 legendas visíveis");
            var issues = PqOverlaps(blocks, hud.GetViewport().GetVisibleRect());
            Require(issues.Length == 0, "legendas por cima da dica de controle:\n  " + issues);
        }
        finally
        {
            PqResetPlayerSettings();
            await WaitSeconds(2.8);
        }
    }

    // --- o jogador nunca fica escondido atrás do HUD ---

    /// <summary>Retângulo do corpo do jogador na tela (pé em <c>GlobalPosition</c>, ~32 u de largura por 44 de altura).</summary>
    private Rect2 PqPlayerScreenRect(Player.Player player)
    {
        var foot = player.GetViewport().GetCanvasTransform() * player.GlobalPosition;
        return new Rect2(foot.X - 10f, foot.Y - 40f, 20f, 40f);
    }

    /// <summary>Zona do HUD (publicada) que cobre mais de 30% do corpo do jogador, ou null.</summary>
    private static string? PqHudCovers(Rect2 body)
    {
        for (var i = 0; i < HudZones.Count; i++)
        {
            var cut = HudZones.Get(i).Intersection(body);
            if (cut.Size.X > 0 && cut.Size.Y > 0 && cut.Size.X * cut.Size.Y > body.Size.X * body.Size.Y * 0.3f)
            {
                return $"zona{i} {HudZones.Get(i)}";
            }
        }

        return null;
    }

    [SmokeScenario("polimento (qa): o jogador não nasce escondido atrás do HUD em nenhuma sala de Brecha nem no Núcleo", order: 1005)]
    private async Task PqPlayerNotUnderHudAtArrival()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var hud = Hud.Current!;
        var player = FindPlayer()!;
        var covered = new List<string>();
        try
        {
            foreach (var (theme, _) in GameManager.Db.Themes)
            {
                await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
                await WaitSeconds(0.5);
                var body = PqPlayerScreenRect(player);
                var by = PqHudCovers(body);
                Log.Info($"qa-hud: {world.MapId} chegada em ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0}) → tela {body}, coberto por: {by ?? "nada"}");
                await Shot($"qa-chegada-{world.MapId}");
                if (by is not null)
                {
                    covered.Add($"{world.MapId} (chegada): {by}");
                }

                // Núcleo: entra pela porta como o jogo faz.
                var core = World.World.Catalog[World.World.CoreMapId];
                var at = core.Arrivals.TryGetValue("daPortaDaBrecha", out var arrival) ? new Vector2(arrival.X, arrival.Y) : new Vector2(core.Spawn.X, core.Spawn.Y);
                world.GoMap(World.World.CoreMapId, at.X, at.Y, instant: true);
                await WaitMapSettled(world);
                await WaitSeconds(0.5);
                body = PqPlayerScreenRect(player);
                by = PqHudCovers(body);
                Log.Info($"qa-hud: core (via {theme}) chegada em ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0}) → tela {body}, coberto por: {by ?? "nada"}");
                await Shot($"qa-chegada-core-{theme}");
                if (by is not null)
                {
                    covered.Add($"core (chegada): {by}");
                }

                await FleeRun(world, combat);
            }
        }
        finally
        {
            await FleeRun(world, combat);
        }

        Require(covered.Count == 0, "jogador nasce escondido atrás do HUD:\n  " + string.Join("\n  ", covered.Distinct()));
    }

    [SmokeScenario("polimento (qa): quanto da área andável das salas de Brecha deixa o jogador escondido atrás do HUD", order: 1006)]
    private async Task PqHiddenAreaSweep()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var player = FindPlayer()!;
        var worst = 0.0;
        var report = new List<string>();
        try
        {
            var maps = GameManager.Db.Themes.Select(t => t.Value.Map).Distinct().Append(World.World.CoreMapId).ToList();
            foreach (var mapId in maps)
            {
                if (mapId == World.World.CoreMapId)
                {
                    EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
                    EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStartRequested, 0);
                    await WaitUntil(() => combat.Brecha.Run is not null, 5, "incursão");
                }
                else if (combat.Brecha.Run is null)
                {
                    var theme = GameManager.Db.Themes.First(t => t.Value.Map == mapId).Key;
                    await EnterBrecha(world, combat, 0, theme, 0);
                }

                var def = World.World.Catalog[mapId];
                world.GoMap(mapId, def.Spawn.X, def.Spawn.Y, instant: true);
                await WaitMapSettled(world);
                await WaitSeconds(0.4);
                var total = 0;
                var hidden = 0;
                var bottomRows = 0;
                var bottomHidden = 0;
                var cam = world.Camera;
                var view = cam.VisibleSize;
                var map = world.Map;
                for (var y = 16; y < map.ImageHeight; y += 12)
                {
                    for (var x = 16; x < map.ImageWidth; x += 12)
                    {
                        if (!world.WalkArea.CanStand(x, y))
                        {
                            continue;
                        }

                        player.Teleport(new Vector2(x, y));
                        cam.Snap();
                        var topLeft = cam.GetScreenCenterPosition() - (view / 2f);
                        var screen = new Vector2(x, y) - topLeft;
                        var body = new Rect2(screen.X - 10f, screen.Y - 40f, 20f, 40f);
                        total++;
                        var covered = PqHudCovers(body) is not null;
                        hidden += covered ? 1 : 0;
                        if (y > map.ImageHeight - 120)
                        {
                            bottomRows++;
                            bottomHidden += covered ? 1 : 0;
                        }
                    }
                }

                var pct = total == 0 ? 0 : 100.0 * hidden / total;
                worst = Math.Max(worst, pct);
                report.Add($"{mapId}: {hidden}/{total} pontos andáveis escondem o jogador atrás do HUD ({pct:0.0}%); faixa de baixo (120 u): {bottomHidden}/{bottomRows}");
            }
        }
        finally
        {
            await FleeRun(world, combat);
        }

        foreach (var line in report)
        {
            Log.Info($"qa-hud: {line}");
        }

        Require(worst < 1.0, "área andável escondida atrás do HUD:" + (char)10 + "  " + string.Join((char)10 + "  ", report));
    }

    // --- remapeamento: vale no jogo, não duplica tecla e as telas de ajuda acompanham ---

    /// <summary>Ações do jogo (fora das ui_*) que respondem ao mesmo evento que <paramref name="e"/>, menos <paramref name="except"/>.</summary>
    private static List<string> PqActionsUsing(InputEvent e, StringName except)
    {
        var list = new List<string>();
        foreach (var action in InputMap.GetActions())
        {
            if (action == except || action.ToString().StartsWith("ui_", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var other in InputMap.ActionGetEvents(action))
            {
                if (other.IsMatch(e, exactMatch: false) || e.IsMatch(other, exactMatch: false))
                {
                    list.Add(action.ToString());
                    break;
                }
            }
        }

        return list;
    }

    [SmokeScenario("polimento (qa): remapear para uma tecla fixa do jogo (1–5, F2, RB) não pode disparar duas ações", order: 1010)]
    private async Task PqRemapFixedKeyConflict()
    {
        await FreshGame("sword");
        InputBindings.RestoreDefaults();
        var problems = new List<string>();
        try
        {
            // Teclado: teclas de arma 1–5, F2 (cheat de XP do debug) e botões do mouse da troca de arma.
            foreach (var key in new[] { Key.Key1, Key.Key3, Key.F2 })
            {
                var result = InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = key }, out _);
                var sharing = PqActionsUsing(new InputEventKey { PhysicalKeycode = key }, InputActions.Attack);
                Log.Info($"qa-remap: Atacar ← {key}: {result}, outras ações na mesma tecla: [{string.Join(", ", sharing)}]");
                if (result is RemapResult.Ok or RemapResult.Swapped && sharing.Count > 0)
                {
                    problems.Add($"Atacar aceitou {key} que já é de [{string.Join(", ", sharing)}] (resultado {result})");
                }

                InputBindings.RestoreDefaults();
            }

            // Controle: RB (troca de arma) para Poção.
            var pad = InputBindings.Assign(InputActions.Potion, pad: true, new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder }, out _);
            var padSharing = PqActionsUsing(new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder }, InputActions.Potion);
            Log.Info($"qa-remap: Poção ← RB: {pad}, outras ações no mesmo botão: [{string.Join(", ", padSharing)}]");
            if (pad is RemapResult.Ok or RemapResult.Swapped && padSharing.Count > 0)
            {
                problems.Add($"Poção aceitou RB que já é de [{string.Join(", ", padSharing)}] (resultado {pad})");
            }
        }
        finally
        {
            InputBindings.RestoreDefaults();
        }

        await Task.CompletedTask;
        Require(problems.Count == 0, "conflito não detectado com ações que não aparecem na tela de teclas:" + (char)10 + "  " + string.Join((char)10 + "  ", problems));
    }

    [SmokeScenario("polimento (qa): teclas remapeadas valem de verdade no jogo (andar, atacar, interagir, poção) e as antigas deixam de valer", order: 1011)]
    private async Task PqRemapWorksInGame()
    {
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current!;
        var combat = CombatDirector.Instance!;
        var state = GameManager.Instance.State;
        InputBindings.RestoreDefaults();
        OnboardingHints.ResetProgress();
        try
        {
            await PqUseKeyboard();
            // Andar para a direita com L (e não mais com D).
            Require(InputBindings.Assign(InputActions.MoveRight, pad: false, new InputEventKey { PhysicalKeycode = Key.L }, out _) == RemapResult.Ok, "Mover para a direita ← L");
            var player = FindPlayer()!;
            player.Teleport(world.CorrectToWalkable(new Vector2(250, 330)));
            await Frames(3);
            var x0 = player.GlobalPosition.X;
            PqKeyDown(Key.D);
            await WaitSeconds(0.6);
            PqKeyUp(Key.D);
            var viaOld = player.GlobalPosition.X - x0;
            x0 = player.GlobalPosition.X;
            PqKeyDown(Key.L);
            await WaitSeconds(0.6);
            PqKeyUp(Key.L);
            var viaNew = player.GlobalPosition.X - x0;
            Log.Info($"qa-remap: andou {viaOld:0.0} u com D (antiga) e {viaNew:0.0} u com L (nova); move_right ainda pressionada={Input.IsActionPressed(InputActions.MoveRight)}");
            Require(viaNew > 6f && viaOld < 1f, $"movimento remapeado: nova={viaNew:0.0} u, antiga={viaOld:0.0} u");
            await PqRestoreKeys();

            // Atacar com K (troca com o ataque forte): K bate, J agora é o forte.
            Require(InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.K }, out _) == RemapResult.Swapped, "Atacar ← K troca com o forte");
            PqKeyDown(Key.K);
            await Frames(2);
            Require(Input.IsActionPressed(InputActions.Attack) && !Input.IsActionPressed(InputActions.AttackHeavy), "K deveria ser só o ataque comum");
            PqKeyUp(Key.K);
            PqKeyDown(Key.J);
            await Frames(2);
            Require(Input.IsActionPressed(InputActions.AttackHeavy) && !Input.IsActionPressed(InputActions.Attack), "J deveria ser só o ataque forte");
            PqKeyUp(Key.J);
            await PqRestoreKeys();

            // Interagir com G: a conversa com a Odete abre e a tecla da dica/prompt é G.
            Require(InputBindings.Assign(InputActions.Interact, pad: false, new InputEventKey { PhysicalKeycode = Key.G }, out _) == RemapResult.Ok, "Interagir ← G");
            var odete = world.GetInteractable("odete") ?? throw new SmokeFailure("sem a Odete");
            await WaitSeconds(0.8); // deixa o golpe forte do passo anterior terminar
            Log.Info($"qa-remap: antes do teleporte: jogador {player.GlobalPosition}, move_right={Input.IsActionPressed(InputActions.MoveRight)}, ataque={Input.IsActionPressed(InputActions.Attack)}/{Input.IsActionPressed(InputActions.AttackHeavy)}");
            player.Teleport(odete.Position + new Vector2(0, 20));
            await WaitSeconds(0.6);
            await PqKeyTap(Key.E);
            await WaitSeconds(0.4);
            Require(!ui.Dialog.IsOpen, "E (antiga) ainda interage depois do remapeamento");
            PqKeyDown(Key.G);
            await Frames(3);
            Log.Info($"qa-remap: player {player.GlobalPosition} odete {odete.Position}/{odete.GlobalPosition}; com G apertado: Interact pressionada={Input.IsActionPressed(InputActions.Interact)}, foco={player.FocusedInteractable?.Id}, bloqueado={GameManager.Instance.IsGameplayBlocked}, diálogo={ui.Dialog.IsOpen}");
            await WaitSeconds(0.2);
            PqKeyUp(Key.G);
            await Frames(2);
            await WaitUntil(() => ui.Dialog.IsOpen, 3, "conversa com G");
            ui.CloseAll();
            await WaitSeconds(0.4);

            // Poção com T: o rótulo da hotbar acompanha.
            Require(InputBindings.Assign(InputActions.Potion, pad: false, new InputEventKey { PhysicalKeycode = Key.T }, out _) == RemapResult.Ok, "Poção ← T");
            await Frames(2);
            Log.Info($"qa-remap: rótulo da poção na hotbar: '{InputBindings.Label(InputActions.Potion, false)}'");
            state.Damage(Math.Max(1, state.Hp / 2));
            var hp0 = state.Hp;
            await PqKeyTap(Key.R);
            await WaitSeconds(0.3);
            Require(state.Hp == hp0, "R (antiga) ainda bebe poção");
            await PqKeyTap(Key.T);
            await WaitSeconds(0.3);
            Require(state.Hp > hp0, "T (nova) não bebeu a poção");
        }
        finally
        {
            ReleaseAllActions();
            InputBindings.RestoreDefaults();
            ui.CloseAll();
            state.RestoreFullHealth();
        }
    }

    [SmokeScenario("polimento (qa): a tela Controles e o tutorial seguem o remapeamento e o dispositivo", order: 1012)]
    private async Task PqHelpFollowsRemap()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var ui = UiOrFail();
        InputBindings.RestoreDefaults();
        var problems = new List<string>();
        try
        {
            // 1. Tela Controles depois de trocar Atacar (J→K, troca com o forte), Esquivar (Espaço→B) e Interagir (E→G).
            InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.K }, out _);
            InputBindings.Assign(InputActions.Dodge, pad: false, new InputEventKey { PhysicalKeycode = Key.B }, out _);
            InputBindings.Assign(InputActions.Interact, pad: false, new InputEventKey { PhysicalKeycode = Key.G }, out _);
            ui.OpenPause();
            await WaitSeconds(0.2);
            ui.Stack.Push(ui.Controls);
            await WaitSeconds(0.3);
            var text = string.Join(" | ", ui.Controls.FindChildren("*", nameof(Label), true, false).OfType<Label>().Select(l => l.Text));
            Log.Info($"qa-ajuda: tela Controles após remapear: {text}");
            await Shot("qa-ajuda-controles");
            if (!text.Contains("G", StringComparison.Ordinal) || text.Contains("| E |", StringComparison.Ordinal))
            {
                problems.Add("tela Controles continua mostrando 'E' para Interagir depois de remapear para G");
            }

            if (text.Contains("Espaço / Shift", StringComparison.Ordinal))
            {
                problems.Add("tela Controles continua mostrando 'Espaço / Shift' para Esquiva depois de remapear para B");
            }

            ui.CloseAll();
            InputBindings.RestoreDefaults();

            // 2. Tutorial para quem joga no controle: sem "tecla F" nem "menu Esc".
            await PqUseGamepad();
            var state = GameManager.Instance.State;
            ui.StartTutorial();
            await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Odete", 4, "tutorial");
            var last = string.Empty;
            for (var i = 0; i < 4 && ui.Dialog.IsOpen; i++)
            {
                last = ui.Dialog.Text;
                if (last.Contains("Supremo", StringComparison.Ordinal))
                {
                    break;
                }

                await SkipTyping(ui);
                await PqPadTap(JoyButton.A);
                await WaitSeconds(0.4);
            }

            await SkipTyping(ui);
            last = ui.Dialog.Text;
            Log.Info($"qa-ajuda: fala do tutorial no controle: {last}");
            await Shot("qa-ajuda-tutorial-controle");
            if (last.Contains("tecla F", StringComparison.Ordinal) || last.Contains("menu Esc", StringComparison.Ordinal))
            {
                problems.Add("o tutorial manda quem joga no controle apertar 'a tecla F' e abrir 'o menu Esc'");
            }
        }
        finally
        {
            ui.CloseAll();
            InputBindings.RestoreDefaults();
            await PqUseKeyboard();
        }

        Require(problems.Count == 0, "ajuda de controles desatualizada:" + (char)10 + "  " + string.Join((char)10 + "  ", problems));
    }

    // --- opções: persistem, aplicam na hora, reduzir flashes de verdade ---

    [SmokeScenario("polimento (qa): todas as opções de acessibilidade são gravadas no settings.cfg e voltam depois de reiniciar", order: 1013)]
    private async Task PqSettingsPersist()
    {
        await FreshGame("sword");
        PqResetPlayerSettings();
        try
        {
            GameSettings.UiScale = 1.5f;
            GameSettings.TextScale = 1.3f;
            GameSettings.DialogSpeed = DialogSpeed.Instant;
            GameSettings.ShakeIntensity = 0.4f;
            GameSettings.ReduceFlashes = true;
            GameSettings.HighContrast = true;
            GameSettings.Captions = true;
            GameSettings.BowToggle = true;
            GameSettings.AimAssist = true;
            InputBindings.Assign(InputActions.Dodge, pad: false, new InputEventKey { PhysicalKeycode = Key.B }, out _);
            InputBindings.Assign(InputActions.Potion, pad: true, new InputEventJoypadButton { ButtonIndex = JoyButton.Y }, out _);

            // O arquivo tem tudo (é o que o próximo boot lê).
            var cfg = new ConfigFile();
            Require(cfg.Load(AudioManager.Instance.SettingsFile) == Error.Ok, "settings.cfg não existe");
            var expected = new (string Section, string Key, Variant Value)[]
            {
                ("access", "ui_scale", 1.5f), ("access", "text_scale", 1.3f), ("access", "dialog_speed", (int)DialogSpeed.Instant),
                ("access", "shake_intensity", 0.4f), ("access", "reduce_flashes", true), ("access", "high_contrast", true),
                ("access", "captions", true), ("access", "bow_toggle", true), ("access", "aim_assist", true),
            };
            foreach (var (section, key, value) in expected)
            {
                Require(cfg.HasSectionKey(section, key), $"[{section}] {key} não foi gravada");
                var got = cfg.GetValue(section, key);
                var same = value.VariantType == Variant.Type.Float ? Mathf.IsEqualApprox((float)got, (float)value) : got.Equals(value);
                Require(same, $"[{section}] {key} gravada como {got}, esperado {value}");
            }

            Require(cfg.HasSectionKey("input_keys", InputActions.Dodge.ToString()) && cfg.HasSectionKey("input_pad", InputActions.Potion.ToString()), "teclas remapeadas não foram gravadas");

            // "Reinicia": zera a memória e relê o arquivo como o boot faz.
            InputMap.LoadFromProjectSettings();
            GameSettings.Load();
            InputBindings.Load();
            Require(Mathf.IsEqualApprox(GameSettings.UiScale, 1.5f) && Mathf.IsEqualApprox(GameSettings.TextScale, 1.3f), "escalas não voltaram");
            Require(GameSettings.DialogSpeed == DialogSpeed.Instant && Mathf.IsEqualApprox(GameSettings.ShakeIntensity, 0.4f), "velocidade do texto/tremor não voltaram");
            Require(GameSettings.ReduceFlashes && GameSettings.HighContrast && GameSettings.Captions && GameSettings.BowToggle && GameSettings.AimAssist, "alternâncias não voltaram");
            Require(InputBindings.Label(InputActions.Dodge, false) == "B" && InputBindings.Label(InputActions.Potion, true) == "Y", "teclas remapeadas não voltaram");

            // Um mundo novo já nasce com a escala e o contraste gravados (HUD em 150%).
            await PqFreshWorld();
            var hud = Hud.Current!;
            await WaitSeconds(0.4);
            Require(Mathf.IsEqualApprox(hud.HpBar.GetGlobalTransform().Scale.X, 1.5f), $"HUD novo não nasceu em 150% ({hud.HpBar.GetGlobalTransform().Scale.X})");
        }
        finally
        {
            PqResetPlayerSettings();
        }
    }

    [SmokeScenario("polimento (qa): 'reduzir flashes' também enfraquece o piscar branco do jogador ao levar dano", order: 1014)]
    private async Task PqReduceFlashesPlayerHurt()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var player = FindPlayer()!;
        var results = new List<(bool Reduce, float Peak)>();
        try
        {
            foreach (var reduce in new[] { false, true })
            {
                GameSettings.ReduceFlashes = reduce;
                combat.PlayerCombat.Revive();
                GameManager.Instance.State.RestoreFullHealth();
                await WaitSeconds(1.6); // a invulnerabilidade do reviver (0,9 s) acaba
                var materials = new List<ShaderMaterial>();
                foreach (var sprite in player.FindChildren("*", "CanvasItem", true, false).OfType<CanvasItem>())
                {
                    if (sprite.Material is ShaderMaterial m)
                    {
                        materials.Add(m);
                    }
                }

                var landed = combat.HurtPlayer(5, new Vector2(player.GlobalPosition.X + 20, player.GlobalPosition.Y));
                Log.Info($"qa-flash: dano entrou={landed}, vida {GameManager.Instance.State.Hp}, invulnerável={combat.PlayerCombat.State.Invulnerable:0.00}, materiais: {string.Join(", ", materials.Select(m => m.Shader?.ResourcePath ?? "?"))}");
                var peak = 0f;
                for (var f = 0; f < 14; f++)
                {
                    await NextPhysicsFrame();
                    foreach (var m in materials)
                    {
                        if (m.GetShaderParameter("flash").VariantType is Variant.Type.Float)
                        {
                            peak = Mathf.Max(peak, (float)m.GetShaderParameter("flash"));
                        }
                    }
                }

                results.Add((reduce, peak));
                Log.Info($"qa-flash: reduzir flashes={reduce}: pico do piscar do jogador {peak:0.00} ({materials.Count} materiais)");
                await WaitSeconds(0.8);
            }
        }
        finally
        {
            GameSettings.ResetAccessibility();
        }

        Require(results[0].Peak > 0.3f, $"o piscar de dano do jogador deveria existir com a opção desligada (pico {results[0].Peak:0.00})");
        Require(results[1].Peak <= results[0].Peak * 0.6f, $"reduzir flashes não enfraqueceu o piscar do jogador: {results[0].Peak:0.00} → {results[1].Peak:0.00}");
    }

    [SmokeScenario("polimento (qa): soltar a tecla remapeada solta a ação (nada fica preso apertado)", order: 1015)]
    private async Task PqRemapKeyReleaseIsClean()
    {
        await FreshGame("sword");
        InputBindings.RestoreDefaults();
        var stuck = new List<string>();
        try
        {
            async Task Probe(string label, StringName action, Key key)
            {
                PqKeyDown(key);
                await Frames(3);
                var down = Input.IsActionPressed(action);
                PqKeyUp(key);
                await Frames(3);
                var up = Input.IsActionPressed(action);
                Log.Info($"qa-remap: {label}: apertada={down}, depois de soltar={up}");
                if (!down)
                {
                    stuck.Add($"{label}: a tecla não acionou a ação");
                }

                if (up)
                {
                    stuck.Add($"{label}: a ação ficou presa depois de soltar a tecla");
                }
            }

            await Probe("padrão: Mover para a direita (D)", InputActions.MoveRight, Key.D);
            await Probe("padrão: Atacar (J)", InputActions.Attack, Key.J);
            InputBindings.Assign(InputActions.MoveRight, pad: false, new InputEventKey { PhysicalKeycode = Key.L }, out _);
            await Probe("remapeado: Mover para a direita (L)", InputActions.MoveRight, Key.L);
            InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.K }, out var other);
            await Probe("remapeado: Atacar (K)", InputActions.Attack, Key.K);
            await Probe("remapeado: Ataque forte (J, trocada)", InputActions.AttackHeavy, Key.J);
        }
        finally
        {
            ReleaseAllActions();
            InputBindings.RestoreDefaults();
        }

        Require(stuck.Count == 0, "tecla remapeada com problema ao soltar:" + (char)10 + "  " + string.Join((char)10 + "  ", stuck));
    }

    // --- dicas que não grudam, saída da Brecha só com intenção, tutorial pulado ---

    [SmokeScenario("polimento (qa): a dica de atacar some depois de 10 s e não volta no quadro seguinte", order: 1020)]
    private async Task PqAttackHintDoesNotNag()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current!;
        var state = GameManager.Instance.State;
        PqResetPlayerSettings();
        var auto = GameUi.AutoCompleteQuests;
        GameUi.AutoCompleteQuests = true;
        try
        {
            await PqUseKeyboard();
            // Missão 'Bonecos de treino' ativa (é onde o jogo novo cai depois do tutorial) e a dica de andar já cumprida.
            state.Save.Quests.Counter(QuestCounters.Odete); // garante o contador
            ui.StartTutorial();
            await WaitUntil(() => ui.Dialog.IsOpen, 4, "tutorial");
            for (var line = 0; line < 8 && ui.Dialog.IsOpen && state.Save.Quests.Counter(QuestCounters.Odete) == 0; line++)
            {
                await SkipTyping(ui);
                await PqKeyTap(Key.Enter);
                await WaitSeconds(0.2);
            }

            await WaitUntil(() => state.CurrentQuest?.Id == "bonecos", 4, "missão 'Bonecos de treino'");
            await DismissUi();
            await PqWalk(false, Vector2.Right, 1.2);
            Require(OnboardingHints.IsDone(OnboardingStep.Move), "dica de andar cumprida");
            // 28 s parado, sem atacar: quanto tempo seguido a dica de atacar fica na tela, e quanto some entre uma e outra.
            var visibleRun = 0.0;
            var longestRun = 0.0;
            var totalVisible = 0.0;
            var hiddenRun = 0.0;
            var longestHidden = 0.0;
            var started = Time.GetTicksMsec();
            var last = started;
            while ((Time.GetTicksMsec() - started) < 28000)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksMsec();
                var dt = (now - last) / 1000.0;
                last = now;
                var shown = hud.Onboarding.Visible && hud.Onboarding.Current == OnboardingStep.Attack;
                if (shown)
                {
                    visibleRun += dt;
                    totalVisible += dt;
                    hiddenRun = 0;
                }
                else
                {
                    visibleRun = 0;
                    hiddenRun += dt;
                }

                longestRun = Math.Max(longestRun, visibleRun);
                longestHidden = Math.Max(longestHidden, hiddenRun);
            }

            Log.Info($"qa-dica: em 28 s sem atacar, a dica de atacar ficou {totalVisible:0.0} s na tela (maior trecho seguido {longestRun:0.0} s; maior sumiço {longestHidden:0.0} s)");
            await Shot("qa-dica-atacar-grudada");
            Require(longestHidden >= 3.0 || totalVisible <= 12.0, $"a dica de atacar fica {totalVisible:0.0} de 28 s na tela (maior trecho {longestRun:0.0} s, maior sumiço {longestHidden:0.0} s): depois dos 10 s ela deveria sumir e voltar só numa próxima vez");
        }
        finally
        {
            GameUi.AutoCompleteQuests = auto;
            PqResetPlayerSettings();
            ui.CloseAll();
        }
    }

    [SmokeScenario("polimento (qa): na borda de fuga, tocar, esquivar ou ficar parado nunca abre 'Saída da Brecha'", order: 1021)]
    private async Task PqEscapeNeverByAccident()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var player = FindPlayer()!;
        var asked = new List<string>();
        void OnEscape(string mapId) => asked.Add(mapId);
        EventBus.Instance.DungeonEscapeRequested += OnEscape;
        try
        {
            foreach (var (theme, def) in GameManager.Db.Themes)
            {
                await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
                var edge = world.Map.Back ?? throw new SmokeFailure($"{world.MapId} sem borda de fuga");
                asked.Clear();
                var cx = edge.Center.X;

                // 1. Parado dentro da borda por 2 s.
                player.Teleport(world.CorrectToWalkable(new Vector2(cx, edge.Y + 2)));
                await WaitSeconds(2.0);
                Require(asked.Count == 0, $"{world.MapId}: parado na borda abriu a fuga");

                // 2. Toques curtos (0,3 s) para baixo, cinco vezes.
                for (var i = 0; i < 5; i++)
                {
                    player.Teleport(world.CorrectToWalkable(new Vector2(cx, edge.Y + 2)));
                    await Frames(2);
                    PressDirection(Vector2.Down);
                    await WaitSeconds(0.3);
                    PressDirection(Vector2.Zero);
                    await WaitSeconds(0.25);
                }

                Require(asked.Count == 0, $"{world.MapId}: toques curtos para baixo abriram a fuga {asked.Count}x");

                // 3. Esquiva para baixo ao lado da borda (segura baixo 0,2 s e solta).
                player.Teleport(world.CorrectToWalkable(new Vector2(cx, edge.Y - 30)));
                await Frames(3);
                PressDirection(Vector2.Down);
                Input.ActionPress(InputActions.Dodge);
                await WaitSeconds(0.2);
                Input.ActionRelease(InputActions.Dodge);
                PressDirection(Vector2.Zero);
                await WaitSeconds(1.0);
                Require(asked.Count == 0, $"{world.MapId}: uma esquiva para baixo perto da borda abriu a fuga");

                // 4. Andar de lado ao longo da borda por 1,5 s (olhando para baixo: o último movimento foi para baixo).
                player.Teleport(world.CorrectToWalkable(new Vector2(edge.X + 6, edge.Y + 2)));
                await Frames(2);
                PressDirection(new Vector2(1, 0.05f).Normalized());
                await WaitSeconds(1.5);
                PressDirection(Vector2.Zero);
                Log.Info($"qa-fuga: {world.MapId}: tocar/parado/esquivar/andar de lado → {asked.Count} pedido(s) de fuga (esperado 0)");
                Require(asked.Count == 0, $"{world.MapId}: andar de lado dentro da borda abriu a fuga ({asked.Count}x)");
                await FleeRun(world, combat);
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
            Input.ActionRelease(InputActions.Dodge);
            EventBus.Instance.DungeonEscapeRequested -= OnEscape;
            await FleeRun(world, combat);
        }
    }

    [SmokeScenario("polimento (qa): pular o tutorial com Esc não trava 'Primeiros passos': falar com a Odete no balcão cumpre", order: 1022)]
    private async Task PqTutorialSkipThenTalk()
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        PqResetPlayerSettings();
        var auto = GameUi.AutoCompleteQuests;
        GameUi.AutoCompleteQuests = true;
        try
        {
            await PqUseKeyboard();
            ui.StartTutorial();
            await WaitUntil(() => ui.Dialog.IsOpen && ui.Dialog.Speaker == "Odete", 4, "tutorial");
            await WaitSeconds(0.5);
            await PqKeyTap(Key.Escape);
            await WaitSeconds(0.5);
            Log.Info($"qa-tutorial: depois do Esc: diálogo aberto={ui.Dialog.IsOpen}, missão={state.CurrentQuest?.Id}, contador Odete={state.Save.Quests.Counter(QuestCounters.Odete)}");
            await Shot("qa-tutorial-pulado");
            ui.CloseAll();
            await WaitSeconds(0.4);
            Require(state.CurrentQuest?.Id == "primeiros_passos" || state.CurrentQuest?.Id == "bonecos", $"missão depois de pular: {state.CurrentQuest?.Id}");

            // Falar com a Odete de verdade (E) até a conversa acabar: a missão tem de andar.
            var odete = world.GetInteractable("odete") ?? throw new SmokeFailure("sem a Odete");
            FindPlayer()!.Teleport(odete.Position + new Vector2(0, 20));
            await WaitSeconds(0.6);
            for (var i = 0; i < 8 && state.CurrentQuest?.Id == "primeiros_passos"; i++)
            {
                if (!ui.Dialog.IsOpen)
                {
                    await PqKeyTap(Key.E);
                    await WaitSeconds(0.5);
                }
                else
                {
                    await SkipTyping(ui);
                    await PqKeyTap(Key.Enter);
                    await WaitSeconds(0.3);
                }
            }

            Log.Info($"qa-tutorial: depois de falar com a Odete: missão={state.CurrentQuest?.Id}, contador={state.Save.Quests.Counter(QuestCounters.Odete)}");
            Require(state.CurrentQuest?.Id != "primeiros_passos", "falar com a Odete no balcão não cumpriu 'Primeiros passos' depois de pular o tutorial");
        }
        finally
        {
            GameUi.AutoCompleteQuests = auto;
            PqResetPlayerSettings();
            ui.CloseAll();
        }
    }

    // --- sensação do combate: luta de verdade com a Lâmina na Brecha (prints + números) ---

    [SmokeScenario("polimento (qa): a Lâmina nos primeiros 30 s de luta de verdade na Brecha tem peso e a telegrafia aparece", order: 1030)]
    private async Task PqBladeFirst30Seconds()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        GameSettings.ResetAccessibility();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        OS.SetEnvironment("VESPERA_BOT_TRACE", "24");
        var running = true;
        var hitstopFrames = 0;
        var frames = 0;
        var peakShake = 0f;
        var telegraphFrames = 0;
        var maxMonsters = 0;
        var kills = 0;
        void OnDied(string id, Vector2 at) => kills++;
        EventBus.Instance.EnemyDied += OnDied;
        try
        {
            await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            async Task Sampler()
            {
                while (running)
                {
                    await NextPhysicsFrame();
                    frames++;
                    hitstopFrames += combat.Clock.Hitstop > 0 ? 1 : 0;
                    peakShake = Mathf.Max(peakShake, world.Camera.CurrentShake);
                    foreach (var e in combat.Enemies)
                    {
                        if (CombatDirector.TelegraphActive(e.Body))
                        {
                            telegraphFrames++;
                            break;
                        }
                    }

                    maxMonsters = Math.Max(maxMonsters, combat.AliveMonsters);
                }
            }

            _ = Sampler();
            var t0 = Time.GetTicksMsec();
            await Bot(combat, () => (Time.GetTicksMsec() - t0) > 30000 || combat.Brecha.Run is null || kills >= 12, 60, "30 s de luta com a Lâmina", shotAt: "qa-luta");
            running = false;
            await NextPhysicsFrame();
            var seconds = (Time.GetTicksMsec() - t0) / 1000.0;
            Log.Info($"qa-luta: {seconds:0} s, {kills} mortes, {maxMonsters} monstros no máx., hitstop em {hitstopFrames} de {frames} quadros ({100.0 * hitstopFrames / Math.Max(1, frames):0.0}%), tremor máx. {peakShake:0.0}, telegrafia em {telegraphFrames} quadros");
            Require(kills > 0, "a Lâmina não matou nada em 30 s");
            Require(hitstopFrames >= 10, $"pouco hitstop em 30 s de luta: {hitstopFrames} quadros");
            Require(telegraphFrames > 0, "nenhum monstro mostrou telegrafia em 30 s de luta");
            Require(100.0 * hitstopFrames / Math.Max(1, frames) < 35.0, $"hitstop demais: {hitstopFrames}/{frames} quadros parados");
        }
        finally
        {
            running = false;
            EventBus.Instance.EnemyDied -= OnDied;
            OS.SetEnvironment("VESPERA_BOT_TRACE", string.Empty);
            await FleeRun(world, combat);
        }
    }

    // --- resoluções ---

    [SmokeScenario("polimento (qa): informa o tamanho real da janela e da área visível e tira prints do HUD em 100/125/150%", order: 1040)]
    private async Task PqResolutionShots()
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var hud = Hud.Current!;
        PqResetPlayerSettings();
        try
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            // VESPERA_QA_WINDOW=1920x1080: janela sem borda no canto da tela, para o fator inteiro da escala valer (a janela com moldura perde ~50 px de altura e cai para 2×).
            if (OS.GetEnvironment("VESPERA_QA_WINDOW") is { Length: > 0 } wanted && wanted.Split('x') is { Length: 2 } wh && int.TryParse(wh[0], out var ww) && int.TryParse(wh[1], out var hh) && DisplayServer.GetName() != "headless")
            {
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetPosition(Vector2I.Zero);
                DisplayServer.WindowSetSize(new Vector2I(ww, hh));
                await WaitSeconds(0.8);
            }

            var win = DisplayServer.WindowGetSize();
            var root = GetTree().Root;
            Log.Info($"qa-res: janela {win}, raiz {root.Size}, visível {GetViewport().GetVisibleRect()}, escala da tela {DisplayServer.ScreenGetScale()}, fator inteiro {UiScaling.WindowFactor(hud)}");
            foreach (var scale in GameSettings.UiScales)
            {
                GameSettings.UiScale = scale;
                await WaitSeconds(0.5);
                var view = GetViewport().GetVisibleRect();
                foreach (var block in new Control[] { hud.HpBar, hud.LocationLabel, hud.PotionSlot })
                {
                    Require(view.Grow(1).Encloses(block.GetGlobalRect()), $"{win} escala {scale}: {block.Name} fora da tela ({block.GetGlobalRect()} em {view})");
                }

                await Shot($"qa-res-{win.X}x{win.Y}-hud-{scale * 100:0}");
            }

            GameSettings.UiScale = 1f;
            GameSettings.TextScale = 1.3f;
            GameUi.Current!.OpenPause();
            await WaitSeconds(0.4);
            await Shot($"qa-res-{win.X}x{win.Y}-pausa");
            EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options");
            await WaitSeconds(0.5);
            await Shot($"qa-res-{win.X}x{win.Y}-opcoes");
        }
        finally
        {
            GameUi.Current?.CloseAll();
            PqResetPlayerSettings();
        }
    }

    // --- opções: todas as linhas aparecem, o foco acompanha e nada vaza ---

    /// <summary>Linhas (SelectableRow) da tela de opções, na ordem em que aparecem.</summary>
    private static List<SelectableRow> PqRows(Control screen) =>
        screen.FindChildren("*", nameof(SelectableRow), true, false).OfType<SelectableRow>().Where(r => r.IsVisibleInTree()).ToList();

    [SmokeScenario("polimento (qa): Opções mostra a primeira linha e o foco acompanha cada linha até o fim em todas as escalas", order: 1050)]
    private async Task PqOptionsAllRowsReachable()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var ui = UiOrFail();
        PqResetPlayerSettings();
        var problems = new List<string>();
        try
        {
            foreach (var (ui_scale, text_scale) in new[] { (1f, 1f), (1.25f, 1.15f), (1.5f, 1.3f) })
            {
                GameSettings.UiScale = ui_scale;
                GameSettings.TextScale = text_scale;
                ui.OpenPause();
                await WaitSeconds(0.3);
                EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options");
                await WaitUntil(() => ui.Options.IsOpen, 2, "opções abrirem");
                await WaitSeconds(0.4);
                var rows = PqRows(ui.Options);
                var scroll = ui.Options.FindChildren("*", nameof(ScrollContainer), true, false).OfType<ScrollContainer>().First();
                var first = rows[0];
                var firstLabel = ((Label)((HBoxContainer)first.GetChild(0)).GetChild(0)).Text;
                var view = scroll.GetGlobalRect();
                var firstRect = first.GetGlobalRect();
                var focusOwner = GetViewport().GuiGetFocusOwner();
                Log.Info($"qa-opcoes: UI {ui_scale * 100:0}% texto {text_scale * 100:0}%: {rows.Count} linhas, primeira '{firstLabel}' em {firstRect}, área rolável {view}, foco em '{(focusOwner as SelectableRow is { } fr ? ((Label)((HBoxContainer)fr.GetChild(0)).GetChild(0)).Text : focusOwner?.Name)}', rolagem {scroll.ScrollVertical}");
                await Shot($"qa-opcoes-{ui_scale * 100:0}-{text_scale * 100:0}-abertas");
                if (firstLabel != UiText.OptionMaster || !view.Grow(1).Encloses(firstRect))
                {
                    problems.Add($"UI {ui_scale * 100:0}%: a primeira linha ('{UiText.OptionMaster}') não está visível ao abrir (primeira visível '{firstLabel}' {firstRect}, rolagem {scroll.ScrollVertical}, área {view})");
                }

                // Desce com o teclado até o fim: o foco tem de estar sempre visível dentro da área rolável.
                var hidden = 0;
                for (var i = 0; i < rows.Count + 2; i++)
                {
                    var owner = GetViewport().GuiGetFocusOwner();
                    if (owner is SelectableRow row && !view.Grow(2).Encloses(row.GetGlobalRect()))
                    {
                        hidden++;
                    }

                    await UiPress("ui_down");
                }

                if (hidden > 0)
                {
                    problems.Add($"UI {ui_scale * 100:0}%: o foco ficou fora da área visível em {hidden} passo(s) descendo pelas opções");
                }

                await Shot($"qa-opcoes-{ui_scale * 100:0}-{text_scale * 100:0}-fim");
                ui.CloseAll();
                await WaitSeconds(0.3);
            }
        }
        finally
        {
            ui.CloseAll();
            PqResetPlayerSettings();
        }

        Require(problems.Count == 0, "Opções com linha escondida ou foco perdido:" + (char)10 + "  " + string.Join((char)10 + "  ", problems));
    }

    [SmokeScenario("estresse: polimento (qa): abrir e fechar Opções e Teclas 100× mexendo em tudo não vaza nós nem perde o foco", order: 1051)]
    private async Task PqOptionsStress()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var ui = UiOrFail();
        PqResetPlayerSettings();
        // Aquecimento: as telas de Opções e de Teclas são criadas sob demanda (um custo único de nós), fora da medida.
        ui.OpenPause();
        await Frames(3);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options");
        await Frames(3);
        ui.Options.KeybindsRow.Activate();
        await Frames(3);
        ui.CloseAll();
        await WaitSeconds(0.6);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var orphans0 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var noFocus = 0;
        var started = Time.GetTicksMsec();
        try
        {
            for (var round = 0; round < 100; round++)
            {
                ui.OpenPause();
                await Frames(2);
                EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options");
                await Frames(3);
                Require(ui.Options.IsOpen, $"rodada {round}: opções não abriram");
                if (GetViewport().GuiGetFocusOwner() is null)
                {
                    noFocus++;
                }

                // mexe em tudo: ← → em 6 linhas, e uma troca de escala por rodada.
                for (var i = 0; i < 6; i++)
                {
                    await UiPress(i % 2 == 0 ? "ui_right" : "ui_left");
                    await UiPress("ui_down");
                }

                GameSettings.UiScale = GameSettings.UiScales[round % 3];
                GameSettings.TextScale = GameSettings.TextScales[(round / 3) % 3];
                if (round % 10 == 0)
                {
                    ui.Options.KeybindsRow.Activate();
                    await Frames(3);
                    Require(ui.Options.Keybinds.IsOpen, $"rodada {round}: teclas não abriram");
                }

                ui.CloseAll();
                await Frames(2);
                if (round == 5)
                {
                    // Depois das primeiras rodadas (pools de som e telas já criados) começa a medida de crescimento.
                    nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                    orphans0 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
                    mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
                }
            }

            GameSettings.ResetAccessibility();
            await WaitSeconds(0.6);
            var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            var orphans1 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
            Log.Info($"qa-opcoes-estresse: 100 rodadas em {(Time.GetTicksMsec() - started) / 1000.0:0} s; nós {nodes0}→{nodes1}, órfãos {orphans0}→{orphans1}, memória {mem0 / (1024 * 1024):0.0}→{mem1 / (1024 * 1024):0.0} MB, sem foco ao abrir em {noFocus} rodadas");
            Require(nodes1 - nodes0 <= 20, $"nós cresceram {nodes0}→{nodes1}");
            Require(orphans1 - orphans0 <= 5, $"órfãos cresceram {orphans0}→{orphans1}");
            Require(mem1 - mem0 < 20 * 1024 * 1024, $"memória cresceu {(mem1 - mem0) / (1024 * 1024):0.0} MB");
            Require(noFocus == 0, $"as opções abriram sem foco em {noFocus} de 100 rodadas");
        }
        finally
        {
            ui.CloseAll();
            PqResetPlayerSettings();
        }
    }

    // --- estresse: trocar teclado↔controle no meio da luta, remapear sem parar, desempenho com a acessibilidade ligada ---

    [SmokeScenario("estresse: polimento (qa): trocar teclado↔controle a cada 0,2 s durante uma luta de verdade não quebra dicas, mira nem HUD", order: 1060)]
    private async Task PqDeviceSwitchMidFight()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        PqResetPlayerSettings();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var switches = 0;
        void OnDevice() => switches++;
        InputHints.DeviceChanged += OnDevice;
        var running = true;
        var kills = 0;
        void OnDied(string id, Vector2 at) => kills++;
        EventBus.Instance.EnemyDied += OnDied;
        var nodes0 = 0.0;
        try
        {
            GameSettings.AimAssist = true;
            GameSettings.Captions = true;
            await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            var hud = Hud.Current!;
            async Task Toggler()
            {
                var pad = false;
                while (running)
                {
                    pad = !pad;
                    if (pad)
                    {
                        // botão sem função: só avisa a UI que o jogador pegou o controle
                        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Guide, Pressed = true });
                        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Guide, Pressed = false });
                    }
                    else
                    {
                        PqNoopKey();
                    }

                    await WaitSeconds(0.2);
                }
            }

            _ = Toggler();
            var t0 = Time.GetTicksMsec();
            await Bot(combat, () => (Time.GetTicksMsec() - t0) > 20000 || combat.Brecha.Run is null, 40, "luta com troca de dispositivo");
            running = false;
            await WaitSeconds(0.3);
            var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            Log.Info($"qa-dispositivo: {switches} trocas teclado↔controle em 20 s de luta, {kills} mortes, nós {nodes0}→{nodes1}");
            await Shot("qa-dispositivo-fim");
            Require(switches >= 20, $"só {switches} trocas de dispositivo foram detectadas");
            Require(kills > 0, "a luta parou de funcionar com a troca de dispositivo");
            Require(nodes1 - nodes0 <= 60, $"nós cresceram {nodes0}→{nodes1} durante as trocas");
            Require(hud.PotionSlot.IsInsideTree() && GameManager.Instance.State.Hp > 0, "HUD/jogador em estado inválido");
        }
        finally
        {
            running = false;
            InputHints.DeviceChanged -= OnDevice;
            EventBus.Instance.EnemyDied -= OnDied;
            PqResetPlayerSettings();
            await FleeRun(world, combat);
        }
    }

    private static void PqNoopKey() => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = false });

    [SmokeScenario("estresse: polimento (qa): 400 remapeamentos aleatórios (com conflitos) nunca deixam ação sem tecla, tecla duplicada nem arquivo inconsistente", order: 1061)]
    private async Task PqRemapFuzz()
    {
        await FreshGame("sword");
        InputBindings.RestoreDefaults();
        var rng = new Random(1234);
        var keys = new[]
        {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J, Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
            Key.Space, Key.Shift, Key.Tab, Key.Up, Key.Down, Key.Left, Key.Right, Key.Key1, Key.F2, Key.Escape, Key.Enter, Key.Backspace,
        };
        var buttons = new[]
        {
            JoyButton.A, JoyButton.B, JoyButton.X, JoyButton.Y, JoyButton.Back, JoyButton.Start, JoyButton.LeftStick, JoyButton.RightStick, JoyButton.LeftShoulder, JoyButton.RightShoulder,
            JoyButton.DpadUp, JoyButton.DpadDown, JoyButton.DpadLeft, JoyButton.DpadRight,
        };
        var problems = new List<string>();
        var results = new Dictionary<RemapResult, int>();
        try
        {
            for (var i = 0; i < 400; i++)
            {
                var r = InputBindings.Remappable[rng.Next(InputBindings.Remappable.Length)];
                var pad = r.Pad && rng.Next(2) == 0;
                InputEvent e = pad
                    ? new InputEventJoypadButton { ButtonIndex = buttons[rng.Next(buttons.Length)] }
                    : new InputEventKey { PhysicalKeycode = keys[rng.Next(keys.Length)] };
                var result = InputBindings.Assign(r.Action, pad, e, out _);
                results[result] = results.GetValueOrDefault(result) + 1;

                // Invariantes: toda ação remapeável tem tecla principal; nenhuma tecla principal repetida entre duas ações.
                var seen = new Dictionary<string, string>();
                foreach (var a in InputBindings.Remappable)
                {
                    var kb = InputBindings.Label(a.Action, false);
                    if (kb is null)
                    {
                        problems.Add($"passo {i}: '{a.Label}' ficou sem tecla de teclado");
                    }
                    else if (seen.TryGetValue("k:" + kb, out var who))
                    {
                        problems.Add($"passo {i}: '{a.Label}' e '{who}' usam a mesma tecla '{kb}'");
                    }
                    else
                    {
                        seen["k:" + kb] = a.Label;
                    }

                    if (a.Pad)
                    {
                        var pb = InputBindings.Label(a.Action, true);
                        if (pb is null)
                        {
                            problems.Add($"passo {i}: '{a.Label}' ficou sem botão de controle");
                        }
                        else if (seen.TryGetValue("p:" + pb, out var who2))
                        {
                            problems.Add($"passo {i}: '{a.Label}' e '{who2}' usam o mesmo botão '{pb}'");
                        }
                        else
                        {
                            seen["p:" + pb] = a.Label;
                        }
                    }
                }

                if (problems.Count > 5)
                {
                    break;
                }
            }

            Log.Info($"qa-remap-fuzz: resultados {string.Join(", ", results.Select(kv => $"{kv.Key}={kv.Value}"))}; problemas {problems.Count}");
            InputBindings.RestoreDefaults();
            foreach (var a in InputBindings.Remappable)
            {
                if (InputBindings.Label(a.Action, false) is null)
                {
                    problems.Add($"depois de restaurar: '{a.Label}' sem tecla");
                }
            }

            var cfg = new ConfigFile();
            cfg.Load(AudioManager.Instance.SettingsFile);
            if (cfg.HasSection("input_keys") && cfg.GetSectionKeys("input_keys").Length > 0)
            {
                problems.Add($"restaurar padrão deixou {cfg.GetSectionKeys("input_keys").Length} tecla(s) no settings.cfg");
            }
        }
        finally
        {
            InputBindings.RestoreDefaults();
        }

        await Task.CompletedTask;
        Require(problems.Count == 0, "remapeamento inconsistente:" + (char)10 + "  " + string.Join((char)10 + "  ", problems.Take(8)));
    }

    [SmokeScenario("estresse: polimento (qa): tempo de frame (média e p95) numa luta com UI 150%, texto 130%, alto contraste e legendas", order: 1062)]
    private async Task PqFrameTimeWithAccessibility()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        PqResetPlayerSettings();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        try
        {
            GameSettings.UiScale = 1.5f;
            GameSettings.TextScale = 1.3f;
            GameSettings.HighContrast = true;
            GameSettings.Captions = true;
            await EnterBrecha(world, combat, spot: 0, theme: "cathedral", rank: 0);
            var deltas = new List<double>();
            var last = Time.GetTicksUsec();
            var memory0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
            var running = true;
            async Task Sampler()
            {
                while (running)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var now = Time.GetTicksUsec();
                    deltas.Add((now - last) / 1000.0);
                    last = now;
                }
            }

            _ = Sampler();
            var t0 = Time.GetTicksMsec();
            await Bot(combat, () => (Time.GetTicksMsec() - t0) > 15000 || combat.Brecha.Run is null, 30, "luta de 15 s com a acessibilidade no máximo");
            running = false;
            await NextPhysicsFrame();
            deltas.Sort();
            var avg = deltas.Average();
            var p95 = deltas[(int)(deltas.Count * 0.95)];
            Log.Info($"qa-frame-a11y: {deltas.Count} quadros, média {avg:0.00} ms, p95 {p95:0.00} ms, máx {deltas[^1]:0.00} ms; memória {memory0 / (1024 * 1024):0.0}→{Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024):0.0} MB; combate {combat.LastStepMs:0.00} ms/passo, {combat.LastStepBytes} B/passo");
            Require(p95 <= 20.0, $"p95 de {p95:0.0} ms > 20 ms (média {avg:0.0} ms)");
        }
        finally
        {
            PqResetPlayerSettings();
            await FleeRun(world, combat);
        }
    }

    // --- todas as telas em 100% e 150%: nada cortado, texto legível, foco visível ---

    /// <summary>Rótulos visíveis com texto cujo retângulo sai da área visível (fora de áreas roláveis).</summary>
    private List<string> PqClippedTexts(Control screen)
    {
        var view = GetViewport().GetVisibleRect().Grow(1.5f);
        var list = new List<string>();
        foreach (var node in screen.FindChildren("*", "Control", true, false))
        {
            if (node is not Label { Text.Length: > 0 } label || !label.IsVisibleInTree())
            {
                continue;
            }

            var inScroll = false;
            for (Node? p = label.GetParent(); p is not null && p != screen; p = p.GetParent())
            {
                if (p is ScrollContainer)
                {
                    inScroll = true;
                    break;
                }
            }

            if (!inScroll && !view.Encloses(label.GetGlobalRect()))
            {
                list.Add($"'{label.Text}' {label.GetGlobalRect()}");
            }
        }

        return list;
    }

    [SmokeScenario("polimento (qa): todas as telas em 100% e 150% cabem na janela, com foco visível e texto legível", order: 1070)]
    private async Task PqScreensGallery()
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        PqResetPlayerSettings();
        var problems = new List<string>();
        var tinyScreens = new List<string>();
        var win = DisplayServer.WindowGetSize();
        try
        {
            if (OS.GetEnvironment("VESPERA_QA_WINDOW") is { Length: > 0 } wanted && wanted.Split('x') is { Length: 2 } wh && int.TryParse(wh[0], out var ww) && int.TryParse(wh[1], out var hh) && DisplayServer.GetName() != "headless")
            {
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetPosition(Vector2I.Zero);
                DisplayServer.WindowSetSize(new Vector2I(ww, hh));
                await WaitSeconds(0.8);
                win = DisplayServer.WindowGetSize();
            }

            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            var screens = new (string Name, Func<Task> Open)[]
            {
                ("pausa", async () => { ui.OpenPause(); await Task.CompletedTask; }),
                ("inventario", async () => { ui.OpenInventory(); await Task.CompletedTask; }),
                ("mural", async () => { ui.OpenBoard(); await Task.CompletedTask; }),
                ("diario", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "journal"); await Task.CompletedTask; }),
                ("ajuda", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "help"); await Task.CompletedTask; }),
                ("controles", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "controls"); await Task.CompletedTask; }),
                ("opcoes", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options"); await Task.CompletedTask; }),
                ("teclas", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options"); await WaitSeconds(0.3); ui.Options.KeybindsRow.Activate(); }),
                ("dialogo-bras", async () => { EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, "talkSmith", "bras"); await Task.CompletedTask; }),
            };
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            {
                GameSettings.UiScale = scale;
                GameSettings.TextScale = scale > 1f ? 1.3f : 1f;
                await WaitSeconds(0.3);
                foreach (var (name, open) in screens)
                {
                    ui.CloseAll();
                    await WaitSeconds(0.25);
                    await open();
                    await WaitSeconds(0.6);
                    var top = ui.Stack.Top;
                    if (top is null)
                    {
                        problems.Add($"{name} {scale * 100:0}%: a tela não abriu");
                        continue;
                    }

                    var clipped = PqClippedTexts(top);
                    var focus = GetViewport().GuiGetFocusOwner();
                    var tiny = top.FindChildren("*", nameof(Label), true, false).OfType<Label>()
                        .Where(l => l.IsVisibleInTree() && l.Text.Length > 0 && l.GetThemeFontSize("font_size") < 7).Select(l => $"'{l.Text}' {l.GetThemeFontSize("font_size")}px").Take(3).ToList();
                    Log.Info($"qa-telas: {name} {scale * 100:0}% janela {win}: cortes {clipped.Count}, foco em {focus?.GetType().Name ?? "nenhum"}, fontes <7px: {tiny.Count}");
                    await Shot($"qa-tela-{win.X}x{win.Y}-{name}-{scale * 100:0}");
                    if (clipped.Count > 0)
                    {
                        problems.Add($"{name} {scale * 100:0}%: texto fora da tela: {string.Join("; ", clipped.Take(3))}");
                    }

                    if (focus is null && name is not ("controles" or "dialogo-bras"))
                    {
                        problems.Add($"{name} {scale * 100:0}%: nenhum controle com foco (foco invisível)");
                    }

                    if (tiny.Count > 0 && scale == 1f)
                    {
                        // Telas de moldura (inventário, mural, pausa, ajuda…) ainda têm texto de 5–6 px base: 10–12 px em 720p.
                        tinyScreens.Add($"{name}: {string.Join("; ", tiny)}");
                    }
                }
            }
        }
        finally
        {
            ui.CloseAll();
            PqResetPlayerSettings();
        }

        Require(problems.Count == 0, "telas com problema de layout/foco:" + (char)10 + "  " + string.Join((char)10 + "  ", problems));
    }

    [SmokeScenario("polimento (qa): nenhuma tela do jogo tem texto menor que 7 px base (14 px em 720p)", order: 1071)]
    private async Task PqScreensMinimumFont()
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var ui = UiOrFail();
        PqResetPlayerSettings();
        var tiny = new List<string>();
        try
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            var opens = new (string Name, Action Open)[]
            {
                ("pausa", () => ui.OpenPause()),
                ("inventário", () => ui.OpenInventory()),
                ("mural", () => ui.OpenBoard()),
                ("diário", () => EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "journal")),
                ("ajuda", () => EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "help")),
                ("controles", () => EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "controls")),
                ("opções", () => EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options")),
            };
            foreach (var (name, open) in opens)
            {
                ui.CloseAll();
                await WaitSeconds(0.25);
                open();
                await WaitSeconds(0.5);
                if (ui.Stack.Top is { } top)
                {
                    foreach (var l in top.FindChildren("*", nameof(Label), true, false).OfType<Label>().Where(l => l.IsVisibleInTree() && l.Text.Length > 0 && l.GetThemeFontSize("font_size") < 7))
                    {
                        tiny.Add($"{name}: '{l.Text}' {l.GetThemeFontSize("font_size")}px");
                    }
                }
            }
        }
        finally
        {
            ui.CloseAll();
            PqResetPlayerSettings();
        }

        foreach (var t in tiny.Distinct())
        {
            Log.Info($"qa-fonte: {t}");
        }

        Require(tiny.Count == 0, $"{tiny.Distinct().Count()} texto(s) abaixo de 7 px base nas telas:" + (char)10 + "  " + string.Join((char)10 + "  ", tiny.Distinct().Take(10)));
    }

    // --- rótulos do mundo (nomes de caçadores, moradores, balões, portais) nunca por cima do HUD ---

    [SmokeScenario("estresse: polimento (qa): 90 s de cidade viva com a câmera passeando: nenhum rótulo do mundo fica visível por cima do HUD", order: 1080)]
    private async Task PqWorldLabelsNeverOverHud()
    {
        var world = await FreshGame("sword");
        var city = CityLife.Instance ?? throw new SmokeFailure("World sem CityLife");
        var labels = Find<WorldLabels>() ?? throw new SmokeFailure("sem WorldLabels");
        var hud = Hud.Current!;
        PqResetPlayerSettings();
        city.Brechas.Reset();
        city.SetSimulationEnabled(true);
        var violations = new List<string>();
        var samples = 0;
        var maxLabels = 0;
        try
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            var player = FindPlayer()!;
            using var speed = new SpeedUp(4);
            var rng = new Random(7);
            var spots = new List<Vector2>();
            while (spots.Count < 8)
            {
                var x = 40 + (float)(rng.NextDouble() * (world.Map.ImageWidth - 80));
                var y = 40 + (float)(rng.NextDouble() * (world.Map.ImageHeight - 80));
                if (world.WalkArea.CanStand(x, y))
                {
                    spots.Add(new Vector2(x, y));
                }
            }

            var scales = new[] { 1f, 1.25f, 1.5f };
            var started = Time.GetTicksMsec();
            var step = 0;
            while ((Time.GetTicksMsec() - started) < 90000)
            {
                GameSettings.UiScale = scales[(step / spots.Count) % scales.Length];
                player.Teleport(world.CorrectToWalkable(spots[step % spots.Count]));
                step++;
                for (var i = 0; i < 12; i++)
                {
                    await WaitSeconds(0.25);
                    var inUse = 0;
                    foreach (var child in labels.GetChildren())
                    {
                        if (child is not Label { Visible: true } label || label.Text.Length == 0)
                        {
                            continue;
                        }

                        inUse++;
                        samples++;
                        // Visível de verdade (não escondido pelo HUD) e encostando numa zona do HUD, de forma persistente (fade leva ~0,1 s).
                        if (label.SelfModulate.A > 0.5f && label.Modulate.A > 0.5f && HudZones.Overlaps(label.GetGlobalRect()))
                        {
                            violations.Add($"'{label.Text}' {label.GetGlobalRect()} escala {GameSettings.UiScale:0.00}");
                        }
                    }

                    maxLabels = Math.Max(maxLabels, inUse);
                }
            }

            Log.Info($"qa-rotulos: {samples} amostras de rótulos visíveis (máx. {maxLabels} ao mesmo tempo), {violations.Count} por cima do HUD");
            await Shot("qa-rotulos-fim");
        }
        finally
        {
            city.SetSimulationEnabled(false);
            city.Brechas.Reset();
            PqResetPlayerSettings();
        }

        Require(violations.Count == 0, $"{violations.Count} rótulo(s) do mundo visíveis por cima do HUD:" + (char)10 + "  " + string.Join((char)10 + "  ", violations.Distinct().Take(6)));
    }

    [SmokeScenario("estresse: polimento (qa): 40 s de spam de teclas reais (andar, interagir, atacar, esquivar, poção, Esc, Tab) com a acessibilidade no máximo", order: 1081)]
    private async Task PqInputSpamWithAccessibility()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        PqResetPlayerSettings();
        OnboardingHints.ResetProgress();
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var ui = UiOrFail();
        var rng = new Random(99);
        var keys = new[] { Key.W, Key.A, Key.S, Key.D, Key.E, Key.J, Key.K, Key.Space, Key.R, Key.Q, Key.C, Key.F, Key.Escape, Key.Tab, Key.M, Key.Enter, Key.Up, Key.Down };
        var down = new HashSet<Key>();
        var nodes0 = 0.0;
        try
        {
            GameSettings.UiScale = 1.5f;
            GameSettings.TextScale = 1.3f;
            GameSettings.HighContrast = true;
            GameSettings.Captions = true;
            GameSettings.ReduceFlashes = true;
            GameSettings.DialogSpeed = DialogSpeed.Instant;
            await WaitSeconds(0.5);
            nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            var started = Time.GetTicksMsec();
            var presses = 0;
            var maxStack = 0;
            var warm = false;
            while (Time.GetTicksMsec() - started < 40000)
            {
                var key = keys[rng.Next(keys.Length)];
                if (down.Contains(key))
                {
                    PqKeyUp(key);
                    down.Remove(key);
                }
                else
                {
                    PqKeyDown(key);
                    down.Add(key);
                    presses++;
                }

                if (rng.Next(25) == 0)
                {
                    foreach (var k in down.ToList())
                    {
                        PqKeyUp(k);
                    }

                    down.Clear();
                }

                if (rng.Next(40) == 0)
                {
                    PqPadDown(JoyButton.A);
                    PqPadUp(JoyButton.A);
                    PqNoopKey();
                }

                maxStack = Math.Max(maxStack, ui.Stack.Count);
                if (!warm && Time.GetTicksMsec() - started > 15000)
                {
                    warm = true; // as telas e os pools de som já foram criados: daqui em diante os nós não podem crescer
                    nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                }

                await Frames(rng.Next(1, 4));
            }

            foreach (var k in down.ToList())
            {
                PqKeyUp(k);
            }

            ReleaseAllActions();
            ui.CloseAll();
            await WaitSeconds(0.8);
            var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            Log.Info($"qa-spam: {presses} teclas em 40 s, pilha de telas máx. {maxStack}, nós {nodes0}→{nodes1}, vida {GameManager.Instance.State.Hp}, mapa {world.MapId}");
            await Shot("qa-spam-fim");
            Require(nodes1 - nodes0 <= 60, $"nós cresceram {nodes0}→{nodes1}");
            Require(maxStack <= 4, $"pilha de telas chegou a {maxStack}");
            Require(!GameManager.Instance.IsGameplayBlocked, $"jogo ficou travado por: {string.Join(", ", GameManager.Instance.GameplayBlocks)}");
            // Depois do spam o jogador ainda anda.
            var x0 = FindPlayer()!.GlobalPosition;
            PqKeyDown(Key.D);
            await WaitSeconds(0.6);
            PqKeyUp(Key.D);
            Require(FindPlayer()!.GlobalPosition.DistanceTo(x0) > 3f, "o jogador não anda depois do spam");
        }
        finally
        {
            foreach (var k in down.ToList())
            {
                PqKeyUp(k);
            }

            ReleaseAllActions();
            ui.CloseAll();
            PqResetPlayerSettings();
        }
    }

    [SmokeScenario("polimento (qa): telegrafia legível (triângulo '!' + desenho no chão) em luta real, normal e em alto contraste", order: 1031)]
    private async Task PqTelegraphShots()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        GameSettings.ResetAccessibility();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        var running = true;
        var shots = 0;
        var telegraphSeen = 0;
        var contrastShots = 0;
        try
        {
            await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            async Task Watcher()
            {
                var cooldown = 0;
                while (running)
                {
                    await NextPhysicsFrame();
                    if (cooldown-- > 0)
                    {
                        continue;
                    }

                    foreach (var e in combat.Enemies)
                    {
                        var foot = FindPlayer()!.GlobalPosition;
                        if (CombatDirector.TelegraphActive(e.Body) && new Vector2((float)e.Body.X, (float)e.Body.Y).DistanceTo(foot) < 170f)
                        {
                            telegraphSeen++;
                            var contrast = GameSettings.HighContrast;
                            await Shot($"qa-telegrafia-{(contrast ? "contraste" : "normal")}-{(contrast ? contrastShots : shots)}-{e.Body.Def.Id}-{e.Body.State}");
                            if (contrast)
                            {
                                contrastShots++;
                            }
                            else
                            {
                                shots++;
                            }

                            cooldown = 40;
                            break;
                        }
                    }
                }
            }

            _ = Watcher();
            var t0 = Time.GetTicksMsec();
            await Bot(combat, () => (Time.GetTicksMsec() - t0) > 40000 || shots >= 3 || combat.Brecha.Run is null, 60, "telegrafia normal");
            GameSettings.HighContrast = true;
            t0 = Time.GetTicksMsec();
            await Bot(combat, () => (Time.GetTicksMsec() - t0) > 40000 || contrastShots >= 3 || combat.Brecha.Run is null, 60, "telegrafia em alto contraste");
            running = false;
            Log.Info($"qa-telegrafia: {telegraphSeen} telegrafias vistas perto do jogador, prints normal {shots}, contraste {contrastShots}");
            Require(telegraphSeen > 0, "nenhum monstro telegrafou um golpe perto do jogador em 80 s");
        }
        finally
        {
            running = false;
            GameSettings.ResetAccessibility();
            await FleeRun(world, combat);
        }
    }
}
