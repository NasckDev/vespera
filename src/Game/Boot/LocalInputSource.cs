using System;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>
/// Fonte de input de um jogador. O jogador nunca lê <c>Input</c> direto: recebe uma <see cref="IInputSource"/>
/// (teclado/mouse, controle N ou, depois, rede), o que deixa o co-op local entrar sem refazer nada (PLANO-V2 §3).
/// </summary>
public interface IInputSource
{
    /// <summary>Id do dispositivo: <see cref="LocalInputSource.KeyboardMouse"/> ou o índice do controle.</summary>
    int DeviceId { get; }

    Vector2 Move { get; }

    bool IsPressed(string action);

    bool IsJustPressed(string action);
}

/// <summary>Lê as associações atuais (<see cref="InputSetup.Table"/>) filtradas pelo dispositivo deste jogador.</summary>
public sealed class LocalInputSource(int deviceId) : IInputSource
{
    public const int KeyboardMouse = -1;
    private const float AxisPressThreshold = 0.5f;

    private readonly System.Collections.Generic.Dictionary<string, bool> _previous = [];

    public int DeviceId { get; } = deviceId;

    public Vector2 Move => new Vector2(Strength(InputActions.MoveRight) - Strength(InputActions.MoveLeft),
        Strength(InputActions.MoveDown) - Strength(InputActions.MoveUp)).LimitLength(1f);

    public bool IsPressed(string action) => Strength(action) >= AxisPressThreshold;

    /// <summary>Borda de subida desde a última consulta desta ação; chame uma vez por quadro por ação.</summary>
    public bool IsJustPressed(string action)
    {
        var now = IsPressed(action);
        var before = _previous.TryGetValue(action, out var was) && was;
        _previous[action] = now;
        return now && !before;
    }

    private float Strength(string action)
    {
        var strength = 0f;
        if (DeviceId == KeyboardMouse)
        {
            foreach (var slot in new[] { BindingSlot.Primary, BindingSlot.Alternate })
            {
                strength = Math.Max(strength, KeyboardStrength(InputSetup.Table.Get(action, slot)));
            }

            return strength;
        }

        strength = Math.Max(strength, PadStrength(InputSetup.Table.Get(action, BindingSlot.Gamepad)));
        return Math.Max(strength, DpadStrength(action));
    }

    private static float KeyboardStrength(string code)
    {
        var parts = code.Split(':', 2);
        if (parts.Length != 2)
        {
            return 0f;
        }

        return parts[0] switch
        {
            "key" when long.TryParse(parts[1], out var key) && Input.IsPhysicalKeyPressed((Key)key) => 1f,
            "mouse" when int.TryParse(parts[1], out var button) && Input.IsMouseButtonPressed((MouseButton)button) => 1f,
            _ => 0f,
        };
    }

    private float PadStrength(string code)
    {
        var parts = code.Split(':', 2);
        if (parts.Length != 2)
        {
            return 0f;
        }

        if (parts[0] == "pad" && int.TryParse(parts[1], out var button))
        {
            return Input.IsJoyButtonPressed(DeviceId, (JoyButton)button) ? 1f : 0f;
        }

        if (parts[0] == "axis" && parts[1].Length >= 2 && int.TryParse(parts[1][..^1], out var axis))
        {
            var value = Input.GetJoyAxis(DeviceId, (JoyAxis)axis) * (parts[1][^1] == '-' ? -1f : 1f);
            return Math.Max(0f, value);
        }

        return 0f;
    }

    private float DpadStrength(string action)
    {
        var button = action switch
        {
            InputActions.MoveUp => JoyButton.DpadUp,
            InputActions.MoveDown => JoyButton.DpadDown,
            InputActions.MoveLeft => JoyButton.DpadLeft,
            InputActions.MoveRight => JoyButton.DpadRight,
            _ => JoyButton.Invalid,
        };
        return button != JoyButton.Invalid && Input.IsJoyButtonPressed(DeviceId, button) ? 1f : 0f;
    }
}
