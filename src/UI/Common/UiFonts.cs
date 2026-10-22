using Godot;

namespace Vespera.UI.Common;

/// <summary>
/// Fontes da UI (DECISIONS D11): Cinzel 700 para títulos, Pixelify Sans para texto (400 e 600), VT323 para números.
/// Vêm do tema do projeto (<c>vespera_theme.tres</c>: fonte padrão e variações <c>TitleLabel</c>, <c>BoldLabel</c>,
/// <c>NumberLabel</c>), então o motor é dono delas (sem cópia estática que vaze ao fechar o jogo).
/// Tamanhos em px da resolução base 640×360 (a tela final multiplica: ×2 em 720p, ×3 em 1080p).
/// </summary>
public static class UiFonts
{
    /// <summary>Texto miúdo (sub-rótulos, teclas).</summary>
    public const int Tiny = 7; // POLI-10: mínimo legível (14 px em 720p); eram 6 e 5

    public const int Small = 7;

    public const int Body = 8;

    public const int Large = 10;

    /// <summary>Títulos de painel (Cinzel).</summary>
    public const int Heading = 12;

    public const int Title = 15;

    public const int Huge = 20;

    private static readonly StringName FontItem = "font";
    private static readonly StringName TitleType = "TitleLabel";
    private static readonly StringName BoldType = "BoldLabel";
    private static readonly StringName NumberType = "NumberLabel";

    /// <summary>Cinzel 700.</summary>
    public static Font Title700 => Theme.GetFont(FontItem, TitleType);

    /// <summary>Pixelify Sans 400 (fonte padrão do tema).</summary>
    public static Font Text => Theme.DefaultFont;

    /// <summary>Pixelify Sans 600.</summary>
    public static Font TextBold => Theme.GetFont(FontItem, BoldType);

    /// <summary>VT323 (números grandes do HUD).</summary>
    public static Font Numbers => Theme.GetFont(FontItem, NumberType);

    private static Theme Theme => ThemeDB.GetProjectTheme();
}
