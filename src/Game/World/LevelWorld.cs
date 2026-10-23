using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// O mundo de uma fase: chão pintado, colisão em grade (invisível), camada de entidades com y-sort (objetos,
/// zonas, monstros e jogadores, todos com origem no pé), efeitos no chão e acima, a câmera e o combate da sala
/// (alvos, peso dos golpes e, nas salas de Brecha, o <see cref="RoomDirector"/>). Montado em código a partir de
/// <see cref="LevelData"/>.
/// </summary>
public partial class LevelWorld : Node2D
{
    private const float DoorPromptHeight = 190f;
    private const float PracaPortalHeight = 250f;
    private const float VignetteEdge = 70f;

    private readonly LevelData _data;
    private readonly IReadOnlyDictionary<string, PropDef> _catalog;
    private readonly bool _withDirector;
    private readonly List<Prop> _props = [];
    private readonly List<Interactable> _zones = [];
    private readonly List<Rect2> _solids = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Npc> _npcs = [];
    private Node2D _groundFx = null!;
    private Node2D _fx = null!;

    public LevelWorld(LevelData data, IReadOnlyDictionary<string, PropDef> catalog, bool withDirector = true)
    {
        _data = data;
        _catalog = catalog;
        _withDirector = withDirector;
        Grid = LevelGrid.Parse(data.Cell, data.Cols, data.Rows, data.Grid);
    }

    public LevelData Data => _data;

    public LevelGrid Grid { get; }

    public Node2D Entities { get; private set; } = null!;

    public Player Player { get; private set; } = null!;

    public CameraRig Camera { get; private set; } = null!;

    public CombatSpace Combat { get; } = new();

    public CombatFeel Feel { get; private set; } = null!;

    public RoomDirector? Director { get; private set; }

    public IReadOnlyList<Prop> Props => _props;

    public IReadOnlyList<Interactable> Zones => _zones;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    public IReadOnlyList<Npc> Npcs => _npcs;

    /// <summary>Limites visíveis e jogáveis: a parte da grade coberta pelo chão pintado.</summary>
    public Rect2 Bounds { get; private set; }

    /// <summary>Retângulo que envolve todas as células andáveis (monstros que atravessam paredes ficam nele).</summary>
    public Rect2 PlayArea { get; private set; }

    public override void _Ready()
    {
        var ground = new Sprite2D
        {
            Name = "Ground",
            Texture = GD.Load<Texture2D>(_data.Ground.Texture),
            Centered = false,
            Scale = new Vector2(_data.Ground.Scale, _data.Ground.Scale),
            ZIndex = -10,
            TextureFilter = _data.Ground.Filter == "nearest" ? TextureFilterEnum.Nearest : TextureFilterEnum.Linear,
        };
        AddChild(ground);
        var groundSize = ground.Texture.GetSize() * _data.Ground.Scale;
        Bounds = new Rect2(0, 0, Mathf.Min(Grid.Width, groundSize.X), Mathf.Min(Grid.Height, groundSize.Y));
        PlayArea = WalkableBounds();
        AddChild(CollisionLayer.Build(Grid, debugVisible: false));

        _groundFx = new Node2D { Name = "GroundFx", ZIndex = -5 };
        AddChild(_groundFx);
        Entities = new Node2D { Name = "Entities", YSortEnabled = true };
        AddChild(Entities);
        _fx = new Node2D { Name = "Fx" };
        AddChild(_fx);
        Feel = new CombatFeel { Name = "Feel", FxLayer = _fx, ActorScale = _data.ActorScale };
        AddChild(Feel);

        foreach (var placement in _data.Props)
        {
            var prop = new Prop(placement.Type, _catalog[placement.Type].Scaled(_data.PropScale), placement.Flip, _data.ActorScale)
            {
                Name = $"{placement.Type}_{_props.Count}",
                Position = new Vector2(placement.At[0], placement.At[1]),
            };
            Entities.AddChild(prop);
            _props.Add(prop);
        }

        foreach (var zone in _data.Zones)
        {
            AddZone(zone);
        }

        SpawnNpcs();
        Player = SpawnPlayer(MergedInput.LocalSolo(), SpawnPoint, RiftSession.Hunter);
        AddChild(new LevelVignette(Bounds, VignetteEdge * _data.ActorScale) { ZIndex = -9 });
        Camera = new CameraRig(Player, Bounds, _data.ActorScale, _data.HumanHeight) { Name = "Camera" };
        AddChild(Camera);
        Camera.MakeCurrent();
        Feel.Camera = Camera;

        if (_data.Rift is { } rift && _withDirector)
        {
            Director = new RoomDirector(this, rift);
            AddChild(Director);
        }
    }

    public Vector2 SpawnPoint => new(_data.Spawn[0], _data.Spawn[1]);

    /// <summary>Cria um caçador no ponto (pé) dado; usado pelo jogador e pelos bots de teste.</summary>
    public Player SpawnPlayer(IInputSource input, Vector2 feet, HunterClass hunter = HunterClass.Lamina)
    {
        var player = new Player(input, hunter, _data.ActorScale) { Name = $"Player_{Entities.GetChildCount()}", Position = feet, World = this };
        Entities.AddChild(player);
        return player;
    }

    public void AddEnemy(Enemy enemy)
    {
        _enemies.Add(enemy);
        enemy.TreeExited += () => _enemies.Remove(enemy);
        Entities.AddChild(enemy);
    }

    public void AddGroundFx(Node2D node) => _groundFx.AddChild(node);

    public void AddProjectile(Arrow arrow)
    {
        arrow.World = this;
        _fx.AddChild(arrow);
    }

    /// <summary>Espalha <paramref name="count"/> Fragmentos em volta do ponto (cada um vale 1).</summary>
    public void DropFragments(Vector2 at, int count, float spread = 60f)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = (Mathf.Tau * i / Mathf.Max(1, count)) + (GD.Randf() * 0.6f);
            var land = ClampToFloor(at + (Vector2.FromAngle(angle) * spread * _data.ActorScale * (0.5f + (0.5f * GD.Randf()))));
            Entities.AddChild(new FragmentPickup(this, at, land) { Name = $"Fragment_{GetInstanceId()}_{i}" });
        }
    }

    /// <summary>O ponto, ou o centro da célula andável mais próxima (para nada nascer dentro de parede).</summary>
    public Vector2 ClampToFloor(Vector2 point)
    {
        var c = (int)Mathf.Floor(point.X / Grid.Cell);
        var r = (int)Mathf.Floor(point.Y / Grid.Cell);
        if (!Grid.IsBlocked(c, r))
        {
            return point;
        }

        for (var ring = 1; ring < 40; ring++)
        {
            Vector2? best = null;
            var bestDistance = float.MaxValue;
            for (var dr = -ring; dr <= ring; dr++)
            {
                for (var dc = -ring; dc <= ring; dc++)
                {
                    if (Mathf.Max(Mathf.Abs(dr), Mathf.Abs(dc)) != ring || Grid.IsBlocked(c + dc, r + dr))
                    {
                        continue;
                    }

                    var (x, y) = Grid.CellCenter(c + dc, r + dr);
                    var d = point.DistanceSquaredTo(new Vector2(x, y));
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = new Vector2(x, y);
                    }
                }
            }

            if (best is { } found)
            {
                return found;
            }
        }

        return SpawnPoint;
    }

    /// <summary>Sólidos fora da grade e dos objetos (barreiras, baú, base de portal), vistos pelo bot de colisão.</summary>
    public void AddSolid(Rect2 rect) => _solids.Add(rect);

    public void RemoveSolid(Rect2 rect) => _solids.Remove(rect);

    /// <summary>
    /// Verdadeiro se o pé (cápsula) com o pé em <paramref name="feet"/> penetra parede da grade, pegada de objeto ou
    /// outro sólido mais que <paramref name="tolerance"/> px. É a checagem independente da física do bot de colisão.
    /// </summary>
    public bool PenetratesSolid(Vector2 feet, float tolerance)
    {
        var s = _data.ActorScale;
        var capsule = CollisionMath.FeetCapsule(feet.X, feet.Y, Player.FeetHalfWidth * s, Player.FeetHalfHeight * s);
        if (CollisionMath.CapsuleHitsGrid(Grid, capsule, tolerance))
        {
            return true;
        }

        foreach (var prop in _props)
        {
            if (HitsRect(capsule, prop.FootprintRect, tolerance))
            {
                return true;
            }
        }

        foreach (var solid in _solids)
        {
            if (HitsRect(capsule, solid, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HitsRect((float X0, float X1, float Y, float R) capsule, Rect2 f, float tolerance) =>
        f.Size.X > 0 && CollisionMath.CapsuleHitsRect(capsule.X0, capsule.X1, capsule.Y, capsule.R,
            new RectF(f.Position.X, f.Position.Y, f.Size.X, f.Size.Y), tolerance);

    private void AddZone(ZoneData zone)
    {
        var at = new Vector2(zone.At[0], zone.At[1]);
        if (zone.Kind == "brecha")
        {
            var portal = new BreachPortal(PracaPortalHeight * _data.ActorScale, zone.Kind, zone.Prompt, zone.Radius)
            {
                Name = "BrechaPortal",
                Position = at,
            };
            portal.Zone.Text = zone.Text;
            Entities.AddChild(portal);
            _solids.Add(portal.Footprint);
            _zones.Add(portal.Zone);
            return;
        }

        var area = new Interactable(zone.Kind, zone.Prompt, zone.Radius)
        {
            Name = $"zone_{zone.Kind}_{_zones.Count}",
            Text = zone.Text,
            Dialogue = zone.Dialogue,
            Target = zone.Target,
            TargetSpawn = zone.Spawn,
            Position = at,
            PromptAnchor = at - new Vector2(0, DoorPromptHeight * _data.ActorScale),
        };
        Entities.AddChild(area);
        _zones.Add(area);
    }

    private Rect2 WalkableBounds()
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var r = 0; r < Grid.Rows; r++)
        {
            for (var c = 0; c < Grid.Cols; c++)
            {
                if (Grid.IsBlocked(c, r))
                {
                    continue;
                }

                var cell = Grid.CellRect(c, r);
                min = min.Min(new Vector2(cell.X, cell.Y));
                max = max.Max(new Vector2(cell.Right, cell.Bottom));
            }
        }

        return min.X > max.X ? Bounds : new Rect2(min, max - min);
    }
}
