using Godot;

namespace Vespera.Game.Components;

/// <summary>Sombra de contato: elipse escura e macia sob o pé, para objetos e personagens não "flutuarem" no chão.</summary>
public static class ContactShadow
{
    private const int TextureSize = 64;
    private const float Flatten = 0.36f;

    private static GradientTexture2D? _texture;

    public static void ClearCache() => _texture = null;

    public static Sprite2D Make(float width, float opacity)
    {
        var texture = _texture ??= BuildTexture();
        var scale = width / TextureSize;
        return new Sprite2D
        {
            Name = "Shadow",
            Texture = texture,
            Scale = new Vector2(scale, scale * Flatten),
            Modulate = new Color(0, 0, 0, opacity),
            ShowBehindParent = true,
            ZIndex = -1,
        };
    }

    private static GradientTexture2D BuildTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradient.AddPoint(0.55f, new Color(1, 1, 1, 0.75f));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = TextureSize,
            Height = TextureSize,
        };
    }
}
