using System;
using Godot;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>Confirmação com duas escolhas (ex.: "Começar do zero?", <c>extra.js:281-286</c>). Foco começa em "Cancelar".</summary>
public partial class ConfirmScreen : Screen
{
    private readonly Label _title;
    private readonly Label _text;
    private readonly SelectableRow _confirm;
    private readonly SelectableRow _cancel;
    private readonly Label _confirmLabel;
    private readonly Label _cancelLabel;
    private Action? _onConfirm;

    public ConfirmScreen()
    {
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.7f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = Ui.Ornate(variation: "OrnatePanelDanger");
        panel.CornerColor = UiPalette.Brecha;
        panel.CustomMinimumSize = new Vector2(260, 0);
        center.AddChild(panel);

        _title = Ui.Title(string.Empty, UiFonts.Heading);
        _text = Ui.Text(string.Empty, UiFonts.Small, UiPalette.Body, wrap: true);
        _text.CustomMinimumSize = new Vector2(240, 0);
        (_confirm, _confirmLabel) = Button("ButtonDanger");
        (_cancel, _cancelLabel) = Button("ButtonNormal");
        _confirm.OnActivate = () =>
        {
            var action = _onConfirm;
            Close();
            action?.Invoke();
        };
        _cancel.OnActivate = Close;
        var buttons = Ui.Row(8, _confirm, _cancel);
        panel.AddChild(Ui.Margin(6, 4, 6, 4, Ui.Column(8, _title, _text, buttons)));
    }

    protected override Control? DefaultFocus => _cancel;

    public SelectableRow ConfirmButton => _confirm;

    public void Setup(string title, string text, string confirm, string cancel, Action onConfirm)
    {
        _title.Text = title;
        _text.Text = text;
        _confirmLabel.Text = confirm;
        _cancelLabel.Text = cancel;
        _onConfirm = onConfirm;
    }

    private static (SelectableRow Row, Label Label) Button(string style)
    {
        var label = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Creme, HorizontalAlignment.Center);
        var row = new SelectableRow
        {
            NormalStyle = style,
            FocusStyle = "ButtonFocus",
            Marker = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 18),
        };
        row.AddChild(label);
        return (row, label);
    }
}
