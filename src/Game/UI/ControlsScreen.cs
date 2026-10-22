using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Controles: lista cada ação com Principal, Alternativa (teclado/mouse) e Controle. Confirmar numa coluna captura
/// a próxima tecla/botão; conflito troca as duas ações de lugar e avisa; "Restaurar padrão" volta tudo.
/// </summary>
public partial class ControlsScreen : UiScreen
{
    private const int ActionColumnWidth = 380;
    private const int SlotColumnWidth = 360;
    private const double CaptureTimeoutSeconds = 6.0;
    private const string FixedPadText = "Analógico";

    private readonly Dictionary<(string Action, BindingSlot Slot), Button> _slotButtons = [];
    private Label _padLabel = null!;
    private Label _status = null!;
    private Button _firstSlot = null!;
    private Timer _timeout = null!;
    private (string Action, BindingSlot Slot)? _capturing;

    protected override Control? DefaultFocus() => _firstSlot;

    public override void _ExitTree() => Input.Singleton.JoyConnectionChanged -= OnJoyConnectionChanged;

    protected override void Build()
    {
        var panel = BuildPage("Controles", out var column, sideMargin: 110);
        var inner = UiKit.MakeColumn(6);
        panel.AddChild(inner);

        _padLabel = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        inner.AddChild(_padLabel);
        inner.AddChild(BuildHeader());

        var scroll = UiKit.MakeScroll();
        inner.AddChild(scroll);
        var list = UiKit.MakeColumn(8);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(list));
        foreach (var info in InputActions.Remappable)
        {
            list.AddChild(BuildActionRow(info));
        }

        _status = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        inner.AddChild(_status);
        column.AddChild(BuildFooter());

        _timeout = new Timer { OneShot = true, WaitTime = CaptureTimeoutSeconds, ProcessMode = ProcessModeEnum.Always };
        _timeout.Timeout += CancelCapture;
        AddChild(_timeout);

        Input.Singleton.JoyConnectionChanged += OnJoyConnectionChanged;
        RefreshAll();
    }

    public override void _Input(InputEvent @event)
    {
        if (_capturing is not { } target)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            {
                CancelCapture();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode is Key.Delete or Key.Backspace)
            {
                ClearSlot(target.Action, target.Slot);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        var code = InputSetup.FromEvent(@event, target.Slot);
        if (code is null)
        {
            if (@event is InputEventKey or InputEventMouseButton or InputEventJoypadButton)
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        GetViewport().SetInputAsHandled();
        AssignBinding(target.Action, target.Slot, code);
    }

    private Control BuildHeader()
    {
        var row = UiKit.MakeRow(8);
        row.AddChild(Cell("Ação", ActionColumnWidth, HorizontalAlignment.Left));
        row.AddChild(Cell("Principal", SlotColumnWidth, HorizontalAlignment.Center));
        row.AddChild(Cell("Alternativa", SlotColumnWidth, HorizontalAlignment.Center));
        row.AddChild(Cell("Controle", SlotColumnWidth, HorizontalAlignment.Center));
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddChild(row);
        return margin;
    }

    private static Label Cell(string text, int width, HorizontalAlignment align)
    {
        var label = UiKit.MakeLabel(text, UiTheme.DimVariation, align);
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    private Control BuildActionRow(ActionInfo info)
    {
        var row = UiKit.MakeRow(8);
        var name = UiKit.MakeLabel(info.Label);
        name.CustomMinimumSize = new Vector2(ActionColumnWidth, 0);
        row.AddChild(name);

        foreach (var slot in new[] { BindingSlot.Primary, BindingSlot.Alternate, BindingSlot.Gamepad })
        {
            var button = UiKit.MakeButton(string.Empty, () => StartCapture(info.Id, slot), SlotColumnWidth);
            
            if (slot == BindingSlot.Gamepad && info.GamepadFixed)
            {
                button.Disabled = true;
            }

            _slotButtons[(info.Id, slot)] = button;
            _firstSlot ??= button;
            row.AddChild(button);
        }

        return row;
    }

    private Control BuildFooter()
    {
        var row = UiKit.MakeRow(24);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        var reset = UiKit.MakeButton("Restaurar padrão", ResetDefaults, 520);
        var back = UiKit.MakeButton("Voltar", Back, 360);
        row.AddChild(reset);
        row.AddChild(back);
        return row;
    }

    private void StartCapture(string action, BindingSlot slot)
    {
        _capturing = (action, slot);
        var device = slot == BindingSlot.Gamepad ? "botão do controle" : "tecla ou botão do mouse";
        _status.Text = $"{InputActions.LabelOf(action)}: pressione {device} (Esc cancela, Apagar remove)";
        _slotButtons[(action, slot)].Text = "...";
        _timeout.Start();
    }

    private void CancelCapture()
    {
        _capturing = null;
        _timeout.Stop();
        _status.Text = string.Empty;
        RefreshAll();
    }

    private void AssignBinding(string action, BindingSlot slot, string code)
    {
        var conflict = InputSetup.Assign(action, slot, code);
        EndCapture(conflict is null
            ? $"{InputActions.LabelOf(action)}: {GlyphNames.Describe(code, GlyphNames.DetectFamily())}"
            : $"Já usado em {InputActions.LabelOf(conflict)}: as duas ações trocaram.");
    }

    private void ClearSlot(string action, BindingSlot slot)
    {
        if (slot == BindingSlot.Primary)
        {
            EndCapture("A coluna Principal não pode ficar vazia.");
            return;
        }

        InputSetup.Clear(action, slot);
        EndCapture($"{InputActions.LabelOf(action)}: removido.");
    }

    private void EndCapture(string message)
    {
        _capturing = null;
        _timeout.Stop();
        _status.Text = message;
        RefreshAll();
    }

    private void ResetDefaults()
    {
        InputSetup.ResetToDefaults();
        _status.Text = "Controles restaurados ao padrão.";
        RefreshAll();
    }

    private void RefreshAll()
    {
        var family = GlyphNames.DetectFamily();
        _padLabel.Text = $"Controle: {GlyphNames.ConnectedPadLabel()}";
        foreach (var ((action, slot), button) in _slotButtons)
        {
            if (slot == BindingSlot.Gamepad && button.Disabled)
            {
                button.Text = FixedPadText;
                continue;
            }

            var code = InputSetup.Table.Get(action, slot);
            button.Text = string.IsNullOrEmpty(code) ? "-" : GlyphNames.Describe(code, family);
        }
    }

    private void OnJoyConnectionChanged(long device, bool connected) => RefreshAll();
}
