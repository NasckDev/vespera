using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>
/// Cria as ações nomeadas na <c>InputMap</c> (teclado, mouse e controle), aplica o remapeamento do jogador
/// e o persiste em <c>user://bindings.json</c>. As ações <c>ui_*</c> são as da engine. O jogador 2 do co-op local tem a
/// tabela dele (<see cref="Player2Table"/>, mesmo arquivo); ela não entra na <c>InputMap</c>: a fonte de input dele lê direto.
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

    /// <summary>Teclas do jogador 2 (metade direita do teclado ou o controle dele).</summary>
    public static BindingTable Player2Table { get; private set; } = BindingTable.DefaultsPlayer2();

    /// <summary>Tabela do jogador <paramref name="player"/> (0 = J1, 1+ = J2).</summary>
    public static BindingTable TableFor(int player) => player <= 0 ? Table : Player2Table;

    public static event Action? Changed;

    public static void Initialize()
    {
        (Table, Player2Table) = Load();
        Apply();
    }

    public static string? Assign(string action, BindingSlot slot, string code) => Assign(0, action, slot, code);

    public static string? Assign(int player, string action, BindingSlot slot, string code)
    {
        var conflict = TableFor(player).Assign(action, slot, code);
        CommitChange();
        return conflict;
    }

    public static void Clear(string action, BindingSlot slot) => Clear(0, action, slot);

    public static void Clear(int player, string action, BindingSlot slot)
    {
        TableFor(player).Clear(action, slot);
        CommitChange();
    }

    public static void ResetToDefaults() => ResetToDefaults(0);

    public static void ResetToDefaults(int player)
    {
        if (player <= 0)
        {
            Table = BindingTable.Defaults();
        }
        else
        {
            Player2Table = BindingTable.DefaultsPlayer2();
        }

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

    private static (BindingTable, BindingTable) Load()
    {
        var json = UserFile.ReadText(UserPaths.BindingsFile);
        if (json is null)
        {
            return (BindingTable.Defaults(), BindingTable.DefaultsPlayer2());
        }

        try
        {
            var file = JsonSerializer.Deserialize<BindingsFile>(json);
            return (BindingTable.FromDictionary(file?.Actions), BindingTable.FromDictionary(file?.Player2, BindingTable.DefaultsPlayer2()));
        }
        catch (JsonException ex)
        {
            Log.Warn($"bindings.json inválido, usando padrões: {ex.Message}");
            return (BindingTable.Defaults(), BindingTable.DefaultsPlayer2());
        }
    }

    private static void Save()
    {
        var file = new BindingsFile { Version = FileVersion, Actions = Table.ToDictionary(), Player2 = Player2Table.ToDictionary() };
        UserFile.WriteText(UserPaths.BindingsFile, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class BindingsFile
    {
        public int Version { get; set; }

        public Dictionary<string, string[]> Actions { get; set; } = [];

        /// <summary>Teclas do jogador 2 do co-op local (ausente em arquivos antigos: padrões).</summary>
        public Dictionary<string, string[]>? Player2 { get; set; }
    }
}
