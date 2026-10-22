using Godot;

namespace Vespera.Game.UI;

/// <summary>Paleta de marca (PLANO-V2 §2 / CLAUDE.md §6) usada pela interface.</summary>
public static class Palette
{
    public static readonly Color Indigo = new("1B1F3B");
    public static readonly Color Purple = new("4A2C6B");
    public static readonly Color Amber = new("F2A541");
    public static readonly Color Magenta = new("E64A8E");
    public static readonly Color Cream = new("F4E7C6");
    public static readonly Color CreamDim = new("8A8FA8");

    /// <summary>Painel translúcido de fundo dos menus.</summary>
    public static readonly Color Panel = new(Indigo, 0.88f);
}
