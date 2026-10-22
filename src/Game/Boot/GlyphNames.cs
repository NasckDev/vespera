using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Boot;

public enum PadFamily
{
    Xbox,
    PlayStation,
    Nintendo,
}

/// <summary>Nomes legíveis (pt-BR) das associações, conforme o tipo de controle conectado.</summary>
public static class GlyphNames
{
    private static readonly Dictionary<Key, string> KeyOverrides = new()
    {
        [Key.Space] = "Espaço",
        [Key.Escape] = "Esc",
        [Key.Up] = "Seta cima",
        [Key.Down] = "Seta baixo",
        [Key.Left] = "Seta esq.",
        [Key.Right] = "Seta dir.",
        [Key.Shift] = "Shift",
        [Key.Tab] = "Tab",
        [Key.Enter] = "Enter",
        [Key.Backspace] = "Apagar",
    };

    public static string Describe(string code, PadFamily family)
    {
        var parts = code.Split(':', 2);
        if (parts.Length != 2)
        {
            return "-";
        }

        return parts[0] switch
        {
            "key" when long.TryParse(parts[1], out var key) => KeyName((Key)key),
            "mouse" when int.TryParse(parts[1], out var button) => MouseName(button),
            "pad" when int.TryParse(parts[1], out var pad) => PadButtonName(pad, family),
            "axis" => AxisName(parts[1], family),
            _ => "-",
        };
    }

    public static PadFamily DetectFamily()
    {
        var pads = Input.GetConnectedJoypads();
        if (pads.Count == 0)
        {
            return PadFamily.Xbox;
        }

        var name = Input.GetJoyName(pads[0]).ToLowerInvariant();
        if (name.Contains("playstation") || name.Contains("dualshock") || name.Contains("dualsense") || name.Contains("sony") || name.StartsWith("ps"))
        {
            return PadFamily.PlayStation;
        }

        return name.Contains("nintendo") || name.Contains("switch") ? PadFamily.Nintendo : PadFamily.Xbox;
    }

    public static string ConnectedPadLabel()
    {
        var pads = Input.GetConnectedJoypads();
        return pads.Count == 0 ? "nenhum controle detectado" : Input.GetJoyName(pads[0]);
    }

    private static string KeyName(Key key)
    {
        if (KeyOverrides.TryGetValue(key, out var name))
        {
            return name;
        }

        var text = OS.GetKeycodeString(key);
        return string.IsNullOrEmpty(text) ? "?" : text;
    }

    private static string MouseName(int button) => button switch
    {
        1 => "Mouse esq.",
        2 => "Mouse dir.",
        3 => "Mouse meio",
        _ => $"Mouse {button}",
    };

    private static string PadButtonName(int index, PadFamily family) => (family, index) switch
    {
        (PadFamily.PlayStation, 0) => "Cruz",
        (PadFamily.PlayStation, 1) => "Círculo",
        (PadFamily.PlayStation, 2) => "Quadrado",
        (PadFamily.PlayStation, 3) => "Triângulo",
        (PadFamily.PlayStation, 4) => "Share",
        (PadFamily.PlayStation, 6) => "Options",
        (PadFamily.PlayStation, 9) => "L1",
        (PadFamily.PlayStation, 10) => "R1",
        (PadFamily.Nintendo, 0) => "B",
        (PadFamily.Nintendo, 1) => "A",
        (PadFamily.Nintendo, 2) => "Y",
        (PadFamily.Nintendo, 3) => "X",
        (PadFamily.Nintendo, 4) => "Menos",
        (PadFamily.Nintendo, 6) => "Mais",
        (PadFamily.Nintendo, 9) => "L",
        (PadFamily.Nintendo, 10) => "R",
        (_, 0) => "A",
        (_, 1) => "B",
        (_, 2) => "X",
        (_, 3) => "Y",
        (_, 4) => "Voltar",
        (_, 6) => "Menu",
        (_, 7) => "Analógico esq.",
        (_, 8) => "Analógico dir.",
        (_, 9) => "LB",
        (_, 10) => "RB",
        (_, 11) => "Direcional cima",
        (_, 12) => "Direcional baixo",
        (_, 13) => "Direcional esq.",
        (_, 14) => "Direcional dir.",
        _ => $"Botão {index}",
    };

    private static string AxisName(string spec, PadFamily family)
    {
        if (spec.Length < 2 || !int.TryParse(spec[..^1], out var axis))
        {
            return "-";
        }

        var positive = spec[^1] == '+';
        var ps = family == PadFamily.PlayStation;
        return axis switch
        {
            0 => positive ? "Analóg. dir." : "Analóg. esq.",
            1 => positive ? "Analóg. baixo" : "Analóg. cima",
            2 => positive ? "Analóg2 dir." : "Analóg2 esq.",
            3 => positive ? "Analóg2 baixo" : "Analóg2 cima",
            4 => ps ? "L2" : "LT",
            5 => ps ? "R2" : "RT",
            _ => $"Eixo {axis}",
        };
    }
}
