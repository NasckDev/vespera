using Godot;
using Vespera.UI.Common;

namespace Vespera.UI.Widgets;

/// <summary>
/// Tecla desenhada (<c>keyCap</c>, <c>core.js:136</c>): caixa escura com borda dourada e o rótulo da tecla. Mostra a
/// tecla do teclado ou o botão do gamepad conforme o último dispositivo usado (<see cref="InputHints"/>).
/// </summary>
[GlobalClass]
public partial class KeyCap : PanelContainer
{
    private readonly Label _label = new();
    private string _keyboard = string.Empty;
    private string _gamepad = string.Empty;

    public KeyCap()
    {
        ThemeTypeVariation = "KeyCap";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.AddThemeFontOverride("font", UiFonts.TextBold);
        _label.AddThemeFontSizeOverride("font_size", UiFonts.Tiny);
        _label.AddThemeConstantOverride("shadow_offset_y", 0);
        AddChild(_label);
    }

    /// <summary>Tecla com rótulos explícitos (teclado, gamepad).</summary>
    public static KeyCap Create(string keyboard, string? gamepad = null, int fontSize = UiFonts.Tiny)
    {
        var cap = new KeyCap();
        cap.SetKeys(keyboard, gamepad ?? keyboard);
        cap._label.AddThemeFontSizeOverride("font_size", fontSize);
        return cap;
    }

    /// <summary>Tecla da ação do Input Map (rótulos de <see cref="InputHints"/>).</summary>
    public static KeyCap ForAction(string action, int fontSize = UiFonts.Tiny)
    {
        var cap = new KeyCap();
        cap._label.AddThemeFontSizeOverride("font_size", fontSize);
        cap.SetAction(action);
        return cap;
    }

    public void SetKeys(string keyboard, string gamepad)
    {
        _keyboard = keyboard;
        _gamepad = gamepad;
        Refresh();
    }

    public void SetAction(string action) =>
        SetKeys(InputHints.LabelFor(action, gamepad: false), InputHints.LabelFor(action, gamepad: true));

    public override void _EnterTree() => InputHints.DeviceChanged += Refresh;

    public override void _ExitTree() => InputHints.DeviceChanged -= Refresh;

    private void Refresh() => _label.Text = InputHints.UsingGamepad ? _gamepad : _keyboard;
}
