using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.UI;

/// <summary>
/// HUD de caçada (camada 4): selo do rank, vida e fôlego (barras pintadas da interface), Fragmentos (da caçada dentro
/// da Brecha, da carteira na cidade — os dois da <see cref="Hunter.Progression"/>), barra do Guardião com nome e
/// núcleos, título da sala e avisos curtos. Lê o estado a cada quadro. No co-op local há um painel por jogador: J1 à
/// esquerda, J2 à direita (espelhado), cada um com a etiqueta e a cor dele, os próprios Fragmentos, classe e rank.
/// </summary>
public partial class CombatHud : Control
{
    private const float ToastSeconds = 2.8f;
    private const float TitleSeconds = 2.0f;
    private const float BossBarWidth = 960f;
    private const float ScreenWidth = 1920f;
    private const float BarTextMaxFactor = 1.25f;
    private const float PanelBackdropLeft = 10f;
    private const float PanelBackdropTop = 10f;
    private const float PanelBackdropWidth = 700f;
    private const float PanelBackdropHeight = 218f;

    /// <summary>Título e avisos ficam abaixo da linha de XP/Lumes da caçada (y 180-260), que antes eles cortavam.</summary>
    private const float TitleY = 300f;
    private const float ToastY = 470f;

    private readonly Queue<string> _toasts = new();
    private readonly PlayerPanel[] _panels = new PlayerPanel[2];
    private LevelWorld? _world;
    private Control _bossBox = null!;
    private TextureProgressBar _bossBar = null!;
    private Label _bossName = null!;
    private CoreDots _cores = null!;
    private Label _toast = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private float _toastTime;
    private float _titleTime;

    public string LastToast { get; private set; } = string.Empty;

    /// <summary>O painel do J2 está à vista (co-op).</summary>
    public bool GuestPanelVisible => _panels[1].Root.Visible;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _panels[0] = BuildPlayerPanel(0);
        _panels[1] = BuildPlayerPanel(1);
        BuildBossBar();
        BuildBanners();
    }

    public void Bind(LevelWorld world) => _world = world;

    public void Toast(string text)
    {
        LastToast = text;
        _toasts.Enqueue(text);
        if (_toasts.Count > 4)
        {
            _toasts.Dequeue();
        }
    }

    /// <summary>Esquece os avisos da sala anterior.</summary>
    public void ClearToasts()
    {
        _toasts.Clear();
        _toastTime = 0f;
    }

    /// <summary>Título grande da sala ao entrar (some sozinho).</summary>
    public void ShowTitle(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _titleTime = TitleSeconds;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        UpdatePlayer(dt);
        UpdateBoss();
        UpdateBanners(dt);
    }

    private void UpdatePlayer(float dt)
    {
        if (_world is null || !IsInstanceValid(_world))
        {
            return;
        }

        var coop = _world.Players.Count > 1;
        for (var slot = 0; slot < _panels.Length; slot++)
        {
            var player = slot == 0 ? _world.Player : _world.PlayerAt(slot);
            var show = player is not null && IsInstanceValid(player) && (slot == 0 || coop);
            _panels[slot].Root.Visible = show;
            if (show)
            {
                UpdatePanel(_panels[slot], player!, coop, dt);
            }
        }
    }

    private static void UpdatePanel(PlayerPanel panel, Player player, bool coop, float dt)
    {
        var combat = player.Combat;
        var slot = player.Slot;
        panel.Hp.MaxValue = combat.MaxHp;
        panel.Hp.Value = Mathf.MoveToward((float)panel.Hp.Value, combat.Hp, combat.MaxHp * 2f * dt);
        panel.HpText.Text = combat.IsDowned ? "CAÍDO" : $"{combat.Hp}/{combat.MaxHp}";
        panel.Stamina.Value = combat.StaminaFraction * 100.0;
        panel.Stamina.Modulate = combat.StaminaFraction < 0.3 ? new Color(1f, 0.75f, 0.75f) : Colors.White;
        var target = slot == 0
            ? (RiftSession.InRift ? RiftSession.Fragments : RiftSession.WalletFragments)
            : Coop.PartyProgression.Fragments(slot, RiftSession.InRift);
        panel.ShownFragments = Mathf.MoveToward(panel.ShownFragments, target, Mathf.Max(8f, Mathf.Abs(target - panel.ShownFragments) * 6f) * dt);
        panel.Fragments.Text = $"{Mathf.RoundToInt(panel.ShownFragments)}";
        var rank = slot == 0 ? (Hunter.Progression.IsActive ? Hunter.Progression.Rank : Vespera.Core.Ranks.Rank.E) : Coop.PartyProgression.RankOf(slot);
        panel.Seal.Rank = rank;
        var lantern = slot == 0 && !RiftSession.InRift && RiftSession.LanternFragments > 0 ? $" · Lanterna na Brecha ({RiftSession.LanternFragments})" : string.Empty;
        var armed = Story.StoryService.Engine.HasFlag(Vespera.Core.Story.StoryFlags.StarterBlade) || RiftSession.InRift;
        panel.ClassName.Text = $"{(armed ? HunterSprites.DisplayName(player.Hunter) : "Sem arma")} · Rank {rank}" + lantern;
        panel.Tag.Visible = coop;
        panel.Root.Modulate = combat.IsDowned ? new Color(1f, 0.7f, 0.7f, 0.85f) : Colors.White;
    }

    private void UpdateBoss()
    {
        var boss = _world is not null && IsInstanceValid(_world) ? _world.Director?.Boss : null;
        var show = boss is not null && IsInstanceValid(boss) && boss.Engaged && !boss.Dead;
        _bossBox.Visible = show;
        if (!show)
        {
            return;
        }

        _bossBar.MaxValue = boss!.MaxHp;
        _bossBar.Value = Mathf.MoveToward((float)_bossBar.Value, (float)boss.TotalHp, (float)boss.MaxHp * 0.02f);
        _bossName.Text = boss.DisplayName;
        _cores.Set(RiftSession.Brecha.Boss.Cores, boss.CoresBroken);
    }

    private void UpdateBanners(float dt)
    {
        if (_toastTime <= 0f && _toasts.Count > 0)
        {
            _toast.Text = _toasts.Dequeue();
            _toastTime = ToastSeconds;
        }

        _toastTime -= dt;
        _toast.Modulate = new Color(1, 1, 1, Fade(_toastTime, ToastSeconds));
        _titleTime -= dt;
        var a = Fade(_titleTime, TitleSeconds);
        _title.Modulate = new Color(1, 1, 1, a);
        _subtitle.Modulate = new Color(1, 1, 1, a);
    }

    private static float Fade(float left, float total) => left <= 0f ? 0f : Mathf.Min(1f, Mathf.Min(left / 0.4f, (total - left) / 0.25f));

    /// <summary>Nós de um painel de jogador (vida, fôlego, selo, Fragmentos, classe e etiqueta).</summary>
    private sealed class PlayerPanel
    {
        public Control Root { get; init; } = null!;

        public RankSealIcon Seal { get; init; } = null!;

        public TextureProgressBar Hp { get; init; } = null!;

        public Label HpText { get; init; } = null!;

        public TextureProgressBar Stamina { get; init; } = null!;

        public Label Fragments { get; init; } = null!;

        public Label ClassName { get; init; } = null!;

        public Label Tag { get; init; } = null!;

        public float ShownFragments { get; set; }
    }

    /// <summary>
    /// Painel do jogador. J1: o leiaute de sempre, à esquerda. J2: o mesmo espelhado à direita (selo no canto, barras
    /// crescendo para dentro, texto alinhado à direita), para cada um achar o seu sem procurar.
    /// </summary>
    private PlayerPanel BuildPlayerPanel(int slot)
    {
        var right = slot > 0;
        float X(float left, float width) => right ? ScreenWidth - left - width : left;
        var root = new Control { Name = $"Panel{slot + 1}", MouseFilter = MouseFilterEnum.Ignore, Visible = slot == 0 };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        root.AddChild(MakeBackdrop(X(PanelBackdropLeft, PanelBackdropWidth)));
        var seal = new RankSealIcon { Position = new Vector2(X(28, 116), 24), Size = new Vector2(116, 116) };
        root.AddChild(seal);
        var hp = UiAssets.MakeBar(UiAssets.BarKind.Vida);
        hp.Position = new Vector2(X(150, 512), 30);
        hp.FillMode = right ? (int)TextureProgressBar.FillModeEnum.RightToLeft : (int)TextureProgressBar.FillModeEnum.LeftToRight;
        root.AddChild(hp);
        var hpText = HudLabel(32, Palette.Cream, BarTextMaxFactor);
        hpText.Position = new Vector2(X(150, 512), 30);
        hpText.Size = new Vector2(512, 48);
        hpText.HorizontalAlignment = HorizontalAlignment.Center;
        hpText.VerticalAlignment = VerticalAlignment.Center;
        root.AddChild(hpText);
        var stamina = UiAssets.MakeBar(UiAssets.BarKind.Folego);
        stamina.Position = new Vector2(X(150, 512 * 0.72f), 82);
        stamina.Scale = new Vector2(0.72f, 0.72f);
        stamina.FillMode = hp.FillMode;
        root.AddChild(stamina);
        root.AddChild(new FragmentIcon { Position = new Vector2(X(160, 40), 124), Size = new Vector2(40, 50) });
        var fragments = HudLabel(52, Palette.Cream, BarTextMaxFactor);
        fragments.Position = new Vector2(right ? X(208, 90) : 208, 116);
        fragments.Size = new Vector2(90, 60);
        fragments.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        root.AddChild(fragments);
        var className = HudLabel(32, Palette.Cream);
        className.Position = new Vector2(right ? X(300, 760) : 300, 130);
        className.Size = new Vector2(760, 40);
        className.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        root.AddChild(className);
        var tag = HudLabel(34, Coop.CoopSession.ColorOf(slot));
        tag.Text = Coop.CoopSession.TagOf(slot);
        tag.Position = new Vector2(X(28, 116), 142);
        tag.Size = new Vector2(116, 40);
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        tag.Visible = false;
        root.AddChild(tag);
        return new PlayerPanel
        {
            Root = root,
            Seal = seal,
            Hp = hp,
            HpText = hpText,
            Stamina = stamina,
            Fragments = fragments,
            ClassName = className,
            Tag = tag,
        };
    }

    private void BuildBossBar()
    {
        _bossBox = new Control { Position = new Vector2((1920 - BossBarWidth) / 2f, 1080 - 150), Size = new Vector2(BossBarWidth, 110), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_bossBox);
        _bossName = new Label { HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(BossBarWidth, 56) };
        _bossName.AddThemeFontOverride("font", GD.Load<Font>("res://assets/v2/fonts/PixelifySans.ttf"));
        HudText.Bind(_bossName, 46, BarTextMaxFactor);
        _bossName.AddThemeColorOverride("font_color", Palette.Cream);
        _bossName.AddThemeColorOverride("font_outline_color", Palette.Indigo);
        _bossName.AddThemeConstantOverride("outline_size", 10);
        _bossBox.AddChild(_bossName);
        _bossBar = UiAssets.MakeBar(UiAssets.BarKind.Vida);
        _bossBar.NinePatchStretch = true;
        _bossBar.StretchMarginLeft = 24;
        _bossBar.StretchMarginRight = 24;
        _bossBar.StretchMarginTop = 14;
        _bossBar.StretchMarginBottom = 14;
        _bossBar.Position = new Vector2(0, 58);
        _bossBar.Size = new Vector2(BossBarWidth, 48);
        _bossBar.TintProgress = new Color(1f, 0.55f, 0.75f);
        _bossBox.AddChild(_bossBar);
        _cores = new CoreDots { Position = new Vector2(BossBarWidth + 14, 62), Size = new Vector2(110, 40) };
        _bossBox.AddChild(_cores);
    }

    private void BuildBanners()
    {
        _toast = HudLabel(36, Palette.Cream);
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.Position = new Vector2(0, ToastY);
        _toast.Size = new Vector2(1920, 50);
        AddChild(_toast);
        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(0, TitleY), Size = new Vector2(1920, 80) };
        _title.AddThemeFontOverride("font", GD.Load<Font>("res://assets/v2/fonts/PixelifySans.ttf"));
        HudText.Bind(_title, 60);
        _title.AddThemeColorOverride("font_color", Palette.Cream);
        _title.AddThemeColorOverride("font_outline_color", Palette.Indigo);
        _title.AddThemeConstantOverride("outline_size", 14);
        _title.Modulate = Colors.Transparent;
        AddChild(_title);
        _subtitle = HudLabel(38, Palette.Amber);
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.Position = new Vector2(0, TitleY + 76);
        _subtitle.Size = new Vector2(1920, 50);
        _subtitle.Modulate = Colors.Transparent;
        AddChild(_subtitle);
    }

    /// <summary>Fundo translúcido atrás do painel do caçador: texto creme sobre cenário claro não pode se perder (#82).</summary>
    private static Panel MakeBackdrop(float x)
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.04f, 0.1f, 0.58f), AntiAliasing = true };
        style.SetCornerRadiusAll(12);
        var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(x, PanelBackdropTop), Size = new Vector2(PanelBackdropWidth, PanelBackdropHeight) };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label HudLabel(int size, Color color, float maxFactor = float.MaxValue)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        HudText.Bind(label, size, maxFactor);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.05f, 0.12f, 0.95f));
        label.AddThemeConstantOverride("outline_size", 12);
        return label;
    }
}
