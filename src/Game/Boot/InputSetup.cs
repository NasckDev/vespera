using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>
/// Cria as ações nomeadas na <c>InputMap</c> (teclado, mouse e controle), aplica o remapeamento do jogador
/// e o persiste em <c>user://bindings.json</c>. As ações <c>ui_*</c> são as da engine.
/// </summary>
public static class InputSetup
{
    private const int FileVersion = 1;
    private const float ActionDeadzone = 0.5f;
    private const float MoveDeadzone = 0.2f;
    private const float CaptureAxisThreshold = 0.6f;

    private static readonly Dictionary<string, JoyButton> MoveDpad = new()
    {
        [InputActions.MoveUp] = JoyButton.DpadUp,
        [InputActions.MoveDown] = JoyButton.DpadDown,
        [InputActions.MoveLeft] = JoyButton.DpadLeft,
        [InputActions.MoveRight] = JoyButton.DpadRight,
    };

    public static BindingTable Table { get; private set; } = BindingTable.Defaults();

    public static event Action? Changed;

    public static void Initialize()
    {
        Table = Load();
        Apply();
    }

    public static string? Assign(string action, BindingSlot slot, string code)
    {
        var conflict = Table.Assign(action, slot, code);
        CommitChange();
        return conflict;
    }

    public static void Clear(string action, BindingSlot slot)
    {
        Table.Clear(action, slot);
        CommitChange();
    }

    public static void ResetToDefaults()
    {
        Table = BindingTable.Defaults();
        CommitChange();
    }

    public static void Apply()
    {
        foreach (var action in Table.Actions)
        {
            var isMove = MoveDpad.ContainsKey(action);
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action, isMove ? MoveDeadzone : ActionDeadzone);
            }

            InputMap.ActionEraseEvents(action);
            foreach (var slot in Enum.GetValues<BindingSlot>())
            {
                var ev = ToEvent(Table.Get(action, slot));
                if (ev is not null)
                {
                    InputMap.ActionAddEvent(action, ev);
                }
            }

            if (MoveDpad.TryGetValue(action, out var dpad))
            {
                InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = dpad });
            }
        }
    }

    /// <summary>Converte um código salvo (<c>key:87</c>) em evento da engine; null para vazio ou inválido.</summary>
    public static InputEvent? ToEvent(string code)
    {
        var parts = code.Split(':', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        switch (parts[0])
        {
            case "key" when long.TryParse(parts[1], out var key):
                return new InputEventKey { PhysicalKeycode = (Key)key };
            case "mouse" when int.TryParse(parts[1], out var button):
                return new InputEventMouseButton { ButtonIndex = (MouseButton)button };
            case "pad" when int.TryParse(parts[1], out var pad):
                return new InputEventJoypadButton { ButtonIndex = (JoyButton)pad };
            case "axis" when parts[1].Length >= 2 && int.TryParse(parts[1][..^1], out var axis):
                return new InputEventJoypadMotion { Axis = (JoyAxis)axis, AxisValue = parts[1][^1] == '-' ? -1f : 1f };
            default:
                return null;
        }
    }

    /// <summary>Converte um evento capturado em código para a coluna pedida; null se o evento não serve a ela.</summary>
    public static string? FromEvent(InputEvent ev, BindingSlot slot)
    {
        if (slot == BindingSlot.Gamepad)
        {
            return ev switch
            {
                InputEventJoypadButton { Pressed: true } b => $"pad:{(int)b.ButtonIndex}",
                InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) >= CaptureAxisThreshold =>
                    $"axis:{(int)m.Axis}{(m.AxisValue < 0 ? '-' : '+')}",
                _ => null,
            };
        }

        return ev switch
        {
            InputEventKey { Pressed: true, Echo: false } k => $"key:{(long)(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode)}",
            InputEventMouseButton { Pressed: true } mb when mb.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle =>
                $"mouse:{(int)mb.ButtonIndex}",
            _ => null,
        };
    }

    private static void CommitChange()
    {
        Apply();
        Save();
        Changed?.Invoke();
    }

    private static BindingTable Load()
    {
        var json = UserFile.ReadText(UserPaths.BindingsFile);
        if (json is null)
        {
            return BindingTable.Defaults();
        }

        try
        {
            var file = JsonSerializer.Deserialize<BindingsFile>(json);
            return BindingTable.FromDictionary(file?.Actions);
        }
        catch (JsonException ex)
        {
            Log.Warn($"bindings.json inválido, usando padrões: {ex.Message}");
            return BindingTable.Defaults();
        }
    }

    private static void Save()
    {
        var file = new BindingsFile { Version = FileVersion, Actions = Table.ToDictionary() };
        UserFile.WriteText(UserPaths.BindingsFile, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class BindingsFile
    {
        public int Version { get; set; }

        public Dictionary<string, string[]> Actions { get; set; } = [];
    }
}
