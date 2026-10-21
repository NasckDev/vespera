using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;

namespace Vespera.UI.Common;

/// <summary>
/// Sabe se o jogador está no teclado/mouse ou no gamepad (último dispositivo usado) e dá o rótulo da tecla de cada
/// ação para as dicas da UI (<see cref="Widgets.KeyCap"/>). Alimente com <see cref="Observe"/> no <c>_Input</c> de quem
/// tem a UI aberta (GameUi e título).
/// </summary>
public static class InputHints
{
    private const float StickThreshold = 0.5f;

    /// <summary>Rótulos (teclado, gamepad) por ação; teclas da v2 (spec 01 §5.1) e botões Xbox.</summary>
    private static readonly Dictionary<string, (string Keyboard, string Gamepad)> Labels = new()
    {
        [InputActions.Interact] = ("E", "A"),
        [InputActions.Attack] = ("J", "X"),
        [InputActions.Dodge] = ("Espaço", "B"),
        [InputActions.Pause] = ("Esc", "Start"),
        [InputActions.Inventory] = ("Tab", "Back"),
        [InputActions.Lantern] = ("F", "RB"),
        ["ui_accept"] = ("Enter", "A"),
        ["ui_cancel"] = ("Esc", "B"),
        ["ui_up"] = ("↑↓", "↑↓"),
        ["ui_left"] = ("← →", "← →"),
    };

    public static bool UsingGamepad { get; private set; }

    /// <summary>Disparado quando troca de teclado para gamepad (ou vice-versa).</summary>
    public static event Action? DeviceChanged;

    public static void Observe(InputEvent @event)
    {
        var gamepad = @event switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion motion => Mathf.Abs(motion.AxisValue) > StickThreshold ? true : UsingGamepad,
            InputEventKey or InputEventMouseButton => false,
            InputEventMouseMotion motion => motion.Relative.LengthSquared() > 4f ? false : UsingGamepad,
            _ => UsingGamepad,
        };

        if (gamepad != UsingGamepad)
        {
            UsingGamepad = gamepad;
            DeviceChanged?.Invoke();
        }
    }

    /// <summary>Rótulo da tecla da ação no dispositivo atual (ex.: <c>interact</c> → "E" ou "A").</summary>
    public static string Label(string action) => LabelFor(action, UsingGamepad);

    /// <summary>Rótulo da tecla da ação num dispositivo específico.</summary>
    public static string LabelFor(string action, bool gamepad) =>
        Labels.TryGetValue(action, out var label) ? (gamepad ? label.Gamepad : label.Keyboard) : action;

    /// <summary>Rótulo para um par explícito (teclado, gamepad).</summary>
    public static string Label(string keyboard, string gamepad) => UsingGamepad ? gamepad : keyboard;
}
