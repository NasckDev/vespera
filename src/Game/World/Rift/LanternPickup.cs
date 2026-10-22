using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.Components;

namespace Vespera.Game.World.Rift;

/// <summary>
/// A Lanterna deixada na última queda (GDD §5.5): fica acesa no ponto onde o caçador caiu; encostar nela devolve à
/// bolsa os Fragmentos daquela caçada.
/// </summary>
public partial class LanternPickup : Node2D
{
    private const string Texture = "res://assets/v2/cenarios/praca/objetos/lanterna_chao.png";
    private const float TouchRadius = 26f;

    private readonly LevelWorld _world;
    private float _t;
    private Sprite2D _sprite = null!;

    public LanternPickup(LevelWorld world, Vector2 feet)
    {
        _world = world;
        Position = feet;
    }

    public override void _Ready()
    {
        var a = _world.Data.ActorScale;
        var texture = GD.Load<Texture2D>(Texture);
        var scale = 30f * a / texture.GetHeight() * 1.4f;
        _sprite = new Sprite2D { Texture = texture, Centered = false, Offset = new Vector2(-19, -80), Scale = Vector2.One * scale };
        AddChild(ContactShadow.Make(20f * a, 0.4f));
        AddChild(_sprite);
        AddChild(new PointLight2D { Texture = LightTexture.Get(), Color = new Color("ffa24a"), Energy = 1.1f, TextureScale = 0.8f * a, Position = new Vector2(0, -20f * a) });
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _sprite.Position = new Vector2(0, -2f * Mathf.Sin(_t * 2f) * _world.Data.ActorScale);
        var player = _world.Player;
        if (player.Combat.IsDead || player.GlobalPosition.DistanceTo(GlobalPosition) > TouchRadius * 2f * _world.Data.ActorScale)
        {
            return;
        }

        var before = RiftSession.Fragments;
        RiftRules.RecoverLantern(RiftSession.Save, RiftSession.Bag);
        player.Combat.RefreshStats(fullHeal: false);
        CombatSfx.Play(_world, CombatSound.Chest, GlobalPosition);
        _world.Feel.Spark(GlobalPosition - new Vector2(0, 20f), new Color("ffa24a"), 20, 260f, Vector2.Up, 90f);
        _world.Feel.Toast($"Lanterna recuperada! +{RiftSession.Fragments - before} Fragmentos de volta.");
        QueueFree();
    }
}
