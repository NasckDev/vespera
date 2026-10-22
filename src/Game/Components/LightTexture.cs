using Godot;

namespace Vespera.Game.Components;

/// <summary>Textura radial compartilhada das luzes quentes (lampiões, lanterna): centro forte e queda suave.</summary>
public static class LightTexture
{
    public const float Radius = 128f;

    private static GradientTexture2D? _texture;

    public static void ClearCache() => _texture = null;

    public static GradientTexture2D Get()
    {
        if (_texture is not null)
        {
            return _texture;
        }

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 0.55f));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradient.AddPoint(0.35f, new Color(1, 1, 1, 0.28f));
        gradient.AddPoint(0.7f, new Color(1, 1, 1, 0.08f));
        _texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = (int)(2 * Radius),
            Height = (int)(2 * Radius),
        };
        return _texture;
    }
}
