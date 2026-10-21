using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Opções (<c>screenOptions</c>, <c>extra.js:199-204</c>, no estilo "◂ valor ▸"): volumes Master/Música/Efeitos/UI
/// (<see cref="AudioManager.SetBusVolume"/>, gravados ao fechar), tela cheia, tremor de tela e painel de missão.
/// ←/→ ajusta, Enter alterna (volume sobe 10% e dá a volta).
/// </summary>
public partial class OptionsScreen : FrameScreen
{
    private const float VolumeStep = 0.1f;

    private readonly List<(SelectableRow Row, Label Value, Func<string> Read)> _rows = [];
    private bool _volumeDirty;

    public OptionsScreen()
        : base(UiText.MenuOptions, 420, 260)
    {
        var list = Ui.Column(2);
        AddVolume(list, UiText.OptionMaster, AudioManager.MasterBus);
        AddVolume(list, UiText.OptionMusic, AudioManager.MusicBus);
        AddVolume(list, UiText.OptionSfx, AudioManager.SfxBus);
        AddVolume(list, UiText.OptionUi, AudioManager.UiBus);
        AddToggle(list, UiText.OptionFullscreen, () => GameSettings.Fullscreen ? UiText.On : UiText.Off, () => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddToggle(list, UiText.OptionShake, () => GameSettings.ScreenShake ? UiText.On : UiText.Off, () => GameSettings.ScreenShake = !GameSettings.ScreenShake);
        AddToggle(list, UiText.OptionHints, () => GameSettings.QuestHints ? UiText.Visible : UiText.Hidden, () => GameSettings.QuestHints = !GameSettings.QuestHints);
        Body.AddChild(list);
        Body.AddChild(Ui.Spacer(horizontal: false));
        Body.AddChild(Ui.Row(10, Ui.Hint("← →", "← →", "Ajustar"), Ui.Hint("Enter", "A", "Alternar")));
    }

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

    protected override void OnOpened() => Refresh();

    protected override void OnClosed()
    {
        if (_volumeDirty)
        {
            AudioManager.Instance?.SaveSettings();
            _volumeDirty = false;
        }
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
        var row = new SelectableRow { OnAdjust = adjust, OnActivate = activate, CustomMinimumSize = new Vector2(0, 18) };
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
