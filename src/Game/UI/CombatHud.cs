using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.UI;

/// <summary>
/// HUD de caçada (camada 4): selo de rank E, vida e fôlego (barras pintadas da interface), Fragmentos da caçada,
/// barra do Guardião com nome e núcleos, título da sala e avisos curtos. Lê o estado a cada quadro.
/// </summary>
public partial class CombatHud : Control
{
    private const float ToastSeconds = 2.8f;
    private const float TitleSeconds = 3.2f;
    private const float BossBarWidth = 960f;

    private readonly Queue<string> _toasts = new();
    private LevelWorld? _world;
    private TextureProgressBar _hp = null!;
    private TextureProgressBar _stamina = null!;
    private Label _hpText = null!;
    private Label _fragments = null!;
    private Label _className = null!;
    private Control _bossBox = null!;
    private TextureProgressBar _bossBar = null!;
    private Label _bossName = null!;
    private CoreDots _cores = null!;
    private Label _toast = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private float _toastTime;
    private float _titleTime;
    private float _shownFragments;

    public string LastToast { get; private set; } = string.Empty;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildPlayerPanel();
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

        var combat = _world.Player.Combat;
        _hp.MaxValue = combat.MaxHp;
        _hp.Value = Mathf.MoveToward((float)_hp.Value, combat.Hp, combat.MaxHp * 2f * dt);
        _hpText.Text = $"{combat.Hp}/{combat.MaxHp}";
        _stamina.Value = combat.StaminaFraction * 100.0;
        _stamina.Modulate = combat.StaminaFraction < 0.3 ? new Color(1f, 0.75f, 0.75f) : Colors.White;
        var target = RiftSession.InRift ? RiftSession.Fragments : RiftSession.Save.MaterialCount(RiftSession.Rules.FragmentMaterial);
        _shownFragments = Mathf.MoveToward(_shownFragments, target, Mathf.Max(8f, Mathf.Abs(target - _shownFragments) * 6f) * dt);
        _fragments.Text = $"{Mathf.RoundToInt(_shownFragments)}";
        _className.Text = $"{HunterSprites.DisplayName(_world.Player.Hunter)} · Rank E" + (RiftSession.Save.LostLantern is not null ? " · sem Lanterna" : string.Empty);
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

    private void BuildPlayerPanel()
    {
        AddChild(new RankSealIcon { Position = new Vector2(28, 24), Size = new Vector2(116, 116) });
        _hp = UiAssets.MakeBar(UiAssets.BarKind.Vida);
        _hp.Position = new Vector2(150, 30);
        AddChild(_hp);
        _hpText = HudLabel(32, Palette.Cream);
        _hpText.Position = new Vector2(150, 30);
        _hpText.Size = new Vector2(512, 48);
        _hpText.HorizontalAlignment = HorizontalAlignment.Center;
        _hpText.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_hpText);
        _stamina = UiAssets.MakeBar(UiAssets.BarKind.Folego);
        _stamina.Position = new Vector2(150, 82);
        _stamina.Scale = new Vector2(0.72f, 0.72f);
        AddChild(_stamina);
        AddChild(new FragmentIcon { Position = new Vector2(160, 124), Size = new Vector2(40, 50) });
        _fragments = HudLabel(52, Palette.Cream);
        _fragments.Position = new Vector2(208, 116);
        AddChild(_fragments);
        _className = HudLabel(32, Palette.CreamDim);
        _className.Position = new Vector2(300, 130);
        AddChild(_className);
    }

    private void BuildBossBar()
    {
        _bossBox = new Control { Position = new Vector2((1920 - BossBarWidth) / 2f, 30), Size = new Vector2(BossBarWidth, 110), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_bossBox);
        _bossName = new Label { HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(BossBarWidth, 56) };
        _bossName.AddThemeFontOverride("font", GD.Load<Font>("res://assets/v2/fonts/PixelifySans.ttf"));
        _bossName.AddThemeFontSizeOverride("font_size", 46);
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
        _toast.Position = new Vector2(0, 170);
        _toast.Size = new Vector2(1920, 50);
        AddChild(_toast);
        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(0, 360), Size = new Vector2(1920, 90) };
        _title.AddThemeFontOverride("font", GD.Load<Font>("res://assets/v2/fonts/PixelifySans.ttf"));
        _title.AddThemeFontSizeOverride("font_size", 72);
        _title.AddThemeColorOverride("font_color", Palette.Cream);
        _title.AddThemeColorOverride("font_outline_color", Palette.Indigo);
        _title.AddThemeConstantOverride("outline_size", 14);
        _title.Modulate = Colors.Transparent;
        AddChild(_title);
        _subtitle = HudLabel(38, Palette.Amber);
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.Position = new Vector2(0, 456);
        _subtitle.Size = new Vector2(1920, 50);
        _subtitle.Modulate = Colors.Transparent;
        AddChild(_subtitle);
    }

    private static Label HudLabel(int size, Color color)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.05f, 0.12f, 0.95f));
        label.AddThemeConstantOverride("outline_size", 8);
        return label;
    }
}
