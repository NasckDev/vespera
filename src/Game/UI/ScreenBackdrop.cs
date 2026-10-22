using Godot;

namespace Vespera.Game.UI;

/// <summary>Fundo de tela cheia: key art escurecida com vinheta (telas de menu) ou apenas um véu sobre o jogo (pausa).</summary>
public partial class ScreenBackdrop(bool showArt) : Control
{
    private const float ArtZoom = 1.9f;
    private const float ArtDarkness = 0.74f;
    private const float VeilAlpha = 0.58f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        if (showArt)
        {
            ArtAssets.DrawKeyArt(this, ArtZoom, Vector2.Zero);
            DrawRect(rect, new Color(0.05f, 0.06f, 0.16f, ArtDarkness));
        }
        else
        {
            DrawRect(rect, new Color(0.05f, 0.06f, 0.16f, VeilAlpha));
        }

        DrawTextureRect(ArtAssets.Vignette, rect, false);
    }
}
