using System;
using Godot;

namespace Vespera.Game.UI;

/// <summary>Texturas compartilhadas da interface: key art, vinheta e círculo suave (brasas e brilho), gerados/lidos uma vez.</summary>
public static class ArtAssets
{
    public const float ScreenWidth = 1920f;
    public const float ScreenHeight = 1080f;
    public const string IntroVideoPath = "res://assets/v2/video/abertura.ogv";
    public const string PrologueVideoPath = "res://assets/v2/video/prologo.ogv";
    private const string KeyArtPath = "res://assets/v2/ui/key-art.png";
    private const string IntroStillPath = "res://assets/v2/video/abertura_ultimo_quadro.png";

    private static Texture2D? _keyArt;
    private static Texture2D? _introStill;
    private static ImageTexture? _vignette;
    private static ImageTexture? _softCircle;

    public static Texture2D KeyArt => _keyArt ??= GD.Load<Texture2D>(KeyArtPath);

    /// <summary>Último quadro da abertura (cidade + logo pintado), em 1920x1080: fundo do título e ponte do fundido.</summary>
    public static Texture2D IntroStill => _introStill ??= GD.Load<Texture2D>(IntroStillPath);

    /// <summary>Escurece as bordas e os cantos (centro transparente).</summary>
    public static ImageTexture Vignette => _vignette ??= Radial(256, 144, 0.45f, 1.15f, new Color("070816"), 0.92f, invert: false);

    /// <summary>Disco branco com borda suave, para tingir via modulate.</summary>
    public static ImageTexture SoftCircle => _softCircle ??= Radial(64, 64, 0f, 1f, Colors.White, 1f, invert: true);

    /// <summary>
    /// Desenha a key art cobrindo a tela, ampliada por <paramref name="zoom"/> e ancorada para cortar o letreiro da arte
    /// (o título do jogo é desenhado em fonte). <paramref name="shift"/> é o deslocamento de paralaxe em pixels.
    /// </summary>
    public static void DrawKeyArt(CanvasItem canvas, float zoom, Vector2 shift)
    {
        var texture = KeyArt;
        var size = texture.GetSize() * zoom;
        var maxX = size.X - ScreenWidth;
        var maxY = size.Y - ScreenHeight;
        var origin = new Vector2(-(maxX * 0.18f) - shift.X, -(maxY * 0.9f) - shift.Y);
        canvas.DrawTextureRect(texture, new Rect2(origin, size), false);
    }

    /// <summary>Desenha o quadro da abertura ampliado por <paramref name="zoom"/> em torno do centro, mais o deslocamento de paralaxe.</summary>
    public static void DrawIntroStill(CanvasItem canvas, float zoom, Vector2 shift)
    {
        var full = new Vector2(ScreenWidth, ScreenHeight);
        var size = full * zoom;
        canvas.DrawTextureRect(IntroStill, new Rect2(((full - size) / 2f) + shift, size), false);
    }

    private static ImageTexture Radial(int w, int h, float inner, float outer, Color color, float maxAlpha, bool invert)
    {
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dx = ((x + 0.5f) / w * 2f) - 1f;
                var dy = ((y + 0.5f) / h * 2f) - 1f;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                var t = Math.Clamp((d - inner) / (outer - inner), 0f, 1f);
                t = t * t * (3f - (2f * t));
                var alpha = (invert ? 1f - t : t) * maxAlpha;
                image.SetPixel(x, y, new Color(color, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
