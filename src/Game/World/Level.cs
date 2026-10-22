using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Components;
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

    private readonly LevelData _data;
    private readonly IReadOnlyDictionary<string, PropDef> _catalog;
    private SubViewport _viewport = null!;
    private InteractionHud _hud = null!;
    private Interactable? _reading;
    private float _readingTimer;

    public Level(LevelData data, IReadOnlyDictionary<string, PropDef> catalog)
    {
        _data = data;
        _catalog = catalog;
    }

    public LevelWorld World { get; private set; } = null!;

    public InteractionHud Hud => _hud;

    public Player Player => World.Player;

    /// <summary>Lê data/levels/&lt;id&gt;.json e o catálogo de objetos e cria a cena.</summary>
    public static Level Load(string id)
    {
        var level = LevelJson.ParseLevel(ReadText($"{LevelsFolder}/{id}.json"));
        var catalog = LevelJson.ParseProps(ReadText($"{LevelsFolder}/props.json"));
        foreach (var problem in LevelJson.Validate(level, catalog))
        {
            Log.Error($"fase '{id}': {problem}");
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
        GameSession.Autosave();
    }

    public override void _ExitTree()
    {
        HunterSprites.ClearCache();
        ContactShadow.ClearCache();
        LightTexture.ClearCache();
        WorldSfx.ClearCache();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        UpdateReading(dt);
        var focus = Player.Focus;
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        var anchor = focus is null ? Vector2.Zero : WorldToScreen(focus.PromptAnchor);
        _hud.ShowPrompt(focus?.Prompt, anchor, InteractionHud.InteractKeyName(deviceId), dt);
    }

    public Vector2 WorldToScreen(Vector2 world) => _viewport.CanvasTransform * world;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(InputActions.Pause) || !SceneRouter.Instance.IsActive(this))
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
        if (_reading is not null || item is null)
        {
            CloseReading();
            return;
        }

        var at = item.GlobalPosition;
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
