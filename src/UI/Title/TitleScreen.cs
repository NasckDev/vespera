using System;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Screens;
using Vespera.UI.Widgets;

namespace Vespera.UI.Title;

/// <summary>
/// Tela de título (efetivo <c>extra.js:261-299</c>): key art com deslocamento lento, gradiente escuro à esquerda, brasas
/// subindo e o menu Continuar (desabilitado sem save) / Novo jogo / Como jogar / Controles / Opções / Sair. Novo jogo
/// com save pede confirmação, depois história (5 capítulos) e escolha de classe; o jogo começa na Guilda (250, 560),
/// Continuar na Guilda (250, 330) (spec 01 §1.4–1.6).
/// </summary>
public partial class TitleScreen : Control
{
    public const string ScenePath = "res://scenes/UI/Title.tscn";
    public const string WorldScenePath = "res://scenes/World/World.tscn";

    private const string KeyArtPath = "res://assets/sprites/ui/keyart.png";
    private const float ArtSway = 3f; // 6u da v2 em px base
    private const float ArtSwaySpeed = 0.15f;
    private const float MenuX = 30f;
    private const float MenuWidth = 210f;

    private readonly ScreenStack _stack = new();
    private readonly HelpScreen _help = new();
    private readonly ControlsScreen _controls = new();
    private readonly OptionsScreen _options = new();
    private readonly ConfirmScreen _confirm = new();
    private readonly StoryScreen _story = new();
    private readonly ClassSelectScreen _class = new();
    private TextureRect _art = null!;
    private VBoxContainer _menu = null!;
    private SelectableRow _continue = null!;
    private SelectableRow _newGame = null!;
    private Label _continueSub = null!;
    private double _time;
    private bool _starting;

    public SelectableRow ContinueRow => _continue;

    public SelectableRow NewGameRow => _newGame;

    public ScreenStack Stack => _stack;

    public ConfirmScreen Confirm => _confirm;

    public StoryScreen Story => _story;

    public ClassSelectScreen ClassSelect => _class;

    public OptionsScreen Options => _options;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildBackground();
        BuildMenu();
        AddChild(_stack);
        _stack.BaseLayer = _menu;

        _story.Finished = OpenClassSelect;
        _class.Started = StartNewGame;

        RefreshContinue();
        AudioManager.Instance?.PlayMusic(AudioManager.DefaultLocationMusic);
        (_continue.Disabled ? _newGame : _continue).GrabFocus();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _art.Position = new Vector2(-ArtSway + (MathF.Sin((float)_time * ArtSwaySpeed) * ArtSway), 0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_stack.Count == 0 && GetViewport().GuiGetFocusOwner() is null && (@event.IsActionPressed("ui_down") || @event.IsActionPressed("ui_up")))
        {
            _newGame.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildBackground()
    {
        AddChild(new ColorRect { Color = UiPalette.Ink, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        _art = new TextureRect
        {
            Texture = GD.Load<Texture2D>(KeyArtPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetRight = ArtSway * 2,
        };
        AddChild(_art);

        // Gradiente escuro à esquerda até 55% da largura (rgba(10,8,18,.92) → 0).
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(10 / 255f, 8 / 255f, 18 / 255f, 0.92f));
        gradient.SetColor(1, new Color(10 / 255f, 8 / 255f, 18 / 255f, 0f));
        AddChild(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 4, FillTo = new Vector2(1, 0) },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 0.55f,
            AnchorBottom = 1,
        });

        AddChild(Embers());
    }

    /// <summary>60 brasas aditivas subindo (âmbar, magenta e #ffcf8a; <c>extra.js:270</c>).</summary>
    private static CpuParticles2D Embers()
    {
        var colors = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant };
        colors.SetColor(0, UiPalette.Ambar);
        colors.SetOffset(1, 0.34f);
        colors.SetColor(1, UiPalette.Brecha);
        colors.AddPoint(0.67f, new Color("#ffcf8a"));
        var fade = new Gradient();
        fade.SetColor(0, new Color(1, 1, 1, 0));
        fade.AddPoint(0.15f, new Color(1, 1, 1, 0.9f));
        fade.AddPoint(0.85f, new Color(1, 1, 1, 0.6f));
        fade.SetColor(fade.GetPointCount() - 1, new Color(1, 1, 1, 0));
        return new CpuParticles2D
        {
            Amount = 60,
            Lifetime = 12,
            Preprocess = 12,
            Position = new Vector2(320, 240),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(330, 120),
            Direction = Vector2.Up,
            Spread = 8,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 5,
            InitialVelocityMax = 17,
            ScaleAmountMin = 1,
            ScaleAmountMax = 2.5f,
            ColorInitialRamp = colors,
            ColorRamp = fade,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }

    private void BuildMenu()
    {
        var top = Mathf.Max(20f, 360f * 0.3f) - 22f;
        var slogan = Ui.Text(UiText.Slogan, UiFonts.Body, UiPalette.Body);
        slogan.Position = new Vector2(MenuX, top + 2);
        AddChild(slogan);

        _menu = Ui.Column(1);
        _menu.Position = new Vector2(MenuX - 7, top + 22);
        _menu.CustomMinimumSize = new Vector2(MenuWidth, 0);
        AddChild(_menu);

        (_continue, _continueSub) = AddItem(UiText.MenuContinue, UiText.MenuContinueEmpty, ContinueGame);
        (_newGame, _) = AddItem(UiText.MenuNewGame, UiText.MenuNewGameSub, NewGame);
        AddItem(UiText.MenuHelp, UiText.MenuHelpSub, () => _stack.Push(_help));
        AddItem(UiText.MenuControls, UiText.MenuControlsSub, () => _stack.Push(_controls));
        AddItem(UiText.MenuOptions, UiText.MenuOptionsSub, () => _stack.Push(_options));
        AddItem(UiText.MenuQuit, UiText.MenuQuitSub, () => GetTree().Quit());

        var footer = Ui.Row(8,
            Ui.Hint("↑↓", "↑↓", "navegar", UiFonts.Small, UiPalette.Dim),
            Ui.Hint("Enter", "A", "confirmar", UiFonts.Small, UiPalette.Dim),
            Ui.Hint("Esc", "B", "voltar", UiFonts.Small, UiPalette.Dim));
        footer.Position = new Vector2(MenuX, 360 - 20);
        AddChild(footer);

        var version = Ui.Text($"v{ProjectSettings.GetSetting("application/config/version")}", UiFonts.Tiny, UiPalette.Disabled, align: HorizontalAlignment.Right);
        version.Position = new Vector2(640 - 70, 360 - 16);
        version.CustomMinimumSize = new Vector2(60, 0);
        AddChild(version);
    }

    private (SelectableRow Row, Label Sub) AddItem(string title, string sub, Action action)
    {
        var subLabel = Ui.Text(sub, UiFonts.Tiny, UiPalette.Dim);
        var row = new SelectableRow
        {
            Marker = false,
            CustomMinimumSize = new Vector2(MenuWidth, 26),
            OnActivate = action,
        };
        var titleLabel = Ui.Title(title, UiFonts.Large, UiPalette.SubLight);
        row.AddChild(Ui.Column(0, titleLabel, subLabel));
        row.OnFocused = () => HighlightMenu();
        _menu.AddChild(row);
        return (row, subLabel);
    }

    private void HighlightMenu()
    {
        foreach (var child in _menu.GetChildren())
        {
            if (child is SelectableRow row && row.GetChild(0) is VBoxContainer column)
            {
                var focused = row.HasFocus();
                ((Label)column.GetChild(0)).AddThemeColorOverride("font_color", row.Disabled ? UiPalette.Disabled : focused ? UiPalette.Creme : UiPalette.SubLight);
                ((Label)column.GetChild(1)).AddThemeColorOverride("font_color", focused ? new Color("#d8c4e6") : UiPalette.Dim);
            }
        }
    }

    private void RefreshContinue()
    {
        var hasSave = SaveService.Instance.HasSave();
        _continue.Disabled = !hasSave;
        if (hasSave)
        {
            GameManager.Instance.LoadGame();
            var state = GameManager.Instance.State;
            var cls = state.EquipmentRules.ClassName(state.Save.ClassId);
            _continueSub.Text = $"Dia {state.Save.Day} · Selo {state.RankDef.Id} · {state.Save.Name} ({cls})";
        }
        else
        {
            _continueSub.Text = UiText.MenuContinueEmpty;
        }

        HighlightMenu();
    }

    private void ContinueGame()
    {
        if (_starting || !SaveService.Instance.HasSave())
        {
            return;
        }

        _starting = true;
        GameManager.Instance.LoadGame();
        World.World.PendingStart = ("guild", new Vector2(250, 330)); // main.js:71 (Continuar)
        GameManager.Instance.ChangeScene(WorldScenePath);
    }

    private void NewGame()
    {
        if (!SaveService.Instance.HasSave())
        {
            _stack.Push(_story);
            return;
        }

        var state = GameManager.Instance.State;
        var text = UiText.ConfirmText
            .Replace("{name}", state.Save.Name, StringComparison.Ordinal)
            .Replace("{day}", state.Save.Day.ToString(), StringComparison.Ordinal)
            .Replace("{rank}", state.RankDef.Id, StringComparison.Ordinal);
        _confirm.Setup(UiText.ConfirmTitle, text, UiText.ConfirmErase, UiText.ConfirmCancel, () => _stack.Push(_story));
        _stack.Push(_confirm);
    }

    private void OpenClassSelect()
    {
        var name = SaveService.Instance.HasSave() ? GameManager.Instance.State.Save.Name : Database.Db.Rules.Start.Name;
        _class.Setup(name);
        _stack.Push(_class);
    }

    private void StartNewGame(string name, string classId)
    {
        if (_starting)
        {
            return;
        }

        _starting = true;
        GameManager.Instance.NewGame(name, classId);
        GameManager.Instance.SaveGame(); // a v2 salva no início do jogo (spec 01 §1.11)
        GameUi.PendingTutorial = true;
        World.World.PendingStart = null; // Guilda (250, 560)
        GameManager.Instance.ChangeScene(WorldScenePath);
    }
}
