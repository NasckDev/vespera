using System;
using Godot;

namespace Vespera.Game.UI;

/// <summary>Parâmetros de uma moldura ornamentada gerada em código (cores em linear, sem gradiente chapado).</summary>
public sealed record FrameSpec(
    Color BgTop,
    Color BgBottom,
    Color OuterTop,
    Color OuterBottom,
    Color Inner,
    int OuterWidth = 3,
    int Gap = 3,
    int InnerWidth = 1,
    bool Ornaments = true);

/// <summary>
/// Gera texturas de moldura para 9-patch: fundo em gradiente índigo→roxo, borda dupla âmbar/creme com cantos chanfrados
/// e losangos ornamentais nas quinas. Tudo calculado por pixel com bordas suavizadas, sem arte externa.
/// </summary>
public static class FrameTextures
{
    public const int Size = 96;
    public const int PatchMargin = 30;
    private const float Chamfer = 16f;
    private const float DiamondRadius = 9f;
    private const float Sqrt2 = 1.41421356f;

    public static StyleBoxTexture MakeStyle(FrameSpec spec, int contentMargin, int patchMargin = PatchMargin)
    {
        var style = new StyleBoxTexture { Texture = Build(spec), TextureMarginLeft = patchMargin, TextureMarginRight = patchMargin, TextureMarginTop = patchMargin, TextureMarginBottom = patchMargin };
        style.ContentMarginLeft = contentMargin;
        style.ContentMarginRight = contentMargin;
        style.ContentMarginTop = contentMargin;
        style.ContentMarginBottom = contentMargin;
        return style;
    }

    public static ImageTexture Build(FrameSpec spec)
    {
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (var y = 0; y < Size; y++)
        {
            var t = y / (float)(Size - 1);
            for (var x = 0; x < Size; x++)
            {
                image.SetPixel(x, y, Shade(spec, x + 0.5f, y + 0.5f, t));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Color Shade(FrameSpec s, float x, float y, float t)
    {
        var inset = Inset(x, y);
        var coverage = Math.Clamp(inset + 0.5f, 0f, 1f);
        if (coverage <= 0f)
        {
            return Colors.Transparent;
        }

        var color = s.BgTop.Lerp(s.BgBottom, t);
        var outerEnd = s.OuterWidth;
        var innerStart = s.OuterWidth + s.Gap;
        if (inset < outerEnd)
        {
            color = s.OuterTop.Lerp(s.OuterBottom, t);
        }
        else if (inset >= innerStart && inset < innerStart + s.InnerWidth)
        {
            color = s.Inner;
        }

        if (s.Ornaments)
        {
            color = BlendOrnament(color, x, y, s);
        }

        color.A *= coverage;
        return color;
    }

    /// <summary>Distância (px) para dentro a partir da borda de um retângulo de cantos chanfrados.</summary>
    private static float Inset(float x, float y)
    {
        var far = Size - x;
        var down = Size - y;
        var inset = Math.Min(Math.Min(x, y), Math.Min(far, down));
        inset = Math.Min(inset, (x + y - Chamfer) / Sqrt2);
        inset = Math.Min(inset, (far + y - Chamfer) / Sqrt2);
        inset = Math.Min(inset, (x + down - Chamfer) / Sqrt2);
        return Math.Min(inset, (far + down - Chamfer) / Sqrt2);
    }

    private static Color BlendOrnament(Color baseColor, float x, float y, FrameSpec s)
    {
        var half = 17f;
        foreach (var (cx, cy) in new[] { (half, half), (Size - half, half), (half, Size - half), (Size - half, Size - half) })
        {
            var d = Math.Abs(x - cx) + Math.Abs(y - cy);
            if (d < DiamondRadius - 3.5f)
            {
                baseColor = s.Inner;
            }
            else if (d < DiamondRadius)
            {
                baseColor = s.OuterTop;
            }
            else if (d < DiamondRadius + 1.5f)
            {
                baseColor = baseColor.Lerp(s.BgTop, 0.7f);
            }
        }

        return baseColor;
    }
}
