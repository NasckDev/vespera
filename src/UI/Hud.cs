using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Ranks;
using Vespera.UI.Common;
using Vespera.UI.HudKit;
using Vespera.UI.Town;
using Vespera.UI.Widgets;

namespace Vespera.UI;

/// <summary>
/// HUD da v2 no estilo do guia hud-kit (spec 02 §4.1, <c>render.js:233-311</c> + <c>extra.js:139-158</c>):
/// retrato com Nível, Selo, barras de Vida (com lag), Energia e Essência (marca do custo do Eco), aviso sem Lanterna,
/// dicas de tecla, missão atual, DIA/local/estação·hora, Brechas abertas, onda, hotbar das 5 armas (cadeado fora da
/// classe), Eco [Q], Poção [R], Supremo [F] com recarga, Lumes/Estilhaços, caixa de loot, toasts, nome do lugar,
/// alerta e barra do chefe, Novo Selo e prompt de interação. Só escuta o EventBus; nada aloca por frame.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const float FlashSeconds = 0.35f; // redT (combat.js:16)
    private const float QuestPollSeconds = 0.5f;
    private const float DefaultEcoMarker = 60f; // render.js:247

    private static readonly Color FlashColor = new(180 / 255f, 20 / 255f, 50 / 255f);

    private readonly List<HotbarSlot> _weaponSlots = [];
    private readonly List<string> _weaponIds = [];
    private readonly Dictionary<string, int> _gearCounts = [];
    private readonly Dictionary<string, int> _gearScratch = [];
    private readonly Dictionary<string, int> _lastMaterials = [];

    private PortraitFrame _portrait = null!;
    private Label _name = null!;
    private Label _rankTitle = null!;
    private UiBar _levelBar = null!;
    private UiBar _hp = null!;
    private Label _hpText = null!;
    private UiBar _energy = null!;
    private UiBar _essence = null!;
    private Label _noLantern = null!;
    private PanelContainer _questPanel = null!;
    private Label _questName = null!;
    private Label _questDesc = null!;
    private Label _questProgress = null!;
    private Label _dayNumber = null!;
    private Label _seasonLine = null!;
    private VBoxContainer _brechaList = null!;
    private Label _invasion = null!;
    private Label _wave = null!;
    private HotbarSlot _ecoSlot = null!;
    private HotbarSlot _potionSlot = null!;
    private UltimateButton _ultimate = null!;
    private Label _ultimateName = null!;
    private Label _weaponName = null!;
    private HBoxContainer _prompt = null!;
    private Label _promptText = null!;
    private ColorRect _flash = null!;
    private Control _chrome = null!;
    private Banner _locationBanner = null!;
    private Banner _bossBanner = null!;
    private Banner _ultimateBanner = null!;
    private Control _root = null!;
    private Control _status = null!;
    private Control _topRight = null!;
    private Control _hotbar = null!;
    private Control _wallet = null!;
    private Control _minimapSlot = null!;
    private Control _essRow = null!;
    private PanelContainer _statusBack = null!;
    private PanelContainer _topBack = null!;
    private float _questExpandT;
    private bool _insideBrecha;
    private float _flashT;
    private float _essenceValue;
    private float _potionCooldown;
    private float _potionTotal = 1f;
    private int _shownMinute = -1;
    private string _locationName = string.Empty;
    private double _questPoll;
    private int _questIndex = -1;
    private int _questCounter = -1;
    private int _lastLumes;
    private int _lastPotions;
    private bool _equipmentMoved;

    /// <summary>HUD do World atual (para a UI mostrar toasts e notas).</summary>
    public static Hud? Current { get; private set; }

    /// <summary>Selo desenhado ao lado do Nível.</summary>
    public RankSeal RankSeal { get; private set; } = null!;

    /// <summary>"Nv. N · Selo X" (DECISIONS D23).</summary>
    public Label LevelLabel { get; private set; } = null!;

    /// <summary>Lumes (número com ponto de milhar, ícone de moeda ao lado).</summary>
    public Label LumesLabel { get; private set; } = null!;

    /// <summary>Estilhaços de Brecha.</summary>
    public Label FragmentsLabel { get; private set; } = null!;

    /// <summary>Nome do local (canto superior direito).</summary>
    public Label LocationLabel { get; private set; } = null!;

    /// <summary>Texto do prompt de interação (sem a tecla).</summary>
    public Label PromptLabel => _promptText;

    public bool PromptVisible => _prompt.Visible;

    public ToastBox Toasts { get; private set; } = null!;

    public LootFeed Loot { get; private set; } = null!;

    /// <summary>Dicas de controle da primeira vez (C1-D13).</summary>
    public OnboardingHints Onboarding { get; private set; } = null!;

    /// <summary>Legendas de sons (C1-D12).</summary>
    public CaptionBox Captions { get; private set; } = null!;

    public RankUpOverlay RankUp { get; private set; } = null!;

    public BossBar Boss { get; private set; } = null!;

    public Banner BossAlert => _bossBanner;

    public Label WaveLabel => _wave;

    public UiBar EnergyBar => _energy;

    public UiBar EssenceBar => _essence;

    public UiBar HpBar => _hp;

    public UltimateButton Ultimate => _ultimate;

    public HotbarSlot PotionSlot => _potionSlot;

    public IReadOnlyList<HotbarSlot> WeaponSlots => _weaponSlots;

    public Label QuestName => _questName;

    public PanelContainer QuestPanel => _questPanel;

    public VBoxContainer BrechaList => _brechaList;

    /// <summary>Painéis do HUD visíveis (falso enquanto uma tela cheia está aberta; toasts e Novo Selo continuam).</summary>
    public bool ChromeVisible
    {
        get => _chrome.Visible;
        set => _chrome.Visible = value;
    }

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        Layer = 10;
        // C1-D4: raiz sem âncoras, escalada pela opção "Escala da interface" (UiScaling.Fit).
        var root = _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);

        root.AddChild(Vignette());
        _flash = new ColorRect { Color = FlashColor with { A = 0 }, MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1, Visible = false };
        root.AddChild(_flash);

        // Painéis do HUD num grupo só: telas cheias (inventário, pausa, mural) escondem o grupo e não brigam com ele.
        _chrome = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _chrome.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_chrome);
        _chrome.AddChild(_status = BuildStatus());
        _chrome.AddChild(_topRight = BuildTopRight());
        _chrome.AddChild(_hotbar = BuildHotbar());
        _chrome.AddChild(_wallet = BuildWallet());
        _chrome.AddChild(BuildPrompt());

        // C1-D7: a caixa de loot fica no canto de baixo, em cima da carteira, e cresce para cima (no máximo 4 linhas).
        Loot = new LootFeed { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -190, OffsetRight = -8, OffsetTop = -150, OffsetBottom = -46 };
        Loot.GrowVertical = Control.GrowDirection.Begin;
        _chrome.AddChild(Loot);

        Boss = new BossBar { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -150, OffsetRight = 150, OffsetTop = 6 };
        _chrome.AddChild(Boss);

        // Onda no alto, no centro (era o 5º bloco empilhado no canto direito).
        _chrome.AddChild(_wave);
        _wave.AnchorLeft = 0.5f;
        _wave.AnchorRight = 0.5f;
        _wave.OffsetLeft = -120;
        _wave.OffsetRight = 120;
        _wave.OffsetTop = 6;
        _wave.HorizontalAlignment = HorizontalAlignment.Center;

        // C1-D8: letreiros menores e no alto; o do chefe fica logo abaixo da barra dele e dura 2 s.
        _locationBanner = new Banner(92, UiFonts.Heading, band: false, rule: true);
        _bossBanner = new Banner(30, UiFonts.Large, band: true, rule: false);
        _ultimateBanner = new Banner(64, UiFonts.Heading, band: true, rule: false);
        _chrome.AddChild(_locationBanner);
        _chrome.AddChild(_bossBanner);
        _chrome.AddChild(_ultimateBanner);

        // C1-D12: legendas acima da hotbar (fora dos painéis: continuam com tela de diálogo aberta).
        Captions = new CaptionBox { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -150, OffsetRight = 150, OffsetTop = -100, OffsetBottom = -64 };
        Captions.GrowVertical = Control.GrowDirection.Begin;
        root.AddChild(Captions);

        // C1-D13: dicas de controle da primeira vez, no mesmo lugar das legendas (logo acima delas).
        Onboarding = new OnboardingHints { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -124, OffsetBottom = -106 };
        Onboarding.GrowHorizontal = Control.GrowDirection.Both;
        Onboarding.GrowVertical = Control.GrowDirection.Begin;
        _chrome.AddChild(Onboarding);

        Toasts = new ToastBox { AnchorLeft = 0.5f, AnchorRight = 0.5f };
        root.AddChild(Toasts);

        RankUp = new RankUpOverlay();
        root.AddChild(RankUp);

        Subscribe();
        Refresh();
        GetViewport().SizeChanged += ApplyScale;
        ApplyScale();
    }

    /// <summary>Escala e contraste atuais (opções da frente C1). Chamado ao abrir, ao redimensionar e quando as opções mudam.</summary>
    private void ApplyScale()
    {
        UiScaling.Fit(_root, GameSettings.UiScale);
        UiScaling.ApplyOversampling(this);
        UiScaling.ApplyText(_root);
        var contrast = GameSettings.HighContrast;
        _statusBack.AddThemeStyleboxOverride("panel", HudBackdrop(contrast));
        _topBack.AddThemeStyleboxOverride("panel", HudBackdrop(contrast));
        Loot.HighContrast = contrast;
    }

    /// <summary>Fundo atrás dos blocos de cima: leve no normal, quase opaco e com borda no alto contraste.</summary>
    private static StyleBoxFlat HudBackdrop(bool contrast)
    {
        var style = new StyleBoxFlat
        {
            BgColor = contrast ? new Color(0.03f, 0.02f, 0.06f, 0.9f) : new Color(0.04f, 0.03f, 0.08f, 0.5f),
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
        };
        style.SetCornerRadiusAll(2);
        if (contrast)
        {
            style.SetBorderWidthAll(1);
            style.BorderColor = UiPalette.Creme with { A = 0.85f };
        }

        return style;
    }

    public override void _ExitTree()
    {
        if (GetViewport() is { } viewport)
        {
            viewport.SizeChanged -= ApplyScale;
        }

        HudZones.Clear();
        HudZones.MinimapSlot = default;
        Unsubscribe();
        if (Current == this)
        {
            Current = null;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_flashT > 0f)
        {
            _flashT = Mathf.Max(0f, _flashT - dt);
            _flash.Color = FlashColor with { A = _flashT * 0.5f * GameSettings.FlashScale }; // C1-D9
            _flash.Visible = _flashT > 0f;
        }

        if (_potionCooldown > 0f)
        {
            _potionCooldown = Mathf.Max(0f, _potionCooldown - dt);
            _potionSlot.VeilRatio = _potionCooldown / _potionTotal;
        }

        UpdateClock();
        if (_questExpandT > 0f)
        {
            _questExpandT -= dt;
            if (_questExpandT <= 0f)
            {
                _questDesc.Visible = false; // C1-D7: missão compacta depois de alguns segundos
            }
        }

        PublishZones();

        _questPoll += delta;
        if (_questPoll >= QuestPollSeconds)
        {
            _questPoll = 0;
            UpdateQuest(force: false);
        }
    }

    /// <summary>Toast no centro (título, subtítulo, cor da borda, ícone do atlas).</summary>
    public void Toast(string title, string sub = "", Color? color = null, string? icon = null, float seconds = ToastBox.DefaultSeconds) =>
        Toasts.Show(title, sub, color ?? UiPalette.Ambar, icon, seconds);

    /// <summary>Nota curta na caixa de loot da direita.</summary>
    public void Note(string text, string? icon = null, Color? color = null) => Loot.Add(text, icon, null, 1, color);

    // --- montagem ---

    private static TextureRect Vignette()
    {
        // render.js vignette: transparente até ~35% e rgba(8,6,16,.75) nas bordas.
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(8 / 255f, 6 / 255f, 16 / 255f, 0f));
        gradient.SetOffset(0, 0.45f);
        gradient.SetColor(1, new Color(8 / 255f, 6 / 255f, 16 / 255f, 0.55f));
        return new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 128,
                Height = 72,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1.05f, 0.5f),
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
    }

    private Control BuildStatus()
    {
        // C1-D7: retrato, nome, Nível/Selo, Vida, Energia e (só com Eco) Essência; texto nunca abaixo de 8 px base
        // (16 px em 720p). As dicas de tecla saíram daqui: viraram as dicas da primeira vez (Onboarding).
        _portrait = new PortraitFrame();
        _name = Ui.Title(string.Empty, 10);
        RankSeal = new RankSeal { CustomMinimumSize = new Vector2(10, 10), LabelFont = UiFonts.TextBold, LabelFontSize = 6, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        LevelLabel = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar);
        _rankTitle = Ui.Text(string.Empty, UiFonts.Body);
        _rankTitle.Visible = false; // o título do Selo está no inventário e no Novo Selo; aqui só pesava a linha
        _levelBar = UiBar.Create("xp", new Vector2(84, 2));
        var identity = Ui.Column(1, _name, Ui.Row(3, RankSeal, LevelLabel, _rankTitle), _levelBar);
        var header = Ui.Row(6, _portrait, identity);

        _hp = UiBar.Create("hp", new Vector2(104, 7), lag: true, segments: 10);
        _hpText = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Creme);
        _energy = UiBar.Create("energy", new Vector2(78, 5), segments: 5);
        _essence = UiBar.Create("essence", new Vector2(63, 4));
        var hpRow = Ui.Row(3, IconRect.Create("ui_hpicon", 11), _hp, _hpText);
        var enRow = Ui.Row(3, IconRect.Create("ui_sticon", 10), _energy);
        _essRow = Ui.Row(3, Ui.Gap(10), _essence, Ui.Text(UiText.HudEssence, UiFonts.Body, UiPalette.EcoText));
        _noLantern = UiScaling.Reading(Ui.Text(UiText.HudNoLantern, UiFonts.Body, UiPalette.Brecha));

        _questName = UiScaling.Reading(Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar));
        _questProgress = UiScaling.Reading(Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Creme));
        _questDesc = UiScaling.Reading(Ui.Text(string.Empty, UiFonts.Body, UiPalette.Body, wrap: true));
        _questDesc.CustomMinimumSize = new Vector2(150, 0);
        var questHeader = Ui.Row(3, IconRect.Create("ic_scroll", 9), _questName, Ui.Spacer(), _questProgress);
        _questPanel = Ui.Panel("SoftPanel", Ui.Column(1, questHeader, _questDesc));
        _questPanel.CustomMinimumSize = new Vector2(150, 0);
        _questPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

        var column = Ui.Column(2, header, hpRow, enRow, _essRow, _noLantern);
        _statusBack = Ui.Panel("SoftPanel", column);
        _statusBack.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        var block = Ui.Column(4, _statusBack, _questPanel);
        block.Position = new Vector2(6, 6);
        return block;
    }

    private Control BuildTopRight()
    {
        _dayNumber = Ui.Title("1", 11, null, HorizontalAlignment.Center);
        var dayBox = Ui.Ornate(Ui.Column(0, Ui.Text(UiText.HudDay, UiFonts.Small, UiPalette.Sub, align: HorizontalAlignment.Center), _dayNumber), "OrnatePanelAmber");
        dayBox.CornerColor = UiPalette.Ambar;
        dayBox.CornerSize = 2f;
        dayBox.InnerLine = false;
        dayBox.AddThemeStyleboxOverride("panel", Compact((StyleBoxFlat)dayBox.GetThemeStylebox("panel", "OrnatePanelAmber")));
        dayBox.CustomMinimumSize = new Vector2(30, 0);

        LocationLabel = Ui.Title(string.Empty, 9, null, HorizontalAlignment.Right);
        _seasonLine = Ui.Text(string.Empty, UiFonts.Body, UiPalette.SubLight, align: HorizontalAlignment.Right);
        var info = Ui.Column(0, LocationLabel, _seasonLine);
        info.Alignment = BoxContainer.AlignmentMode.Center;
        _topBack = Ui.Panel("SoftPanel", info);
        var top = Ui.Row(4, _topBack, dayBox);
        top.Alignment = BoxContainer.AlignmentMode.End;

        _brechaList = Ui.Column(2);
        _invasion = UiScaling.Reading(Ui.Text(UiText.HudInvasion, UiFonts.Body, UiPalette.Brecha, align: HorizontalAlignment.Right));
        _wave = Ui.Title(string.Empty, UiFonts.Large, UiPalette.Brecha, HorizontalAlignment.Right);
        _wave.Visible = false;
        // Integração v0.1: o minimapa do motor fica logo abaixo do nome do local; C1-D7: o HUD reserva o lugar e diz
        // ao minimapa onde desenhar (HudZones.MinimapSlot), para os dois crescerem juntos com a escala da interface.
        _minimapSlot = new Control { CustomMinimumSize = new Vector2(120, 70), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        var column = Ui.Column(4, top, _minimapSlot, _brechaList, _invasion);
        column.AnchorLeft = 1;
        column.AnchorRight = 1;
        column.OffsetLeft = -180;
        column.OffsetRight = -6;
        column.OffsetTop = 6;
        column.GrowHorizontal = Control.GrowDirection.Begin;
        return column;
    }

    private Control BuildHotbar()
    {
        var db = Database.Db;
        _ultimate = new UltimateButton();
        _ultimateName = Ui.Text(string.Empty, UiFonts.Body, UiPalette.Creme, align: HorizontalAlignment.Center);
        var ultCap = KeyCap.ForAction(InputActions.Ultimate, UiFonts.Small); // C1-D6: segue o remapeamento
        var ultBox = new Control { CustomMinimumSize = new Vector2(29, 29), MouseFilter = Control.MouseFilterEnum.Ignore };
        ultBox.AddChild(_ultimate);
        ultBox.AddChild(ultCap);
        ultCap.Position = new Vector2(-4, 18);
        var ultColumn = Ui.Column(1, _ultimateName, ultBox);
        ultColumn.Alignment = BoxContainer.AlignmentMode.End;

        var weapons = Ui.Row(3);
        for (var i = 0; i < db.WeaponOrder.Count; i++)
        {
            var weapon = db.WeaponOrder[i];
            var slot = new HotbarSlot { IconKey = weapon.Icon };
            slot.SetKey((i + 1).ToString(), UiPalette.Dim);
            weapons.AddChild(slot);
            _weaponSlots.Add(slot);
            _weaponIds.Add(weapon.Id);
        }

        _ecoSlot = new HotbarSlot { IconKey = "i_eco", IconScale = 0.7f };
        _potionSlot = new HotbarSlot { IconKey = "ic_potion", IconScale = 0.6f };
        RefreshSlotKeys();

        _weaponName = Ui.Text(string.Empty, UiFonts.Body, new Color("#d8cfe0"), align: HorizontalAlignment.Center);
        var slots = Ui.Row(8, ultColumn, weapons, Ui.Row(3, _ecoSlot, _potionSlot));
        slots.Alignment = BoxContainer.AlignmentMode.Center;
        var column = Ui.Column(2, _weaponName, slots);
        column.AnchorLeft = 0.5f;
        column.AnchorRight = 0.5f;
        column.AnchorTop = 1;
        column.AnchorBottom = 1;
        column.OffsetLeft = -125;
        column.OffsetRight = 125;
        column.OffsetTop = -60;
        column.OffsetBottom = -6;
        column.GrowVertical = Control.GrowDirection.Begin;
        column.Alignment = BoxContainer.AlignmentMode.End;
        _weaponName.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        return column;
    }

    private Control BuildWallet()
    {
        FragmentsLabel = Ui.Bold("0", UiFonts.Large, UiPalette.Creme, HorizontalAlignment.Right);
        LumesLabel = Ui.Bold("0", UiFonts.Large, UiPalette.Creme, HorizontalAlignment.Right);
        var shards = Ui.Row(3, IconRect.Create("i_shard", 9), FragmentsLabel);
        var lumes = Ui.Row(3, IconRect.Create("i_coin", 10), LumesLabel);
        shards.Alignment = BoxContainer.AlignmentMode.End;
        lumes.Alignment = BoxContainer.AlignmentMode.End;
        var column = Ui.Column(1, shards, lumes);
        column.AnchorLeft = 1;
        column.AnchorRight = 1;
        column.AnchorTop = 1;
        column.AnchorBottom = 1;
        column.OffsetLeft = -90;
        column.OffsetRight = -9;
        column.OffsetTop = -34;
        column.OffsetBottom = -7;
        column.GrowVertical = Control.GrowDirection.Begin;
        column.Alignment = BoxContainer.AlignmentMode.End;
        return column;
    }

    private Control BuildPrompt()
    {
        _promptText = Ui.Text(string.Empty, UiFonts.Body);
        var cap = KeyCap.ForAction(InputActions.Interact, UiFonts.Small);
        var panel = Ui.Panel("SoftPanel", Ui.Row(4, cap, _promptText));
        _prompt = Ui.Row(0, panel);
        _prompt.Alignment = BoxContainer.AlignmentMode.Center;
        _prompt.AnchorLeft = 0.5f;
        _prompt.AnchorRight = 0.5f;
        _prompt.AnchorTop = 1;
        _prompt.AnchorBottom = 1;
        _prompt.OffsetLeft = -120;
        _prompt.OffsetRight = 120;
        _prompt.OffsetTop = -78;
        _prompt.OffsetBottom = -60;
        _prompt.Visible = false;
        return _prompt;
    }

    private static StyleBoxFlat Compact(StyleBoxFlat source)
    {
        var style = (StyleBoxFlat)source.Duplicate();
        style.ContentMarginLeft = 3;
        style.ContentMarginRight = 3;
        style.ContentMarginTop = 1;
        style.ContentMarginBottom = 1;
        return style;
    }

    // --- sinais ---

    private void Subscribe()
    {
        var bus = EventBus.Instance;
        bus.GameLoaded += Refresh;
        bus.PlayerDamaged += OnPlayerDamaged;
        bus.PlayerHealed += OnHealth;
        bus.PlayerStatsChanged += RefreshHealth;
        bus.XpGained += OnXpGained;
        bus.RankChanged += OnRankChanged;
        bus.LevelChanged += OnLevelChanged;
        bus.LumesChanged += OnLumesChanged;
        bus.FragmentsChanged += OnFragmentsChanged;
        bus.MaterialChanged += OnMaterialChanged;
        bus.InventoryChanged += OnInventoryChanged;
        bus.EquipmentChanged += OnEquipmentChanged;
        bus.PotionsChanged += OnPotionsChanged;
        bus.LocationChanged += OnLocationChanged;
        bus.InteractionPromptChanged += OnInteractionPromptChanged;
        bus.StaminaChanged += OnStaminaChanged;
        bus.EssenceChanged += OnEssenceChanged;
        bus.WeaponChanged += OnWeaponChanged;
        bus.WeaponUpgraded += OnWeaponUpgraded;
        bus.AbilityCooldownChanged += OnAbilityCooldown;
        bus.EcoChanged += OnEcoChanged;
        bus.LanternStateChanged += OnLanternState;
        bus.DayChanged += OnDayChanged;
        bus.WaveStarted += OnWaveStarted;
        bus.BossSpawned += OnBossSpawned;
        bus.BossHealthChanged += OnBossHealth;
        bus.BrechaStarted += OnBrechaStarted;
        bus.BrechaEnded += OnBrechaEnded;
        bus.ToastRequested += OnToastRequested;
        bus.QuestCompleted += OnQuestCompleted;
        bus.PortalOpened += OnBrechasChanged;
        bus.PortalClosed += OnPortalClosed;
        bus.InvasionStateChanged += OnInvasionChanged;
        bus.InvasionRequested += OnInvasionRequested;
        GameSettings.Changed += OnSettingsChanged;
        InputBindings.Changed += RefreshSlotKeys;
        InputHints.DeviceChanged += RefreshSlotKeys;
        bus.CaptionRequested += Caption;
    }

    private void Unsubscribe()
    {
        var bus = EventBus.Instance;
        bus.GameLoaded -= Refresh;
        bus.PlayerDamaged -= OnPlayerDamaged;
        bus.PlayerHealed -= OnHealth;
        bus.PlayerStatsChanged -= RefreshHealth;
        bus.XpGained -= OnXpGained;
        bus.RankChanged -= OnRankChanged;
        bus.LevelChanged -= OnLevelChanged;
        bus.LumesChanged -= OnLumesChanged;
        bus.FragmentsChanged -= OnFragmentsChanged;
        bus.MaterialChanged -= OnMaterialChanged;
        bus.InventoryChanged -= OnInventoryChanged;
        bus.EquipmentChanged -= OnEquipmentChanged;
        bus.PotionsChanged -= OnPotionsChanged;
        bus.LocationChanged -= OnLocationChanged;
        bus.InteractionPromptChanged -= OnInteractionPromptChanged;
        bus.StaminaChanged -= OnStaminaChanged;
        bus.EssenceChanged -= OnEssenceChanged;
        bus.WeaponChanged -= OnWeaponChanged;
        bus.WeaponUpgraded -= OnWeaponUpgraded;
        bus.AbilityCooldownChanged -= OnAbilityCooldown;
        bus.EcoChanged -= OnEcoChanged;
        bus.LanternStateChanged -= OnLanternState;
        bus.DayChanged -= OnDayChanged;
        bus.WaveStarted -= OnWaveStarted;
        bus.BossSpawned -= OnBossSpawned;
        bus.BossHealthChanged -= OnBossHealth;
        bus.BrechaStarted -= OnBrechaStarted;
        bus.BrechaEnded -= OnBrechaEnded;
        bus.ToastRequested -= OnToastRequested;
        bus.QuestCompleted -= OnQuestCompleted;
        bus.PortalOpened -= OnBrechasChanged;
        bus.PortalClosed -= OnPortalClosed;
        bus.InvasionStateChanged -= OnInvasionChanged;
        bus.InvasionRequested -= OnInvasionRequested;
        GameSettings.Changed -= OnSettingsChanged;
        InputBindings.Changed -= RefreshSlotKeys;
        InputHints.DeviceChanged -= RefreshSlotKeys;
        bus.CaptionRequested -= Caption;
    }

    /// <summary>Mostra uma legenda de som se a opção estiver ligada (C1-D12).</summary>
    public void Caption(string text)
    {
        if (GameSettings.Captions)
        {
            Captions.Show(text);
        }
    }

    /// <summary>Relê tudo do estado atual (início e depois de carregar um save).</summary>
    private void Refresh()
    {
        var game = GameManager.Instance;
        var state = game.State;
        _name.Text = state.Save.Name;
        _portrait.SetClass(state.Save.ClassId);
        RankSeal.Rank = game.Rank;
        UpdateLevel();
        RefreshHealth();
        _lastLumes = state.Lumes;
        _lastPotions = state.Save.Potions;
        LumesLabel.Text = Ui.Thousands(state.Lumes);
        FragmentsLabel.Text = state.Shards.ToString();
        _lastMaterials.Clear();
        foreach (var (id, count) in state.Save.Materials)
        {
            _lastMaterials[id] = count;
        }

        CountGear(state.Save.Backpack, notify: false);

        _noLantern.Visible = state.HasLostLantern;
        _dayNumber.Text = state.Save.Day.ToString();
        _shownMinute = -1;
        UpdateClock();
        _energy.SetRatio(1f, instant: true);
        _essenceValue = 0;
        _essence.SetRatio(0, instant: true);
        UpdateEco();
        UpdateHotbar(state.Save.ClassId);
        UpdatePotions(state.Save.Potions);
        var ult = Database.Db.Ultimates.TryGetValue(state.Save.ClassId, out var u) ? u : null;
        _ultimate.Setup(Database.Db.Weapons[state.Save.ClassId].Icon, UiPalette.FromHex(ult?.Color, UiPalette.Ambar));
        _ultimateName.Text = ult?.Name ?? string.Empty;
        _ultimate.SetCooldown(0, 1);
        _questIndex = -1;
        UpdateQuest(force: true);
        OnSettingsChanged();
        RefreshBrechas();
    }

    private void RefreshHealth()
    {
        var game = GameManager.Instance;
        OnHealth(game.CurrentHp, game.MaxHp);
    }

    private void OnHealth(int hp, int max)
    {
        _hp.SetRatio(max > 0 ? hp / (float)max : 0f);
        _hpText.Text = $"{Math.Max(0, hp)} / {max}";
    }

    private void OnPlayerDamaged(int amount, int hp, int max)
    {
        OnHealth(hp, max);
        _flashT = FlashSeconds;
    }

    private void OnXpGained(int amount, int totalXp, float progressInRank) => UpdateLevel();

    private void OnRankChanged(Rank previous, Rank current)
    {
        RankSeal.Rank = current;
        UpdateLevel();
        if (current > previous)
        {
            var def = Database.Db.Ranks[(int)current];
            RankUp.Play(current, def.Id, def.Title, UiPalette.FromHex(def.Color, UiPalette.Ambar));
        }
    }

    private void OnLevelChanged(int previous, int current) => UpdateLevel();

    private void UpdateLevel()
    {
        var state = GameManager.Instance.State;
        var level = state.Level;
        LevelLabel.Text = $"Nv. {level.Level} · Selo {state.RankDef.Id}";
        _rankTitle.Text = state.RankDef.Title;
        _rankTitle.AddThemeColorOverride("font_color", UiPalette.FromHex(state.RankDef.Color, UiPalette.Creme));
        _levelBar.SetRatio((float)level.Progress);
        _portrait.SetLevel(level.Level.ToString());
    }

    private void OnLumesChanged(int total)
    {
        var delta = total - _lastLumes;
        _lastLumes = total;
        LumesLabel.Text = Ui.Thousands(total);
        if (delta > 0)
        {
            Loot.Add($"+{delta} Lumes", "i_coin", "lumes", 1, UiPalette.Ambar);
        }
    }

    private void OnFragmentsChanged(int total) => FragmentsLabel.Text = total.ToString();

    private void OnMaterialChanged(string materialId, int count)
    {
        var before = _lastMaterials.GetValueOrDefault(materialId);
        _lastMaterials[materialId] = count;
        if (count > before && Database.Db.Materials.TryGetValue(materialId, out var mat))
        {
            Loot.Add(mat.Name, mat.Icon, "m:" + materialId, count - before, UiPalette.FromHex(Database.Db.Rarities[mat.Rarity].Color, UiPalette.Creme));
        }
    }

    private void OnInventoryChanged()
    {
        // Trocar/desequipar só move itens entre o slot e a mochila: não é loot.
        CountGear(GameManager.Instance.State.Save.Backpack, notify: !_equipmentMoved);
        _equipmentMoved = false;
    }

    private void OnEquipmentChanged(string slotId) => _equipmentMoved = true;

    /// <summary>Recontagem da mochila; com <paramref name="notify"/>, cada cópia nova vira uma linha na caixa de loot.</summary>
    private void CountGear(List<string> backpack, bool notify)
    {
        foreach (var id in _gearCounts.Keys)
        {
            _gearScratch[id] = 0;
        }

        foreach (var id in backpack)
        {
            _gearScratch[id] = _gearScratch.GetValueOrDefault(id) + 1;
        }

        foreach (var (id, count) in _gearScratch)
        {
            var before = _gearCounts.GetValueOrDefault(id);
            if (notify && count > before && Database.Db.Gear.TryGetValue(id, out var gear))
            {
                Loot.Add(gear.Name, gear.Icon, "g:" + id, count - before, UiPalette.FromHex(Database.Db.Rarities[gear.Rarity].Color, UiPalette.Creme));
            }

            _gearCounts[id] = count;
        }

        _gearScratch.Clear();
    }

    private void OnPotionsChanged(int count)
    {
        if (count > _lastPotions)
        {
            Loot.Add("Poção", "ic_potion", "potion", count - _lastPotions, UiPalette.Heal);
        }

        _lastPotions = count;
        UpdatePotions(count);
    }

    private void UpdatePotions(int count)
    {
        _potionSlot.SetCorner(count.ToString(), UiPalette.Creme);
        _potionSlot.IconAlpha = count > 0 ? 1f : 0.3f;
    }

    private void OnLocationChanged(string locationName)
    {
        LocationLabel.Text = locationName;
        if (locationName != _locationName)
        {
            _locationName = locationName;
            _locationBanner.Play(locationName, string.Empty, UiPalette.Creme, 2.4f);
        }
    }

    /// <summary>
    /// Integração v0.1 (DECISIONS INT-D5): o motor do mundo já mostra o prompt "[E] Falar com…" em cima do interagível,
    /// com a tecla do último dispositivo usado; o rodapé do HUD só repetiria o texto. Fica desligado (o texto continua
    /// atualizado em <see cref="PromptLabel"/>); ligue se um dia o prompt do mundo sair.
    /// </summary>
    public static bool ShowPromptInHud { get; set; }

    private void OnInteractionPromptChanged(string prompt)
    {
        _prompt.Visible = ShowPromptInHud && prompt.Length > 0;
        _promptText.Text = prompt;
    }

    private void OnStaminaChanged(float current, float max) => _energy.SetRatio(max > 0 ? current / max : 0f);

    private void OnEssenceChanged(float current, float max)
    {
        _essenceValue = current;
        _essence.SetRatio(max > 0 ? current / max : 0f);
        UpdateEco();
    }

    private void UpdateEco()
    {
        var state = GameManager.Instance.State;
        var cost = state.Save.EquippedEco is { } eco ? state.EcoCost(eco) : DefaultEcoMarker;
        _essence.SetMarker(cost / 100f);
        var hasEco = state.Save.EquippedEco is not null;
        _ecoSlot.Visible = hasEco; // C1-D7: slot vazio e barra de Essência só aparecem quando há Eco para usar
        _essRow.Visible = hasEco;
        if (!hasEco)
        {
            _ecoSlot.IconAlpha = 0f;
            _ecoSlot.VeilRatio = 0f;
            return;
        }

        var ok = _essenceValue >= cost;
        _ecoSlot.IconAlpha = ok ? 1f : 0.35f;
        _ecoSlot.VeilRatio = ok ? 0f : 1f - (_essenceValue / cost);
    }

    private void OnEcoChanged(string ecoId) => UpdateEco();

    private void OnWeaponChanged(string weaponId) => UpdateHotbar(weaponId);

    private void OnWeaponUpgraded(int level) => UpdateHotbar(GameManager.Instance.State.Save.ClassId);

    private void UpdateHotbar(string current)
    {
        var save = GameManager.Instance.State.Save;
        for (var i = 0; i < _weaponSlots.Count; i++)
        {
            var id = _weaponIds[i];
            var slot = _weaponSlots[i];
            var level = save.WeaponLevel(id);
            slot.Selected = id == current;
            slot.Locked = id != save.ClassId; // extra.js:141: só a arma da classe
            slot.Visible = !slot.Locked; // C1-D7: os 4 cadeados não diziam nada ao novato e enchiam o rodapé
            slot.IconAlpha = level > 0 ? 1f : 0.18f;
            slot.SetKey(InputHints.UsingGamepad ? string.Empty : (i + 1).ToString(), slot.Selected ? UiPalette.Ambar : UiPalette.Dim);
            slot.SetCorner(level > 1 ? $"+{level}" : string.Empty, UiPalette.Ambar);
        }

        _weaponName.Text = Database.Db.Weapons.TryGetValue(current, out var w) ? w.Name : string.Empty;
    }

    private void OnAbilityCooldown(string abilityId, float remaining, float total)
    {
        if (abilityId == "ultimate")
        {
            // Supremo acabou de ser usado: banner com o nome (extra.js:156). A recarga inicial de 6 s não é uso.
            var cls = GameManager.Instance.State.Save.ClassId;
            if (remaining >= total - 0.05f && _ultimate.IsReady && Database.Db.Ultimates.TryGetValue(cls, out var ult)
                && Mathf.IsEqualApprox(total, (float)ult.Cooldown))
            {
                _ultimateBanner.Play(ult.Name.ToUpperInvariant(), string.Empty, UiPalette.FromHex(ult.Color, UiPalette.Ambar), 1.4f, 6f, 3f, 40f);
            }

            _ultimate.SetCooldown(remaining, total);
        }
        else if (abilityId == "potion")
        {
            _potionCooldown = remaining;
            _potionTotal = Mathf.Max(0.01f, total);
            _potionSlot.VeilRatio = remaining / _potionTotal;
        }
        else if (abilityId.StartsWith("eco", StringComparison.Ordinal))
        {
            _ecoSlot.VeilRatio = total > 0 ? Mathf.Max(_ecoSlot.VeilRatio, remaining / total) : 0f;
        }
    }

    private void OnLanternState(bool lost) => _noLantern.Visible = lost;

    private void OnDayChanged(int day)
    {
        _dayNumber.Text = day.ToString();
        _shownMinute = -1;
        UpdateClock(); // o toast "Dia N · Estação" é da Cidade (api-cidade: reage ao DayChanged), não daqui
    }

    private void UpdateClock()
    {
        var state = GameManager.Instance.State;
        var minutes = (int)state.Save.ClockMinutes;
        if (minutes == _shownMinute)
        {
            return;
        }

        _shownMinute = minutes;
        var clock = Core.Rules.SeasonCalendar.FormatClock(state.Save.ClockMinutes);
        var vespera = state.Calendar.IsVespera(state.Save.ClockMinutes) ? UiText.HudVespera : string.Empty;
        _seasonLine.Text = $"{state.Season.Name} · {clock}{vespera}";
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        _wave.Text = UiText.HudWave.Replace("{n}", wave.ToString(), StringComparison.Ordinal).Replace("{total}", totalWaves.ToString(), StringComparison.Ordinal);
        _wave.Visible = true;
    }

    private void OnBossSpawned(string bossName, int maxHp)
    {
        Boss.Begin(bossName, maxHp);
        Caption(UiText.CaptionBoss);
        Toasts.Top = 42;
        _locationBanner.Stop(); // o nome da Câmara do Núcleo ainda estava na tela e sobrepunha o do chefe
        _bossBanner.Play(bossName.ToUpperInvariant(), UiText.BossIntroHint, UiPalette.Brecha, 2.0f, 4f, 2.5f); // C1-D8: era 3,2 s no meio da arena
        _wave.Visible = false;
    }

    private void OnBossHealth(int hp, int maxHp)
    {
        Boss.SetHealth(hp, maxHp);
        if (!Boss.Visible)
        {
            Toasts.Top = 40;
        }
    }

    private void OnBrechaStarted(string themeId, int rank)
    {
        _wave.Visible = false;
        _insideBrecha = true;
        CallDeferred(MethodName.RefreshBrechas);
    }

    private void OnBrechaEnded(string themeId, bool cleared)
    {
        _insideBrecha = false;
        Boss.End();
        Toasts.Top = 40;
        _wave.Visible = false;
        CallDeferred(MethodName.RefreshBrechas);
    }

    /// <summary><c>ToastRequested(msg)</c>: "título" ou "título\nsubtítulo".</summary>
    private void OnToastRequested(string message)
    {
        var split = message.IndexOf('\n');
        if (split < 0)
        {
            Toast(message);
        }
        else
        {
            Toast(message[..split], message[(split + 1)..]);
        }
    }

    private void OnQuestCompleted(string questId) => UpdateQuest(force: true);

    private void UpdateQuest(bool force)
    {
        var state = GameManager.Instance.State;
        var quest = state.CurrentQuest;
        var index = state.Save.Quests.Index;
        var counter = quest is { Condition.Key: { } key } ? state.Save.Quests.Counter(key) : -1;
        if (!force && index == _questIndex && counter == _questCounter)
        {
            return;
        }

        _questIndex = index;
        _questCounter = counter;
        if (quest is null)
        {
            _questPanel.Visible = false;
            return;
        }

        _questName.Text = quest.Name;
        _questDesc.Text = quest.Desc;
        var progress = state.QuestRules.ProgressText(quest, state.Save);
        _questProgress.Text = progress ?? string.Empty;
        _questPanel.Visible = GameSettings.QuestHints;
        ExpandQuest();
    }

    private void OnSettingsChanged()
    {
        _questPanel.Visible = GameSettings.QuestHints && GameManager.Instance.State.CurrentQuest is not null;
        ApplyScale();
    }

    /// <summary>Segundos que a missão fica aberta (descrição) depois de mudar; depois fica só o nome e o progresso.</summary>
    public const float QuestExpandSeconds = 8f;

    /// <summary>A descrição da missão está à vista (smoke test).</summary>
    public bool QuestExpanded => _questDesc.Visible;

    /// <summary>Mostra a descrição da missão por <see cref="QuestExpandSeconds"/> (missão nova, progresso, Diário).</summary>
    public void ExpandQuest()
    {
        _questDesc.Visible = true;
        _questExpandT = QuestExpandSeconds;
    }

    /// <summary>Teclas dos slots pelo dispositivo e pelo remapeamento (C1-D6).</summary>
    private void RefreshSlotKeys()
    {
        var pad = InputHints.UsingGamepad;
        _ecoSlot.SetKey(InputBindings.Label(InputActions.Eco1, pad) ?? string.Empty, UiPalette.EcoText);
        _potionSlot.SetKey(InputBindings.Label(InputActions.Potion, pad) ?? string.Empty, UiPalette.Dim);
        if (_weaponName is not null)
        {
            UpdateHotbar(GameManager.Instance.State.Save.ClassId);
        }
    }

    /// <summary>
    /// Zonas ocupadas pelo HUD para os textos do mundo não passarem por cima (C1-D7) e o lugar do minimapa. Sem
    /// alocação: só retângulos.
    /// </summary>
    private void PublishZones()
    {
        Toasts.Avoid = _chrome.Visible ? new Rect2(_status.Position, _status.Size) : default;
        HudZones.Clear();
        HudZones.MinimapSlot = _chrome.Visible && _minimapSlot.IsVisibleInTree() ? _minimapSlot.GetGlobalRect() : default;
        if (!_chrome.Visible)
        {
            return;
        }

        AddZone(_statusBack);
        AddZone(_questPanel);
        AddZone(_topBack);
        AddZone(_minimapSlot);
        AddZone(_brechaList);
        AddZone(_hotbar);
        AddZone(_wallet);
        AddZoneChildren(Loot);
        AddZone(Boss);
        AddZone(_wave);
    }

    /// <summary>Só as linhas visíveis (a caixa vazia ocupa o canto inteiro sem desenhar nada).</summary>
    private static void AddZoneChildren(Control container)
    {
        if (!container.IsVisibleInTree())
        {
            return;
        }

        var count = container.GetChildCount();
        for (var i = 0; i < count; i++)
        {
            if (container.GetChild(i) is Control { Visible: true } child)
            {
                HudZones.Add(child.GetGlobalRect().Grow(2f));
            }
        }
    }

    private static void AddZone(Control c)
    {
        if (c.IsVisibleInTree())
        {
            HudZones.Add(c.GetGlobalRect().Grow(2f));
        }
    }

    private void OnBrechasChanged(int spot, string themeId, int rank)
    {
        Caption(UiText.CaptionPortal);
        CallDeferred(MethodName.RefreshBrechas);
    }

    private void OnPortalClosed(int spot) => CallDeferred(MethodName.RefreshBrechas);

    private void OnInvasionChanged(bool active) => CallDeferred(MethodName.RefreshBrechas);

    private void OnInvasionRequested(string themeId, int rank)
    {
        Caption(UiText.CaptionInvasion);
        CallDeferred(MethodName.RefreshBrechas);
    }

    /// <summary>Lista de Brechas não fechadas (<c>render.js:257-266</c>), sem timer (a Cidade não publica).</summary>
    public void RefreshBrechas()
    {
        Ui.Clear(_brechaList);
        var tracker = BrechaTracker.Current;
        _invasion.Visible = tracker?.Invasion == true;
        if (tracker is null || _insideBrecha)
        {
            return; // C1-D7: dentro da Brecha a lista só repetia "você está dentro"
        }

        foreach (var b in tracker.All)
        {
            if (b.Status == BrechaStatus.Closed)
            {
                continue;
            }

            var inside = tracker.Inside == b;
            var ruptured = b.Status == BrechaStatus.Ruptured;
            var highlight = inside || tracker.TrackedId == b.Id;
            var sub = ruptured ? UiText.HudRuptured : inside ? UiText.HudInside : BrechaTracker.Title(b);
            var rankDef = Database.Db.Ranks[Math.Clamp(b.Rank, 0, Database.Db.Ranks.Count - 1)];
            var lines = Ui.Column(0,
                Ui.Text(UiText.HudBrecha + BrechaTracker.Bairro(b.ThemeId), UiFonts.Body),
                Ui.Text(sub, UiFonts.Body, ruptured ? UiPalette.Brecha : new Color("#b9aec4")));
            var row = Ui.Row(4, IconRect.Create(rankDef.Icon, 11), lines);
            var panel = Ui.Panel(highlight || ruptured ? "SoftPanelDanger" : "SoftPanel", row);
            panel.CustomMinimumSize = new Vector2(120, 0);
            panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            _brechaList.AddChild(panel);
        }
    }
}
