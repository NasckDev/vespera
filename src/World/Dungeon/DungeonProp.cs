using Godot;
using Vespera.Items;

namespace Vespera.World.Dungeon;

/// <summary>
/// Objeto da sala da Brecha (<c>G.props</c>, <c>world.js:91-99</c>): decorativo, quebrável (barril, caixote, saco, urna)
/// ou baú. Quebráveis e baús são sólidos: bloqueiam monstros (teste de elipse <c>dx² + dy²·2.2 &lt; r²</c>, como
/// <c>walkable</c>) e o jogador (cápsula deitada na camada "world"). Baú abre com [E] ("Abrir baú", raio 20).
/// </summary>
public partial class DungeonProp : Node2D
{
    private AtlasSprite _sprite = null!;
    private StaticBody2D? _body;

    public string Kind { get; private set; } = string.Empty;

    public string ThemeId { get; private set; } = string.Empty;

    public bool Breakable { get; private set; }

    public bool IsChest { get; private set; }

    public bool Purple { get; private set; }

    public bool Broken { get; private set; }

    public bool Open { get; private set; }

    /// <summary>Raio sólido (0 = decorativo).</summary>
    public float SolidRadius { get; private set; }

    public bool BlocksMovement => SolidRadius > 0 && !Broken;

    public CallbackInteractable? ChestInteraction { get; private set; }

    /// <summary><c>kind</c> = nome do prop do tema (<c>barrel</c>, <c>urn</c>…) ou "chest".</summary>
    public static DungeonProp Create(string kind, string themeId, Vector2 at, bool purple = false, string? spriteKey = null)
    {
        var chest = kind == "chest";
        var breakable = !chest && kind is "barrel" or "crate" or "crate2" or "sack" or "urn";
        var prop = new DungeonProp
        {
            Name = $"Prop_{kind}",
            Kind = kind,
            ThemeId = themeId,
            Breakable = breakable,
            IsChest = chest,
            Purple = purple,
            SolidRadius = chest ? 8 : breakable ? 7 : 0,
            Position = at,
        };
        prop.AddChild(new AtlasSprite { Key = "shadow", SpriteScale = 0.8f, Position = new Vector2(0, 1), Modulate = new Color(1, 1, 1, 0.4f) });
        prop._sprite = new AtlasSprite { Key = spriteKey ?? (chest ? (purple ? "pr_chest3" : "pr_chest1") : $"pr_{kind}") };
        prop.AddChild(prop._sprite);
        if (prop.SolidRadius > 0)
        {
            prop._body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
            prop._body.AddChild(new CollisionShape2D
            {
                Shape = new CapsuleShape2D { Radius = prop.SolidRadius / 1.48f, Height = prop.SolidRadius * 2 },
                Rotation = Mathf.Pi / 2,
            });
            prop.AddChild(prop._body);
        }

        if (chest)
        {
            prop.ChestInteraction = new CallbackInteractable { Prompt = "Abrir baú", Radius = 20 };
            prop.AddChild(prop.ChestInteraction);
        }

        return prop;
    }

    /// <summary>Ponto bloqueado por este prop (<c>walkable</c>, <c>core.js:245</c>).</summary>
    public bool Blocks(float x, float y)
    {
        if (!BlocksMovement)
        {
            return false;
        }

        var dx = x - Position.X;
        var dy = y - Position.Y;
        return (dx * dx) + (dy * dy * 2.2f) < SolidRadius * SolidRadius;
    }

    /// <summary>Quebra: some e para de bloquear.</summary>
    public void Break()
    {
        Broken = true;
        Visible = false;
        _body?.QueueFree();
        _body = null;
    }

    /// <summary>Abre o baú: roxo vira <c>pr_chest4</c>, comum fica translúcido (<c>drawProp</c>).</summary>
    public void OpenChest()
    {
        Open = true;
        if (Purple)
        {
            _sprite.Key = "pr_chest4";
        }
        else
        {
            _sprite.Modulate = new Color(1, 1, 1, 0.55f);
        }

        if (ChestInteraction is not null)
        {
            ChestInteraction.Enabled = false;
        }
    }
}
