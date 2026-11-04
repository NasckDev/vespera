using Godot;

namespace Vespera.Game.Combat;

/// <summary>
/// Efeitos pintados do Ato III (issue #59) no lugar dos provisórios por código: o feixe do Farol Afogado (4 quadros
/// empilhados, fundo preto, desenhado com mistura aditiva) e a âncora com corrente do Ancoreiro (4 quadros lado a lado,
/// a âncora na ponta direita). Os dois são esticados ao longo da faixa do golpe e somem rápido.
/// </summary>
public static class ActThreeFx
{
    public const string BeamTexture = "res://assets/v2/objetos/ato3/fx_feixe_farol.png";
    public const string AnchorTexture = "res://assets/v2/objetos/ato3/fx_ancora_corrente.png";
    private const int Frames = 4;
    private const float FrameSeconds = 0.06f;

    /// <summary>Feixe de luz de <paramref name="from"/> na direção <paramref name="dir"/>; some em <paramref name="seconds"/>.</summary>
    public static Node2D Beam(Vector2 from, Vector2 dir, float length, float width, float seconds)
    {
        var texture = GD.Load<Texture2D>(BeamTexture);
        var frameHeight = texture.GetHeight() / (float)Frames;
        var sprite = new Sprite2D
        {
            Name = "FeixeDoFarol",
            Texture = texture,
            Vframes = Frames,
            Centered = false,
            Offset = new Vector2(0, -frameHeight * 0.5f),
            Position = from,
            Rotation = dir.Angle(),
            Scale = new Vector2(length / texture.GetWidth(), width / frameHeight),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Animate(sprite, seconds, vertical: true);
        return sprite;
    }

    /// <summary>Âncora voando presa à corrente, de <paramref name="from"/> até <paramref name="length"/> px na direção.</summary>
    public static Node2D Anchor(Vector2 from, Vector2 dir, float length, float height, float seconds)
    {
        var texture = GD.Load<Texture2D>(AnchorTexture);
        var frameWidth = texture.GetWidth() / (float)Frames;
        var sprite = new Sprite2D
        {
            Name = "AncoraDoAncoreiro",
            Texture = texture,
            Hframes = Frames,
            Centered = false,
            Offset = new Vector2(0, -texture.GetHeight() * 0.5f),
            Position = from,
            Rotation = dir.Angle(),
            FlipV = dir.X < 0f,
            Scale = new Vector2(length / frameWidth, height / texture.GetHeight()),
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Animate(sprite, seconds, vertical: false);
        return sprite;
    }

    /// <summary>Passa os 4 quadros e some com fade no fim.</summary>
    private static void Animate(Sprite2D sprite, float seconds, bool vertical)
    {
        var tween = sprite.CreateTween();
        tween.TweenMethod(Callable.From<int>(frame =>
        {
            if (vertical)
            {
                sprite.FrameCoords = new Vector2I(0, frame % Frames);
            }
            else
            {
                sprite.Frame = frame % Frames;
            }
        }), 0, Frames - 1, FrameSeconds * Frames);
        tween.TweenProperty(sprite, "modulate:a", 0f, Mathf.Max(0.1f, seconds - (FrameSeconds * Frames)));
        tween.TweenCallback(Callable.From(sprite.QueueFree));
    }
}
