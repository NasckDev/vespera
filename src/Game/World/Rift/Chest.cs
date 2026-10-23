using System;
using Godot;
using Vespera.Game.Combat;
using Vespera.Game.Components;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Baú da sala do tesouro: pé com pegada sólida e sombra, aviso "Abrir o baú"; aberto, troca para a arte aberta,
/// brilha e espalha o saque de baú sorteado pela progressão (<see cref="RiftLoot.ChestOpened"/>).
/// </summary>
public partial class Chest : Node2D
{
    private const string ClosedPath = "res://assets/v2/objetos/v01/cathedral_chest_closed.png";
    private const string OpenPath = "res://assets/v2/objetos/v01/cathedral_chest_open.png";
    /// <summary>
    /// Altura do baú ÷ altura humana do mapa (contrato de escala, §2.1: objeto medido contra o equivalente pintado). Os
    /// barris pintados no chão da sala do tesouro medem 40–45 px num mapa de humano 79 px (≈ 0,53); o baú fica da altura
    /// deles: 0,52 humano (41 px ali). Antes era 86 × actorScale = 54 px (0,69 humano), maior que os barris ao lado.
    /// </summary>
    public const float HumanFraction = 0.52f;

    private readonly LevelWorld _world;
    private Sprite2D _sprite = null!;
    private float _scale;

    public Chest(LevelWorld world, Vector2 feet)
    {
        _world = world;
        Position = feet;
        Zone = new Interactable("chest", "Abrir o baú", 60f * world.Data.ActorScale) { Name = "Zone" };
    }

    public Interactable Zone { get; }

    public bool IsOpen { get; private set; }

    /// <summary>Altura-alvo em px de mundo (fração da altura humana do mapa).</summary>
    public float TargetHeight => HumanFraction * _world.Data.HumanHeight;

    /// <summary>Altura opaca renderizada agora (px de mundo), para o teste de escala.</summary>
    public float RenderedHeight => OpaqueHeight((Texture2D)_sprite.Texture) * _sprite.GlobalScale.Y;

    private static float OpaqueHeight(Texture2D texture) => texture.GetImage() is { } image ? Mathf.Max(1, image.GetUsedRect().Size.Y) : texture.GetHeight();

    public event Action<Chest>? Opened;

    /// <summary>Pegada sólida no mundo.</summary>
    public Rect2 Footprint => new(GlobalPosition.X - (24f * _scale), GlobalPosition.Y - (12f * _scale), 48f * _scale, 12f * _scale);

    public override void _Ready()
    {
        var closed = GD.Load<Texture2D>(ClosedPath);
        _scale = TargetHeight / OpaqueHeight(closed);
        _sprite = new Sprite2D
        {
            Texture = closed,
            Centered = false,
            Offset = new Vector2(-closed.GetWidth() / 2f, -closed.GetHeight() + 3f),
            Scale = Vector2.One * _scale,
        };
        AddChild(ContactShadow.Make(48f * _scale * 1.2f, 0.45f));
        AddChild(_sprite);
        var body = new StaticBody2D { CollisionLayer = Actors.Player.WorldLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(48f, 12f) * _scale }, Position = new Vector2(0, -6f * _scale) });
        AddChild(body);
        Zone.PromptAnchor = GlobalPosition - new Vector2(0, TargetHeight + (30f * _world.Data.ActorScale));
        AddChild(Zone);
        _world.AddSolid(Footprint);
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        Zone.Enabled = false;
        var open = GD.Load<Texture2D>(OpenPath);
        _sprite.Texture = open;
        _sprite.Offset = new Vector2(-open.GetWidth() / 2f, -open.GetHeight() + 3f);
        var a = _world.Data.ActorScale;
        AddChild(new PointLight2D { Texture = LightTexture.Get(), Color = new Color("f2a541"), Energy = 1.2f, TextureScale = 0.9f * a, Position = new Vector2(0, -30f * a) });
        CombatSfx.Play(this, CombatSound.Chest, Vector2.Zero);
        _world.Feel.Spark(GlobalPosition - new Vector2(0, 30f * a), new Color("f2a541"), 24, 360f, Vector2.Up, 70f);
        RiftLoot.ChestOpened(_world, GlobalPosition + new Vector2(0, 10f * a));
        Opened?.Invoke(this);
    }
}
