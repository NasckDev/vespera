using Godot;

namespace Vespera.UI.Common;

/// <summary>
/// Fontes da UI (DECISIONS D11): Cinzel para títulos, Pixelify Sans para texto, VT323 para números do HUD.
/// Tamanhos em px da resolução base 640×360 (a tela final multiplica: ×2 em 720p, ×3 em 1080p).
/// </summary>
public static class UiFonts
{
    /// <summary>Texto miúdo (sub-rótulos, teclas).</summary>
    public const int Tiny = 6;

    public const int Small = 7;

    public const int Body = 8;

    public const int Large = 10;

    /// <summary>Títulos de painel (Cinzel).</summary>
    public const int Heading = 12;

    public const int Title = 15;

    public const int Huge = 20;

    private const int WeightTag = 2003265652; // 'wght'
    private const int LigaturesTag = 1818847073; // 'liga': a ligadura "fi" da Pixelify Sans sai errada em tamanhos pequenos

    private static Font? _title;
    private static Font? _titleRegular;
    private static Font? _body;
    private static Font? _bodyBold;
    private static Font? _numbers;

    /// <summary>Cinzel 700.</summary>
    public static Font Title700 => _title ??= Weighted("res://assets/fonts/Cinzel.ttf", 700);

    /// <summary>Cinzel 500.</summary>
    public static Font TitleRegular => _titleRegular ??= Weighted("res://assets/fonts/Cinzel.ttf", 500);

    /// <summary>Pixelify Sans 400.</summary>
    public static Font Text => _body ??= Weighted("res://assets/fonts/PixelifySans.ttf", 400);

    /// <summary>Pixelify Sans 600.</summary>
    public static Font TextBold => _bodyBold ??= Weighted("res://assets/fonts/PixelifySans.ttf", 600);

    /// <summary>VT323 (números grandes do HUD).</summary>
    public static Font Numbers => _numbers ??= GD.Load<Font>("res://assets/fonts/VT323-Regular.ttf");

    private static Font Weighted(string path, int weight)
    {
        var variation = new FontVariation { BaseFont = GD.Load<Font>(path) };
        variation.VariationOpentype = new Godot.Collections.Dictionary { { WeightTag, weight } };
        variation.OpentypeFeatures = new Godot.Collections.Dictionary { { LigaturesTag, 0 } };
        return variation;
    }
}
