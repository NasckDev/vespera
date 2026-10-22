using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;

namespace Vespera.UI.Screens;

/// <summary>
/// Tabela de controles (<c>screenControls</c>, <c>extra.js:194-198</c>) com a coluna do gamepad (D9). As teclas vêm do
/// Input Map (POLI-4): depois de remapear, a tabela mostra as teclas novas (e as secundárias que ficaram).
/// </summary>
public partial class ControlsScreen : FrameScreen
{
    private readonly Control _focusAnchor;
    private readonly List<(Label Keys, Label Pad, Func<bool, string> Read)> _rows = [];

    public ControlsScreen()
        : base(UiText.MenuControls)
    {
        var grid = new GridContainer { Columns = 3, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 3);
        grid.AddChild(Ui.Text("Teclado e mouse", UiFonts.Small, UiPalette.Sub));
        grid.AddChild(Ui.Text("Gamepad", UiFonts.Small, UiPalette.Sub));
        grid.AddChild(Ui.Text(string.Empty));
        AddRow(grid, UiText.ControlMove, pad => pad ? UiText.ControlMovePad : Move());
        AddRow(grid, UiText.ControlAim, pad => pad ? UiText.ControlAimPad : UiText.ControlAimMouse);
        AddRow(grid, UiText.ControlAttack, pad => Keys(InputActions.Attack, pad));
        AddRow(grid, UiText.ControlHeavy, pad => Keys(InputActions.AttackHeavy, pad));
        AddRow(grid, UiText.ControlDodge, pad => Keys(InputActions.Dodge, pad));
        AddRow(grid, UiText.ControlUltimate, pad => Keys(InputActions.Ultimate, pad));
        AddRow(grid, UiText.ControlEco, pad => $"{Keys(InputActions.Eco1, pad)} / {Keys(InputActions.Eco2, pad)}");
        AddRow(grid, UiText.ControlPotion, pad => Keys(InputActions.Potion, pad));
        AddRow(grid, UiText.ControlInteract, pad => Keys(InputActions.Interact, pad));
        AddRow(grid, UiText.ControlInventory, pad => Keys(InputActions.Inventory, pad));
        AddRow(grid, UiText.ControlMap, pad => Keys(InputActions.Map, pad));
        AddRow(grid, UiText.ControlPause, pad => Keys(InputActions.Pause, pad));

        var panel = Ui.Panel("InsetPanel", Ui.Margin(6, 4, 6, 4, grid));
        Body.AddChild(panel);
        Body.AddChild(Ui.Spacer(horizontal: false));
        Body.AddChild(Ui.Text(UiText.ControlsTip, UiFonts.Small, UiPalette.Sub, wrap: true));

        // Tela sem itens navegáveis: um controle focável invisível segura o foco (Esc/B volta).
        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    /// <summary>Os rótulos da coluna de teclado, na ordem das linhas (smoke test).</summary>
    public IReadOnlyList<string> KeyboardColumn => Column(pad: false);

    /// <summary>Os rótulos da coluna do gamepad, na ordem das linhas (smoke test).</summary>
    public IReadOnlyList<string> PadColumn => Column(pad: true);

    protected override Control? DefaultFocus => _focusAnchor;

    public override void _EnterTree() => InputBindings.Changed += Refresh;

    public override void _ExitTree() => InputBindings.Changed -= Refresh;

    protected override void OnOpened() => Refresh();

    private static string Keys(StringName action, bool pad)
    {
        var labels = InputBindings.Labels(action, pad);
        return labels.Count == 0 ? "-" : string.Join(", ", labels);
    }

    /// <summary>"WASD / Setas": as quatro teclas principais juntas; "/ Setas" enquanto as setas continuarem como secundárias.</summary>
    private static string Move()
    {
        StringName[] actions = [InputActions.MoveUp, InputActions.MoveLeft, InputActions.MoveDown, InputActions.MoveRight];
        var main = string.Empty;
        var secondary = true;
        foreach (var action in actions)
        {
            var labels = InputBindings.Labels(action, pad: false);
            main += labels.Count > 0 ? labels[0] : "-";
            secondary &= labels.Count > 1;
        }

        return secondary ? $"{main} / Setas" : main;
    }

    private void AddRow(GridContainer grid, string action, Func<bool, string> read)
    {
        var keys = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar);
        var pad = Ui.Text(string.Empty, UiFonts.Body, UiPalette.SubLight);
        grid.AddChild(keys);
        grid.AddChild(pad);
        grid.AddChild(Ui.Text(action, UiFonts.Body));
        _rows.Add((keys, pad, read));
    }

    private void Refresh()
    {
        foreach (var (keys, pad, read) in _rows)
        {
            keys.Text = read(false);
            pad.Text = read(true);
        }
    }

    private List<string> Column(bool pad)
    {
        var list = new List<string>();
        foreach (var row in _rows)
        {
            list.Add(row.Read(pad));
        }

        return list;
    }
}
