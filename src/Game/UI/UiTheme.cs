using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Tema único da interface em 1920x1080: texto creme sobre painéis ornamentados (gradiente índigo→roxo, borda dupla âmbar).
/// Títulos em Pixelify Sans, corpo em VT323. Os tamanhos seguem a base de 1080p (corpo ≥ 32 px, título ≥ 96 px).
/// </summary>
public static class UiTheme
{
    public const string TitleVariation = "TitleLabel";
    public const string HeadingVariation = "HeadingLabel";
    public const string DimVariation = "DimLabel";
    public const string LogoVariation = "LogoLabel";
    public const string ReadingVariation = "ReadingLabel";
    public const string MenuButtonVariation = "MainMenuButton";

    private const string BodyFontPath = "res://assets/v2/fonts/VT323-Regular.ttf";
    private const string TitleFontPath = "res://assets/v2/fonts/PixelifySans.ttf";
    private const int ButtonPatchMargin = 22;
    private const int ButtonContentX = 28;
    private const int ButtonContentY = 6;
    private const int PanelContent = 36;

    private static readonly int[] BodySizes = [36, 44];
    private static readonly int[] TitleSizes = [96, 108];
    private static readonly int[] ReadingSizes = [46, 54];
    private static readonly int[] MenuSizes = [50, 58];
    private static readonly int[] LogoSizes = [176, 176];

    private static readonly Color Ink = new("0F1230");
    private static readonly Color Night = new("1B1F3B");
    private static readonly Color Dusk = new("33204F");
    private static readonly Color AmberHi = new("F2A541");
    private static readonly Color AmberLo = new("B8782A");
    private static readonly Color Cream = new("F4E7C6");

    public static Theme Build(int textSizeIndex)
    {
        var i = Mathf.Clamp(textSizeIndex, 0, BodySizes.Length - 1);
        var body = NoLigatures(GD.Load<FontFile>(BodyFontPath));
        var title = NoLigatures(GD.Load<FontFile>(TitleFontPath));

        var theme = new Theme { DefaultFont = body, DefaultFontSize = BodySizes[i] };
        AddLabelStyles(theme, body, title, i);
        AddButtonStyles(theme, body, BodySizes[i], i);
        AddPanelStyles(theme);
        AddScrollStyles(theme);
        return theme;
    }

    /// <summary>Moldura de painel: fundo em gradiente, borda dupla âmbar/creme e losangos nas quinas.</summary>
    public static StyleBoxTexture PanelFrame() => FrameTextures.MakeStyle(
        new FrameSpec(new Color(Ink, 0.94f), new Color(Dusk, 0.94f), AmberHi, AmberLo, new Color(Cream, 0.6f), OuterWidth: 3, Gap: 4, InnerWidth: 1),
        PanelContent);

    /// <summary>Moldura discreta das linhas de opção (sem losangos); destacada quando a linha tem o foco.</summary>
    public static StyleBoxTexture RowFrame(bool highlighted) => FrameTextures.MakeStyle(
        new FrameSpec(
            new Color(highlighted ? Dusk : Ink, highlighted ? 0.92f : 0.7f),
            new Color(highlighted ? new Color("4A2C6B") : Dusk, highlighted ? 0.92f : 0.7f),
            highlighted ? AmberHi : new Color("7A5A3A"),
            highlighted ? AmberLo : new Color("5A3F33"),
            new Color(Cream, highlighted ? 0.4f : 0.15f),
            OuterWidth: 2,
            Gap: 2,
            InnerWidth: 1,
            Ornaments: false),
        14,
        ButtonPatchMargin);

    private static FontVariation NoLigatures(Font baseFont) => new()
    {
        BaseFont = baseFont,
        OpentypeFeatures = new Godot.Collections.Dictionary { ["liga"] = 0, ["clig"] = 0 },
    };

    private static void AddLabelStyles(Theme theme, Font body, Font title, int i)
    {
        theme.SetFont("font", "Label", body);
        theme.SetFontSize("font_size", "Label", BodySizes[i]);
        theme.SetColor("font_color", "Label", Palette.Cream);

        AddVariation(theme, TitleVariation, title, TitleSizes[i], Palette.Cream, outline: 10);
        AddVariation(theme, HeadingVariation, body, BodySizes[i], Palette.Amber);
        AddVariation(theme, LogoVariation, title, LogoSizes[i], Cream, outline: 14);
        AddVariation(theme, ReadingVariation, body, ReadingSizes[i], Palette.Cream);
        AddVariation(theme, DimVariation, body, BodySizes[i], new Color("B7B2C9"));
    }

    private static void AddVariation(Theme theme, string name, Font font, int size, Color color, int outline = 0)
    {
        theme.SetTypeVariation(name, "Label");
        theme.SetFont("font", name, font);
        theme.SetFontSize("font_size", name, size);
        theme.SetColor("font_color", name, color);
        if (outline > 0)
        {
            theme.SetConstant("outline_size", name, outline);
            theme.SetColor("font_outline_color", name, new Color(Ink, 0.95f));
        }
    }

    private static void AddButtonStyles(Theme theme, Font body, int size, int i)
    {
        var normal = ButtonFrame(new Color(Night, 0.9f), new Color(Dusk, 0.9f), new Color("8A6A45"), new Color("5A3F33"), 0.25f);
        var hover = ButtonFrame(new Color("26264F", 0.94f), new Color("4A2C6B", 0.94f), AmberHi, AmberLo, 0.45f);
        var pressed = ButtonFrame(new Color("B8782A", 0.95f), new Color("7A4A1E", 0.95f), Cream, AmberHi, 0.7f);
        var disabled = ButtonFrame(new Color(Night, 0.5f), new Color(Dusk, 0.5f), new Color("5A4A45", 0.7f), new Color("3A2F33", 0.7f), 0.1f);

        theme.SetFont("font", "Button", body);
        theme.SetFontSize("font_size", "Button", size);
        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetStylebox("hover_pressed", "Button", pressed);
        theme.SetStylebox("disabled", "Button", disabled);
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", Palette.Cream);
        theme.SetColor("font_hover_color", "Button", Palette.Cream);
        theme.SetColor("font_focus_color", "Button", Palette.Amber);
        theme.SetColor("font_pressed_color", "Button", Ink);
        theme.SetColor("font_hover_pressed_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("8A8FA8"));

        theme.SetTypeVariation(MenuButtonVariation, "Button");
        theme.SetFontSize("font_size", MenuButtonVariation, MenuSizes[Mathf.Clamp(i, 0, MenuSizes.Length - 1)]);
    }

    private static StyleBoxTexture ButtonFrame(Color bgTop, Color bgBottom, Color outerTop, Color outerBottom, float innerAlpha)
    {
        var style = FrameTextures.MakeStyle(
            new FrameSpec(bgTop, bgBottom, outerTop, outerBottom, new Color(Cream, innerAlpha), OuterWidth: 2, Gap: 2, InnerWidth: 1, Ornaments: false),
            ButtonContentX,
            ButtonPatchMargin);
        style.ContentMarginTop = ButtonContentY;
        style.ContentMarginBottom = ButtonContentY;
        return style;
    }

    private static void AddPanelStyles(Theme theme)
    {
        var panel = PanelFrame();
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panel);
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = new Color(AmberHi, 0.5f), Thickness = 2 });
        theme.SetConstant("separation", "HSeparator", 14);
    }

    private static void AddScrollStyles(Theme theme)
    {
        var track = new StyleBoxFlat { BgColor = new Color(Cream, 0.08f) };
        track.SetContentMarginAll(6);
        var grab = new StyleBoxFlat { BgColor = new Color(AmberHi, 0.8f) };
        grab.SetContentMarginAll(6);
        var grabHot = new StyleBoxFlat { BgColor = Cream };
        grabHot.SetContentMarginAll(6);
        theme.SetStylebox("scroll", "VScrollBar", track);
        theme.SetStylebox("grabber", "VScrollBar", grab);
        theme.SetStylebox("grabber_highlight", "VScrollBar", grabHot);
        theme.SetStylebox("grabber_pressed", "VScrollBar", grabHot);
    }
}
