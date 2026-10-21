using Godot;
using Vespera.UI.Common;

namespace Vespera.UI.Screens;

/// <summary>Tabela de controles (<c>screenControls</c>, <c>extra.js:194-198</c>) com a coluna do gamepad (D9).</summary>
public partial class ControlsScreen : FrameScreen
{
    private readonly Control _focusAnchor;

    public ControlsScreen()
        : base(UiText.MenuControls)
    {
        var grid = new GridContainer { Columns = 3, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 5);
        grid.AddChild(Ui.Text("Teclado e mouse", UiFonts.Small, UiPalette.Sub));
        grid.AddChild(Ui.Text("Gamepad", UiFonts.Small, UiPalette.Sub));
        grid.AddChild(Ui.Text(string.Empty));
        foreach (var (keys, pad, action) in UiText.Controls)
        {
            grid.AddChild(Ui.Bold(keys, UiFonts.Body, UiPalette.Ambar));
            grid.AddChild(Ui.Text(pad, UiFonts.Body, UiPalette.SubLight));
            grid.AddChild(Ui.Text(action, UiFonts.Body));
        }

        var panel = Ui.Panel("InsetPanel", Ui.Margin(6, 4, 6, 4, grid));
        Body.AddChild(panel);
        Body.AddChild(Ui.Spacer(horizontal: false));
        Body.AddChild(Ui.Text(UiText.ControlsTip, UiFonts.Small, UiPalette.Sub, wrap: true));

        // Tela sem itens navegáveis: um controle focável invisível segura o foco (Esc/B volta).
        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    protected override Control? DefaultFocus => _focusAnchor;
}
