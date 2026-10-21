using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Pausa (efetivo <c>uiPause</c>, <c>extra.js:245-258</c>): Continuar · Diário de missões · Como jogar · Controles ·
/// Opções · Salvar jogo · Voltar ao menu principal (salva antes). Sub-telas voltam para a pausa com a seleção
/// preservada. A "prévia" de multijogador da v2 fica de fora (DECISIONS D41).
/// </summary>
public partial class PauseScreen : Screen
{
    private readonly GameUi _ui;
    private readonly Label _subtitle;
    private readonly List<SelectableRow> _rows = [];
    private int _lastFocus;

    public PauseScreen(GameUi ui)
    {
        _ui = ui;
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.75f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = Ui.Ornate();
        panel.CustomMinimumSize = new Vector2(230, 0);
        center.AddChild(panel);

        _subtitle = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Sub, align: HorizontalAlignment.Right);
        var list = Ui.Column(1);
        Add(list, UiText.PauseContinue, Close);
        Add(list, UiText.PauseJournal, () => Open(ui.Journal));
        Add(list, UiText.MenuHelp, () => Open(ui.Help));
        Add(list, UiText.MenuControls, () => Open(ui.Controls));
        Add(list, UiText.MenuOptions, () => Open(ui.Options));
        Add(list, UiText.PauseSave, SaveGame);
        Add(list, UiText.PauseQuit, QuitToTitle);
        var header = Ui.Row(6, Ui.Title(UiText.PauseTitle, UiFonts.Heading), Ui.Spacer(), _subtitle);
        panel.AddChild(Ui.Margin(4, 2, 4, 2, Ui.Column(5, header, Ui.Rule(), list)));
    }

    public IReadOnlyList<SelectableRow> Rows => _rows;

    protected override Control? DefaultFocus => _rows[Math.Clamp(_lastFocus, 0, _rows.Count - 1)];

    public override bool HandleInput(InputEvent @event)
    {
        // Start (gamepad) também fecha a pausa.
        if (InputReady && !@event.IsEcho() && @event.IsActionPressed(InputActions.Pause))
        {
            UiSound();
            Close();
            return true;
        }

        return base.HandleInput(@event);
    }

    protected override void OnOpened()
    {
        _lastFocus = 0;
        var state = GameManager.Instance.State;
        var place = _ui.World?.Map?.Name ?? string.Empty;
        _subtitle.Text = $"Dia {state.Save.Day} · {place}";
    }

    private void Add(VBoxContainer list, string label, Action action)
    {
        var index = _rows.Count;
        var row = new SelectableRow { CustomMinimumSize = new Vector2(0, 19), OnActivate = action, OnFocused = () => _lastFocus = index };
        row.AddChild(Ui.Text(label, UiFonts.Body, UiPalette.SubLight));
        list.AddChild(row);
        _rows.Add(row);
    }

    private void Open(Screen screen) => Stack?.Push(screen);

    private void SaveGame()
    {
        GameManager.Instance.SaveGame();
        _ui.Note(UiText.Saved, "i_lantern");
        Close();
    }

    private void QuitToTitle()
    {
        GameManager.Instance.SaveGame();
        _ui.CloseAll();
        GameManager.Instance.ChangeScene(Title.TitleScreen.ScenePath);
    }
}
