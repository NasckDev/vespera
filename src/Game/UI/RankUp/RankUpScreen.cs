using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>
/// Celebração de rank: véu escuro, selo que cresce com raios girando e chuva de fagulhas na cor do rank, o nome do novo rank,
/// o estágio visual (se mudou) e o que o rank destrava. Abre sozinha quando <see cref="Progression.RankChanged"/> dispara.
/// </summary>
public partial class RankUpScreen : HunterScreen
{
    private const int SealSize = 320;
    private const float RevealSeconds = 0.75f;

    private static RankUpScreen? _active;

    private readonly RankChange _change;
    private RankBurst _burst = null!;
    private RankBadge _seal = null!;
    private Control _texts = null!;
    private Button _continue = null!;
    private float _pulse;

    public RankUpScreen(RankChange change)
    {
        _change = change;
    }

    /// <summary>Mudança celebrada por esta tela (para testes).</summary>
    public RankChange Change => _change;

    /// <summary>Texto principal mostrado (ex.: "Rank D").</summary>
    public string HeadlineText { get; private set; } = string.Empty;

    /// <summary>Linha do estágio visual; vazia se o estágio não mudou.</summary>
    public string StageText { get; private set; } = string.Empty;

    public bool Revealed { get; private set; }

    /// <summary>Tela de celebração em exibição agora, se houver.</summary>
    public static RankUpScreen? Active => _active is not null && IsInstanceValid(_active) ? _active : null;

    public static void Show(RankChange change)
    {
        if (!Progression.IsActive || SceneRouter.Instance is null || Active is not null)
        {
            return;
        }

        var screen = new RankUpScreen(change);
        _active = screen;
        Open(screen);
    }

    protected override Control? DefaultFocus() => _continue;

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_active == this)
        {
            _active = null;
        }
    }

    protected override void Build()
    {
        AddChild(UiKit.MakeBackdrop(showArt: false));
        var veil = new ColorRect { Color = new Color(0.03f, 0.03f, 0.1f, 0.82f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = UiKit.MakeColumn(18);
        column.Alignment = BoxContainer.AlignmentMode.Center;
        center.AddChild(column);

        var area = new Control { CustomMinimumSize = new Vector2(SealSize + 300, SealSize + 150), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _burst = new RankBurst(RankColors.Of(_change.To));
        _burst.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        area.AddChild(_burst);
        _seal = new RankBadge(_change.To, SealSize)
        {
            Position = new Vector2((area.CustomMinimumSize.X - SealSize) / 2f, 75),
            PivotOffset = new Vector2(SealSize / 2f, SealSize / 2f),
            Scale = Vector2.Zero,
        };
        area.AddChild(_seal);
        column.AddChild(area);

        _texts = BuildTexts();
        _texts.Modulate = new Color(1, 1, 1, 0);
        column.AddChild(_texts);

        _continue = UiKit.MakeButton("Continuar", Close, 420);
        _continue.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _continue.Modulate = new Color(1, 1, 1, 0);
        column.AddChild(_continue);

        PlayReveal();
    }

    public override void _Process(double delta)
    {
        _pulse += (float)delta;
        _seal.Glow = 0.85f + (0.15f * Mathf.Sin(_pulse * 3f));
    }

    private Control BuildTexts()
    {
        var engine = Progression.Engine;
        var box = UiKit.MakeColumn(4);
        var color = RankColors.Of(_change.To);
        var step = engine.Catalog.Step(_change.To);

        box.AddChild(UiKit.MakeLabel("Novo rank", UiTheme.DimVariation, HorizontalAlignment.Center));
        HeadlineText = $"Rank {_change.To}";
        var headline = UiKit.MakeLabel(HeadlineText, UiTheme.TitleVariation, HorizontalAlignment.Center);
        headline.AddThemeColorOverride("font_color", color.Lerp(Palette.Cream, 0.35f));
        box.AddChild(headline);
        var title = UiKit.MakeLabel(step.Title, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        title.AddThemeColorOverride("font_color", color);
        box.AddChild(title);

        if (_change.StageChanged)
        {
            StageText = $"Novo estágio: {engine.Ladder.StageName(_change.ToStage)}. Seu visual mudou.";
            var stage = UiKit.MakeLabel(StageText, UiTheme.HeadingVariation, HorizontalAlignment.Center);
            box.AddChild(stage);
        }

        var gain = engine.Ladder.MaxHpBonus(_change.To) - engine.Ladder.MaxHpBonus(_change.From);
        box.AddChild(UiKit.MakeLabel($"+{gain} de vida máxima · armas até o nível {engine.Forge.LevelCap(_change.To)}", UiTheme.DimVariation, HorizontalAlignment.Center));
        return box;
    }

    private void PlayReveal()
    {
        HunterSfx.Play(HunterSound.RankUp);
        var tween = CreateTween();
        tween.TweenProperty(_seal, "scale", Vector2.One, RevealSeconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenMethod(Callable.From<float>(v => _burst.Intensity = v), 0f, 1f, RevealSeconds);
        tween.TweenCallback(Callable.From(() => _burst.Sparks()));
        tween.TweenProperty(_texts, "modulate:a", 1f, 0.4);
        tween.Parallel().TweenProperty(_continue, "modulate:a", 1f, 0.4).SetDelay(0.2);
        tween.TweenCallback(Callable.From(() => Revealed = true));
        _seal.Glow = 1f;
    }
}
