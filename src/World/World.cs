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

    /// <summary>Mapa inicial (novo jogo da v2: Guilda em (250, 560), <c>main.js:71</c>).</summary>
    [Export]
    public string StartMapId { get; set; } = "guild";

    [Export]
    public Vector2 StartPosition { get; set; } = new(250, 560);

    /// <summary>Se definido antes de a cena entrar, substitui o início (ex.: "Continuar" → Guilda (250, 330)).</summary>
    public static (string MapId, Vector2 Position)? PendingStart { get; set; }

    public static MapCatalog Catalog => _catalog ??= MapCatalog.Parse(FileAccess.GetFileAsString(MapsPath));

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

        var (mapId, position) = PendingStart ?? (StartMapId, StartPosition);
        PendingStart = null;
        GoMap(mapId, position, instant: true);
    }

    public override void _ExitTree()
    {
        RenderingServer.SetDefaultClearColor(_previousClearColor);
        GameManager.Instance?.UnblockGameplay(TransitionBlock);
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
        BuildCollision(map);
        ClearMapEntities();
        BuildInteractables(map);
        DoorOpen = false;
        _backCooldown = 0;

        Player.Teleport(CorrectToWalkable(at));
        Camera.SetMap(new Vector2(map.ImageWidth, map.ImageHeight), map.ViewHeight);
        GameManager.Instance.UnblockGameplay(TransitionBlock);

        Log.Info($"Mapa: {map.Id} ({map.Name}) em ({Player.GlobalPosition.X:0}, {Player.GlobalPosition.Y:0}).");
        EventBus.Instance.EmitSignal(EventBus.SignalName.MapChanged, map.Id, map.Name);
        EventBus.Instance.EmitSignal(EventBus.SignalName.LocationChanged, map.Name);
    }

    private void BuildCollision(MapDef map)
    {
        foreach (var child in CollisionRoot.GetChildren())
        {
            CollisionRoot.RemoveChild(child);
            child.QueueFree();
        }

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
