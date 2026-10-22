using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Opções (<c>screenOptions</c>, <c>extra.js:199-204</c>, no estilo "◂ valor ▸"): volumes Master/Música/Efeitos/UI
/// (<see cref="AudioManager.SetBusVolume"/>, gravados ao fechar), tela cheia, tremor de tela e painel de missão; e a
/// acessibilidade da frente C1 (DECISIONS C1-D4 a C1-D12): escala da interface, tamanho e velocidade do texto,
/// reduzir flashes, alto contraste, legendas, arco segurar/alternar, mira assistida e o remapeamento de teclas
/// (<see cref="KeybindScreen"/>). ←/→ ajusta, Enter alterna; tudo aplica na hora e vai para <c>user://settings.cfg</c>.
/// </summary>
public partial class OptionsScreen : FrameScreen
{
    private const float VolumeStep = 0.1f;

    private readonly List<(SelectableRow Row, Label Value, Func<string> Read)> _rows = [];
    private KeybindScreen? _keybinds;
    private bool _volumeDirty;
    private ScrollContainer _scroll = null!;

    public OptionsScreen()
        : base(UiText.MenuOptions, 460, 300)
    {
        var list = Ui.Column(1);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddVolume(list, UiText.OptionMaster, AudioManager.MasterBus);
        AddVolume(list, UiText.OptionMusic, AudioManager.MusicBus);
        AddVolume(list, UiText.OptionSfx, AudioManager.SfxBus);
        AddVolume(list, UiText.OptionUi, AudioManager.UiBus);
        AddToggle(list, UiText.OptionFullscreen, () => GameSettings.Fullscreen ? UiText.On : UiText.Off, () => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddRow(list, UiText.OptionShake, () => $"{Mathf.RoundToInt(GameSettings.ShakeIntensity * 100)}%",
            dir => Changed(() => GameSettings.ShakeIntensity += dir * 0.1f),
            () => Changed(() => GameSettings.ShakeIntensity = GameSettings.ShakeIntensity > 0f ? 0f : 1f));
        AddToggle(list, UiText.OptionHints, () => GameSettings.QuestHints ? UiText.Visible : UiText.Hidden, () => GameSettings.QuestHints = !GameSettings.QuestHints);

        list.AddChild(Section(UiText.OptionAccessibility));
        AddCycle(list, UiText.OptionUiScale, () => Percent(GameSettings.UiScale), GameSettings.UiScales, () => GameSettings.UiScale, v => GameSettings.UiScale = v);
        AddCycle(list, UiText.OptionTextScale, () => Percent(GameSettings.TextScale), GameSettings.TextScales, () => GameSettings.TextScale, v => GameSettings.TextScale = v);
        AddRow(list, UiText.OptionDialogSpeed, () => UiText.DialogSpeeds[(int)GameSettings.DialogSpeed],
            dir => Changed(() => GameSettings.DialogSpeed = (DialogSpeed)Math.Clamp((int)GameSettings.DialogSpeed + dir, 0, 3)),
            () => Changed(() => GameSettings.DialogSpeed = (DialogSpeed)(((int)GameSettings.DialogSpeed + 1) % 4)));
        AddToggle(list, UiText.OptionReduceFlashes, () => GameSettings.ReduceFlashes ? UiText.On : UiText.Off, () => GameSettings.ReduceFlashes = !GameSettings.ReduceFlashes);
        AddToggle(list, UiText.OptionHighContrast, () => GameSettings.HighContrast ? UiText.On : UiText.Off, () => GameSettings.HighContrast = !GameSettings.HighContrast);
        AddToggle(list, UiText.OptionCaptions, () => GameSettings.Captions ? UiText.On : UiText.Off, () => GameSettings.Captions = !GameSettings.Captions);
        AddToggle(list, UiText.OptionBowHold, () => GameSettings.BowToggle ? UiText.BowToggle : UiText.BowHold, () => GameSettings.BowToggle = !GameSettings.BowToggle);
        AddToggle(list, UiText.OptionAimAssist, () => GameSettings.AimAssist ? UiText.On : UiText.Off, () => GameSettings.AimAssist = !GameSettings.AimAssist);

        list.AddChild(Section(UiText.OptionControlsSection));
        var keys = new SelectableRow { CustomMinimumSize = new Vector2(0, 16), OnActivate = () => Stack?.Push(Keybinds) };
        keys.AddChild(Ui.Row(6, Ui.Text(UiText.OptionKeybinds, UiFonts.Body, UiPalette.SubLight), Ui.Spacer(), Ui.Bold("▸", UiFonts.Body, UiPalette.Ambar)));
        list.AddChild(keys);
        KeybindsRow = keys;

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        var margin = Ui.Margin(0, 0, 4, 0, list);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        _scroll = scroll;
        Body.AddChild(scroll);
        Body.AddChild(Ui.Row(10, Ui.Hint("← →", "← →", "Ajustar"), Ui.Hint("Enter", "A", "Alternar")));
    }

    /// <summary>Linha "Teclas e controle" (smoke test).</summary>
    public SelectableRow KeybindsRow { get; }

    /// <summary>Sub-tela de remapeamento.</summary>
    public KeybindScreen Keybinds => _keybinds ??= new KeybindScreen(); // criada só quando usada (sem nó órfão)

    protected override Control? DefaultFocus => _rows.Count > 0 ? _rows[0].Row : null;

    /// <summary>Linha de opção pelo rótulo (smoke test).</summary>
    public SelectableRow? RowFor(string label)
    {
        foreach (var (row, _, _) in _rows)
        {
            if (((Label)((HBoxContainer)row.GetChild(0)).GetChild(0)).Text == label)
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>Valor mostrado numa linha (smoke test).</summary>
    public string ValueOf(string label)
    {
        foreach (var (row, value, _) in _rows)
        {
            if (((Label)((HBoxContainer)row.GetChild(0)).GetChild(0)).Text == label)
            {
                return value.Text;
            }
        }

        return string.Empty;
    }

    protected override void OnOpened()
    {
        OpenScrollAtTop(_scroll); // POLI-3
        Refresh();
    }

    protected override void OnResumed() => Refresh();

    protected override void OnClosed()
    {
        if (_volumeDirty)
        {
            AudioManager.Instance?.SaveSettings();
            _volumeDirty = false;
        }
    }

    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100)}%";

    private static Control Section(string title)
    {
        var label = Ui.Title(title.ToUpperInvariant(), UiFonts.Body, UiPalette.Gold);
        return Ui.Margin(2, 6, 0, 1, label);
    }

    private void Changed(Action change)
    {
        change();
        Common.UiSound.Click();
        Refresh();
    }

    private void AddVolume(VBoxContainer list, string label, string bus)
    {
        void Change(int dir, bool wrap)
        {
            if (AudioManager.Instance is not { } audio)
            {
                return;
            }

            var steps = Mathf.RoundToInt(audio.GetBusVolume(bus) / VolumeStep) + dir;
            steps = wrap && steps > 10 ? 0 : Math.Clamp(steps, 0, 10);
            audio.SetBusVolume(bus, steps * VolumeStep, persist: false);
            _volumeDirty = true;
            Common.UiSound.Click();
            Refresh();
        }

        AddRow(list, label, () => $"{Mathf.RoundToInt((AudioManager.Instance?.GetBusVolume(bus) ?? 1f) * 100)}%", dir => Change(dir, false), () => Change(1, true));
    }

    private void AddCycle(VBoxContainer list, string label, Func<string> read, float[] options, Func<float> get, Action<float> set)
    {
        int Index()
        {
            var current = get();
            for (var i = 0; i < options.Length; i++)
            {
                if (Mathf.IsEqualApprox(options[i], current))
                {
                    return i;
                }
            }

            return 0;
        }

        AddRow(list, label, read,
            dir => Changed(() => set(options[Math.Clamp(Index() + dir, 0, options.Length - 1)])),
            () => Changed(() => set(options[(Index() + 1) % options.Length])));
    }

    private void AddToggle(VBoxContainer list, string label, Func<string> read, Action toggle) =>
        AddRow(list, label, read, _ =>
        {
            toggle();
            Common.UiSound.Click();
            Refresh();
        }, () =>
        {
            toggle();
            Refresh();
        });

    private void AddRow(VBoxContainer list, string label, Func<string> read, Action<int> adjust, Action activate)
    {
        var value = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar, HorizontalAlignment.Right);
        var content = Ui.Row(6, Ui.Text(label, UiFonts.Body, UiPalette.SubLight), Ui.Spacer(), value);
        var row = new SelectableRow { OnAdjust = adjust, OnActivate = activate, CustomMinimumSize = new Vector2(0, 16) };
        row.AddChild(content);
        list.AddChild(row);
        _rows.Add((row, value, read));
    }

    private void Refresh()
    {
        foreach (var (_, value, read) in _rows)
        {
            value.Text = $"◂ {read()} ▸";
        }
    }
}
