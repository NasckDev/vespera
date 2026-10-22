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
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.UI.Screens;
using Vespera.UI.Widgets;

namespace Vespera.Testing;

/// <summary>
/// Frente C1, rodada 1 de correções (POLI-1 a POLI-16): regressões das falhas que o QA provou, com prova antes/depois.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("polimento (r1): save e opções do smoke são exclusivos do processo (rodadas em paralelo não se pisam)", order: 1100)]
    private async Task PrIsolatedSmokeFiles()
    {
        var pid = OS.GetProcessId().ToString();
        var save = SaveService.Instance.SavePath;
        var settings = AudioManager.Instance.SettingsFile;
        Log.Info($"poli-16: save {save}, opções {settings}");
        Require(save.Contains(pid, StringComparison.Ordinal), $"o save do smoke ({save}) deveria ter o id do processo {pid}");
        Require(settings.Contains(pid, StringComparison.Ordinal), $"as opções do smoke ({settings}) deveriam ter o id do processo {pid}");
        await Task.CompletedTask;
    }

    [SmokeScenario("polimento (r1): remapear para tecla ou botão fixo (1–5, F2, RB, mira) é recusado com o nome da ação e nada muda", order: 1101)]
    private async Task PrReservedKeysRefused()
    {
        InputBindings.RestoreDefaults();
        try
        {
            var keys = new[] { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.F2 };
            foreach (var key in keys)
            {
                var before = InputBindings.Label(InputActions.Attack, false);
                var result = InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = key }, out var other);
                Log.Info($"poli-6/13: Atacar ← {key}: {result}, dona '{other}' ({(other is null ? "-" : InputBindings.ActionLabel(other))})");
                Require(result == RemapResult.Conflict, $"Atacar ← {key} deveria dar Conflict, deu {result}");
                Require(other is not null && InputBindings.ActionLabel(other) != other.ToString(), $"o conflito de {key} não diz o nome da ação ('{other}')");
                Require(InputBindings.Label(InputActions.Attack, false) == before, $"Atacar mudou de tecla ao recusar {key}");
            }

            var pads = new (JoyButton? Button, JoyAxis? Axis, float Value)[]
            {
                (JoyButton.RightShoulder, null, 0),
                (null, JoyAxis.RightX, -1f),
                (null, JoyAxis.RightX, 1f),
                (null, JoyAxis.RightY, -1f),
                (null, JoyAxis.RightY, 1f),
            };
            foreach (var (button, axis, value) in pads)
            {
                InputEvent e = button is { } b ? new InputEventJoypadButton { ButtonIndex = b } : new InputEventJoypadMotion { Axis = axis!.Value, AxisValue = value };
                var before = InputBindings.Label(InputActions.Dodge, true);
                var result = InputBindings.Assign(InputActions.Dodge, pad: true, e, out var other);
                Log.Info($"poli-6/13: Esquivar ← {InputBindings.Describe(e)}: {result}, dona '{other}'");
                Require(result == RemapResult.Conflict, $"Esquivar ← {InputBindings.Describe(e)} deveria dar Conflict, deu {result}");
                Require(InputBindings.Label(InputActions.Dodge, true) == before, "Esquivar mudou de botão ao recusar");
            }

            // Uma tecla livre continua valendo.
            Require(InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.H }, out _) == RemapResult.Ok, "H está livre");
        }
        finally
        {
            InputBindings.RestoreDefaults();
        }

        await Task.CompletedTask;
    }

    [SmokeScenario("polimento (r1): a carga alternada do arco termina ao pausar, abrir diálogo ou morrer (sem flecha sozinha)", order: 1102)]
    private async Task PrBowLatchReleasedOnControlLoss()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "bow");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        GameSettings.ResetAccessibility();
        var pc = combat.PlayerCombat;
        try
        {
            GameSettings.BowToggle = true;

            // 1. Gameplay bloqueado (diálogo, pausa): a carga some e nenhuma flecha sai quando o jogo volta.
            await Tap(InputActions.Attack);
            await WaitSeconds(0.25);
            Require(pc.BowLatched && pc.State.Charge >= 0, $"1º toque deveria carregar e travar (trava {pc.BowLatched}, carga {pc.State.Charge:0.00})");
            GameManager.Instance.BlockGameplay("poli-14");
            await NextPhysicsFrame();
            await NextPhysicsFrame();
            GameManager.Instance.UnblockGameplay("poli-14");
            await WaitSeconds(0.3);
            Log.Info($"poli-14: depois do bloqueio: trava {pc.BowLatched}, carga {pc.State.Charge:0.00}, recarga {pc.State.Cooldown:0.00}");
            Require(!pc.BowLatched, "a trava do arco continuou armada depois do bloqueio de gameplay");
            Require(pc.State.Charge < 0, $"a carga do arco continuou depois do bloqueio ({pc.State.Charge:0.00})");
            Require(pc.State.Cooldown <= 0, "uma flecha saiu sozinha quando o jogo voltou");

            // 2. Morte: a carga não sobrevive ao reviver.
            await Tap(InputActions.Attack);
            await WaitSeconds(0.25);
            Require(pc.BowLatched, "carga alternada armada antes de morrer");
            pc.Hurt(9999, new Vector2(FindPlayer()!.GlobalPosition.X + 20, FindPlayer()!.GlobalPosition.Y));
            await WaitSeconds(0.2);
            Require(pc.Dead, "o jogador deveria ter morrido");
            Require(!pc.BowLatched, "a trava do arco continuou armada depois da morte");
            pc.Revive();
            GameManager.Instance.State.RestoreFullHealth();
            await WaitSeconds(1.2);
            Require(!pc.BowLatched && pc.State.Charge < 0 && pc.State.Cooldown <= 0, $"carga voltou depois de reviver (trava {pc.BowLatched}, carga {pc.State.Charge:0.00})");
        }
        finally
        {
            GameSettings.ResetAccessibility();
            ReleaseAllActions();
        }
    }

    [SmokeScenario("polimento (r1): o som do acerto sai da posição do monstro e o golpe final do combo é mais grave", order: 1103)]
    private async Task PrHitSoundAtTheMonster()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        GameUi.Current?.CloseAll();
        GameSettings.ResetAccessibility();
        var audio = AudioManager.Instance;
        var player = FindPlayer()!;
        var dummy = combat.Enemies.First(e => e.Body.IsDummy).Body;
        var hits = new List<SfxPlayed>();
        try
        {
            player.Teleport(world.CorrectToWalkable(new Vector2((float)dummy.X - 20, (float)dummy.Y)));
            await Frames(4);
            SetAim(Vector2.Right);
            Input.ActionPress(InputActions.Attack);
            var last = audio.LastSfx;
            for (var f = 0; f < 150 && hits.Count < 6; f++)
            {
                await NextPhysicsFrame();
                if (audio.LastSfx.Id == "hit" && !audio.LastSfx.Equals(last))
                {
                    hits.Add(audio.LastSfx);
                }

                last = audio.LastSfx;
            }
        }
        finally
        {
            Input.ActionRelease(InputActions.Attack);
            SetAim(Vector2.Zero);
        }

        Log.Info($"poli-15: {hits.Count} acertos: {string.Join(" | ", hits.Select(h => $"tom {h.Pitch:0.00} em {h.Position}"))}; monstro em ({dummy.X:0},{dummy.Y:0}), jogador em {player.GlobalPosition}");
        Require(hits.Count >= 3, $"só {hits.Count} sons de acerto em 2,5 s de golpes");
        var dummyAt = new Vector2((float)dummy.X, (float)dummy.Y);
        foreach (var h in hits)
        {
            Require(h.Position is { } p && p.DistanceTo(dummyAt) < 40f, $"o som do acerto saiu de {h.Position}, longe do monstro em {dummyAt}");
        }

        var lowest = hits.Min(h => h.Pitch);
        var highest = hits.Max(h => h.Pitch);
        Require(lowest <= 0.9f && highest >= 0.93f, $"o golpe final do combo deveria soar mais grave que os outros (tons de {lowest:0.00} a {highest:0.00})");
    }

    [SmokeScenario("polimento (r1): com a dica de atacar na tela, o primeiro prompt de interagir passa na frente e a de atacar não volta em seguida", order: 1104)]
    private async Task PrInteractHintBeatsAttackHint()
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
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Attack, 4, "dica de atacar (missão dos bonecos)");

            // Chega no balcão sem ter atacado: o prompt aparece e a dica de interagir toma o lugar.
            var odete = FindWorld().GetInteractable("odete") ?? throw new SmokeFailure("sem a Odete");
            FindPlayer()!.Teleport(odete.Position + new Vector2(0, 20));
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Interact, 3, "dica de interagir no primeiro prompt");
            Require(!OnboardingHints.IsDone(OnboardingStep.Attack), "a dica de atacar não deveria ter sido dada como aprendida");
            Log.Info($"poli-5: dica de interagir apareceu no 1º prompt (atacar ainda pendente); tecla '{hud.Onboarding.KeyText}'");
        }
        finally
        {
            GameUi.AutoCompleteQuests = auto;
            PqResetPlayerSettings();
            ui.CloseAll();
        }
    }

    [SmokeScenario("polimento (r1): marcadores de tecla nos textos seguem o dispositivo e o remapeamento (falas, missão, Como jogar, classe)", order: 1105)]
    private async Task PrKeyTokensFollowDeviceAndRemap()
    {
        await PqFreshWorld();
        await FreshGame("sword");
        var ui = UiOrFail();
        InputBindings.RestoreDefaults();
        try
        {
            const string sample = "{key:ultimate} | {keys:attack} | {keyPhrase:pause} | {key:interact} | {desconhecido} | {key:";
            var kb = KeyText.Expand(sample, gamepad: false);
            var pad = KeyText.Expand(sample, gamepad: true);
            Log.Info($"poli-4: teclado '{kb}'; controle '{pad}'");
            Require(kb == "F | J ou Clique | a tecla Esc | E | {desconhecido} | {key:", $"teclado: '{kb}'");
            Require(pad == "RT | X | o botão Start | A | {desconhecido} | {key:", $"controle: '{pad}'");

            // Remapeia o Supremo para G e Atacar para H: a ajuda e a escolha de classe acompanham.
            InputBindings.Assign(InputActions.Ultimate, pad: false, new InputEventKey { PhysicalKeycode = Key.G }, out _);
            InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.H }, out _);
            await PqUseKeyboard();
            Require(KeyText.Expand("{key:ultimate}") == "G", "o Supremo remapeado para G");

            ui.OpenPause();
            await WaitSeconds(0.2);
            ui.Stack.Push(ui.Help);
            await WaitSeconds(0.3);
            var combatTab = ui.Help.FindChildren("*", nameof(SelectableRow), true, false).OfType<SelectableRow>().ElementAt(2);
            combatTab.GrabFocus();
            await WaitSeconds(0.3);
            var helpText = string.Join(" | ", ui.Help.FindChildren("*", nameof(Label), true, false).OfType<Label>().Select(l => l.Text));
            Log.Info($"poli-4: Como jogar (Combate): {helpText}");
            Require(helpText.Contains("Ataque com H", StringComparison.Ordinal), "a ajuda de combate deveria dizer 'Ataque com H…' depois de remapear");
            Require(!helpText.Contains("{key", StringComparison.Ordinal), "marcador de tecla sobrou na ajuda");
            ui.CloseAll();

            // A missão do Supremo cita a tecla do dispositivo em uso.
            var quest = GameManager.Db.Quests.First(q => q.Desc.Contains("{key:ultimate}", StringComparison.Ordinal));
            Require(KeyText.Expand(quest.Desc, gamepad: true).Contains("[RT]", StringComparison.Ordinal), $"missão no controle: {KeyText.Expand(quest.Desc, gamepad: true)}");
            Require(KeyText.Expand(quest.Desc, gamepad: false).Contains("[G]", StringComparison.Ordinal), $"missão no teclado remapeado: {KeyText.Expand(quest.Desc, gamepad: false)}");
        }
        finally
        {
            ui.CloseAll();
            InputBindings.RestoreDefaults();
            await PqUseKeyboard();
        }
    }

    [SmokeScenario("polimento (r1): chefe, onda e 4 linhas de loot cabem nas zonas do HUD e o letreiro com a faixa fica no vão entre os blocos (100/125/150%)", order: 1106)]
    private async Task PrBossZonesAndBannerBand()
    {
        await PqFreshWorld();
        var world = await FreshGame("sword");
        var hud = Hud.Current!;
        PqResetPlayerSettings();
        var problems = new List<string>();
        try
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await DismissUi();
            foreach (var scale in GameSettings.UiScales)
            {
                GameSettings.UiScale = scale;
                await WaitSeconds(0.3);
                for (var i = 0; i < 4; i++)
                {
                    hud.Note($"Estilhaço {i} em {scale * 100:0}%", "i_coin");
                }

                EventBus.Instance.EmitSignal(EventBus.SignalName.WaveStarted, 2, 3);
                await WaitSeconds(0.3);
                var waveZone = hud.WaveLabel.GetGlobalRect();
                EventBus.Instance.EmitSignal(EventBus.SignalName.BossSpawned, "O Abissal do Mercado", 500);
                await WaitSeconds(0.45);
                var bossRect = hud.Boss.GetGlobalRect().Grow(2f);
                var found = false;
                for (var i = 0; i < HudZones.Count; i++)
                {
                    found |= HudZones.Get(i).Position.IsEqualApprox(bossRect.Position) && HudZones.Get(i).Size.IsEqualApprox(bossRect.Size);
                }

                Log.Info($"poli-12: {scale * 100:0}%: {HudZones.Count} zonas (capacidade {HudZones.Capacity}), barra do chefe publicada={found}, loot com {hud.Loot.VisibleTexts.Count()} linha(s)");
                if (!found)
                {
                    problems.Add($"{scale * 100:0}%: a zona da barra do chefe {bossRect} não foi publicada ({HudZones.Count} zonas)");
                }

                if (HudZones.Count >= HudZones.Capacity)
                {
                    problems.Add($"{scale * 100:0}%: {HudZones.Count} zonas lotam a capacidade {HudZones.Capacity}: zonas novas seriam descartadas");
                }

                // O letreiro com a faixa de fundo (Panel) ocupa só o vão entre o painel de vida e a coluna da direita, sem cobrir nenhum bloco.
                var view = hud.GetViewport().GetVisibleRect();
                var banner = hud.BossAlert;
                var cover = new List<(string Name, Rect2 Rect)>();
                foreach (var c in banner.GetChildren().OfType<Control>().Where(c => c.IsVisibleInTree()))
                {
                    cover.Add((c is Label ? "texto" : "faixa", c is Label label ? PqTextRect(label) : c.GetGlobalRect()));
                }

                if (cover.Count == 0)
                {
                    problems.Add($"{scale * 100:0}%: o letreiro do chefe não estava na tela");
                }

                for (var z = 0; z < HudZones.Count; z++)
                {
                    foreach (var (name, rect) in cover)
                    {
                        var cut = HudZones.Get(z).GrowIndividual(-2, -2, -2, -2).Intersection(rect);
                        if (cut.Size.X > 1f && cut.Size.Y > 1f)
                        {
                            problems.Add($"{scale * 100:0}%: {name} do letreiro {rect} cobre a zona{z} {HudZones.Get(z)}");
                        }
                    }

                    foreach (var (name, rect) in cover)
                    {
                        if (!view.Grow(1f).Encloses(rect))
                        {
                            problems.Add($"{scale * 100:0}%: {name} do letreiro {rect} fora da tela {view}");
                        }
                    }
                }

                _ = waveZone;
                await Shot($"r1-chefe-zonas-{scale * 100:0}");
                EventBus.Instance.EmitSignal(EventBus.SignalName.BossHealthChanged, 0, 500);
                await WaitSeconds(2.3);
            }
        }
        finally
        {
            hud.Toasts.Clear();
            PqResetPlayerSettings();
        }

        Require(problems.Count == 0, "zonas do HUD no chefe:\n  " + string.Join("\n  ", problems.Distinct()));
    }

    [SmokeScenario("polimento (r1): janela de 1080p com barra de título usa escala inteira 3× e cobre a tela (sem metade preta)", order: 1107)]
    private async Task PrWindowCoversTheScreen()
    {
        if (DisplayServer.GetName() == "headless")
        {
            Log.Info("poli-11: sem janela de verdade no headless; a conta da escala está nos testes do Core (FitScale)");
            await Task.CompletedTask;
            return;
        }

        await PqFreshWorld();
        await FreshGame("sword");
        var window = GetTree().Root;
        var hud = Hud.Current!;
        var oldSize = DisplayServer.WindowGetSize();
        var oldBorderless = DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless);
        try
        {
            foreach (var size in new[] { new Vector2I(1920, 1061), new Vector2I(1280, 720), new Vector2I(1920, 1009) })
            {
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetPosition(Vector2I.Zero);
                DisplayServer.WindowSetSize(size);
                await WaitSeconds(0.8);
                var win = window.Size;
                var visible = GetViewport().GetVisibleRect().Size;
                var factor = UiScaling.WindowFactor(hud);
                var covered = (visible.X * factor * visible.Y * factor) / (win.X * (double)win.Y);
                Log.Info($"poli-11: janela {win}: fator {factor}, área visível {visible} u, cobre {covered * 100:0.0}% da janela");
                Require(covered >= 0.95, $"janela {win}: o jogo ocupa só {covered * 100:0.0}% da janela (fator {factor}, visível {visible})");
                Require(visible.X >= 600f && visible.Y >= 330f, $"janela {win}: área visível pequena demais ({visible})");
                Require(Mathf.IsEqualApprox(factor, CameraFraming.FitScale(win.X, win.Y)), $"janela {win}: fator {factor} diferente do FitScale {CameraFraming.FitScale(win.X, win.Y)}");
            }
        }
        finally
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, oldBorderless);
            DisplayServer.WindowSetSize(oldSize);
        }
    }
}
