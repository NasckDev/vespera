using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Core.Story;
using Vespera.Game.Components;
using Vespera.Game.Story;
using Vespera.Game.UI;

namespace Vespera.Game.World;

/// <summary>
/// Cena de jogo de uma fase: o <see cref="LevelWorld"/> renderizado num SubViewport (câmera própria, luzes 2D) e,
/// por cima, a <see cref="InteractionHud"/>. Trata pausa (Esc/Start) e o que cada interação faz.
/// </summary>
public partial class Level : Control
{
    public const string LevelsFolder = "res://data/levels";
    private const float FlavorSeconds = 5f;
    private const float LeaveDistance = 1.8f;
    private const float AutoplayDelay = 0.7f;
    private const float DialogueCooldown = 0.3f;

    private readonly LevelData _data;
    private readonly IReadOnlyDictionary<string, PropDef> _catalog;
    private SubViewport _viewport = null!;
    private InteractionHud _hud = null!;
    private Interactable? _reading;
    private float _readingTimer;
    private float _noticeTimer;
    private DialogueBox _box = null!;
    private StoryHud _storyHud = null!;
    private Npc? _speaking;
    private readonly List<string> _pendingEvents = [];

    public Level(LevelData data, IReadOnlyDictionary<string, PropDef> catalog)
    {
        _data = data;
        _catalog = catalog;
    }

    public LevelWorld World { get; private set; } = null!;

    public InteractionHud Hud => _hud;

    public Player Player => World.Player;

    /// <summary>Lê data/levels/&lt;id&gt;.json e o catálogo de objetos e cria a cena.</summary>
    public static Level Load(string id) => Load(id, null);

    /// <summary>Como <see cref="Load(string)"/>, com o jogador nascendo em <paramref name="spawn"/> (pé) em vez do ponto padrão.</summary>
    public static Level Load(string id, float[]? spawn)
    {
        var level = LevelJson.ParseLevel(ReadText($"{LevelsFolder}/{id}.json"));
        var catalog = LevelJson.ParseProps(ReadText($"{LevelsFolder}/props.json"));
        foreach (var problem in LevelJson.Validate(level, catalog))
        {
            Log.Error($"fase '{id}': {problem}");
        }

        if (spawn is { Length: 2 })
        {
            level.Spawn = spawn;
        }

        return new Level(level, catalog) { Name = $"Level_{id}" };
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var container = new SubViewportContainer { Name = "View", Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);
        _viewport = new SubViewport
        {
            Name = "WorldViewport",
            Size = new Vector2I(1920, 1080),
            HandleInputLocally = false,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
            PhysicsObjectPicking = false,
        };
        container.AddChild(_viewport);
        World = new LevelWorld(_data, _catalog) { Name = "World" };
        _viewport.AddChild(World);
        World.Player.InteractRequested += OnInteract;

        _hud = new InteractionHud { Name = "Hud" };
        AddChild(_hud);
        _storyHud = new StoryHud { Name = "StoryHud" };
        AddChild(_storyHud);
        _box = new DialogueBox { Name = "Dialogue" };
        AddChild(_box);
        _box.Closed += OnDialogueClosed;
        StoryService.Engine.EventRaised += OnStoryEvent;
        StoryService.Engine.Progress.LevelId = _data.Id;
        GameSession.Autosave();
        if (!StoryService.SuppressAutoplay)
        {
            GetTree().CreateTimer(AutoplayDelay).Timeout += StartAutoplay;
        }
    }

    /// <summary>Verdadeiro enquanto uma conversa está aberta (o mundo continua, mas o jogador fica parado).</summary>
    public bool DialogueOpen => _box.IsOpen;

    public DialogueBox Dialogue => _box;

    /// <summary>Abre a fala <paramref name="id"/>; <paramref name="npc"/> (se houver) para e vira o rosto para o jogador.</summary>
    public bool StartDialogue(string id, Npc? npc = null)
    {
        var runner = StoryService.StartDialogue(id);
        if (runner is null || _box.IsOpen)
        {
            return false;
        }

        CloseReading();
        Player.Frozen = true;
        _speaking = npc;
        npc?.Hold(true, Player.GlobalPosition);
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        _box.Open(runner, StoryService.Script, InteractionHud.InteractKeyName(deviceId));
        return true;
    }

    private void StartAutoplay() => PlayAutoplay();

    /// <summary>Abre a fala automática desta fase se a história pede (a chegada à Praça); falso se não há nada a tocar.</summary>
    public bool PlayAutoplay()
    {
        if (!IsInsideTree() || _box.IsOpen || StoryService.Engine.AutoplayFor(_data.Id) is not { } auto)
        {
            return false;
        }

        return StartDialogue(auto.Dialogue);
    }

    private void OnDialogueClosed()
    {
        Player.Frozen = false;
        _speaking?.Hold(false);
        _speaking = null;
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        GameSession.Autosave();
        foreach (var name in events)
        {
            if (name == StoryFlags.EventOpenForge)
            {
                ForgeHook.Open(this);
            }
        }
    }

    private void OnStoryEvent(string name) => _pendingEvents.Add(name);

    /// <summary>Aviso curto na parte de baixo (some sozinho); usado por ganchos sem tela própria ainda.</summary>
    public void ShowNotice(string text)
    {
        _hud.OpenPanel(string.Empty, text, string.Empty);
        _noticeTimer = FlavorSeconds;
    }

    public override void _ExitTree()
    {
        StoryService.Engine.EventRaised -= OnStoryEvent;
        NpcSprites.ClearCache();
        HunterSprites.ClearCache();
        ContactShadow.ClearCache();
        LightTexture.ClearCache();
        WorldSfx.ClearCache();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        UpdateReading(dt);
        _storyHud.Visible = !_box.IsOpen;
        if (_noticeTimer > 0f && (_noticeTimer -= dt) <= 0f && _reading is null)
        {
            _hud.ClosePanel();
        }
        var focus = _box.IsOpen ? null : Player.Focus;
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        var anchor = focus is null ? Vector2.Zero : WorldToScreen(focus.PromptAnchor);
        _hud.ShowPrompt(focus?.Prompt, anchor, InteractionHud.InteractKeyName(deviceId), dt);
    }

    public Vector2 WorldToScreen(Vector2 world) => _viewport.CanvasTransform * world;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.Journal) && SceneRouter.Instance.IsActive(this) && !_box.IsOpen)
        {
            GetViewport().SetInputAsHandled();
            GetTree().Paused = true;
            SceneRouter.Instance.Push(ScreenKeys.Journal);
            return;
        }

        if (!@event.IsActionPressed(InputActions.Pause) || !SceneRouter.Instance.IsActive(this) || _box.IsOpen)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (_reading is not null)
        {
            CloseReading();
            return;
        }

        GetTree().Paused = true;
        SceneRouter.Instance.Push(ScreenKeys.Pause);
    }

    private void OnInteract(Player player, Interactable? item)
    {
        if (_box.IsOpen || _box.SecondsSinceClosed < DialogueCooldown)
        {
            return;
        }

        if (_noticeTimer > 0f)
        {
            _noticeTimer = 0f;
            _hud.ClosePanel();
            return;
        }

        if (_reading is not null || item is null)
        {
            CloseReading();
            return;
        }

        var at = item.GlobalPosition;
        if (item.Dialogue.Length > 0 && StartDialogue(item.Dialogue, item.OwnerNpc as Npc))
        {
            WorldSfx.Play(World, WorldSound.Paper, at);
            return;
        }

        if (item.Kind == "exit" && item.Target.Length > 0)
        {
            Player.Frozen = true;
            WorldSfx.Play(World, WorldSound.Chime, at);
            GameEntry.Travel(item.Target, item.TargetSpawn);
            return;
        }

        switch (item.Kind)
        {
            case "lamp" when item.OwnerProp is { } lamp:
                lamp.SetLight(!lamp.LightOn);
                WorldSfx.Play(World, lamp.LightOn ? WorldSound.LampOn : WorldSound.LampOff, at);
                return;
            case "board":
                WorldSfx.Play(World, WorldSound.Paper, at);
                OpenReading(item, item.Title, freeze: true);
                return;
            case "water":
                WorldSfx.Play(World, WorldSound.Water, at);
                OpenReading(item, string.Empty, freeze: false);
                return;
            case "door" or "exit":
                WorldSfx.Play(World, WorldSound.DoorLocked, at);
                OpenReading(item, string.Empty, freeze: false);
                return;
            default:
                WorldSfx.Play(World, WorldSound.Chime, at);
                OpenReading(item, string.Empty, freeze: false);
                return;
        }
    }

    private void OpenReading(Interactable item, string title, bool freeze)
    {
        _reading = item;
        _readingTimer = freeze ? -1f : FlavorSeconds;
        Player.Frozen = freeze;
        var key = InteractionHud.InteractKeyName(Player.Input.DeviceId);
        _hud.OpenPanel(title, item.Text, $"{key}: fechar");
    }

    private void CloseReading()
    {
        _reading = null;
        Player.Frozen = false;
        _hud.ClosePanel();
    }

    private void UpdateReading(float dt)
    {
        if (_reading is null)
        {
            return;
        }

        if (_readingTimer > 0f)
        {
            _readingTimer -= dt;
            if (_readingTimer <= 0f)
            {
                CloseReading();
                return;
            }
        }

        if (_reading is null)
        {
            return;
        }

        var shape = _reading.GetChild<CollisionShape2D>(0).Shape as CircleShape2D;
        var reach = (shape?.Radius ?? 64f) * LeaveDistance;
        if (Player.GlobalPosition.DistanceTo(_reading.GlobalPosition) > reach)
        {
            CloseReading();
        }
    }

    private static string ReadText(string path)
    {
        var text = FileAccess.GetFileAsString(path);
        if (string.IsNullOrEmpty(text))
        {
            throw new System.IO.FileNotFoundException($"não consegui ler {path}: {FileAccess.GetOpenError()}");
        }

        return text;
    }
}
