using Godot;
using Vespera.Game.Actors;

namespace Vespera.Game.Combat;

/// <summary>Rastro da esquiva: cópia do quadro atual do caçador, tingida de magenta, que some em 0,22 s.</summary>
public partial class AfterImage : Sprite2D
{
    private const float Life = 0.22f;
    private float _t;

    public static void Spawn(Player player)
    {
        var sprite = player.Sprite;
        var texture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame);
        if (texture is null || player.GetParent() is not Node2D parent)
        {
            return;
        }

        var ghost = new AfterImage
        {
            Texture = texture,
            Offset = sprite.Offset,
            Scale = sprite.Scale,
            FlipH = sprite.FlipH,
            GlobalPosition = player.GlobalPosition,
            Modulate = new Color(0.95f, 0.45f, 0.75f, 0.5f),
            ZIndex = -1,
        };
        ghost.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        parent.AddChild(ghost);
        ghost.GlobalPosition = player.GlobalPosition;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        Modulate = Modulate with { A = 0.5f * (1f - (_t / Life)) };
        if (_t >= Life)
        {
            QueueFree();
        }
    }
}
