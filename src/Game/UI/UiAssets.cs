using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Peças pintadas do kit de interface (pacote GPT, em <c>assets/v2/interface</c>): cursor, dica (tooltip), espaços de item,
/// painel de diálogo e barras de HUD. Botões e painéis de menu continuam com as molduras ornamentadas de
/// <see cref="FrameTextures"/>, que ficaram mais ricas que as do pacote (fundo em gradiente e borda dupla).
/// </summary>
public static class UiAssets
{
    public const string TooltipVariation = "TooltipPanel";

    private const string Folder = "res://assets/v2/interface/";
    private const int CornerMargin = 28;
    private const int SlotMargin = 24;
    private const int BarFrameMargin = 16;
    // Centro do corpo da lanterna: é para onde o jogador mira (na alça de cima o clique saía ~30 px acima).
    private static readonly Vector2 CursorHotspot = new(32, 32);

    public enum BarKind
    {
        Vida,
        Folego,
        Xp,
    }

    /// <summary>Instala o cursor de lanterna (setas e mão) para toda a janela.</summary>
    public static void InstallCursor()
    {
        var cursor = GD.Load<Texture2D>(Folder + "cursor.png");
        Input.SetCustomMouseCursor(cursor, Input.CursorShape.Arrow, CursorHotspot);
        Input.SetCustomMouseCursor(cursor, Input.CursorShape.PointingHand, CursorHotspot);
    }

    /// <summary>Registra no tema o painel de dica (tooltip) pintado.</summary>
    public static void AddTo(Theme theme)
    {
        theme.SetStylebox("panel", "TooltipPanel", Patch("tooltip.png", CornerMargin, 18));
        theme.SetColor("font_color", "TooltipLabel", Palette.Cream);
    }

    public static StyleBoxTexture SlotStyle(bool selected) => Patch(selected ? "slot_selecionado.png" : "slot_normal.png", SlotMargin, 6);

    public static StyleBoxTexture DialogPanel() => Patch("painel_dialogo.png", CornerMargin, 24);

    /// <summary>Barra de HUD com moldura por cima do preenchimento (as duas imagens têm o mesmo tamanho, 512x48).</summary>
    public static TextureProgressBar MakeBar(BarKind kind)
    {
        var name = kind switch
        {
            BarKind.Vida => "barra_vida",
            BarKind.Folego => "barra_folego",
            _ => "barra_xp",
        };
        return new TextureProgressBar
        {
            TextureUnder = null,
            TextureOver = GD.Load<Texture2D>($"{Folder}{name}_moldura.png"),
            TextureProgress = GD.Load<Texture2D>($"{Folder}{name}_preenchimento.png"),
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            NinePatchStretch = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static StyleBoxTexture Patch(string file, int patch, int content)
    {
        var style = new StyleBoxTexture
        {
            Texture = GD.Load<Texture2D>(Folder + file),
            TextureMarginLeft = patch,
            TextureMarginRight = patch,
            TextureMarginTop = patch,
            TextureMarginBottom = patch,
        };
        style.ContentMarginLeft = content;
        style.ContentMarginRight = content;
        style.ContentMarginTop = content;
        style.ContentMarginBottom = content;
        return style;
    }
}
