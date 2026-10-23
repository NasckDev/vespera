using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Cena de jogo: o <see cref="LevelWorld"/> da sala atual renderizado num SubViewport (câmera própria, luzes 2D) e,
/// por cima, a HUD de caçada, os números de dano e a <see cref="InteractionHud"/>. Troca de sala com fade (Praça ⇄
/// salas da Brecha, em <c>Level.Rift.cs</c>), conversas e diário (<c>Level.Story.cs</c>), trata pausa (Esc/Start) e o
/// que cada interação faz.
/// </summary>
public partial class Level : Control
{
    public const string LevelsFolder = "res://data/levels";
    private const float FlavorSeconds = 5f;
    private const float LeaveDistance = 1.8f;

    private LevelData _data;
    private readonly IReadOnlyDictionary<string, PropDef> _catalog;
    private SubViewport _viewport = null!;
    private InteractionHud _hud = null!;
    private Interactable? _reading;
    private float _readingTimer;
    private float _noticeTimer;

    public Level(LevelData data, IReadOnlyDictionary<string, PropDef> catalog)
    {
        _data = data;
        _catalog = catalog;
    }

    public LevelWorld World { get; private set; } = null!;

    public InteractionHud Hud => _hud;

    public CombatHud CombatHud { get; private set; } = null!;

    public Player Player => World.Player;

    /// <summary>Lê data/levels/&lt;id&gt;.json e o catálogo de objetos e cria a cena.</summary>
    public static Level Load(string id) => Load(id, null);

    /// <summary>Como <see cref="Load(string)"/>, com o jogador nascendo em <paramref name="spawn"/> (pé) em vez do ponto padrão.</summary>
    public static Level Load(string id, float[]? spawn)
    {
        RiftSession.Reset();
        var data = LoadData(id);
        if (spawn is { Length: 2 })
        {
            data.Spawn = spawn;
        }

        return new Level(data, LoadCatalog()) { Name = $"Level_{id}" };
    }

    public static LevelData LoadData(string id)
    {
        var level = LevelJson.ParseLevel(ReadText($"{LevelsFolder}/{id}.json"));
        foreach (var problem in LevelJson.Validate(level, LoadCatalog()))
        {
            Log.Error($"fase '{id}': {problem}");
        }

        return level;
    }

    public static IReadOnlyDictionary<string, PropDef> LoadCatalog() => LevelJson.ParseProps(ReadText($"{LevelsFolder}/props.json"));

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

        Numbers = new FloatingNumbers(WorldToScreen) { Name = "Numbers" };
        AddChild(Numbers);
        CombatHud = new CombatHud { Name = "CombatHud" };
        AddChild(CombatHud);
        _hud = new InteractionHud { Name = "Hud" };
        AddChild(_hud);
        BuildStory();
        BuildFade();
        MountWorld(new LevelWorld(_data, _catalog) { Name = "World" });
        GameSession.Autosave();
        ScheduleAutoplay();
    }

    /// <summary>Aviso curto na parte de baixo (some sozinho); usado por ganchos sem tela própria ainda.</summary>
    public void ShowNotice(string text)
    {
        _hud.OpenPanel(string.Empty, text, string.Empty);
        _noticeTimer = FlavorSeconds;
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1.0;
        UnbindStory();
        NpcSprites.ClearCache();
        HunterSprites.ClearCache();
        MonsterSprites.ClearCache();
        ContactShadow.ClearCache();
        LightTexture.ClearCache();
        WorldSfx.ClearCache();
        CombatSfx.ClearCache();
        CombatFeel.ClearCache();
        HitFlash.ClearCache();
        BreachPortal.ClearCache();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        UpdateReading(dt);
        UpdateStory();
        if (_noticeTimer > 0f && (_noticeTimer -= dt) <= 0f && _reading is null)
        {
            _hud.ClosePanel();
        }

        var focus = DialogueOpen ? null : Player.Focus;
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        var anchor = focus is null ? Vector2.Zero : WorldToScreen(focus.PromptAnchor);
        var prompt = !Player.Combat.IsDead && !_sceneBusy ? focus?.Prompt : null;
        _hud.ShowPrompt(prompt, anchor, InteractionHud.InteractKeyName(deviceId), dt);
    }

    public Vector2 WorldToScreen(Vector2 world) => _viewport.CanvasTransform * world;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key && HandleDebugKey(key.PhysicalKeycode))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (HandleJournalKey(@event))
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.Pause) && SkipSceneIfBusy())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!@event.IsActionPressed(InputActions.Pause) || !SceneRouter.Instance.IsActive(this) || DialogueOpen)
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

    private FloatingNumbers Numbers { get; set; } = null!;

    /// <summary>Põe um mundo novo no SubViewport e liga jogador, HUD e efeitos a ele.</summary>
    private void MountWorld(LevelWorld world)
    {
        World = world;
        _viewport.AddChild(world);
        world.Player.InteractRequested += OnInteract;
        world.Player.Combat.Died += OnPlayerDied;
        world.Feel.NumberRequested += (at, text, color, size) => Numbers.Spawn(at, text, color, size);
        world.Feel.ToastRequested += CombatHud.Toast;
        CombatHud.Bind(world);
        if (world.Director is { } director)
        {
            director.ExitRequested += OnRoomExit;
        }

        OnWorldMounted();
    }

    private void OnInteract(Player player, Interactable? item)
    {
        if (SkipSceneIfBusy() || DialogueBlocksInteraction)
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
            case "brecha":
                EnterFromPortal();
                return;
            case "chest":
                World.Director?.Chest?.Open();
                return;
            case "descer" or "saida":
                World.Director?.RequestExit();
                return;
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

        if (!IsInstanceValid(_reading))
        {
            CloseReading();
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
