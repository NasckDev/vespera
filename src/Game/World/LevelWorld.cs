using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Core.Story;
using Vespera.Game.Components;
using Vespera.Game.Story;

namespace Vespera.Game.World;

/// <summary>
/// O mundo de uma fase: chão pintado, colisão em grade (invisível), camada de entidades com y-sort (objetos,
/// zonas e jogadores, todos com origem no pé) e a câmera. Montado em código a partir de <see cref="LevelData"/>.
/// </summary>
public partial class LevelWorld : Node2D
{
    private const float DoorPromptHeight = 190f;

    private readonly LevelData _data;
    private readonly IReadOnlyDictionary<string, PropDef> _catalog;
    private readonly List<Prop> _props = [];
    private readonly List<Interactable> _zones = [];
    private readonly List<Npc> _npcs = [];

    public LevelWorld(LevelData data, IReadOnlyDictionary<string, PropDef> catalog)
    {
        _data = data;
        _catalog = catalog;
        Grid = LevelGrid.Parse(data.Cell, data.Cols, data.Rows, data.Grid);
    }

    public LevelData Data => _data;

    public LevelGrid Grid { get; }

    public Node2D Entities { get; private set; } = null!;

    public Player Player { get; private set; } = null!;

    public CameraRig Camera { get; private set; } = null!;

    public IReadOnlyList<Prop> Props => _props;

    public IReadOnlyList<Interactable> Zones => _zones;

    public IReadOnlyList<Npc> Npcs => _npcs;

    /// <summary>Limites visíveis e jogáveis: a parte da grade coberta pelo chão pintado.</summary>
    public Rect2 Bounds { get; private set; }

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
        AddChild(CollisionLayer.Build(Grid, debugVisible: false));

        Entities = new Node2D { Name = "Entities", YSortEnabled = true };
        AddChild(Entities);
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
            var at = new Vector2(zone.At[0], zone.At[1]);
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

        SpawnNpcs();
        Player = SpawnPlayer(MergedInput.LocalSolo(), SpawnPoint);
        Camera = new CameraRig(Player, Bounds, _data.ActorScale) { Name = "Camera", Zoom = new Vector2(_data.CameraZoom, _data.CameraZoom) };
        AddChild(Camera);
        Camera.MakeCurrent();
    }

    /// <summary>Liga/desliga os NPCs como obstáculos e patrulheiros (o bot de colisão varre a fase sem eles).</summary>
    public void SetNpcsActive(bool active)
    {
        foreach (var npc in _npcs)
        {
            npc.SetActive(active);
        }
    }

    private void SpawnNpcs()
    {
        foreach (var placement in _data.Npcs)
        {
            if (!NpcSprites.Exists(placement.Id) || !StoryConditions.Evaluate(placement.Conditions, StoryService.Engine))
            {
                continue;
            }

            var npc = new Npc(placement.Id, placement, _data.ActorScale)
            {
                Name = $"Npc_{placement.Id}_{_npcs.Count}",
                Position = new Vector2(placement.At[0], placement.At[1]),
            };
            Entities.AddChild(npc);
            _npcs.Add(npc);
        }
    }

    public Vector2 SpawnPoint => new(_data.Spawn[0], _data.Spawn[1]);

    /// <summary>Cria um caçador no ponto (pé) dado; usado pelo jogador e pelos bots de teste.</summary>
    public Player SpawnPlayer(IInputSource input, Vector2 feet, HunterClass hunter = HunterClass.Lamina)
    {
        var player = new Player(input, hunter, _data.ActorScale) { Name = $"Player_{Entities.GetChildCount()}", Position = feet };
        Entities.AddChild(player);
        return player;
    }

    /// <summary>
    /// Verdadeiro se o pé (cápsula) com o pé em <paramref name="feet"/> penetra parede da grade ou pegada de objeto
    /// mais que <paramref name="tolerance"/> px. É a checagem independente da física usada pelo bot de colisão.
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
            var f = prop.FootprintRect;
            if (f.Size.X > 0 && CollisionMath.CapsuleHitsRect(capsule.X0, capsule.X1, capsule.Y, capsule.R,
                    new RectF(f.Position.X, f.Position.Y, f.Size.X, f.Size.Y), tolerance))
            {
                return true;
            }
        }

        return false;
    }
}
