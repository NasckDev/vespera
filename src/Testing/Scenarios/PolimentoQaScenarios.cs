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
}
