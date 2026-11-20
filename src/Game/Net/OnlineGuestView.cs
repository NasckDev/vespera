using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.Coop;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>
/// Tela do convidado no co-op online: não simula nada. Monta o mesmo cenário do host (chão pintado pelo id da sala, objetos,
/// NPCs), desenha os caçadores e os monstros nas posições interpoladas (agora − atraso) na pose que o host está tocando, as
/// flechas, os efeitos de golpe (faíscas, rastros, números de dano, tremor) e o HUD de vida e fôlego do próprio caçador.
/// A câmera segue o próprio caçador. Esc abre a confirmação de sair do online.
/// </summary>
public partial class OnlineGuestView : Control
{
    private const float MoveEpsilon = 0.6f;
    private const float TraumaDecayPerSecond = 1.4f;
    private const float MaxShake = 26f;
    private const float BarWidth = 380f;
    private static readonly Color LifeColor = new("e85d5d");
    private static readonly Color StaminaColor = new("6fbf73");

    private readonly Dictionary<int, AnimatedSprite2D> _hunters = [];
    private readonly Dictionary<int, (AnimatedSprite2D Sprite, ColorRect Bar, ColorRect Fill)> _enemies = [];
    private readonly Dictionary<int, Vector2> _lastHunterPos = [];
    private readonly List<Node2D> _scenery = [];
    private readonly List<ArrowGhost> _arrows = [];
    private readonly Dictionary<int, (string Id, AnimatedSprite2D Sprite)> _npcs = [];
    private SubViewport _viewport = null!;
    private Node2D _world = null!;
    private Node2D _entities = null!;
    private Node2D _fx = null!;
    private Sprite2D? _ground;
    private Camera2D _camera = null!;
    private CombatFeel _feel = null!;
    private FloatingNumbers _numbers = null!;
    private Label _status = null!;
    private Label _dialogue = null!;
    private Label _hp = null!;
    private Label _paused = null!;
    private ColorRect _lifeFill = null!;
    private ColorRect _staminaFill = null!;
    private string _levelId = string.Empty;
    private LevelData? _data;
    private IReadOnlyDictionary<string, PropDef>? _catalog;
    private int _sceneSerial;
    private float _trauma;
    private NetSession? _subscribed;

    /// <summary>Sala que está desenhada agora (vazia antes do primeiro snapshot).</summary>
    public string LevelId => _levelId;

    /// <summary>Trocas de sala vistas (para os bots).</summary>
    public int LevelChanges { get; private set; }

    /// <summary>Altura humana da sala desenhada (0 sem sala).</summary>
    public float HumanHeight => _data?.HumanHeight ?? 0f;

    /// <summary>Monstros desenhados agora.</summary>
    public int EnemiesDrawn => _enemies.Count;

    /// <summary>Objetos de cenário desenhados (o cenário que o host mandou).</summary>
    public int SceneryDrawn => _scenery.Count;

    /// <summary>NPCs desenhados.</summary>
    public int NpcsDrawn => _npcs.Count;

    /// <summary>Flechas desenhadas.</summary>
    public int ArrowsDrawn => _arrows.FindAll(a => a.Visible).Count;

    /// <summary>Efeitos de combate tocados desde que a tela abriu (golpes, números de dano; para os bots).</summary>
    public int EffectsPlayed { get; private set; }

    /// <summary>Números de dano que chegaram do host.</summary>
    public int DamageNumbersPlayed { get; private set; }

    /// <summary>Texto de vida no HUD do convidado.</summary>
    public string HudLifeText => _hp.Text;

    /// <summary>Preenchimento da barra de vida (0..1).</summary>
    public float HudLifeFraction => _lifeFill.Size.X / BarWidth;

    /// <summary>Aviso "host pausou" aparecendo.</summary>
    public bool HostPausedShown => _paused.Visible;

    /// <summary>Posição desenhada de um caçador (null se não está no snapshot).</summary>
    public Vector2? HunterDrawnAt(int slot) => _hunters.TryGetValue(slot, out var s) && s.Visible ? s.Position : null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var container = new SubViewportContainer { Name = "View", Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);
        _viewport = new SubViewport
        {
            Name = "GuestViewport",
            Size = new Vector2I(1920, 1080),
            HandleInputLocally = false,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
        };
        container.AddChild(_viewport);
        _world = new Node2D { Name = "World" };
        _viewport.AddChild(_world);
        _entities = new Node2D { Name = "Entities", YSortEnabled = true };
        _world.AddChild(_entities);
        _fx = new Node2D { Name = "Fx" };
        _world.AddChild(_fx);
        _feel = new CombatFeel { Name = "Feel", FxLayer = _fx };
        AddChild(_feel);
        _camera = new Camera2D { Name = "Camera", Enabled = true };
        _world.AddChild(_camera);

        _numbers = new FloatingNumbers(world => _viewport.CanvasTransform * world) { Name = "Numbers" };
        AddChild(_numbers);
        BuildHud();

        if (NetSession.Instance is { } session)
        {
            _subscribed = session;
            session.EffectsReceived += OnEffects;
        }
    }

    public override void _ExitTree()
    {
        if (_subscribed is not null && IsInstanceValid(_subscribed))
        {
            _subscribed.EffectsReceived -= OnEffects;
        }

        _subscribed = null;
    }

    private void BuildHud()
    {
        _status = UiKit.MakeLabel("Esperando o primeiro quadro do host…", UiTheme.DimVariation);
        _status.Position = new Vector2(36, 24);
        AddChild(_status);

        _paused = UiKit.MakeLabel("O host pausou o jogo.", UiTheme.TitleVariation, HorizontalAlignment.Center);
        _paused.Position = new Vector2(560, 90);
        _paused.Size = new Vector2(800, 60);
        _paused.Visible = false;
        AddChild(_paused);

        _hp = UiKit.MakeLabel(string.Empty);
        _hp.Position = new Vector2(36, 930);
        AddChild(_hp);
        _lifeFill = AddBar(new Vector2(36, 978), new Vector2(BarWidth, 22), LifeColor);
        _staminaFill = AddBar(new Vector2(36, 1008), new Vector2(BarWidth, 12), StaminaColor);

        _dialogue = UiKit.MakeWrapped(string.Empty);
        _dialogue.Position = new Vector2(320, 820);
        _dialogue.Size = new Vector2(1280, 160);
        AddChild(_dialogue);
    }

    private ColorRect AddBar(Vector2 position, Vector2 size, Color color)
    {
        var back = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Position = position, Size = size + new Vector2(4, 4), MouseFilter = MouseFilterEnum.Ignore };
        var fill = new ColorRect { Color = color, Position = new Vector2(2, 2), Size = size, MouseFilter = MouseFilterEnum.Ignore };
        back.AddChild(fill);
        AddChild(back);
        return fill;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(InputActions.Pause) || SceneRouter.Instance.OverlayDepth > 0)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        SceneRouter.Instance.Push(new ConfirmScreen("Sair da partida online?", () => NetSession.Instance?.LeaveAsGuest()));
    }

    public override void _Process(double delta)
    {
        if (NetSession.Instance is not { Interpolator: { } interp } session || interp.Latest is not { } latest)
        {
            return;
        }

        if (latest.LevelId != _levelId)
        {
            LoadRoom(latest.LevelId);
        }

        ApplyScene(session);
        var now = NetSession.GameNow;
        DrawHunters(latest, interp, now);
        DrawEnemies(latest, interp, now);
        DrawArrows(latest.Arrows);
        DrawNpcs(latest.NpcList);
        _dialogue.Text = latest.Dialogue;
        _paused.Visible = latest.HostPaused && latest.LevelId != SnapshotBuilder.WaitingLevel;
        _status.Text = latest.LevelId == SnapshotBuilder.WaitingLevel
            ? "O host está nos menus. Esperando ele voltar ao jogo…"
            : $"Online · {CoopSession.TagOf(session.Slot)} · Esc: sair";
        UpdateHud(session, latest);
        UpdateShake((float)delta);
    }

    private void UpdateHud(NetSession session, WorldSnapshot latest)
    {
        if (latest.Hunter(session.Slot) is { } me)
        {
            _hp.Text = me.Downed
                ? "Caído! Espere o aliado reanimar."
                : $"{CoopSession.TagOf(session.Slot)} · Vida {Math.Ceiling(me.Hp):0}/{me.MaxHp:0}";
            _lifeFill.Size = new Vector2(BarWidth * (me.MaxHp > 0 ? Mathf.Clamp(me.Hp / me.MaxHp, 0f, 1f) : 0f), _lifeFill.Size.Y);
            _staminaFill.Size = new Vector2(BarWidth * (me.Stamina / 255f), _staminaFill.Size.Y);
            _staminaFill.Color = me.Stamina < 77 ? new Color(1f, 0.75f, 0.75f) : StaminaColor;
            if (_hunters.TryGetValue(session.Slot, out var mine))
            {
                _camera.Position = mine.Position;
            }
        }
        else
        {
            // Entrou no grupo, mas o caçador só nasce fora da Brecha (ou fora de uma cena): o convidado fica sabendo o porquê.
            _hp.Text = latest.LevelId == SnapshotBuilder.WaitingLevel
                ? string.Empty
                : "Esperando o host sair da Brecha (ou terminar a cena) para o seu caçador entrar…";
            _lifeFill.Size = new Vector2(0, _lifeFill.Size.Y);
            _staminaFill.Size = new Vector2(0, _staminaFill.Size.Y);
        }
    }

    private void UpdateShake(float delta)
    {
        if (_trauma <= 0f)
        {
            _camera.Offset = Vector2.Zero;
            return;
        }

        _trauma = Mathf.Max(0f, _trauma - (TraumaDecayPerSecond * delta));
        var strength = _trauma * _trauma * MaxShake * (GameSettings.Current.ScreenShake / 100f) / Mathf.Max(0.01f, _camera.Zoom.X);
        _camera.Offset = new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * strength;
    }

    /// <summary>Efeitos de combate do host: o convidado só toca (o host decidiu o golpe, o dano e a cor).</summary>
    private void OnEffects(IReadOnlyList<NetEffect> effects)
    {
        if (_data is null)
        {
            return;
        }

        foreach (var e in effects)
        {
            var at = new Vector2(e.X, e.Y);
            var color = new Color(e.R / 255f, e.G / 255f, e.Blue / 255f);
            switch (e.Kind)
            {
                case NetEffectKind.Spark:
                    _feel.Spark(at, color, e.N, e.A, Vector2.FromAngle(e.B), e.C);
                    break;
                case NetEffectKind.Ring:
                    _feel.Ring(at, e.A, color, e.B, e.C);
                    break;
                case NetEffectKind.Number:
                    _numbers.Spawn(at, e.Text, color, e.A);
                    DamageNumbersPlayed++;
                    break;
                case NetEffectKind.Shake:
                    _trauma = Mathf.Min(1f, _trauma + e.A);
                    break;
                case NetEffectKind.Slash:
                    _fx.AddChild(new SlashTrail(at, Vector2.FromAngle(e.A), e.B, e.C, e.D, WeaponLook.For(e.N)));
                    break;
            }

            EffectsPlayed++;
        }
    }

    private void LoadRoom(string id)
    {
        _levelId = id;
        LevelChanges++;
        foreach (var s in _hunters.Values)
        {
            s.QueueFree();
        }

        foreach (var e in _enemies.Values)
        {
            e.Sprite.QueueFree();
        }

        foreach (var n in _npcs.Values)
        {
            n.Sprite.QueueFree();
        }

        foreach (var a in _arrows)
        {
            a.QueueFree();
        }

        ClearScenery();
        _hunters.Clear();
        _enemies.Clear();
        _npcs.Clear();
        _arrows.Clear();
        _lastHunterPos.Clear();
        _numbers.Clear();
        _ground?.QueueFree();
        _ground = null;
        _data = null;
        _sceneSerial = -1;
        if (id == SnapshotBuilder.WaitingLevel)
        {
            return;
        }

        try
        {
            // O id vem validado (só letras, dígitos, _ e -): não vira caminho fora de res://data/levels.
            _data = Level.LoadData(id);
            _catalog ??= Level.LoadCatalog();
        }
        catch (Exception ex)
        {
            Log.Error($"online: sala '{id}' desconhecida no convidado: {ex.Message}");
            return;
        }

        _ground = new Sprite2D
        {
            Name = "Ground",
            Texture = GD.Load<Texture2D>(_data.Ground.Texture),
            Centered = false,
            Scale = Vector2.One * _data.Ground.Scale,
            ZIndex = -10,
            TextureFilter = _data.Ground.Filter == "nearest" ? TextureFilterEnum.Nearest : TextureFilterEnum.Linear,
        };
        _world.AddChild(_ground);
        _camera.Zoom = Vector2.One * CameraRig.ZoomFor(_data.HumanHeight);
        _feel.ActorScale = _data.ActorScale;
        Log.Info($"online: convidado desenhando a sala '{id}'");
    }

    // ------------------------------------------------------------ cenário

    private void ClearScenery()
    {
        foreach (var node in _scenery)
        {
            node.QueueFree();
        }

        _scenery.Clear();
    }

    /// <summary>Monta o cenário que o host mandou (objetos e variante do chão) quando chega um novo para esta sala.</summary>
    private void ApplyScene(NetSession session)
    {
        if (session.SceneSerial == _sceneSerial || session.LatestScene is not { } scene || scene.LevelId != _levelId || _data is null || _catalog is null)
        {
            return;
        }

        _sceneSerial = session.SceneSerial;
        if (_ground is not null)
        {
            var variants = _data.Ground.Variants;
            var path = scene.GroundVariant > 0 && scene.GroundVariant - 1 < variants.Count ? variants[scene.GroundVariant - 1].Texture : _data.Ground.Texture;
            if (GD.Load<Texture2D>(path) is { } texture)
            {
                _ground.Texture = texture;
            }
        }

        ClearScenery();
        foreach (var entry in scene.Entries)
        {
            if (!_catalog.TryGetValue(entry.Type, out var def))
            {
                continue;
            }

            _scenery.Add(NewProp(def.Scaled(_data.PropScale), entry));
        }
    }

    private Node2D NewProp(PropDef def, SceneEntry entry)
    {
        var node = new Node2D { Name = $"{entry.Type}_{_scenery.Count}", Position = new Vector2(entry.X, entry.Y) };
        node.AddChild(new Sprite2D
        {
            Name = "Sprite",
            Texture = GD.Load<Texture2D>(def.Texture),
            Centered = false,
            Offset = -new Vector2(def.Anchor[0], def.Anchor[1]),
            Scale = new Vector2(entry.Flip ? -def.Scale : def.Scale, def.Scale),
        });
        var footprint = def.WorldFootprint;
        if (footprint.W > 0 && footprint.H > 0)
        {
            node.AddChild(ContactShadow.Make(footprint.W * 1.15f, 0.38f));
        }

        _entities.AddChild(node);
        return node;
    }

    // ------------------------------------------------------------ caçadores

    private void DrawHunters(WorldSnapshot latest, SnapshotInterpolator interp, double now)
    {
        foreach (var h in latest.Hunters)
        {
            if (!_hunters.TryGetValue(h.Slot, out var sprite))
            {
                sprite = NewHunter(h);
                _hunters[h.Slot] = sprite;
            }

            var at = interp.HunterAt(now, h.Slot) is { } p ? new Vector2(p.X, p.Y) : new Vector2(h.X, h.Y);
            var moving = _lastHunterPos.TryGetValue(h.Slot, out var last) && last.DistanceTo(at) > MoveEpsilon;
            _lastHunterPos[h.Slot] = at;
            sprite.Position = at;
            sprite.Visible = true;
            sprite.FlipH = h.Facing == 3;
            if (!AnimTable.Apply(sprite, h.Anim, h.Frame))
            {
                var facing = h.Facing switch { 0 => Facing.Down, 1 => Facing.Up, _ => Facing.Side };
                var anim = h.Downed ? HunterSprites.IdleAnimation(facing) : moving ? HunterSprites.WalkAnimation(facing) : HunterSprites.IdleAnimation(facing);
                if (sprite.SpriteFrames.HasAnimation(anim) && sprite.Animation != anim)
                {
                    sprite.Play(anim);
                }
            }

            sprite.Modulate = h.Downed ? new Color(0.6f, 0.6f, 0.7f, 0.8f) : Colors.White;
        }

        foreach (var (slot, sprite) in _hunters)
        {
            if (latest.Hunter(slot) is null)
            {
                sprite.Visible = false;
            }
        }
    }

    private AnimatedSprite2D NewHunter(NetHunter h)
    {
        var hunter = Enum.IsDefined(typeof(HunterClass), (int)h.Kind) ? (HunterClass)h.Kind : HunterClass.Lamina;
        var actorScale = _data?.ActorScale ?? 1f;
        var scale = ScaleRegistry.SpriteScale(HunterSprites.ClassFolder(hunter), actorScale * ScaleRules.ReferenceHuman);
        var sprite = new AnimatedSprite2D
        {
            Name = $"Hunter_{CoopSession.TagOf(h.Slot)}",
            SpriteFrames = HunterSprites.Build(hunter),
            Offset = HunterSprites.FootOffset,
            Scale = Vector2.One * scale,
            SelfModulate = Colors.White,
        };
        var tag = UiKit.MakeLabel(CoopSession.TagOf(h.Slot));
        tag.Modulate = CoopSession.ColorOf(h.Slot);
        tag.Position = new Vector2(-12, -HunterSprites.FootOffset.Y - 90);
        sprite.AddChild(tag);
        _entities.AddChild(sprite);
        return sprite;
    }

    // ------------------------------------------------------------ monstros

    private void DrawEnemies(WorldSnapshot latest, SnapshotInterpolator interp, double now)
    {
        var seen = new HashSet<int>();
        foreach (var e in latest.Enemies)
        {
            seen.Add(e.Id);
            if (!_enemies.TryGetValue(e.Id, out var view))
            {
                view = NewEnemy(e);
                _enemies[e.Id] = view;
            }

            var at = interp.EnemyAt(now, e.Id) is { } p ? new Vector2(p.X, p.Y) : new Vector2(e.X, e.Y);
            view.Sprite.FlipH = e.FlipH;
            view.Sprite.Position = at;
            AnimTable.Apply(view.Sprite, e.Anim, e.Frame);
            var frac = e.MaxHp > 0 ? Mathf.Clamp(e.Hp / e.MaxHp, 0f, 1f) : 0f;
            view.Bar.Visible = frac < 0.999f;
            view.Fill.Size = new Vector2(view.Bar.Size.X * frac, view.Bar.Size.Y);
        }

        var gone = new List<int>();
        foreach (var id in _enemies.Keys)
        {
            if (!seen.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (var id in gone)
        {
            _enemies[id].Sprite.QueueFree();
            _enemies.Remove(id);
        }
    }

    private (AnimatedSprite2D, ColorRect, ColorRect) NewEnemy(NetEnemy e)
    {
        var sprite = new AnimatedSprite2D { Name = $"Enemy_{e.Id}" };
        var height = 60f;
        if (e.Type.Length > 0 && _data is not null)
        {
            try
            {
                var sheet = MonsterSprites.Load(e.Type, _data.ActorScale);
                sprite.SpriteFrames = sheet.Frames;
                sprite.Offset = sheet.Offset;
                sprite.Scale = Vector2.One * sheet.SpriteScale;
                height = sheet.StandHeight * sheet.SpriteScale;
                if (sprite.SpriteFrames.HasAnimation("parado"))
                {
                    sprite.Play("parado");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"online: sem folha para o monstro '{e.Type}': {ex.Message}");
            }
        }

        var scale = sprite.Scale.X > 0 ? sprite.Scale.X : 1f;
        var width = 70f / scale;
        var bar = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Size = new Vector2(width, 6f / scale), Position = new Vector2(-width / 2f, (-height - 14f) / scale) };
        var fill = new ColorRect { Color = new Color("e85d5d"), Size = bar.Size };
        bar.AddChild(fill);
        sprite.AddChild(bar);
        _entities.AddChild(sprite);
        return (sprite, bar, fill);
    }

    // ------------------------------------------------------------ flechas e NPCs

    private void DrawArrows(IReadOnlyList<NetProjectile> arrows)
    {
        var scale = _data?.ActorScale ?? 1f;
        while (_arrows.Count < arrows.Count)
        {
            var ghost = new ArrowGhost(scale);
            _fx.AddChild(ghost);
            _arrows.Add(ghost);
        }

        for (var i = 0; i < _arrows.Count; i++)
        {
            var ghost = _arrows[i];
            ghost.Visible = i < arrows.Count;
            if (i < arrows.Count)
            {
                ghost.Position = new Vector2(arrows[i].X, arrows[i].Y);
                ghost.Rotation = arrows[i].Angle;
                ghost.Pierce = arrows[i].Pierce;
            }
        }
    }

    private void DrawNpcs(IReadOnlyList<NetNpc> npcs)
    {
        if (_data is null)
        {
            return;
        }

        for (var i = 0; i < npcs.Count; i++)
        {
            var n = npcs[i];
            if (_npcs.TryGetValue(i, out var current) && current.Id != n.Id)
            {
                current.Sprite.QueueFree();
                _npcs.Remove(i);
            }

            if (!_npcs.TryGetValue(i, out current))
            {
                if (!NpcSprites.Exists(n.Id))
                {
                    continue;
                }

                current = (n.Id, NewNpc(n.Id));
                _npcs[i] = current;
            }

            var facing = n.Facing switch { 0 => Facing.Down, 1 => Facing.Up, _ => Facing.Side };
            var anim = NpcSprites.Animation(n.Walking && NpcSprites.HasWalk(n.Id), facing);
            current.Sprite.Position = new Vector2(n.X, n.Y);
            current.Sprite.FlipH = n.Facing == 3;
            if (current.Sprite.SpriteFrames.HasAnimation(anim) && current.Sprite.Animation != anim)
            {
                current.Sprite.Play(anim);
            }
        }

        foreach (var key in _npcs.Keys.Where(k => k >= npcs.Count).ToList())
        {
            _npcs[key].Sprite.QueueFree();
            _npcs.Remove(key);
        }
    }

    private AnimatedSprite2D NewNpc(string id)
    {
        var scale = ScaleRegistry.SpriteScale(id, _data!.HumanHeight);
        var sprite = new AnimatedSprite2D
        {
            Name = $"Npc_{id}",
            SpriteFrames = NpcSprites.Build(id),
            Offset = NpcSprites.FootOffset,
            Scale = new Vector2(scale, scale),
        };
        sprite.AddChild(ContactShadow.Make(48f * scale / Mathf.Max(0.01f, scale), 0.42f));
        sprite.Play(NpcSprites.Animation(false, Facing.Down));
        _entities.AddChild(sprite);
        return sprite;
    }
}
