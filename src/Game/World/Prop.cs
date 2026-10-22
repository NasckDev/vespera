using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Components;

namespace Vespera.Game.World;

/// <summary>
/// Objeto de cenário com a origem no pé: sprite deslocado, pegada sólida (StaticBody2D), sombra de contato, luz
/// opcional, interação opcional e oclusão (fica 50% transparente quando um jogador passa atrás dele).
/// </summary>
public partial class Prop : Node2D
{
    public const float HiddenAlpha = 0.5f;
    private const float FadeSpeed = 6f;
    private const float InteractRadiusPad = 56f;
    private const float SpritePadding = 2f;

    private readonly PropDef _def;
    private readonly string _type;
    private Sprite2D _sprite = null!;
    private PointLight2D? _light;
    private float _alpha = 1f;

    private readonly float _worldScale;

    public Prop(string type, PropDef def, bool flip, float worldScale)
    {
        _type = type;
        _def = def;
        Flip = flip;
        _worldScale = worldScale;
    }

    public string Type => _type;

    public PropDef Def => _def;

    public bool Flip { get; }

    public bool LightOn { get; private set; } = true;

    public Interactable? Interaction { get; private set; }

    /// <summary>Transparência atual do sprite (1 = opaco; <see cref="HiddenAlpha"/> = jogador atrás).</summary>
    public float Alpha => _alpha;

    /// <summary>Pegada sólida no mundo.</summary>
    public Rect2 FootprintRect
    {
        get
        {
            var f = _def.WorldFootprint;
            return new Rect2(GlobalPosition.X + f.X, GlobalPosition.Y + f.Y, f.W, f.H);
        }
    }

    /// <summary>Área visível do sprite no mundo (sem a margem transparente da textura).</summary>
    public Rect2 SpriteRect
    {
        get
        {
            var size = _sprite.Texture.GetSize() * _def.Scale;
            var topLeft = GlobalPosition - (new Vector2(_def.Anchor[0], _def.Anchor[1]) * _def.Scale);
            return new Rect2(topLeft, size).Grow(-SpritePadding * _def.Scale);
        }
    }

    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>(_def.Texture);
        _sprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = texture,
            Centered = false,
            Offset = -new Vector2(_def.Anchor[0], _def.Anchor[1]),
            Scale = new Vector2(Flip ? -_def.Scale : _def.Scale, _def.Scale),
        };
        AddChild(_sprite);

        var footprint = _def.WorldFootprint;
        if (footprint.W > 0 && footprint.H > 0)
        {
            AddChild(ContactShadow.Make(footprint.W * 1.15f, 0.38f));
            var body = new StaticBody2D { Name = "Footprint", CollisionLayer = Player.WorldLayer, CollisionMask = 0 };
            body.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(footprint.W, footprint.H) },
                Position = new Vector2(footprint.X + (footprint.W / 2f), footprint.Y + (footprint.H / 2f)),
            });
            AddChild(body);
        }

        AddLight();
        AddInteraction(footprint);
    }

    public override void _Process(double delta)
    {
        if (!_def.Occludes)
        {
            return;
        }

        var target = IsAnyPlayerBehind() ? HiddenAlpha : 1f;
        _alpha = Mathf.MoveToward(_alpha, target, FadeSpeed * (float)delta);
        _sprite.Modulate = new Color(_sprite.Modulate, _alpha);
    }

    /// <summary>Liga/desliga a luz (lampião). Apagado, o sprite escurece um pouco.</summary>
    public void SetLight(bool on)
    {
        LightOn = on;
        if (_light is not null)
        {
            _light.Enabled = on;
        }

        var shade = on ? 1f : 0.62f;
        _sprite.SelfModulate = new Color(shade, shade, shade * 1.05f);
        if (Interaction is not null && _def.Interact is not null && !string.IsNullOrEmpty(_def.Interact.PromptOff))
        {
            Interaction.Prompt = on ? _def.Interact.Prompt : _def.Interact.PromptOff;
        }
    }

    private bool IsAnyPlayerBehind()
    {
        var spriteRect = SpriteRect;
        foreach (var node in GetTree().GetNodesInGroup(Player.Group))
        {
            if (node is Player player && player.GlobalPosition.Y < GlobalPosition.Y && spriteRect.Intersects(player.BodyRect))
            {
                return true;
            }
        }

        return false;
    }

    private void AddLight()
    {
        if (_def.Light is not { } light)
        {
            return;
        }

        _light = new PointLight2D
        {
            Name = "Light",
            Texture = LightTexture.Get(),
            Color = new Color(light.Color),
            Energy = light.Energy,
            TextureScale = light.Radius / LightTexture.Radius,
            Position = new Vector2(Flip ? -light.Offset[0] : light.Offset[0], light.Offset[1]) * _def.Scale,
            BlendMode = Light2D.BlendModeEnum.Add,
        };
        AddChild(_light);
    }

    private void AddInteraction(RectF footprint)
    {
        if (_def.Interact is not { } interact)
        {
            return;
        }

        var radius = interact.Radius > 0 ? interact.Radius : (Mathf.Max(footprint.W, footprint.H) / 2f) + (InteractRadiusPad * _worldScale);
        Interaction = new Interactable(interact.Kind, interact.Prompt, radius)
        {
            Name = "Interaction",
            Title = interact.Title,
            Text = interact.Text,
            OwnerProp = this,
            Position = new Vector2(footprint.X + (footprint.W / 2f), footprint.Y + (footprint.H / 2f)),
        };
        AddChild(Interaction);
        Interaction.PromptAnchor = GlobalPosition + new Vector2(0, -_def.Anchor[1] * _def.Scale);
    }
}
