using Godot;

namespace Vespera.UI.Common;

/// <summary>
/// Cores da interface: marca (CLAUDE.md §6) + tons dos painéis da v2 (<c>COL</c>, <c>data.js:3</c>; textos recorrentes,
/// spec 02 §4.10). Use estas constantes em vez de <c>new Color("#...")</c> espalhado.
/// </summary>
public static class UiPalette
{
    // --- Marca ---
    public static readonly Color Indigo = new("#1B1F3B");
    public static readonly Color Roxo = new("#4A2C6B");
    public static readonly Color Ambar = new("#F2A541");
    public static readonly Color Brecha = new("#E64A8E");
    public static readonly Color Creme = new("#F4E7C6");
    public static readonly Color Hp = new("#E85D5D");

    // --- Painéis (v2) ---
    public static readonly Color Ink = new("#0d0b16");
    public static readonly Color Panel = new("#120f1d");
    public static readonly Color Gold = new("#c9963e");
    public static readonly Color GoldDark = new("#5e4426");
    public static readonly Color Border = new("#8a6a35");
    public static readonly Color SlotFill = new("#110d1b");
    public static readonly Color SlotBorder = new("#3d2e22");
    public static readonly Color SlotHover = new("#a07a3a");
    public static readonly Color KeyBorder = new("#c9a46a");
    public static readonly Color Selection = new(74 / 255f, 44 / 255f, 107 / 255f, 0.78f);

    // --- Texto (spec 02 §4.10) ---
    public static readonly Color Body = new("#e4dbe9");
    public static readonly Color Sub = new("#b39fc0");
    public static readonly Color SubLight = new("#cdbfd6");
    public static readonly Color Dim = new("#8d7f99");
    public static readonly Color Disabled = new("#6f6479");
    public static readonly Color EcoText = new("#d99cff");
    public static readonly Color Heal = new("#7cf0a0");
    public static readonly Color Locked = new("#ff8a8a");
    public static readonly Color Done = new("#7cc47f");
    public static readonly Color Mute = new("#9d93a8");

    /// <summary>Fundo escuro de overlays (<c>rgba(6,4,12,α)</c>).</summary>
    public static Color Overlay(float alpha) => new(6 / 255f, 4 / 255f, 12 / 255f, alpha);

    /// <summary>Cor de dificuldade da escolha de classe (Fácil verde, Média âmbar, Difícil magenta; <c>extra.js:317</c>).</summary>
    public static Color Difficulty(string difficulty) => difficulty switch
    {
        "Fácil" => Done,
        "Média" => Ambar,
        _ => Brecha,
    };

    /// <summary>Cor hex dos dados (<c>ranks.json</c>, <c>gear.json</c>…) com fallback.</summary>
    public static Color FromHex(string? hex, Color fallback) => hex is { Length: > 0 } && Color.HtmlIsValid(hex) ? new Color(hex) : fallback;
}
