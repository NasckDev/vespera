using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.UI.HudKit;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>Frente C1: HUD legível, escala da interface e as opções de acessibilidade (DECISIONS C1-D4 a C1-D13).</summary>
public partial class SmokeTest
{
    [SmokeScenario("polimento: HUD legível, escala da interface e rótulos do mundo fora do HUD", order: 963)]
    private async Task PolishHudScale()
    {
        await FreshGame("sword");
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        GameSettings.ResetAccessibility();
        try
        {
            await WaitSeconds(0.3);
            // Texto mínimo legível: nenhum rótulo visível do HUD abaixo de 7 px base (14 px em 720p; antes havia 5 e 6).
            var small = hud.FindChildren("*", nameof(Label), true, false).OfType<Label>()
                .Where(l => l.IsVisibleInTree() && l.Text.Length > 0 && l.GetThemeFontSize("font_size") < 7)
                .Select(l => $"'{l.Text}' {l.GetThemeFontSize("font_size")}px").ToList();
            Require(small.Count == 0, $"rótulos pequenos demais no HUD: {string.Join(", ", small)}");
            Require(hud.WeaponSlots.Count(s => s.Visible) == 1, "só a arma da classe aparece na hotbar (sem 4 cadeados)");
            await Shot("polimento-hud-100");

            // Loot sem empilhar: 8 notas → no máximo 4 linhas, no canto de baixo.
            for (var i = 0; i < 8; i++)
            {
                hud.Note($"Nota {i}", "i_coin");
            }

            await NextPhysicsFrame();
            Require(hud.Loot.VisibleTexts.Count() <= LootFeed.Capacity, $"caixa de loot com {hud.Loot.VisibleTexts.Count()} linhas");
            Require(hud.Loot.GetGlobalRect().Position.Y > GetViewport().GetVisibleRect().Size.Y * 0.45f, $"caixa de loot no canto de baixo: {hud.Loot.GetGlobalRect()}");

            foreach (var scale in GameSettings.UiScales)
            {
                GameSettings.UiScale = scale;
                await WaitSeconds(0.2);
                var view = GetViewport().GetVisibleRect();
                foreach (var block in new Control[] { hud.HpBar, hud.LumesLabel, hud.LocationLabel, hud.PotionSlot })
                {
                    Require(view.Encloses(block.GetGlobalRect()), $"escala {scale}: {block.Name} fora da tela ({block.GetGlobalRect()} em {view})");
                }

                Require(Mathf.IsEqualApprox(hud.HpBar.GetGlobalTransform().Scale.X, scale), $"escala {scale}: HUD com escala {hud.HpBar.GetGlobalTransform().Scale.X}");
                Require(HudZones.MinimapSlot.Size.X >= 120f * scale - 1f, $"escala {scale}: minimapa acompanha o HUD ({HudZones.MinimapSlot})");
                await Shot($"polimento-hud-{Mathf.RoundToInt(scale * 100)}");
            }

            // Missão compacta: a descrição some sozinha e volta quando muda.
            Require(hud.QuestExpanded || !GameSettings.QuestHints, "missão nova aparece aberta");
            hud.ExpandQuest();
            await WaitSeconds(Hud.QuestExpandSeconds + 0.4);
            Require(!hud.QuestExpanded, "a descrição da missão deveria recolher depois de alguns segundos");

            // Rótulo do mundo (nome de caçador) por cima do painel de vida: some; fora do HUD: aparece.
            var labels = Find<WorldLabels>() ?? throw new SmokeFailure("sem WorldLabels");
            var name = labels.Rent(WorldLabels.Style.HunterName);
            try
            {
                WorldLabels.SetText(name, "Caçador de Teste");
                WorldLabels.PlaceCentered(name, hud.HpBar.GetGlobalRect().GetCenter());
                await WaitSeconds(0.4);
                Require(WorldLabels.HiddenByHud(name), "nome do caçador por cima do painel de vida deveria sumir");
                WorldLabels.PlaceCentered(name, GetViewport().GetVisibleRect().GetCenter());
                await WaitSeconds(0.4);
                Require(!WorldLabels.HiddenByHud(name), "nome do caçador no meio da tela deveria aparecer");
            }
            finally
            {
                labels.Return(name);
            }

            // Alto contraste: fundo opaco atrás do status.
            GameSettings.HighContrast = true;
            await WaitSeconds(0.2);
            await Shot("polimento-hud-alto-contraste");
        }
        finally
        {
            GameSettings.ResetAccessibility();
        }
    }

    [SmokeScenario("polimento: texto do diálogo (tamanho, velocidade) e legendas", order: 964)]
    private async Task PolishDialogAndCaptions()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        var hud = Hud.Current!;
        GameSettings.ResetAccessibility();
        try
        {
            ui.ShowDialog("Odete", "Uma fala comprida o bastante para a máquina de escrever ainda estar digitando no próximo quadro.");
            await NextPhysicsFrame();
            Require(ui.Dialog.IsTyping, "velocidade normal digita");
            var normal = TextSize(ui);
            ui.CloseAll();

            GameSettings.DialogSpeed = DialogSpeed.Instant;
            GameSettings.TextScale = 1.3f;
            ui.ShowDialog("Odete", "Uma fala comprida o bastante para a máquina de escrever ainda estar digitando no próximo quadro.");
            await NextPhysicsFrame();
            Require(!ui.Dialog.IsTyping, "'Instantâneo' mostra a fala inteira na hora");
            Require(TextSize(ui) > normal, $"tamanho do texto 130% deveria aumentar a fonte ({normal} → {TextSize(ui)})");
            await WaitSeconds(0.2);
            await Shot("polimento-dialogo-texto-130");
            ui.CloseAll();

            // Legendas: desligadas não aparecem; ligadas mostram o chefe, o portal e o perigo fora da tela.
            EventBus.Instance.EmitSignal(EventBus.SignalName.CaptionRequested, "[Teste]");
            Require(!hud.Captions.Shown.Any(), "legenda com a opção desligada");
            GameSettings.Captions = true;
            EventBus.Instance.EmitSignal(EventBus.SignalName.BossSpawned, "O Abissal do Mercado", 500);
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, 0, "fish", 0);
            await NextPhysicsFrame();
            var shown = hud.Captions.Shown.ToList();
            Require(shown.Contains(UiText.CaptionBoss) && shown.Contains(UiText.CaptionPortal), $"legendas: {string.Join(" | ", shown)}");
            await WaitSeconds(0.2);
            await Shot("polimento-legendas-chefe");

            // Banner do chefe: pequeno, no alto e curto (sai antes de 2,5 s).
            Require(hud.BossAlert.Visible && hud.BossAlert.GetGlobalRect().Position.Y < 60, $"alerta do chefe no alto: {hud.BossAlert.GetGlobalRect()}");
            await WaitSeconds(2.3);
            Require(!hud.BossAlert.Visible, "alerta do chefe deveria sumir em ~2 s");
            EventBus.Instance.EmitSignal(EventBus.SignalName.BossHealthChanged, 0, 500);
            EventBus.Instance.EmitSignal(EventBus.SignalName.PortalClosed, 0);
            UiOrFail().Brechas.Clear();
        }
        finally
        {
            ui.CloseAll();
            GameSettings.ResetAccessibility();
            hud.Toasts.Clear();
        }
    }

    [SmokeScenario("polimento: remapear teclado e controle, conflito e restaurar padrão", order: 965)]
    private async Task PolishRemap()
    {
        await FreshGame("sword");
        var ui = UiOrFail();
        InputBindings.RestoreDefaults();
        try
        {
            ui.OpenPause();
            await WaitSeconds(0.2);
            EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "options");
            await WaitUntil(() => ui.Options.IsOpen, 2, "opções abrirem");
            await WaitSeconds(0.2);
            await Shot("polimento-opcoes");
            ui.Options.KeybindsRow.Activate();
            var keys = ui.Options.Keybinds;
            await WaitUntil(() => keys.IsOpen, 2, "teclas e controle abrir");
            await WaitSeconds(0.2);
            Require(GetViewport().GuiGetFocusOwner() is not null, "tela de teclas abriu sem foco visível");

            // Atacar (J) ← K, que é a principal do Ataque forte: as duas trocam.
            var attack = Array.FindIndex(InputBindings.Remappable, r => r.Action == InputActions.Attack);
            keys.StartListening(attack);
            await WaitSeconds(0.3);
            await UiKey(Key.K);
            await NextPhysicsFrame();
            Require(InputBindings.Label(InputActions.Attack, false) == "K" && InputBindings.Label(InputActions.AttackHeavy, false) == "J",
                $"troca J↔K: Atacar={InputBindings.Label(InputActions.Attack, false)}, forte={InputBindings.Label(InputActions.AttackHeavy, false)} ({keys.Status})");
            Require(keys.Status.Contains("trocaram", StringComparison.Ordinal), $"aviso de troca: {keys.Status}");
            Require(InputHints.LabelFor(InputActions.Attack, false) == "K", "dicas de tecla da UI seguem o remapeamento");

            // Conflito com uma tecla secundária (↑ do "Mover para cima") é recusado.
            keys.StartListening(attack);
            await WaitSeconds(0.3);
            await UiKey(Key.Up);
            await NextPhysicsFrame();
            Require(InputBindings.Label(InputActions.Attack, false) == "K", "↑ é do movimento: não pode virar Atacar");
            Require(keys.Status.Contains("já é usada", StringComparison.Ordinal), $"aviso de conflito: {keys.Status}");

            // Controle: Esquivar (B) ← Y, principal do Ataque forte no controle.
            var dodge = Array.FindIndex(InputBindings.Remappable, r => r.Action == InputActions.Dodge);
            keys.SetColumn(1);
            keys.StartListening(dodge);
            await WaitSeconds(0.3);
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = true });
            await NextPhysicsFrame();
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = false });
            await NextPhysicsFrame();
            Require(InputBindings.Label(InputActions.Dodge, true) == "Y" && InputBindings.Label(InputActions.AttackHeavy, true) == "B",
                $"troca no controle: esquiva={InputBindings.Label(InputActions.Dodge, true)}, forte={InputBindings.Label(InputActions.AttackHeavy, true)}");
            await Shot("polimento-teclas");

            // Persistência: as trocas estão no settings.cfg e voltam ao recarregar.
            var config = new ConfigFile();
            config.Load(AudioManager.Instance.SettingsFile);
            Require((string)config.GetValue("input_keys", InputActions.Attack.ToString(), string.Empty) == $"k:{(long)Key.K}", "Atacar = K gravado em [input_keys]");
            InputMap.LoadFromProjectSettings();
            InputBindings.Load();
            Require(InputBindings.Label(InputActions.Attack, false) == "K", "remapeamento volta ao carregar");

            keys.RestoreRow.Activate();
            await NextPhysicsFrame();
            Require(InputBindings.Label(InputActions.Attack, false) == "J" && InputBindings.Label(InputActions.Dodge, true) == "B", "restaurar padrão");
        }
        finally
        {
            InputBindings.RestoreDefaults();
            ui.CloseAll();
        }
    }

    [SmokeScenario("polimento: dicas da primeira vez seguem o dispositivo", order: 966)]
    private async Task PolishOnboarding()
    {
        await FreshGame("sword");
        var hud = Hud.Current!;
        OnboardingHints.ResetProgress();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false }); // volta ao teclado
        try
        {
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Move, 2, "dica de andar");
            await Frames(2); // o evento de teclado acima é processado no quadro seguinte
            Require(hud.Onboarding.KeyText == "WASD", $"tecla da dica no teclado: {hud.Onboarding.KeyText}");
            await WaitSeconds(0.2);
            await Shot("polimento-dica-andar");
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder, Pressed = false });
            await NextPhysicsFrame();
            Require(hud.Onboarding.KeyText == UiText.HintStick, $"no controle a dica mostra o analógico: {hud.Onboarding.KeyText}");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });
            PressDirection(Vector2.Right);
            await WaitSeconds(1.1);
            PressDirection(Vector2.Zero);
            Require(OnboardingHints.IsDone(OnboardingStep.Move), "andar conclui a dica");

            // Primeiro prompt → dica de interagir com a tecla E.
            var odete = FindWorld().GetInteractable("odete") ?? throw new SmokeFailure("sem a Odete");
            FindPlayer()!.Teleport(odete.Position + new Vector2(0, 20));
            await WaitUntil(() => hud.Onboarding.Current == OnboardingStep.Interact, 3, "dica de interagir");
            Require(hud.Onboarding.KeyText == "E", $"tecla de interagir: {hud.Onboarding.KeyText}");
            await Shot("polimento-dica-interagir");
        }
        finally
        {
            PressDirection(Vector2.Zero);
            OnboardingHints.ResetProgress();
            UiOrFail().CloseAll();
        }
    }

    [SmokeScenario("polimento: tremor, flashes, arco alternado e mira assistida", order: 967)]
    private async Task PolishCombatOptions()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "bow");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        GameSettings.ResetAccessibility();
        try
        {
            GameSettings.ShakeIntensity = 0.3f;
            Require(Mathf.IsEqualApprox(GameSettings.ShakeIntensity, 0.3f) && GameSettings.ScreenShake, "tremor em 30%");
            GameSettings.ReduceFlashes = true;
            Require(GameSettings.FlashScale < 0.5f, "reduzir flashes enfraquece os clarões");

            // Arco: alternar — apertar começa a carga, apertar de novo solta.
            GameSettings.BowToggle = true;
            await Tap(InputActions.Attack);
            await WaitSeconds(0.3);
            Require(combat.PlayerCombat.BowLatched, "1º toque deveria começar a carga e segurar sozinho");
            await Tap(InputActions.Attack);
            await NextPhysicsFrame();
            Require(!combat.PlayerCombat.BowLatched, "2º toque deveria soltar a flecha");

            // Mira assistida no controle: boneco ~0,25 rad abaixo da mira do analógico (0 rad) puxa a mira para ele.
            GameSettings.AimAssist = true;
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder, Pressed = false });
            var dummy = combat.Enemies.First(e => e.Body.IsDummy).Body;
            var player = FindPlayer()!;
            var target = new Vector2((float)dummy.X, (float)(dummy.Y - (dummy.HitHeight * 0.5)));
            player.Teleport(world.CorrectToWalkable(target + new Vector2(-70, -18) + new Vector2(0, 10)));
            await Frames(3);
            Input.ActionPress(InputActions.AimRight, 1f);
            await Frames(3);
            var assisted = combat.PlayerCombat.Aim;
            GameSettings.AimAssist = false;
            await Frames(2);
            var raw = combat.PlayerCombat.Aim;
            Input.ActionRelease(InputActions.AimRight);
            Log.Info($"polimento: mira do analógico {raw:0.00} rad, com assistência {assisted:0.00} rad");
            Require(MathF.Abs(raw) < 0.01f && assisted > 0.05f, $"mira assistida deveria puxar para o boneco (sem: {raw:0.00}, com: {assisted:0.00})");
        }
        finally
        {
            Input.ActionRelease(InputActions.AimRight);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });
            GameSettings.ResetAccessibility();
        }
    }

    private static int TextSize(GameUi ui) =>
        ui.Dialog.FindChildren("*", nameof(Label), true, false).OfType<Label>().First(l => l.Text == ui.Dialog.Text).GetThemeFontSize("font_size");

    [SmokeScenario("polimento: Lâmina nos bonecos tem peso (hitstop, faíscas, amasso)", order: 968)]
    private async Task PolishBladeFeel()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        var dummy = combat.Enemies.First(e => e.Body.IsDummy);
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2((float)dummy.Body.X - 18, (float)dummy.Body.Y)));
        await Frames(4);
        try
        {
            SetAim(Vector2.Right);
            var hitstops = 0;
            var popped = false;
            Input.ActionPress(InputActions.Attack);
            for (var frame = 0; frame < 90; frame++)
            {
                await NextPhysicsFrame();
                hitstops += combat.Clock.Hitstop > 0 ? 1 : 0;
                popped |= dummy.Body.Flash > 0;
                if (frame is 3 or 5)
                {
                    await Shot($"polimento-golpe-{frame:00}");
                }
            }

            Input.ActionRelease(InputActions.Attack);
            Log.Info($"polimento: 1,5 s de Lâmina no boneco: {hitstops} quadros em hitstop, flash={popped}");
            Require(popped && hitstops >= 3, $"acerto sem peso: {hitstops} quadros de hitstop, flash {popped}");
        }
        finally
        {
            Input.ActionRelease(InputActions.Attack);
            SetAim(Vector2.Zero);
        }
    }
}
