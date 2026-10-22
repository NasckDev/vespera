using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Art;

namespace Vespera.World;

/// <summary>
/// Baú do layout (atlas <c>pr_chest0..4</c>, DECISIONS D45). Interagir ("Abrir baú", r 20 como a v2) abre com animação: a
/// tampa (metade de cima do recorte) vira para trás em volta da dobradiça, sobe um brilho de dentro com faíscas e o saque
/// de <c>LootTables.ChestDrops</c> sai em textos escalonados. O roxo (tier ≥ 3) usa as chances de baú roxo e termina no
/// sprite aberto <c>pr_chest4</c>. Fica aberto até o dia seguinte. Sólido como na v2 (r 8).
/// Com a arte aprovada dos 5 tiers (DECISIONS C2-D5) a tampa não é mais um recorte girando: a abertura toca os quadros
/// desenhados (fechado → abrindo 1 → abrindo 2 → aberto), o saque sai com o baú aberto e, entregue, ele fica vazio.
/// </summary>
public partial class Chest : Node2D
{
    private const float LidFraction = 0.46f;
    private const float OpenSeconds = 0.45f;
    private const float SolidRadius = 8f; // world.js: baús r=8, sólidos
    private const float InteractRadius = 20f; // world.js:197 "Abrir baú" r 20

    private AtlasSprite _base = null!;
    private Sprite2D _lid = null!;
    private Sprite2D _glow = null!;
    private CpuParticles2D _sparkles = null!;
    private WorldInteractable _interact = null!;
    private float _openT = -1f;
    private float _scale = 1f;
    private float _lidHeight;
    private string _key = string.Empty;
    private ArtFrames? _art;
    private Sprite2D? _artSprite;
    private float _emptyT = -1f;
    private const float EmptyAfterSeconds = 0.9f;

    private static readonly StringName[] OpeningFrames = ["closed", "opening_1", "opening_2", "open"];
    private static readonly StringName EmptyFrame = "empty";

    public int Tier { get; private set; }

    public bool IsOpen { get; private set; }

    /// <summary>Animação de abrir em andamento.</summary>
    public bool IsOpening => _openT >= 0f;

    public bool Purple => Tier >= 3;

    /// <summary>Usa os quadros desenhados do tier.</summary>
    public bool HasStateArt => _art is not null;

    /// <summary>Quadro desenhado mostrado agora (<c>closed</c>, <c>opening_1</c>, <c>opening_2</c>, <c>open</c>, <c>empty</c>; "" sem arte).</summary>
    public string ArtFrame { get; private set; } = string.Empty;

    public WorldInteractable Interactable => _interact;

    /// <summary>O dono sorteia e entrega o saque (chamado quando a tampa abre).</summary>
    public Action<Chest>? DropLoot { get; set; }

    public string MapId { get; private set; } = string.Empty;

    /// <summary>Chave no <see cref="World.Objects"/> (aberto até o dia seguinte).</summary>
    public string Key
    {
        get => _key;
        set
        {
            _key = value;
            SeenKey = value + ":seen";
        }
    }

    /// <summary>Chave da marca "já visto" (minimapa), criada uma vez.</summary>
    public string SeenKey { get; private set; } = ":seen";

    public void Setup(string mapId, Core.World.LayoutChest def, bool alreadyOpen, Texture2D glowTexture)
    {
        MapId = mapId;
        Tier = Math.Clamp(def.Tier, 0, 4);
        Name = $"Chest_{(int)def.X}_{(int)def.Y}";
        Position = new Vector2(def.X, def.Y);
        _scale = WorldScale.Prop;

        AddChild(new AtlasSprite { Key = "shadow", ScaleClass = SpriteScaleClass.Prop, SpriteScale = 0.9f, Modulate = new Color(1, 1, 1, 0.4f), Position = new Vector2(0, 1) });
        _base = new AtlasSprite { Key = $"pr_chest{Tier}", ScaleClass = SpriteScaleClass.Prop };
        AddChild(_base);

        // a tampa é a parte de cima do mesmo recorte, desenhada por cima do corpo e girando na dobradiça (borda de cima)
        _lid = new Sprite2D { Name = "Lid", Centered = false };
        if (AtlasLibrary.TryGet($"pr_chest{Tier}", out var frame) && frame.Texture is AtlasTexture at)
        {
            var region = at.Region;
            _lidHeight = MathF.Round(region.Size.Y * LidFraction);
            _lid.Texture = new AtlasTexture { Atlas = at.Atlas, Region = new Rect2(region.Position, new Vector2(region.Size.X, _lidHeight)) };
            var k = _scale / frame.Density;
            _lid.Scale = Vector2.One * k;
            _lid.Offset = new Vector2(-frame.Anchor.X, 0);
            _lid.Position = new Vector2(0, -frame.Anchor.Y * k);
            // o corpo de baixo desenha só a metade inferior quando aberto; fechado, a tampa cobre a de cima igual ao sprite
        }

        AddChild(_lid);

        if (PropArt.Chest(Tier) is { } artPath && ArtFrames.TryLoad(artPath, out var art))
        {
            _art = art;
            _artSprite = new Sprite2D { Name = "Art" };
            AddChild(_artSprite);
            _base.Visible = false;
            _lid.Visible = false;
            ShowArt(OpeningFrames[0]);
        }

        _glow = new Sprite2D
        {
            Name = "Glow",
            Texture = glowTexture,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            Position = new Vector2(0, -10f * _scale),
            Modulate = (Purple ? new Color("#E64A8E") : new Color("#F2A541")) with { A = 0f },
            Scale = new Vector2(0.55f, 0.4f) * _scale,
        };
        AddChild(_glow);
        _sparkles = CreateSparkles();
        AddChild(_sparkles);

        var body = new StaticBody2D { Name = "Solid", CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = SolidRadius * _scale * 0.8f } });
        AddChild(body);

        _interact = new WorldInteractable { Name = "Open", Prompt = "Abrir baú", Radius = InteractRadius * _scale, PromptHeight = 30f * _scale, Handler = _ => Open() };
        AddChild(_interact);

        if (alreadyOpen)
        {
            SetOpenVisual(1f);
            ShowArt(EmptyFrame); // já foi saqueado hoje
        }
    }

    /// <summary>Abre (animação + saque). Ignorado se já aberto.</summary>
    public void Open()
    {
        if (IsOpen || IsOpening)
        {
            return;
        }

        _openT = 0f;
        _interact.Enabled = false;
        AudioManager.Instance?.PlaySfx("open", GlobalPosition, pitchJitter: 0f);
    }

    /// <summary>Volta fechado (dia seguinte).</summary>
    public void Close()
    {
        IsOpen = false;
        _openT = -1f;
        _emptyT = -1f;
        if (_art is not null)
        {
            ShowArt(OpeningFrames[0]);
            _glow.Modulate = _glow.Modulate with { A = 0f };
            _interact.Enabled = true;
            return;
        }

        _interact.Enabled = true;
        _lid.Visible = true;
        _lid.Scale = new Vector2(MathF.Abs(_lid.Scale.X), MathF.Abs(_lid.Scale.X));
        _lid.Rotation = 0f;
        _base.Key = $"pr_chest{Tier}";
        _base.Modulate = Colors.White;
        _glow.Modulate = _glow.Modulate with { A = 0f };
    }

    public override void _Process(double delta)
    {
        if (_emptyT >= 0f)
        {
            _emptyT += (float)delta;
            if (_emptyT >= EmptyAfterSeconds)
            {
                _emptyT = -1f;
                ShowArt(EmptyFrame);
            }
        }

        if (_openT < 0f)
        {
            if (IsOpen)
            {
                // brilho fraco de dentro, respirando
                var t = (float)Time.GetTicksMsec() / 1000f;
                _glow.Modulate = _glow.Modulate with { A = 0.18f + (0.06f * MathF.Sin(t * 2.2f)) };
            }

            return;
        }

        _openT += (float)delta;
        var k = Math.Clamp(_openT / OpenSeconds, 0f, 1f);
        SetOpenVisual(k);
        if (k >= 1f)
        {
            _openT = -1f;
            IsOpen = true;
            _sparkles.Restart();
            DropLoot?.Invoke(this);
            if (_art is not null)
            {
                _emptyT = 0f; // o saque saiu: depois de um instante o baú fica vazio
            }

            EventBus.Instance.EmitSignal(EventBus.SignalName.ChestOpened, MapId, Tier, GlobalPosition);
        }
    }

    private void ShowArt(StringName frame)
    {
        if (_art is null || _artSprite is null)
        {
            return;
        }

        ArtFrame = frame.ToString();
        _art.Apply(_artSprite, frame, 0);
    }

    private void SetOpenVisual(float k)
    {
        if (_art is not null)
        {
            // quadros desenhados; pulinho do corpo e brilho de dentro como antes
            var hopK = MathF.Sin(MathF.Min(1f, k * 1.6f) * MathF.PI) * 2.5f * _scale;
            _artSprite!.Position = new Vector2(0, -hopK * 0.35f);
            ShowArt(OpeningFrames[PropArtStates.ChestFrame(k)]);
            var easeA = 1f - MathF.Pow(1f - k, 3f);
            _glow.Modulate = _glow.Modulate with { A = MathF.Sin(MathF.Min(1f, k * 1.3f) * MathF.PI * 0.5f) * (k >= 1f ? 0.24f : 0.65f) };
            _glow.Scale = new Vector2(0.55f + (0.25f * easeA), 0.4f + (0.5f * easeA)) * _scale;
            if (k >= 1f)
            {
                IsOpen = true;
                _interact.Enabled = false;
            }

            return;
        }

        // tampa: pulinho e vira para trás (escala y de 1 até −0,42 mostrando o lado de dentro), com leve giro
        var ease = 1f - MathF.Pow(1f - k, 3f);
        var baseK = MathF.Abs(_lid.Scale.X);
        var flip = 1f - (1.42f * ease);
        _lid.Scale = new Vector2(baseK, baseK * flip);
        _lid.Rotation = MathF.Sin(ease * MathF.PI) * -0.08f;
        _lid.Offset = _lid.Offset with { Y = flip < 0f ? 0f : 0f };
        var hop = MathF.Sin(MathF.Min(1f, k * 1.6f) * MathF.PI) * 2.5f * _scale;
        _base.Position = new Vector2(0, -hop * 0.35f);
        _glow.Modulate = _glow.Modulate with { A = MathF.Sin(MathF.Min(1f, k * 1.3f) * MathF.PI * 0.5f) * (k >= 1f ? 0.24f : 0.65f) };
        _glow.Scale = new Vector2(0.55f + (0.25f * ease), 0.4f + (0.5f * ease)) * _scale;
        if (k >= 1f)
        {
            IsOpen = true;
            _interact.Enabled = false;
            if (Purple)
            {
                // v2: baú roxo aberto = pr_chest4 (world.js:94)
                _base.Key = "pr_chest4";
                _lid.Visible = false;
            }
        }
    }

    private CpuParticles2D CreateSparkles()
    {
        var c = Purple ? new Color("#ff9ac6") : new Color("#ffe08a");
        var ramp = new Gradient { Offsets = [0f, 0.6f, 1f], Colors = [c, c with { A = 0.8f }, c with { A = 0f }] };
        return new CpuParticles2D
        {
            Name = "Sparkles",
            Emitting = false,
            OneShot = true,
            Amount = 18,
            Lifetime = 0.9,
            Explosiveness = 0.8f,
            LocalCoords = false,
            Position = new Vector2(0, -10f * _scale),
            Direction = new Vector2(0, -1),
            Spread = 50f,
            Gravity = new Vector2(0, 40),
            InitialVelocityMin = 25f,
            InitialVelocityMax = 60f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.6f,
            ColorRamp = ramp,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }
}
