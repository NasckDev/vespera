using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>
/// Tela do convidado no co-op online: não simula nada. Desenha o chão pintado da sala do host (pelo id do snapshot), os
/// caçadores e os monstros nas posições interpoladas (agora − atraso), com barra de vida, a fala aberta e o próprio painel
/// de vida. A câmera segue o próprio caçador. Esc abre a confirmação de sair do online.
/// </summary>
public partial class OnlineGuestView : Control
{
    private const float MoveEpsilon = 0.6f;

    private readonly Dictionary<int, AnimatedSprite2D> _hunters = [];
    private readonly Dictionary<int, (AnimatedSprite2D Sprite, ColorRect Bar, ColorRect Fill)> _enemies = [];
    private readonly Dictionary<int, Vector2> _lastHunterPos = [];
    private SubViewport _viewport = null!;
    private Node2D _world = null!;
    private Node2D _entities = null!;
    private Sprite2D? _ground;
    private Camera2D _camera = null!;
    private Label _status = null!;
    private Label _dialogue = null!;
    private Label _hp = null!;
    private string _levelId = string.Empty;
    private LevelData? _data;

    /// <summary>Sala que está desenhada agora (vazia antes do primeiro snapshot).</summary>
    public string LevelId => _levelId;

    /// <summary>Trocas de sala vistas (para os bots).</summary>
    public int LevelChanges { get; private set; }

    /// <summary>Monstros desenhados agora.</summary>
    public int EnemiesDrawn => _enemies.Count;

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
        _camera = new Camera2D { Name = "Camera", Enabled = true };
        _world.AddChild(_camera);

        _status = UiKit.MakeLabel("Esperando o primeiro quadro do host…", UiTheme.DimVariation);
        _status.Position = new Vector2(36, 24);
        AddChild(_status);
        _hp = UiKit.MakeLabel(string.Empty);
        _hp.Position = new Vector2(36, 1000);
        AddChild(_hp);
        _dialogue = UiKit.MakeWrapped(string.Empty);
        _dialogue.Position = new Vector2(320, 820);
        _dialogue.Size = new Vector2(1280, 160);
        AddChild(_dialogue);
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

        var now = NetSession.Now;
        DrawHunters(latest, interp, now);
        DrawEnemies(latest, interp, now);
        _dialogue.Text = latest.Dialogue;
        _status.Text = latest.LevelId == SnapshotBuilder.WaitingLevel
            ? "O host está nos menus. Esperando ele voltar ao jogo…"
            : $"Online · {CoopSession.TagOf(session.Slot)} · Esc: sair";
        if (latest.Hunter(session.Slot) is { } me)
        {
            _hp.Text = me.Downed ? "Caído! Espere o aliado reanimar." : $"Vida {me.Hp:0}/{me.MaxHp:0}";
            if (_hunters.TryGetValue(session.Slot, out var mine))
            {
                _camera.Position = mine.Position;
            }
        }
        else
        {
            _hp.Text = "Entrando na sala do host…";
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

        _hunters.Clear();
        _enemies.Clear();
        _lastHunterPos.Clear();
        _ground?.QueueFree();
        _ground = null;
        _data = null;
        if (id == SnapshotBuilder.WaitingLevel)
        {
            return;
        }

        try
        {
            _data = Level.LoadData(id);
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
        Log.Info($"online: convidado desenhando a sala '{id}'");
    }

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
            var facing = h.Facing switch { 0 => Facing.Down, 1 => Facing.Up, _ => Facing.Side };
            var anim = h.Downed ? HunterSprites.IdleAnimation(facing) : moving ? HunterSprites.WalkAnimation(facing) : HunterSprites.IdleAnimation(facing);
            if (sprite.SpriteFrames.HasAnimation(anim) && sprite.Animation != anim)
            {
                sprite.Play(anim);
            }

            sprite.FlipH = h.Facing == 3;
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
            if (at.X < view.Sprite.Position.X - MoveEpsilon)
            {
                view.Sprite.FlipH = true;
            }
            else if (at.X > view.Sprite.Position.X + MoveEpsilon)
            {
                view.Sprite.FlipH = false;
            }

            view.Sprite.Position = at;
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
}
