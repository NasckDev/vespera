using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Remapeamento de teclado e controle (frente C1, DECISIONS C1-D6): uma linha por ação com a tecla principal no
/// teclado/mouse e o botão no controle. ←/→ escolhe a coluna, Enter/A começa a escutar e a próxima tecla (ou botão)
/// vira a principal; conflito com outra ação troca as duas ou é recusado com aviso. "Restaurar padrão" volta ao
/// <c>project.godot</c>. Foco visível, navegável só com teclado ou só com controle.
/// </summary>
public partial class KeybindScreen : FrameScreen
{
    private const double ListenDelay = 0.2; // o Enter/A que abriu a escuta não vira a tecla nova

    private readonly List<(SelectableRow Row, RemappableAction Action, Label Keyboard, Label Pad)> _rows = [];
    private readonly Label _status;
    private SelectableRow _restore = null!;
    private int _column;
    private ScrollContainer _scroll = null!;
    private int _listening = -1;
    private double _listenAge;

    public KeybindScreen()
        : base(UiText.KeybindsTitle, 460, 300)
    {
        var list = Ui.Column(1);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        list.AddChild(Ui.Row(6, Ui.Text(string.Empty, UiFonts.Body), Ui.Spacer(),
            Header(UiText.KeybindsKeyboard), Header(UiText.KeybindsPad)));
        for (var i = 0; i < InputBindings.Remappable.Length; i++)
        {
            var action = InputBindings.Remappable[i];
            var index = i;
            var keyboard = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar, HorizontalAlignment.Center);
            var pad = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar, HorizontalAlignment.Center);
            keyboard.CustomMinimumSize = new Vector2(96, 0);
            pad.CustomMinimumSize = new Vector2(80, 0);
            var row = new SelectableRow
            {
                CustomMinimumSize = new Vector2(0, 16),
                OnActivate = () => StartListening(index),
                OnAdjust = dir => SetColumn(_column + dir),
                OnFocused = () => Refresh(),
            };
            row.AddChild(Ui.Row(6, Ui.Text(action.Label, UiFonts.Body, UiPalette.SubLight), Ui.Spacer(), keyboard, pad));
            list.AddChild(row);
            _rows.Add((row, action, keyboard, pad));
        }

        _restore = new SelectableRow { CustomMinimumSize = new Vector2(0, 16), OnActivate = RestoreDefaults };
        _restore.AddChild(Ui.Row(6, Ui.Text(UiText.KeybindsRestore, UiFonts.Body, UiPalette.Creme), Ui.Spacer(), Ui.Bold("↺", UiFonts.Body, UiPalette.Ambar)));
        list.AddChild(Ui.Gap(0, 4));
        list.AddChild(_restore);

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
        _status = UiScaling.Reading(Ui.Text(UiText.KeybindsHint, UiFonts.Body, UiPalette.Sub, wrap: true));
        Body.AddChild(_status);
    }

    /// <summary>Esperando a tecla nova (smoke test).</summary>
    public bool Listening => _listening >= 0;

    /// <summary>Coluna escolhida: 0 teclado, 1 controle.</summary>
    public int Column => _column;

    /// <summary>Aviso da última troca (smoke test).</summary>
    public string Status => _status.Text;

    public IReadOnlyList<(SelectableRow Row, RemappableAction Action, Label Keyboard, Label Pad)> Rows => _rows;

    public SelectableRow RestoreRow => _restore;

    protected override Control? DefaultFocus => _rows.Count > 0 ? _rows[0].Row : null;

    /// <summary>Começa a escutar a linha (smoke test e Enter/A).</summary>
    public void StartListening(int index)
    {
        if (_column == 1 && !_rows[index].Action.Pad)
        {
            _status.Text = UiText.KeybindsInvalid;
            Common.UiSound.Denied();
            return;
        }

        _listening = index;
        _listenAge = 0;
        var action = _rows[index].Action.Label;
        _status.Text = _column == 0
            ? UiText.KeybindsListening.Replace("{action}", action).Replace("{device}", UiText.KeybindsKeyboard)
            : UiText.KeybindsListeningPad.Replace("{action}", action);
        Refresh();
    }

    public void SetColumn(int column)
    {
        _column = Mathf.Clamp(column, 0, 1);
        Common.UiSound.Click();
        Refresh();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_listening >= 0)
        {
            _listenAge += delta;
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Escutando: a primeira tecla/botão apertado vai para o remapeamento antes da GUI (senão Enter/setas navegariam).
        if (_listening < 0 || !Visible || _listenAge < ListenDelay || !@event.IsPressed() || @event.IsEcho())
        {
            return;
        }

        if (@event is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) < 0.6f)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        var cancel = @event is InputEventKey { Keycode: Key.Escape } or InputEventJoypadButton { ButtonIndex: JoyButton.Start };
        if (cancel)
        {
            StopListening(UiText.KeybindsHint);
            return;
        }

        var pad = @event is InputEventJoypadButton or InputEventJoypadMotion;
        if (pad != (_column == 1))
        {
            return; // tecla na coluna do controle (ou o contrário): continua escutando
        }

        var row = _rows[_listening];
        var key = InputBindings.Describe(InputBindings.Normalize(@event)) ?? "?";
        var result = InputBindings.Assign(row.Action.Action, pad, @event, out var other);
        var otherLabel = other is null ? string.Empty : InputBindings.ActionLabel(other);
        var message = result switch
        {
            RemapResult.Swapped => UiText.KeybindsSwapped.Replace("{key}", key).Replace("{other}", otherLabel),
            RemapResult.Conflict => UiText.KeybindsConflict.Replace("{key}", key).Replace("{other}", otherLabel),
            RemapResult.Invalid => UiText.KeybindsInvalid,
            _ => UiText.KeybindsHint,
        };
        if (result is RemapResult.Conflict or RemapResult.Invalid)
        {
            Common.UiSound.Denied();
        }
        else
        {
            Common.UiSound.Click();
        }

        StopListening(message);
    }

    public override bool HandleInput(InputEvent @event) => _listening < 0 && base.HandleInput(@event);

    protected override void OnOpened()
    {
        _listening = -1;
        OpenScrollAtTop(_scroll);
        _column =InputHints.UsingGamepad ? 1 : 0;
        _status.Text = UiText.KeybindsHint;
        Refresh();
    }

    protected override void OnClosed() => _listening = -1;

    private static Label Header(string text)
    {
        var label = Ui.Text(text, UiFonts.Body, UiPalette.Sub, align: HorizontalAlignment.Center);
        label.CustomMinimumSize = new Vector2(text == UiText.KeybindsKeyboard ? 96 : 80, 0);
        return label;
    }

    private void StopListening(string message)
    {
        _listening = -1;
        _status.Text = message;
        Refresh();
    }

    private void RestoreDefaults()
    {
        InputBindings.RestoreDefaults();
        _status.Text = UiText.KeybindsRestored;
        Refresh();
    }

    private void Refresh()
    {
        var focus = GetViewport()?.GuiGetFocusOwner();
        for (var i = 0; i < _rows.Count; i++)
        {
            var (row, action, keyboard, pad) = _rows[i];
            var listening = _listening == i;
            keyboard.Text = listening && _column == 0 ? "…" : Cell(InputBindings.Label(action.Action, false), _column == 0 && row == focus);
            pad.Text = !action.Pad ? "—" : listening && _column == 1 ? "…" : Cell(InputBindings.Label(action.Action, true), _column == 1 && row == focus);
            keyboard.AddThemeColorOverride("font_color", _column == 0 ? UiPalette.Ambar : UiPalette.SubLight);
            pad.AddThemeColorOverride("font_color", _column == 1 && action.Pad ? UiPalette.Ambar : UiPalette.SubLight);
        }
    }

    private static string Cell(string? label, bool selected) => selected ? $"[ {label ?? "—"} ]" : label ?? "—";
}
