using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Art;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Quebrável do layout (atlas <c>pr_barrel</c>, <c>pr_crate</c>, <c>pr_crate2</c>, <c>pr_sack</c>, <c>pr_urn</c>; DECISIONS D45):
/// sólido (elipse <c>dx² + 2,2·dy² &lt; r²</c> da v2, r = 7 × propScale), com vida por tipo nos dados. Cada golpe treme e
/// toca <c>hit</c>; ao quebrar solta estilhaços, toca <c>brk</c> e dá o saque de <c>LootTables.PropDrop</c> com texto subindo.
/// Fica quebrado até o dia seguinte (<see cref="World.Objects"/>). O combate acerta pelo grupo <see cref="HittableGroup"/>
/// chamando <see cref="Hit"/>.
/// Barril e caixote têm a arte de estados do designer (DECISIONS C2-D5): inteiro → rachado (metade da vida) → quebrado,
/// que fica no chão como destroço (sem colisão) até o dia seguinte, e os 6 estilhaços desenhados voam do lugar de cada
/// pedaço. Os outros tipos continuam no atlas.
/// </summary>
public partial class Breakable : Node2D
{
    /// <summary>Grupo dos alvos que aceitam <c>Hit(int dano, Vector2 origem)</c>.</summary>
    public const string HittableGroup = "hittable";

    private const float SolidRadius = 7f; // world.js: props sólidos r=7
    private const float ShakeSeconds = 0.28f;

    private Sprite2D _sprite = null!;
    private AtlasSprite _shadow = null!;
    private StaticBody2D _body = null!;
    private CpuParticles2D _debris = null!;
    private PropHealth _health = null!;
    private float _shakeT = -1f;
    private float _shakeAmp;
    private float _hitDirection = 1f;
    private float _scaleK = 1f;
    private ArtFrames? _art;
    private Sprite2D[] _shards = [];
    private Vector2[] _shardVel = [];
    private float[] _shardSpin = [];
    private float _shardT = -1f;
    private const float ShardSeconds = 0.75f;

    private static readonly StringName StageWhole = "whole";
    private static readonly StringName StageCracked = "cracked";
    private static readonly StringName StageBroken = "broken";
    private static readonly StringName[] ShardNames = ["shard_0", "shard_1", "shard_2", "shard_3", "shard_4", "shard_5"];

    public string Kind { get; private set; } = "barrel";

    public string Key { get; private set; } = string.Empty;

    public string MapId { get; private set; } = string.Empty;

    public bool IsBroken => _health.IsBroken;

    /// <summary>Estado desenhado agora (só com arte de estados; sem ela, <see cref="BreakableStage.Whole"/> até sumir).</summary>
    public BreakableStage Stage { get; private set; } = BreakableStage.Whole;

    /// <summary>Usa a arte de estados do designer.</summary>
    public bool HasStateArt => _art is not null;

    /// <summary>Estilhaços voando agora.</summary>
    public bool ShardsFlying => _shardT >= 0f;

    public int Hp => _health.Hp;

    public int MaxHp => _health.MaxHp;

    /// <summary>Tremendo agora (levou golpe há menos de 0,28 s).</summary>
    public bool IsShaking => _shakeT >= 0f;

    /// <summary>Quando o quebrável quebra (o saque já foi dado).</summary>
    public event Action<Breakable>? Broken;

    /// <summary>Saque ao quebrar: o dono (WorldDressing) sorteia e entrega.</summary>
    public Action<Breakable>? DropLoot { get; set; }

    /// <summary>
    /// Monta o quebrável <paramref name="kind"/> com o pé em <paramref name="position"/>. <paramref name="visual"/> troca o
    /// sprite <c>pr_{kind}</c> do atlas (objetos do catálogo com <c>breakable</c>).
    /// </summary>
    public void Setup(string mapId, string kind, Vector2 position, string key, int hp, bool alreadyBroken, Sprite2D? visual = null)
    {
        MapId = mapId;
        Kind = kind;
        Key = key;
        Name = $"Prop_{kind}_{(int)position.X}_{(int)position.Y}";
        Position = position;
        _health = new PropHealth(hp);
        _scaleK = WorldScale.Prop;

        // v2 (render.js:133): sombra escala 0,8 alpha 0,4 + sprite
        _shadow = new AtlasSprite { Key = "shadow", ScaleClass = SpriteScaleClass.Prop, SpriteScale = 0.8f, Modulate = new Color(1, 1, 1, 0.4f), Position = new Vector2(0, 1) };
        var artPath = visual is null || IsCatalogArtSource(visual) ? PropArt.Breakable(kind) : null;
        if (artPath is not null && ArtFrames.TryLoad(artPath, out var art))
        {
            _art = art;
            visual?.QueueFree();
            _sprite = new Sprite2D { Name = "Art" };
            art.Apply(_sprite, StageWhole, 0);
        }
        else
        {
            _sprite = visual ?? new AtlasSprite { Key = $"pr_{kind}", ScaleClass = SpriteScaleClass.Prop };
        }

        AddChild(_shadow);
        AddChild(_sprite);
        CreateShards();

        _body = new StaticBody2D { Name = "Solid", CollisionLayer = 1, CollisionMask = 0 };
        var r = SolidRadius * _scaleK;
        var ry = r / MathF.Sqrt(2.2f);
        var ellipse = new Vector2[10];
        for (var i = 0; i < ellipse.Length; i++)
        {
            var a = MathF.Tau * i / ellipse.Length;
            ellipse[i] = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * ry);
        }

        _body.AddChild(new CollisionShape2D { Shape = new ConvexPolygonShape2D { Points = ellipse } });
        AddChild(_body);
        _debris = CreateDebris(kind);
        AddChild(_debris);
        AddToGroup(HittableGroup);

        if (alreadyBroken)
        {
            _health.Hit(_health.MaxHp);
            SetBrokenVisual();
        }
    }

    /// <summary>Volta inteiro (dia seguinte).</summary>
    public void Restore()
    {
        _health = new PropHealth(_health.MaxHp);
        _sprite.Visible = true;
        _shadow.Visible = true;
        _sprite.Modulate = Colors.White;
        SetStage(BreakableStage.Whole);
        _shardT = -1f;
        foreach (var shard in _shards)
        {
            shard.Visible = false;
        }

        _body.ProcessMode = ProcessModeEnum.Inherit;
        _body.GetChild<CollisionShape2D>(0).SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
        if (!IsInGroup(HittableGroup))
        {
            AddToGroup(HittableGroup);
        }
    }

    /// <summary>
    /// Golpe de <paramref name="damage"/> vindo de <paramref name="origin"/> (posição de quem bateu, para o tremor ir
    /// para o lado certo). Devolve o resultado (ignorado se já quebrado).
    /// </summary>
    public PropHitResult Hit(int damage, Vector2 origin)
    {
        if (_health is null)
        {
            return PropHitResult.Ignored;
        }

        var result = _health.Hit(damage);
        if (result == PropHitResult.Ignored)
        {
            return result;
        }

        _hitDirection = MathF.Sign(GlobalPosition.X - origin.X) is var s && s != 0 ? s : 1f;
        _shakeT = 0f;
        _shakeAmp = 1.2f + (2.2f * (1f - _health.Fraction));
        if (result == PropHitResult.Damaged)
        {
            SetStage(PropArtStates.Breakable(_health.Hp, _health.MaxHp));
            AudioManager.Instance?.PlaySfx("hit", GlobalPosition);
            return result;
        }

        LaunchShards();

        AudioManager.Instance?.PlaySfx("brk", GlobalPosition);
        _debris.GlobalPosition = GlobalPosition + new Vector2(0, -8f * _scaleK);
        _debris.Restart();
        SetBrokenVisual();
        DropLoot?.Invoke(this);
        Broken?.Invoke(this);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PropBroken, MapId, Kind, GlobalPosition);
        return result;
    }

    public override void _Process(double delta)
    {
        if (_shardT >= 0f)
        {
            StepShards(MathF.Min(0.05f, (float)delta));
        }

        if (_shakeT < 0f)
        {
            return;
        }

        _shakeT += (float)delta;
        if (_shakeT >= ShakeSeconds)
        {
            _shakeT = -1f;
            _sprite.Position = Vector2.Zero;
            _sprite.Rotation = 0f;
            return;
        }

        var k = 1f - (_shakeT / ShakeSeconds);
        _sprite.Position = new Vector2(MathF.Sin(_shakeT * 60f) * _shakeAmp * k * _hitDirection, 0);
        _sprite.Rotation = MathF.Sin(_shakeT * 45f) * 0.06f * k * _hitDirection;
    }

    private void SetBrokenVisual()
    {
        if (_art is not null)
        {
            // o quebrado fica no chão como destroço até o dia seguinte (sem colisão, sem sombra)
            SetStage(BreakableStage.Broken);
            _sprite.Position = Vector2.Zero;
            _sprite.Rotation = 0f;
        }
        else
        {
            _sprite.Visible = false;
        }

        _shadow.Visible = false;
        _body.GetChild<CollisionShape2D>(0).SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        RemoveFromGroup(HittableGroup);
    }

    private void SetStage(BreakableStage stage)
    {
        Stage = stage;
        if (_art is null)
        {
            return;
        }

        _art.Apply(_sprite, stage switch { BreakableStage.Broken => StageBroken, BreakableStage.Cracked => StageCracked, _ => StageWhole }, 0);
    }

    /// <summary>O visual passado é o PNG do catálogo de onde a arte de estados saiu (barril/caixote comuns).</summary>
    private static bool IsCatalogArtSource(Sprite2D visual) =>
        visual.Texture is { ResourcePath: { Length: > 0 } path } && PropArt.ForCatalogSprite(path).Role == PropArtRole.Breakable;

    /// <summary>6 estilhaços pré-criados (nada é criado ao quebrar): cada PNG tem o pedaço no lugar dele na tela do barril.</summary>
    private void CreateShards()
    {
        if (_art is null)
        {
            return;
        }

        var count = 0;
        foreach (var name in ShardNames)
        {
            count += _art.Has(name) ? 1 : 0;
        }

        _shards = new Sprite2D[count];
        _shardVel = new Vector2[count];
        _shardSpin = new float[count];
        _shardCenter = new Vector2[count];
        var upp = _art.UnitsPerPixel(StageWhole);
        var anchor = _art.Anchor(StageWhole);
        for (var i = 0; i < count; i++)
        {
            var shard = new Sprite2D { Name = $"Shard{i}", Visible = false, ZIndex = 1 };
            _art.Apply(shard, ShardNames[i], 0);
            AddChild(shard);
            _shards[i] = shard;
            _shardCenter[i] = ShardCenter(ShardNames[i], anchor, upp);
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, Vector2> ShardCenters = [];
    private Vector2[] _shardCenter = [];

    /// <summary>Centro do pedaço relativo ao pé (u), lido uma vez por PNG: define para onde ele voa.</summary>
    private Vector2 ShardCenter(StringName name, Vector2 anchor, float upp)
    {
        var key = _art!.Path + ":" + name;
        if (!ShardCenters.TryGetValue(key, out var center))
        {
            var tex = _art.Texture(name, 0);
            var used = tex?.GetImage() is { } img ? img.GetUsedRect() : new Rect2I(Vector2I.Zero, Vector2I.One);
            center = ((Vector2)used.Position + ((Vector2)used.Size / 2f) - anchor) * upp;
            ShardCenters[key] = center;
        }

        return center;
    }

    private void LaunchShards()
    {
        if (_shards.Length == 0 || _art is null)
        {
            return;
        }

        for (var i = 0; i < _shards.Length; i++)
        {
            var shard = _shards[i];
            var center = _shardCenter[i];
            var side = center.X == 0f ? (i % 2 == 0 ? 1f : -1f) : MathF.Sign(center.X);
            var lift = 0.55f + (0.45f * Math.Clamp(-center.Y / 20f, 0f, 1f)); // pedaços de cima sobem mais
            _shardVel[i] = new Vector2((side * (26f + (i * 4f))) + (_hitDirection * 10f), -(38f + (i * 5f)) * lift);
            _shardSpin[i] = ((i % 2 == 0) ? 1f : -1f) * (5f + i);
            shard.Position = Vector2.Zero;
            shard.Rotation = 0f;
            shard.Modulate = Colors.White;
            shard.Visible = true;
        }

        _shardT = 0f;
    }

    private void StepShards(float dt)
    {
        _shardT += dt;
        var fade = Math.Clamp((_shardT - (ShardSeconds * 0.55f)) / (ShardSeconds * 0.45f), 0f, 1f);
        for (var i = 0; i < _shards.Length; i++)
        {
            var shard = _shards[i];
            _shardVel[i] += new Vector2(0f, 210f * dt);
            var p = shard.Position + (_shardVel[i] * dt);
            if (p.Y > 2f)
            {
                // quica no chão e escorrega
                p.Y = 2f;
                _shardVel[i] = new Vector2(_shardVel[i].X * 0.55f, -_shardVel[i].Y * 0.3f);
                _shardSpin[i] *= 0.5f;
            }

            shard.Position = p;
            shard.Rotation += _shardSpin[i] * dt;
            shard.Modulate = new Color(1f, 1f, 1f, 1f - fade);
        }

        if (_shardT >= ShardSeconds)
        {
            _shardT = -1f;
            foreach (var shard in _shards)
            {
                shard.Visible = false;
            }
        }
    }

    private CpuParticles2D CreateDebris(string kind)
    {
        // cores do material: madeira (barril, caixas), pano (saco), cerâmica (urna)
        var (a, b) = kind switch
        {
            "sack" => (new Color("#8a6a45"), new Color("#c9a56b")),
            "urn" => (new Color("#4a3d52"), new Color("#8a7f9a")),
            _ => (new Color("#6b4426"), new Color("#a8743e")),
        };
        var ramp = new Gradient { Offsets = [0f, 0.7f, 1f], Colors = [b, a, a with { A = 0f }] };
        return new CpuParticles2D
        {
            Name = "Debris",
            Emitting = false,
            OneShot = true,
            Amount = 16,
            Lifetime = 0.7,
            Explosiveness = 0.95f,
            LocalCoords = false,
            Direction = new Vector2(0, -1),
            Spread = 75f,
            Gravity = new Vector2(0, 180),
            InitialVelocityMin = 40f,
            InitialVelocityMax = 95f,
            AngularVelocityMin = -360f,
            AngularVelocityMax = 360f,
            ScaleAmountMin = 1.5f,
            ScaleAmountMax = 3.2f,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(6 * _scaleK, 6 * _scaleK),
        };
    }
}
