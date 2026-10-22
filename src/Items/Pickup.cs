using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.World;

namespace Vespera.Items;

/// <summary>Tipo de item no chão (<c>k.kind</c>, <c>combat.js:476-509</c>).</summary>
public enum PickupKind
{
    Lume,
    Material,
    Gear,
    Potion,
    Eco,

    /// <summary>Lanterna de outro caçador (entregar à Odete).</summary>
    Lantern,

    /// <summary>A sua Lanterna perdida.</summary>
    MyLantern,
}

/// <summary>
/// Item no chão (<c>G.picks</c>): salta, quica e é puxado pelo ímã (<see cref="PickupMotion"/>); Ecos e Lanternas
/// (<see cref="NeedsInteract"/>) ficam parados e pedem [E] (interagível de raio 18). O
/// <see cref="Combat.CombatDirector"/> avança o movimento e coleta; este nó só desenha (ícone no tamanho da v2 e sombra).
/// </summary>
public partial class Pickup : Node2D
{
    private const float InteractRadius = 18;

    private AtlasSprite _icon = null!;
    private Node2D _iconRoot = null!;
    private float _phase;

    public PickupMotion Motion;

    public PickupKind Kind { get; private set; }

    /// <summary>Material, equipamento, Eco ou nome do dono da Lanterna.</summary>
    public string Id { get; private set; } = string.Empty;

    public int Amount { get; private set; }

    public bool NeedsInteract => Kind is PickupKind.Eco or PickupKind.Lantern or PickupKind.MyLantern;

    public bool Collected { get; set; }

    /// <summary>Interagível do [E] (null para itens atraídos pelo ímã).</summary>
    public CallbackInteractable? Interaction { get; private set; }

    public static Pickup Create(PickupKind kind, string id, int amount, string iconKey, PickupMotion motion, string? label)
    {
        var p = new Pickup { Name = $"Pick_{kind}", Kind = kind, Id = id, Amount = amount, Motion = motion };
        p._phase = (float)motion.X;
        p._iconRoot = new Node2D();
        p.AddChild(p._iconRoot);
        var size = kind switch
        {
            PickupKind.Eco => 18f,
            PickupKind.Gear => 13f,
            PickupKind.Lume => 8f,
            _ => 11f,
        };
        p._icon = new AtlasSprite { Key = iconKey };
        if (kind is not (PickupKind.Lantern or PickupKind.MyLantern) && AtlasLibrary.TryGet(iconKey, out var frame))
        {
            var px = Math.Max(frame.Texture.GetWidth(), frame.Texture.GetHeight());
            p._icon.SpriteScale = size * frame.Density / px; // drawPick: tamanho fixo em u
        }

        p._iconRoot.AddChild(p._icon);
        if (p.NeedsInteract && label is not null)
        {
            p.Interaction = new CallbackInteractable { Prompt = label, Radius = InteractRadius };
            p.AddChild(p.Interaction);
        }

        p.Position = new Vector2((float)motion.X, (float)motion.Y);
        return p;
    }

    /// <summary>Posição e quique visuais (<c>drawPick</c>: bob de <c>z</c> e flutuação).</summary>
    public void Sync(float time)
    {
        Position = new Vector2((float)Motion.X, (float)Motion.Y);
        var bob = (float)Motion.Z + (NeedsInteract ? (MathF.Sin(time * 3) * 1.5f) + 2 : MathF.Sin((time * 5) + _phase) * 0.6f);
        _iconRoot.Position = new Vector2(0, -bob);
    }

    public override void _Draw()
    {
        // sombra (render.js:139): elipse 4 × 1,6
        DrawSetTransform(new Vector2(0, 1), 0, new Vector2(1, 0.4f));
        DrawCircle(Vector2.Zero, 4, new Color(0, 0, 0, 0.35f));
    }
}
