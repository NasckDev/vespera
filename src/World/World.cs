using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Mundo da v2: carrega qualquer mapa do <c>maps.json</c> por id (pintura, colisão, interagíveis), testa saídas,
/// porta e fuga pelo ponto do pé do jogador e troca de mapa com <see cref="GoMap(string, Vector2, bool)"/>
/// (fade da spec 01 §1.2/§1.7). Entidades do mapa (mobs, props, NPCs…) vão em <see cref="Entities"/> (y-sort pelo pé)
/// e são liberadas na troca de mapa; só o jogador fica.
/// Motor (frente B4a): lê o <c>layout_&lt;id&gt;.json</c> do mapa (<see cref="Layout"/>): colisão do <c>walk_&lt;id&gt;.png</c>
/// gerado pela ferramenta (ou das formas do layout), e o <see cref="WorldDressing"/> monta oclusores, água, luzes,
/// quebráveis, baús, decoração, efeitos e sons ambiente.
/// </summary>
public partial class World : Node2D
{
    public const string MapsPath = "res://src/Data/maps.json";
    public const string CityMapId = "forge";
    public const string CoreMapId = "core";

    private const float MaxDelta = 0.05f; // main.js:18
    private const float FadeOutRate = 3.5f; // main.js:81 (≈0,29 s até preto)
    private const float FadeInRate = 3f; // main.js:81 (≈0,33 s)
    private const float BackPushUp = 14f; // world.js:115 (D6: um empurrão de 14 u, não 28)
    private const double BackCooldownSeconds = 1.0;
    private const string TransitionBlock = "map-transition";

    private static readonly Color FadeColor = new(8 / 255f, 6 / 255f, 14 / 255f); // render.js: rgba(8,6,14,fade)
    private static readonly Color ClearColor = new("#0b0a14"); // render.js:21
    private static readonly Dictionary<string, ShapeWalkArea> ShapeAreas = [];
    private static MapCatalog? _catalog;
    private static WorldObjectRegistry? _objects;

    private readonly Dictionary<string, Interactable> _interactables = [];
    private float _fade;
    private float _fadeTo;
    private Action? _fadeCallback;
    private double _backCooldown;
    private Color _previousClearColor;

    [Export]
    public Sprite2D MapSprite { get; set; } = null!;

    /// <summary>Recebe o corpo de colisão do mapa atual (MaskCollision ou ShapeCollision).</summary>
    [Export]
    public Node2D CollisionRoot { get; set; } = null!;

    /// <summary>Raiz com y-sort: jogador e tudo que anda ou fica de pé no mapa.</summary>
    [Export]
    public Node2D Entities { get; set; } = null!;

    [Export]
    public Node2D InteractablesRoot { get; set; } = null!;

    [Export]
    public Player.Player Player { get; set; } = null!;

    [Export]
    public WorldCamera Camera { get; set; } = null!;

    [Export]
    public ColorRect FadeRect { get; set; } = null!;

    /// <summary>Monta o que o layout descreve (oclusores, água, luzes, objetos, efeitos, sons). Frente B4a.</summary>
    [Export]
    public WorldDressing Dressing { get; set; } = null!;

    /// <summary>Minimapa do canto (M / Tab / Back em tela cheia). Frente B4a.</summary>
    [Export]
    public Minimap Minimap { get; set; } = null!;

    /// <summary>Mapa inicial (novo jogo da v2: Guilda em (250, 560), <c>main.js:71</c>).</summary>
    [Export]
    public string StartMapId { get; set; } = "guild";

    [Export]
    public Vector2 StartPosition { get; set; } = new(250, 560);

    /// <summary>Se definido antes de a cena entrar, substitui o início (ex.: "Continuar" → Guilda (250, 330)).</summary>
    public static (string MapId, Vector2 Position)? PendingStart { get; set; }

    public static MapCatalog Catalog => _catalog ??= MapCatalog.Parse(FileAccess.GetFileAsString(MapsPath));

    /// <summary>Quebráveis quebrados, baús abertos, flores colhidas e lampiões da sessão (voltam no dia seguinte, D45).</summary>
    public static WorldObjectRegistry Objects => _objects ??= new WorldObjectRegistry();

    /// <summary>
    /// Layouts trocados por id de mapa (caminho <c>res://</c>). Só o smoke usa (fixtures em <c>src/Testing/Fixtures</c>);
    /// o jogo lê <c>layout_&lt;id&gt;.json</c> da pasta da pintura.
    /// </summary>
    public static Dictionary<string, string> LayoutOverrides { get; } = [];

    /// <summary>Layout do mapa atual (vazio se o mapa não tem).</summary>
    public MapLayout Layout { get; private set; } = MapLayout.Empty;

    /// <summary>De onde veio a colisão do mapa atual: <c>walk-png</c>, <c>layout</c>, <c>mask</c> ou <c>shapes</c>.</summary>
    public string CollisionSource { get; private set; } = string.Empty;

    public MapDef Map { get; private set; } = null!;

    public string MapId => Map.Id;

    /// <summary>Área andável do mapa atual (máscara ou formas), em u.</summary>
    public IWalkArea WalkArea { get; private set; } = null!;

    /// <summary>Verdadeiro do pedido de troca até o mapa novo carregar (<c>fadeTo &gt; 0</c>): sem saídas nem movimento.</summary>
    public bool IsTransitioning => _fadeTo > 0f;

    /// <summary>Opacidade atual do fade (0–1).</summary>
    public float FadeAlpha => _fade;

    /// <summary>Porta da sala do tema liberada (ondas limpas). Volta a falso a cada troca de mapa.</summary>
    public bool DoorOpen { get; set; }

    /// <summary>Quantas trocas de mapa já aconteceram (útil para testes).</summary>
    public int MapLoads { get; private set; }

    public override void _Ready()
    {
        _previousClearColor = RenderingServer.GetDefaultClearColor();
        RenderingServer.SetDefaultClearColor(ClearColor);
        Camera.Target = Player;
        // o foco fica na mesma altura relativa do corpo que na v2 (−14 u num personagem de 32 u), D41
        Camera.FocusOffsetY = CameraFraming.FocusOffsetY * WorldScale.Character;
        Minimap.World = this;
        EventBus.Instance.DayChanged += OnDayChanged;

        var (mapId, position) = PendingStart ?? (StartMapId, StartPosition);
        PendingStart = null;
        GoMap(mapId, position, instant: true);
    }

    public override void _ExitTree()
    {
        RenderingServer.SetDefaultClearColor(_previousClearColor);
        GameManager.Instance?.UnblockGameplay(TransitionBlock);
        if (EventBus.Instance is { } bus)
        {
            bus.DayChanged -= OnDayChanged;
        }
    }

    /// <summary>Troca de mapa. Normal: fade até preto, carrega, fade de volta. <paramref name="instant"/>: já carrega e só clareia.</summary>
    public void GoMap(string mapId, Vector2 at, bool instant = false)
    {
        if (!Catalog.TryGet(mapId, out var map))
        {
            Log.Error($"GoMap: mapa '{mapId}' não existe.");
            return;
        }

        if (instant)
        {
            _fadeCallback = null;
            Load(map, at);
            _fade = 1f;
            _fadeTo = 0f;
            ApplyFade();
            return;
        }

        if (IsTransitioning)
        {
            return; // já trocando; a v2 também não testa saídas com fadeTo > 0
        }

        _fadeTo = 1f;
        _fadeCallback = () =>
        {
            Load(map, at);
            _fadeTo = 0f;
        };
        GameManager.Instance.BlockGameplay(TransitionBlock);
    }

    public void GoMap(string mapId, float x, float y, bool instant = false) => GoMap(mapId, new Vector2(x, y), instant);

    /// <summary>Sai da Brecha para a Rua da Forja, em frente ao portal do <paramref name="portalSpot"/> (<c>world.js:126</c>).</summary>
    public void LeaveDungeon(int portalSpot)
    {
        var spots = Catalog[CityMapId].PortalSpots;
        var spot = spots[Math.Clamp(portalSpot, 0, spots.Count - 1)];
        GoMap(CityMapId, spot.X + MapDef.PortalReturnOffset.X, spot.Y + MapDef.PortalReturnOffset.Y);
    }

    /// <summary>Interagível do mapa atual pelo id do maps.json (ex.: <c>portal0</c>), ou null.</summary>
    public Interactable? GetInteractable(string id) => _interactables.GetValueOrDefault(id);

    /// <summary>Liga/desliga um interagível e opcionalmente troca o rótulo (ex.: <c>"Brecha · Mercado"</c>).</summary>
    public void SetInteractableEnabled(string id, bool enabled, string? label = null)
    {
        if (_interactables.TryGetValue(id, out var it))
        {
            it.Enabled = enabled;
            if (label is not null)
            {
                it.Prompt = label;
            }
        }
    }

    /// <summary>Ponto onde dá para ficar de pé mais perto de <paramref name="at"/> (o próprio, se já serve).</summary>
    public Vector2 CorrectToWalkable(Vector2 at)
    {
        if (WalkArea.CanStand(at.X, at.Y))
        {
            return at;
        }

        if (WalkArea.NearestWalkable(at.X, at.Y) is { } fixedPoint)
        {
            // DECISIONS D6: spawn/chegada em célula bloqueada é corrigido, não reproduzido.
            Log.Warning($"{MapId}: ponto ({at.X}, {at.Y}) bloqueado; corrigido para ({fixedPoint.X}, {fixedPoint.Y}).");
            return new Vector2(fixedPoint.X, fixedPoint.Y);
        }

        Log.Error($"{MapId}: nenhum ponto andável perto de ({at.X}, {at.Y}).");
        return at;
    }

    public override void _Process(double delta)
    {
        var dt = MathF.Min(MaxDelta, (float)delta);
        if (_fadeTo > _fade)
        {
            _fade = MathF.Min(1f, _fade + (dt * FadeOutRate));
        }
        else if (_fadeTo < _fade)
        {
            _fade = MathF.Max(0f, _fade - (dt * FadeInRate));
        }

        if (_fade >= 1f && _fadeCallback is { } callback)
        {
            _fadeCallback = null;
            callback();
        }

        ApplyFade();
    }

    public override void _PhysicsProcess(double delta)
    {
        _backCooldown = Math.Max(0, _backCooldown - delta);
        if (IsTransitioning)
        {
            return;
        }

        var foot = Player.GlobalPosition;

        // main.js:110 — saídas por retângulo, ponto do pé.
        if (Map.FindExit(foot.X, foot.Y) is { } exit)
        {
            GoMap(exit.To, exit.At.X, exit.At.Y);
            return;
        }

        // world.js:114 — porta liberada leva à Câmara do Núcleo.
        if (DoorOpen && Map.Door is { } door && door.Contains(foot.X, foot.Y))
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.DungeonDoorEntered, MapId);
            var core = Catalog[CoreMapId];
            GoMap(CoreMapId, core.Arrivals.TryGetValue("daPortaDaBrecha", out var arrival) ? new Vector2(arrival.X, arrival.Y) : new Vector2(core.Spawn.X, core.Spawn.Y));
            return;
        }

        // world.js:115 — andando dentro de back: empurra para cima e pede o diálogo de fuga.
        if (Map.Back is { } back && back.Contains(foot.X, foot.Y) && Player.IsMoving && _backCooldown <= 0)
        {
            _backCooldown = BackCooldownSeconds;
            Player.MoveAndCollide(new Vector2(0, -BackPushUp));
            EventBus.Instance.EmitSignal(EventBus.SignalName.DungeonEscapeRequested, MapId);
        }
    }

    private void Load(MapDef map, Vector2 at)
    {
        Map = map;
        MapLoads++;
        MapSprite.Texture = GD.Load<Texture2D>(map.Image);
        Layout = LoadLayout(map);
        BuildCollision(map);
        ClearMapEntities();
        BuildInteractables(map);
        DoorOpen = false;
        _backCooldown = 0;

        Player.Teleport(CorrectToWalkable(at));
        Dressing.Build(this, map, Layout);

        // Quem planeja caminho (NPCs da cidade, bots do smoke) enxerga também os corpos sólidos que o motor montou.
        var obstacles = new ObstacleWalkArea(WalkArea);
        Dressing.StampObstacles(obstacles);
        WalkArea = obstacles;
        Camera.SetMap(new Vector2(map.ImageWidth, map.ImageHeight), map.ViewHeight);
        GameManager.Instance.UnblockGameplay(TransitionBlock);

        Log.Info($"Mapa: {map.Id} ({map.Name}) em ({Player.GlobalPosition.X:0}, {Player.GlobalPosition.Y:0}).");
        EventBus.Instance.EmitSignal(EventBus.SignalName.MapChanged, map.Id, map.Name);
        if (map.Dungeon)
        {
            // Brechas tocam a música da cidade invertida, com reverb (GDD §6, D36).
            AudioManager.Instance?.RegisterLocationMusic(map.Name, "brecha", reverb: true);
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.LocationChanged, map.Name);
    }

    /// <summary>Caminho do layout do mapa: <c>layout_&lt;id&gt;.json</c> na pasta da pintura (ou o override do smoke).</summary>
    public static string LayoutPath(MapDef map) =>
        LayoutOverrides.TryGetValue(map.Id, out var path) ? path : $"{map.Image.GetBaseDir()}/layout_{map.Id}.json";

    /// <summary>Máscara gerada por <c>tools/maps/layout.py build</c> (células de <c>layout.cell</c> u).</summary>
    public static string WalkMaskPath(MapDef map) => $"{map.Image.GetBaseDir()}/walk_{map.Id}.png";

    private static MapLayout LoadLayout(MapDef map)
    {
        var path = LayoutPath(map);
        if (!FileAccess.FileExists(path))
        {
            return MapLayout.Empty;
        }

        try
        {
            return MapLayout.Parse(FileAccess.GetFileAsString(path));
        }
        catch (MapFormatException ex)
        {
            Log.Error($"{map.Id}: {path} inválido ({ex.Message}); seguindo sem layout.");
            return MapLayout.Empty;
        }
    }

    private void BuildCollision(MapDef map)
    {
        foreach (var child in CollisionRoot.GetChildren())
        {
            CollisionRoot.RemoveChild(child);
            child.QueueFree();
        }

        // D40: a colisão redesenhada pela frente de mapa ganha da v2. PNG gerado pela ferramenta primeiro; sem PNG,
        // a mesma conta em C# a partir das formas do layout (fixtures do smoke, layout novo ainda sem build).
        var overridden = LayoutOverrides.ContainsKey(map.Id);
        if (!overridden && ResourceLoader.Exists(WalkMaskPath(map)))
        {
            var walk = new MaskCollision { Name = "WalkCollision", Mask = GD.Load<Texture2D>(WalkMaskPath(map)), CellSize = Layout.Cell };
            CollisionRoot.AddChild(walk);
            WalkArea = walk.WalkArea;
            CollisionSource = "walk-png";
            return;
        }

        if (Layout.Walk.Count > 0 || Layout.Block.Count > 0 || Layout.Base == "none")
        {
            var grid = LayoutGrid.Build(map.ImageWidth, map.ImageHeight, BaseWalkArea(map), Layout);
            var collision = new MaskCollision { Name = "LayoutCollision", PresetGrid = grid, CellSize = Layout.Cell };
            CollisionRoot.AddChild(collision);
            WalkArea = collision.WalkArea;
            CollisionSource = "layout";
            return;
        }

        CollisionSource = map.HasMask ? "mask" : "shapes";
        if (map.HasMask)
        {
            var mask = new MaskCollision { Name = "MaskCollision", Mask = GD.Load<Texture2D>(map.Mask), CellSize = map.MaskCell };
            CollisionRoot.AddChild(mask);
            WalkArea = mask.WalkArea;
        }
        else
        {
            if (!ShapeAreas.TryGetValue(map.Id, out var area))
            {
                area = map.CreateShapeWalkArea();
                ShapeAreas[map.Id] = area;
            }

            CollisionRoot.AddChild(new ShapeCollision { Name = "ShapeCollision", WalkArea = area });
            WalkArea = area;
        }
    }

    /// <summary>Base da colisão do layout: máscara da v2, formas do maps.json ou nada (<c>"base"</c>).</summary>
    private IWalkArea? BaseWalkArea(MapDef map)
    {
        // mesma regra do tools/maps/layout.py (build_grid): "mask" só vale com máscara, "shapes" só sem
        if (Layout.Base == "mask" && map.HasMask)
        {
            return new MaskWalkArea(MaskCollision.ReadGrid(GD.Load<Texture2D>(map.Mask!).GetImage(), 0.5f), map.MaskCell);
        }

        if (Layout.Base != "shapes" || map.HasMask)
        {
            return null;
        }

        if (!ShapeAreas.TryGetValue(map.Id, out var area))
        {
            area = map.CreateShapeWalkArea();
            ShapeAreas[map.Id] = area;
        }

        return area;
    }

    private void OnDayChanged(int day)
    {
        Objects.NewDay();
        Dressing.OnNewDay();
    }

    private void ClearMapEntities()
    {
        foreach (var child in Entities.GetChildren())
        {
            if (child != Player)
            {
                Entities.RemoveChild(child);
                child.QueueFree();
            }
        }
    }

    private void BuildInteractables(MapDef map)
    {
        foreach (var child in InteractablesRoot.GetChildren())
        {
            InteractablesRoot.RemoveChild(child);
            child.QueueFree();
        }

        _interactables.Clear();
        foreach (var def in map.Interactables)
        {
            var node = new Interactable
            {
                Name = def.Id,
                Id = def.Id,
                Kind = def.Action,
                Prompt = def.Label,
                Radius = def.Radius,
                Enabled = def.StartsEnabled,
                Position = new Vector2(def.X, def.Y),
                PromptHeight = MathF.Max(44f, WorldScale.CharacterHeight + 10f), // acima da cabeça do NPC (D41)
            };
            InteractablesRoot.AddChild(node);
            _interactables[def.Id] = node;
        }
    }

    private void ApplyFade()
    {
        FadeRect.Color = FadeColor with { A = _fade };
        FadeRect.Visible = _fade > 0f;
    }
}
