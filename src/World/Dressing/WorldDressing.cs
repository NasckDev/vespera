using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Monta e anima o que o layout do mapa descreve (frente B4a, <c>docs/design/api-mundo.md</c>): recortes de oclusão,
/// brilho da água, halos de lampiões e braseiros, fumaça e vagalumes, quebráveis, baús, decoração com interação,
/// objetos do catálogo (B5) e sons ambiente. O <see cref="World"/> chama <see cref="Build"/> em cada mapa carregado,
/// depois de liberar as entidades do mapa anterior. Objetos no y-sort vão para <see cref="World.Entities"/>; o resto
/// fica em raízes próprias com z fixo. Nada é criado por quadro: o <c>_Process</c> só lê posições e ajusta valores.
/// </summary>
public partial class WorldDressing : Node2D
{
    public const string ObjectCatalogPath = "res://src/Data/objects.json";

    private static DressingCatalog? _catalog;
    private static Texture2D? _glowTexture;

    private readonly List<Occluder> _occluders = [];
    private readonly List<Breakable> _breakables = [];
    private readonly List<Chest> _chests = [];
    private readonly List<CatalogObject> _objects = [];
    private readonly List<DecorSpot> _decor = [];
    private readonly IRandom _random = new SeededRandom((int)(Time.GetTicksUsec() & 0x7fffffff));

    private LootTables? _loot;
    private World _world = null!;
    private Node2D _waterRoot = null!;
    private Node2D _decorRoot = null!;
    private double _slowTick;
    private bool _built;

    /// <summary>
    /// Até o combate existir, o ataque do jogador perto de um quebrável chama <see cref="Breakable.Hit"/> (D45). O
    /// combate desliga isto e acerta pelo grupo <see cref="Breakable.HittableGroup"/>.
    /// </summary>
    public static bool FallbackAttackEnabled { get; set; } = true;

    /// <summary>Catálogo extra (smoke: <c>src/Testing/Fixtures/objects_fixture.json</c>), somado ao do jogo.</summary>
    public static string? ExtraCatalogPath { get; set; }

    /// <summary>Catálogo de objetos: <c>src/Data/objects.json</c> (frente B5) + o extra do smoke, se houver.</summary>
    public static DressingCatalog Catalog => _catalog ??= LoadCatalog();

    public LightGlows Lights { get; private set; } = null!;

    private LootTables Loot => _loot ??= new LootTables(GameManager.Db);

    public AmbientFx Fx { get; private set; } = null!;

    public FloatingTextPool Popups { get; private set; } = null!;

    public InteractionPrompt Prompt { get; private set; } = null!;

    public LoreCard Lore { get; private set; } = null!;

    public FishingBar FishingBar { get; private set; } = null!;

    public IReadOnlyList<Occluder> Occluders => _occluders;

    public IReadOnlyList<Breakable> Breakables => _breakables;

    public IReadOnlyList<Chest> Chests => _chests;

    public IReadOnlyList<CatalogObject> Objects => _objects;

    public int WaterCount => _waterRoot?.GetChildCount() ?? 0;

    public int DecorCount => _decor.Count;

    /// <summary>O jogador já viu este baú (entrou na vista da câmera alguma vez): o minimapa mostra.</summary>
    public static bool IsDiscovered(Chest chest) => World.Objects.GetFlag(chest.SeenKey, false);

    /// <summary>Força um catálogo novo na próxima leitura (smoke troca o extra).</summary>
    public static void ReloadCatalog() => _catalog = null;

    public override void _Ready()
    {
        _waterRoot = new Node2D { Name = "Water", ZIndex = -9 };
        _decorRoot = new Node2D { Name = "Decor" };
        Lights = new LightGlows { Name = "Lights", ZIndex = 5 };
        Fx = new AmbientFx { Name = "Fx", ZIndex = 6 };
        Popups = new FloatingTextPool { Name = "Popups" };
        Prompt = new InteractionPrompt { Name = "Prompt" };
        Lore = new LoreCard { Name = "Lore" };
        FishingBar = new FishingBar { Name = "FishingBar" };
        foreach (var n in new Node[] { _waterRoot, _decorRoot, Lights, Fx, Popups, Prompt, Lore, FishingBar })
        {
            AddChild(n);
        }

        _glowTexture ??= new GradientTexture2D
        {
            Gradient = new Gradient { Offsets = [0f, 1f], Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)] },
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 64,
            Height = 64,
        };
    }

    public override void _ExitTree()
    {
        EndActivity();
        AudioManager.Instance?.SetAmbientEmitters([], 0.4f);
    }

    /// <summary>Monta o mapa novo (as entidades do anterior já foram liberadas pelo World).</summary>
    public void Build(World world, MapDef map, MapLayout layout)
    {
        _world = world;
        EndActivity();
        _occluders.Clear();
        _breakables.Clear();
        _chests.Clear();
        _objects.Clear();
        _decor.Clear();
        ClearChildren(_waterRoot);
        ClearChildren(_decorRoot);
        Lore.HideCard();
        Prompt.Player = world.Player;
        Lore.Player = world.Player;

        var rules = WorldScale.Rules;
        var coveredAlpha = (float)(rules?.Occlusion.CoveredAlpha ?? 0.4);
        var fadeRate = (float)(rules?.Occlusion.FadeRate ?? 10.0);
        var texture = world.MapSprite.Texture;

        foreach (var def in layout.Occluders)
        {
            var o = new Occluder { Target = world.Player };
            o.Setup(def, texture, coveredAlpha, fadeRate, WorldFx.Sway);
            world.Entities.AddChild(o);
            _occluders.Add(o);
        }

        if (WorldFx.WaterShimmer)
        {
            foreach (var shape in layout.Water)
            {
                var w = new WaterSurface();
                w.Setup(shape, texture);
                _waterRoot.AddChild(w);
            }
        }

        Lights.Build(map);
        Fx.Build(map, layout);

        for (var i = 0; i < layout.Props.Count; i++)
        {
            var p = layout.Props[i];
            AddBreakable(map, p.Kind, new Vector2(p.X, p.Y), WorldObjectRegistry.Key(map.Id, "prop", i), null);
        }

        for (var i = 0; i < layout.Chests.Count; i++)
        {
            var c = layout.Chests[i];
            AddChest(map, c, WorldObjectRegistry.Key(map.Id, "chest", i));
        }

        for (var i = 0; i < layout.Decor.Count; i++)
        {
            AddDecor(map, layout.Decor[i], i, WorldObjectRegistry.Key(map.Id, "decor", i));
        }

        BuildCatalogObjects(map, layout, coveredAlpha, fadeRate);

        AudioManager.Instance?.SetAmbientEmitters(WorldFx.AmbientSound ? layout.Ambient : []);
        _built = true;
        RefreshSpent();
    }

    /// <summary>Dia seguinte: quebráveis inteiros, baús fechados, flores crescidas (o registro já foi zerado).</summary>
    public void OnNewDay()
    {
        foreach (var b in _breakables)
        {
            if (b.IsBroken)
            {
                b.Restore();
                if (_obstacles is { } area && _breakableObstacles.TryGetValue(b, out var id))
                {
                    area.SetActive(id, true);
                }
            }
        }

        foreach (var c in _chests)
        {
            if (c.IsOpen)
            {
                c.Close();
            }
        }

        RefreshSpent();
    }

    public override void _Process(double delta)
    {
        if (!_built || _world is null || !IsInstanceValid(_world.Player))
        {
            return;
        }

        var dt = MathF.Min(0.05f, (float)delta);
        var player = _world.Player;
        AudioManager.Instance?.UpdateAmbientListener(player.VisualPosition, dt);
        ProcessActivity(dt);

        _slowTick -= dt;
        if (_slowTick <= 0)
        {
            _slowTick = 0.25;
            MarkSeenChests();
            RefreshSpent();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_built)
        {
            return;
        }

        if (HandleActivityInput(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (FallbackAttackEnabled && @event.IsActionPressed(InputActions.Attack) && !(GameManager.Instance?.IsGameplayBlocked ?? true))
        {
            FallbackAttack();
        }
    }

    /// <summary>Golpe provisório: anima o jogador e acerta os quebráveis no alcance e no arco da arma (D45).</summary>
    public int FallbackAttack()
    {
        var player = _world.Player;
        if (player.IsDead || player.Animator.IsAttacking)
        {
            return 0;
        }

        var weapon = GameManager.Db.Weapons.TryGetValue(player.ClassId, out var w) ? w : null;
        var rules = WorldScale.Rules?.FallbackAttack;
        var range = (float)(weapon?.Range ?? rules?.Range ?? 30.0) * MathF.Max(1f, WorldScale.Character * 0.75f);
        var arc = (float)(weapon?.Arc ?? rules?.Arc ?? 2.1);
        var damage = weapon?.Damage ?? 12;
        var facing = player.Facing == Vector2.Zero ? Vector2.Down : player.Facing.Normalized();
        player.Aim = facing.Angle();
        player.PlayAttack(0.3f);
        AudioManager.Instance?.PlaySfx("swing", player.GlobalPosition);

        var hits = 0;
        var origin = player.GlobalPosition + new Vector2(0, -WorldScale.CharacterHeight * 0.3f);
        foreach (var b in _breakables)
        {
            if (b.IsBroken || !IsInstanceValid(b))
            {
                continue;
            }

            var to = b.GlobalPosition + new Vector2(0, -6f) - origin;
            if (to.Length() > range + 8f)
            {
                continue;
            }

            if (to.LengthSquared() > 64f && MathF.Abs(Mathf.AngleDifference(facing.Angle(), to.Angle())) > arc / 2f)
            {
                continue;
            }

            b.Hit(damage, player.GlobalPosition);
            hits++;
        }

        if (hits > 0)
        {
            _world.Camera.Shake(1.5f);
        }

        return hits;
    }

    // --- montagem ---

    private Breakable AddBreakable(MapDef map, string kind, Vector2 at, string key, Sprite2D? visual)
    {
        var rules = WorldScale.Rules;
        var hp = rules is null ? 12 : PropHealth.HpFor(kind, rules.Props.Hp, rules.Props.FallbackHp);
        var broken = World.Objects.IsSpent(key, CurrentDay, Now);
        var b = new Breakable { DropLoot = DropPropLoot };
        _world.Entities.AddChild(b);
        b.Setup(map.Id, kind, at, key, hp, broken, visual);
        b.Broken += OnPropBroken;
        _breakables.Add(b);
        return b;
    }

    private Chest AddChest(MapDef map, LayoutChest def, string key)
    {
        var open = World.Objects.IsSpent(key, CurrentDay, Now);
        var c = new Chest { DropLoot = DropChestLoot };
        _world.Entities.AddChild(c);
        c.Setup(map.Id, def, open, _glowTexture!);
        c.Key = key;
        _chests.Add(c);
        return c;
    }

    private void BuildCatalogObjects(MapDef map, MapLayout layout, float coveredAlpha, float fadeRate)
    {
        if (layout.Objects.Count == 0)
        {
            return;
        }

        var catalog = Catalog;
        for (var i = 0; i < layout.Objects.Count; i++)
        {
            var placed = layout.Objects[i];
            if (!catalog.TryGet(placed.Id, out var def))
            {
                Log.Warning($"{map.Id}: objeto '{placed.Id}' não existe no catálogo ({ObjectCatalogPath}).");
                continue;
            }

            var at = new Vector2(placed.X, placed.Y);
            var key = WorldObjectRegistry.Key(map.Id, "object", i);
            if (def.Breakable is { } kind)
            {
                AddBreakable(map, kind, at, key, ObjectVisuals.Create(def, placed.Flip, out _));
                continue;
            }

            if (def.Chest is { } tier)
            {
                AddChest(map, new LayoutChest(placed.X, placed.Y, tier), key);
                continue;
            }

            var o = new CatalogObject { Target = _world.Player };
            _world.Entities.AddChild(o);
            o.Setup(def, placed, coveredAlpha, fadeRate, WorldFx.Sway);
            _objects.Add(o);

            if (def.Light is { } light)
            {
                Lights.AddLight(at + new Vector2(placed.Flip ? -light.X : light.X, light.Y), light.Radius, light.Kind);
            }

            if (def.Interact is { } interact)
            {
                var decor = new LayoutDecor(interact.Kind, placed.X, placed.Y, interact.Radius, interact.Label, interact.Text, null);
                AddDecor(map, decor, layout.Decor.Count + i, key, MathF.Max(24f, o.Bounds.Height + 6f));
            }
        }
    }

    // --- saque ---

    private void DropPropLoot(Breakable b)
    {
        var drop = Loot.PropDrop(ThemeOf(_world.Map), _random);
        if (drop is { } d)
        {
            WorldLoot.Grant(d, b.GlobalPosition + new Vector2(0, -8f), Popups);
        }
        else
        {
            Popups.Show(b.GlobalPosition + new Vector2(0, -14f), "vazio", null, WorldUiStyle.Ink with { A = 0.7f });
        }
    }

    private void OnPropBroken(Breakable b)
    {
        World.Objects.MarkSpent(b.Key, CurrentDay, Now);
        if (_obstacles is { } area && _breakableObstacles.TryGetValue(b, out var id))
        {
            area.SetActive(id, false);
        }
    }

    // --- obstáculos para quem planeja caminho ---

    private readonly Dictionary<Breakable, int> _breakableObstacles = [];
    private ObstacleWalkArea? _obstacles;

    /// <summary>
    /// Carimba na área andável os corpos sólidos montados neste mapa (quebráveis inteiros, baús, objetos com footprint),
    /// para NPCs, bots e o <c>WalkPathfinder</c> desviarem do que o jogador encontra. Chamado pelo <see cref="World"/>
    /// logo depois do <see cref="Build"/>.
    /// </summary>
    public void StampObstacles(ObstacleWalkArea area)
    {
        _obstacles = area;
        _breakableObstacles.Clear();
        foreach (var b in _breakables)
        {
            var id = StampBodies(area, b);
            if (id >= 0)
            {
                _breakableObstacles[b] = id;
                area.SetActive(id, !b.IsBroken);
            }
        }

        foreach (var c in _chests)
        {
            StampBodies(area, c);
        }

        foreach (var o in _objects)
        {
            StampBodies(area, o);
        }
    }

    /// <summary>Junta as formas dos <c>StaticBody2D</c> filhos de <paramref name="owner"/> num obstáculo (−1 se não tem).</summary>
    private static int StampBodies(ObstacleWalkArea area, Node owner)
    {
        var first = -1;
        foreach (var child in owner.GetChildren())
        {
            if (child is not StaticBody2D body)
            {
                continue;
            }

            foreach (var shapeNode in body.GetChildren())
            {
                var (points, xf) = shapeNode switch
                {
                    CollisionPolygon2D cp => (cp.Polygon, cp.GlobalTransform),
                    CollisionShape2D { Shape: ConvexPolygonShape2D convex } cs => (convex.Points, cs.GlobalTransform),
                    CollisionShape2D { Shape: CircleShape2D circle } cs => (Circle(circle.Radius), cs.GlobalTransform),
                    _ => (null, Transform2D.Identity),
                };
                if (points is null || points.Length < 3)
                {
                    continue;
                }

                var poly = new MapPoint[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    var g = xf * points[i];
                    poly[i] = new MapPoint(g.X, g.Y);
                }

                var id = area.Add(poly);
                first = first < 0 ? id : first;
            }
        }

        return first;
    }

    private static Vector2[] Circle(float r)
    {
        var points = new Vector2[12];
        for (var i = 0; i < points.Length; i++)
        {
            var a = MathF.Tau * i / points.Length;
            points[i] = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }

        return points;
    }

    private void DropChestLoot(Chest c)
    {
        var drops = Loot.ChestDrops(c.Purple, _random);
        for (var i = 0; i < drops.Count; i++)
        {
            WorldLoot.Grant(drops[i], c.GlobalPosition + new Vector2((i % 3 - 1) * 6f, -14f - ((i / 3) * 8f)), Popups, i * 0.12f);
        }

        World.Objects.MarkSpent(c.Key, CurrentDay, Now);
    }

    /// <summary>Tema do saque de quebráveis: o do mapa (Brechas) ou o primeiro tema (cidade e interiores, D45).</summary>
    private static ThemeDef ThemeOf(MapDef map)
    {
        var db = GameManager.Db;
        return map.Theme is { } t && db.Themes.TryGetValue(t, out var theme) ? theme : db.Themes[db.Rules.Brecha.FirstTheme];
    }

    // --- estado ---

    private static int CurrentDay => GameManager.Instance is { } gm ? SafeDay(gm) : 1;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private static int SafeDay(GameManager gm)
    {
        try
        {
            return gm.Day;
        }
        catch (InvalidOperationException)
        {
            return 1;
        }
    }

    private void MarkSeenChests()
    {
        if (_chests.Count == 0)
        {
            return;
        }

        var view = new Rect2(_world.Camera.Position, _world.Camera.VisibleSize);
        foreach (var c in _chests)
        {
            if (view.HasPoint(c.GlobalPosition))
            {
                World.Objects.SetFlag(c.SeenKey, true);
            }
        }
    }

    private static void ClearChildren(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static DressingCatalog LoadCatalog()
    {
        var catalog = DressingCatalog.Empty;
        foreach (var path in new[] { ObjectCatalogPath, ExtraCatalogPath })
        {
            if (path is null || !FileAccess.FileExists(path))
            {
                continue;
            }

            try
            {
                catalog = catalog.With(DressingCatalog.Parse(FileAccess.GetFileAsString(path)));
            }
            catch (MapFormatException ex)
            {
                Log.Error($"{path}: {ex.Message}");
            }
        }

        return catalog;
    }
}
