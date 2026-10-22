using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.Autoload;

/// <summary>Resultado de <see cref="InputBindings.Assign"/>.</summary>
public enum RemapResult
{
    /// <summary>Tecla nova e livre.</summary>
    Ok,

    /// <summary>A tecla era a principal de outra ação: as duas trocaram (a outra ficou com a antiga desta).</summary>
    Swapped,

    /// <summary>A tecla já é de outra ação (secundária, como as setas do movimento): nada mudou.</summary>
    Conflict,

    /// <summary>Evento que não serve para esta coluna (tecla no gamepad, botão do sistema…).</summary>
    Invalid,
}

/// <summary>Ação remapeável na tela de Opções.</summary>
public readonly record struct RemappableAction(StringName Action, string Label, bool Pad);

/// <summary>
/// Remapeamento de teclado e gamepad (frente C1, DECISIONS C1-D6). Cada ação tem uma tecla **principal** (o primeiro
/// evento de teclado/mouse no Input Map) e um botão principal (o primeiro evento de gamepad); remapear troca só esse
/// evento, mantendo os secundários (setas, clique do mouse, direcional). Conflito com a principal de outra ação troca
/// as duas; com uma secundária é recusado. As trocas vão para <c>user://settings.cfg</c> (<c>[input_keys]</c> e
/// <c>[input_pad]</c>, só o que difere do padrão) e são aplicadas no boot; "restaurar padrão" recarrega o
/// <c>project.godot</c>. Rótulos curtos para a UI em <see cref="Label(StringName, bool)"/>.
/// </summary>
public static class InputBindings
{
    private const string KeysSection = "input_keys";
    private const string PadSection = "input_pad";

    /// <summary>Ações que a tela de Opções deixa remapear (o movimento no gamepad fica no analógico).</summary>
    public static readonly RemappableAction[] Remappable =
    [
        new(InputActions.MoveUp, "Mover para cima", false),
        new(InputActions.MoveDown, "Mover para baixo", false),
        new(InputActions.MoveLeft, "Mover para a esquerda", false),
        new(InputActions.MoveRight, "Mover para a direita", false),
        new(InputActions.Attack, "Atacar", true),
        new(InputActions.AttackHeavy, "Ataque forte", true),
        new(InputActions.Dodge, "Esquivar", true),
        new(InputActions.Interact, "Interagir", true),
        new(InputActions.Potion, "Poção", true),
        new(InputActions.Eco1, "Eco 1", true),
        new(InputActions.Eco2, "Eco 2", true),
        new(InputActions.Ultimate, "Golpe Supremo", true),
        new(InputActions.Inventory, "Inventário", true),
        new(InputActions.Map, "Mapa", true),
        new(InputActions.Pause, "Menu", true),
    ];

    private static readonly Dictionary<StringName, (string? Keyboard, string? Pad)> LabelCache = [];

    /// <summary>Alguma tecla mudou (as dicas de tecla da UI se atualizam).</summary>
    public static event Action? Changed;

    /// <summary>Aplica as trocas gravadas por cima do Input Map do projeto. Chamado no boot (Main).</summary>
    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(FilePath) != Error.Ok)
        {
            return;
        }

        foreach (var r in Remappable)
        {
            if (config.HasSectionKey(KeysSection, r.Action) && Decode((string)config.GetValue(KeysSection, r.Action)) is { } key && !IsPad(key))
            {
                Replace(r.Action, pad: false, key);
            }

            if (r.Pad && config.HasSectionKey(PadSection, r.Action) && Decode((string)config.GetValue(PadSection, r.Action)) is { } button && IsPad(button))
            {
                Replace(r.Action, pad: true, button);
            }
        }

        Invalidate();
    }

    /// <summary>Evento principal da ação no teclado/mouse (<paramref name="pad"/> = false) ou no gamepad.</summary>
    public static InputEvent? Primary(StringName action, bool pad)
    {
        if (!InputMap.HasAction(action))
        {
            return null;
        }

        foreach (var e in InputMap.ActionGetEvents(action))
        {
            if (IsPad(e) == pad && IsBindable(e))
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>Rótulo curto da tecla principal ("E", "Espaço", "Clique", "A", "LB", "RT"); null se não houver.</summary>
    public static string? Label(StringName action, bool pad)
    {
        if (!LabelCache.TryGetValue(action, out var labels))
        {
            labels = (Describe(Primary(action, false)), Describe(Primary(action, true)));
            LabelCache[action] = labels;
        }

        return pad ? labels.Pad : labels.Keyboard;
    }

    /// <summary>Nome curto de um evento de entrada para a UI.</summary>
    public static string? Describe(InputEvent? e) => e switch
    {
        InputEventKey k => KeyName(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton m => m.ButtonIndex switch
        {
            MouseButton.Left => "Clique",
            MouseButton.Right => "Clique dir.",
            MouseButton.Middle => "Roda",
            MouseButton.WheelUp => "Roda ↑",
            MouseButton.WheelDown => "Roda ↓",
            _ => $"Mouse {(int)m.ButtonIndex}",
        },
        InputEventJoypadButton b => PadButtonName(b.ButtonIndex),
        InputEventJoypadMotion a => a.Axis switch
        {
            JoyAxis.TriggerLeft => "LT",
            JoyAxis.TriggerRight => "RT",
            JoyAxis.LeftX => a.AxisValue < 0 ? "L ←" : "L →",
            JoyAxis.LeftY => a.AxisValue < 0 ? "L ↑" : "L ↓",
            JoyAxis.RightX => a.AxisValue < 0 ? "R ←" : "R →",
            JoyAxis.RightY => a.AxisValue < 0 ? "R ↑" : "R ↓",
            _ => $"Eixo {(int)a.Axis}",
        },
        _ => null,
    };

    /// <summary>Que ação remapeável já usa este evento (qualquer um, principal ou secundário), menos <paramref name="except"/>.</summary>
    public static StringName? Owner(InputEvent e, StringName? except = null)
    {
        foreach (var r in Remappable)
        {
            if (except is not null && r.Action == except)
            {
                continue;
            }

            foreach (var other in InputMap.ActionGetEvents(r.Action))
            {
                if (Same(other, e))
                {
                    return r.Action;
                }
            }
        }

        return null;
    }

    /// <summary>Nome da ação remapeável para a UI ("Atacar").</summary>
    public static string ActionLabel(StringName action)
    {
        foreach (var r in Remappable)
        {
            if (r.Action == action)
            {
                return r.Label;
            }
        }

        return action.ToString();
    }

    /// <summary>
    /// Troca a tecla principal de <paramref name="action"/> por <paramref name="e"/>. Se ela já for a principal de
    /// outra ação remapeável, as duas trocam (<paramref name="other"/> diz qual); se for secundária de outra, recusa.
    /// </summary>
    public static RemapResult Assign(StringName action, bool pad, InputEvent e, out StringName? other)
    {
        other = null;
        var normalized = Normalize(e);
        if (normalized is null || IsPad(normalized) != pad || !IsBindable(normalized))
        {
            return RemapResult.Invalid;
        }

        var current = Primary(action, pad);
        if (current is not null && Same(current, normalized))
        {
            return RemapResult.Ok;
        }

        var result = RemapResult.Ok;
        if (Owner(normalized, action) is { } owner)
        {
            other = owner;
            if (Primary(owner, pad) is not { } ownerPrimary || !Same(ownerPrimary, normalized) || current is null)
            {
                return RemapResult.Conflict;
            }

            Replace(owner, pad, Normalize(current)!);
            result = RemapResult.Swapped;
        }
        else if (Owner(normalized) is { } self && self == action)
        {
            // Já era secundária desta ação (ex.: ↑ em "Mover para cima"): vira a principal e a antiga sai.
            RemoveEvent(action, normalized);
        }

        Replace(action, pad, normalized);
        Persist();
        Invalidate();
        return result;
    }

    /// <summary>Volta todas as teclas ao padrão do projeto.</summary>
    public static void RestoreDefaults()
    {
        InputMap.LoadFromProjectSettings();
        var config = new ConfigFile();
        config.Load(FilePath);
        if (config.HasSection(KeysSection))
        {
            config.EraseSection(KeysSection);
        }

        if (config.HasSection(PadSection))
        {
            config.EraseSection(PadSection);
        }

        config.Save(FilePath);
        Invalidate();
    }

    /// <summary>Texto gravado de um evento (<c>k:código</c>, <c>m:botão</c>, <c>b:botão</c>, <c>a:eixo:sinal</c>).</summary>
    public static string? Encode(InputEvent e) => e switch
    {
        InputEventKey k => $"k:{(long)(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode)}",
        InputEventMouseButton m => $"m:{(int)m.ButtonIndex}",
        InputEventJoypadButton b => $"b:{(int)b.ButtonIndex}",
        InputEventJoypadMotion a => $"a:{(int)a.Axis}:{(a.AxisValue < 0 ? -1 : 1)}",
        _ => null,
    };

    public static InputEvent? Decode(string text)
    {
        var parts = text.Split(':');
        if (parts.Length < 2 || !long.TryParse(parts[1], out var code))
        {
            return null;
        }

        return parts[0] switch
        {
            "k" => new InputEventKey { PhysicalKeycode = (Key)code },
            "m" => new InputEventMouseButton { ButtonIndex = (MouseButton)code },
            "b" => new InputEventJoypadButton { ButtonIndex = (JoyButton)code },
            "a" when parts.Length == 3 && int.TryParse(parts[2], out var sign) => new InputEventJoypadMotion { Axis = (JoyAxis)code, AxisValue = sign < 0 ? -1f : 1f },
            _ => null,
        };
    }

    /// <summary>Evento limpo para o Input Map (tecla física, sem modificadores; eixo só com o sinal).</summary>
    public static InputEvent? Normalize(InputEvent e) => e switch
    {
        InputEventKey k => new InputEventKey { PhysicalKeycode = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode },
        InputEventMouseButton m => new InputEventMouseButton { ButtonIndex = m.ButtonIndex },
        InputEventJoypadButton b => new InputEventJoypadButton { ButtonIndex = b.ButtonIndex },
        InputEventJoypadMotion a when MathF.Abs(a.AxisValue) >= 0.5f => new InputEventJoypadMotion { Axis = a.Axis, AxisValue = a.AxisValue < 0 ? -1f : 1f },
        _ => null,
    };

    private static string FilePath => AudioManager.Instance?.SettingsFile ?? AudioManager.SettingsPath;

    private static bool IsPad(InputEvent e) => e is InputEventJoypadButton or InputEventJoypadMotion;

    /// <summary>Teclas e botões que a tela aceita (Esc, Enter e o Start ficam reservados para os menus).</summary>
    private static bool IsBindable(InputEvent e) => e switch
    {
        InputEventKey k => (k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode) is not (Key.None or Key.Enter or Key.KpEnter),
        InputEventMouseButton m => m.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle or MouseButton.Xbutton1 or MouseButton.Xbutton2,
        InputEventJoypadButton => true,
        InputEventJoypadMotion a => a.Axis is JoyAxis.TriggerLeft or JoyAxis.TriggerRight || MathF.Abs(a.AxisValue) >= 0.5f,
        _ => false,
    };

    private static bool Same(InputEvent a, InputEvent b) => a switch
    {
        InputEventKey ka when b is InputEventKey kb => Code(ka) == Code(kb),
        InputEventMouseButton ma when b is InputEventMouseButton mb => ma.ButtonIndex == mb.ButtonIndex,
        InputEventJoypadButton ba when b is InputEventJoypadButton bb => ba.ButtonIndex == bb.ButtonIndex,
        InputEventJoypadMotion aa when b is InputEventJoypadMotion ab => aa.Axis == ab.Axis && MathF.Sign(aa.AxisValue) == MathF.Sign(ab.AxisValue),
        _ => false,
    };

    private static Key Code(InputEventKey k) => k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;

    /// <summary>Troca o evento principal da coluna (mantém a posição na lista: os rótulos continuam vindo dele).</summary>
    private static void Replace(StringName action, bool pad, InputEvent e)
    {
        var events = InputMap.ActionGetEvents(action);
        InputMap.ActionEraseEvents(action);
        var replaced = false;
        foreach (var existing in events)
        {
            if (!replaced && IsPad(existing) == pad && IsBindable(existing))
            {
                InputMap.ActionAddEvent(action, e);
                replaced = true;
            }
            else if (!Same(existing, e))
            {
                InputMap.ActionAddEvent(action, existing);
            }
        }

        if (!replaced)
        {
            InputMap.ActionAddEvent(action, e);
        }
    }

    private static void RemoveEvent(StringName action, InputEvent e)
    {
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (Same(existing, e))
            {
                InputMap.ActionEraseEvent(action, existing);
            }
        }
    }

    /// <summary>Grava só as principais que diferem do <c>project.godot</c>.</summary>
    private static void Persist()
    {
        var config = new ConfigFile();
        config.Load(FilePath);
        foreach (var r in Remappable)
        {
            Write(config, KeysSection, r.Action, Primary(r.Action, false), DefaultPrimary(r.Action, false));
            if (r.Pad)
            {
                Write(config, PadSection, r.Action, Primary(r.Action, true), DefaultPrimary(r.Action, true));
            }
        }

        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            Log.Warning($"Não foi possível gravar as teclas em {FilePath}: {error}");
        }
    }

    private static void Write(ConfigFile config, string section, StringName action, InputEvent? current, InputEvent? fallback)
    {
        if (current is not null && (fallback is null || !Same(current, fallback)) && Encode(current) is { } text)
        {
            config.SetValue(section, action, text);
        }
        else if (config.HasSectionKey(section, action))
        {
            config.EraseSectionKey(section, action);
        }
    }

    private static InputEvent? DefaultPrimary(StringName action, bool pad)
    {
        var setting = ProjectSettings.GetSetting($"input/{action}");
        if (setting.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        var dict = setting.AsGodotDictionary();
        if (!dict.TryGetValue("events", out var list))
        {
            return null;
        }

        foreach (var item in list.AsGodotArray())
        {
            if (item.AsGodotObject() is InputEvent e && IsPad(e) == pad && IsBindable(e))
            {
                return e;
            }
        }

        return null;
    }

    private static void Invalidate()
    {
        LabelCache.Clear();
        Changed?.Invoke();
    }

    private static string KeyName(Key key) => key switch
    {
        Key.Space => "Espaço",
        Key.Shift => "Shift",
        Key.Ctrl => "Ctrl",
        Key.Alt => "Alt",
        Key.Escape => "Esc",
        Key.Tab => "Tab",
        Key.Backspace => "⌫",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        _ => OS.GetKeycodeString(key),
    };

    private static string PadButtonName(JoyButton button) => button switch
    {
        JoyButton.A => "A",
        JoyButton.B => "B",
        JoyButton.X => "X",
        JoyButton.Y => "Y",
        JoyButton.Back => "Back",
        JoyButton.Start => "Start",
        JoyButton.Guide => "Guia",
        JoyButton.LeftStick => "L3",
        JoyButton.RightStick => "R3",
        JoyButton.LeftShoulder => "LB",
        JoyButton.RightShoulder => "RB",
        JoyButton.DpadUp => "D↑",
        JoyButton.DpadDown => "D↓",
        JoyButton.DpadLeft => "D←",
        JoyButton.DpadRight => "D→",
        _ => $"Botão {(int)button}",
    };
}
