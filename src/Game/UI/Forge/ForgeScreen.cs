using System.Globalization;
using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>
/// Forja da Odete: mostra a arma da classe, os atributos de agora e do próximo nível, o custo em Fragmentos e materiais (em vermelho
/// o que falta) e forja ao confirmar, com som, brilho âmbar e fagulhas. Abra com <see cref="Open()"/>.
/// </summary>
public partial class ForgeScreen : HunterScreen
{
    private const int SealArea = 400;
    private const int SealSize = 224;
    private const int LeftWidth = 640;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Color Good = new("7CC47F");
    private static readonly Color Bad = new("E4737A");

    private ForgeGlow _glow = null!;
    private ItemIconSlot? _seal;
    private Label _weaponName = null!;
    private Label _weaponLine = null!;
    private Label _tierLine = null!;
    private Label _flash = null!;
    private Label _heading = null!;
    private GridContainer _stats = null!;
    private GridContainer _cost = null!;
    private Label _reason = null!;
    private Label _capLine = null!;
    private Button _forge = null!;
    private Tween? _tween;
    private Control _sealHolder = null!;

    /// <summary>Abre a forja sobre o jogo (a história chama isto ao falar com a Odete).</summary>
    public static void Open() => Open(new ForgeScreen());

    /// <summary>Resultado da última tentativa, para testes.</summary>
    public ForgeCheck? LastCheck { get; private set; }

    /// <summary>Texto atual do motivo (vazio quando dá para forjar).</summary>
    public string ReasonText => _reason.Text;

    public string HeadingText => _heading.Text;

    public Button ForgeButton => _forge;

    protected override Control? DefaultFocus() => _forge;

    /// <summary>Aciona o botão Forjar (o mesmo que confirmar).</summary>
    public void Forge() => TryForge();

    protected override void Build()
    {
        var panel = BuildPage("Forja", out var column, sideMargin: 130);
        var content = UiKit.MakeRow(40);
        panel.AddChild(content);
        content.AddChild(BuildWeaponPane());
        content.AddChild(BuildUpgradePane());

        var footer = MakeFooter();
        _forge = UiKit.MakeButton("Forjar", TryForge, 380);
        footer.AddChild(_forge);
        footer.AddChild(UiKit.MakeButton("Voltar", Close, 300));
        column.AddChild(footer);
        Refresh();
    }

    protected override void OnProgressionChanged()
    {
        if (IsInsideTree())
        {
            Refresh();
        }
    }

    private Control BuildWeaponPane()
    {
        var box = UiKit.MakeColumn(6);
        box.CustomMinimumSize = new Vector2(LeftWidth, 0);
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var area = new Control { CustomMinimumSize = new Vector2(SealArea, SealArea), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _glow = new ForgeGlow();
        _glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        area.AddChild(_glow);
        _sealHolder = area;
        _flash = UiKit.MakeLabel(string.Empty, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        _flash.AddThemeConstantOverride("outline_size", 10);
        _flash.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.06f, 0.18f, 0.95f));
        _flash.Modulate = new Color(1, 1, 1, 0);
        _flash.Position = new Vector2(0, 14);
        _flash.Size = new Vector2(SealArea, 60);
        area.AddChild(_flash);
        box.AddChild(area);

        _weaponName = UiKit.MakeLabel(string.Empty, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        _weaponLine = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        _tierLine = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        box.AddChild(_weaponName);
        box.AddChild(_weaponLine);
        box.AddChild(_tierLine);
        return box;
    }

    private Control BuildUpgradePane()
    {
        var box = UiKit.MakeColumn(12);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _heading = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation);
        box.AddChild(_heading);
        _stats = new GridContainer { Columns = 4 };
        _stats.AddThemeConstantOverride("h_separation", 26);
        _stats.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(_stats);
        box.AddChild(new HSeparator());
        box.AddChild(UiKit.MakeLabel("Custo", UiTheme.HeadingVariation));
        _cost = new GridContainer { Columns = 4 };
        _cost.AddThemeConstantOverride("h_separation", 30);
        _cost.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(_cost);
        _reason = UiKit.MakeWrapped(string.Empty);
        _reason.AddThemeColorOverride("font_color", Bad);
        box.AddChild(_reason);
        _capLine = UiKit.MakeWrapped(string.Empty, UiTheme.DimVariation);
        box.AddChild(_capLine);
        return box;
    }

    private void Refresh()
    {
        var engine = Progression.Engine;
        var check = engine.CanUpgrade();
        LastCheck = check;
        var current = check.Current;

        SetSeal(engine, current);
        _weaponName.Text = current.DisplayName;
        _weaponName.AddThemeColorOverride("font_color", RankColors.Of(current.TierRank));
        _weaponLine.Text = $"{engine.Catalog.Weapons[current.ClassId].Name} · Nível {current.Level} de {engine.Forge.MaxLevel}";
        _tierLine.Text = $"Rank {current.TierRank} · {engine.Catalog.Step(current.TierRank).Rarity}";

        Clear(_stats);
        Clear(_cost);
        if (check.Next is null)
        {
            _heading.Text = "Nível máximo";
            _capLine.Text = string.Empty;
            _reason.Text = check.Reason;
            _reason.AddThemeColorOverride("font_color", Palette.CreamDim);
            SetForgeEnabled(false);
            return;
        }

        _heading.Text = $"Aprimorar para o nível {check.Next.Level}";
        var cap = engine.Forge.LevelCap(engine.Rank);
        _capLine.Text = $"Seu rank ({engine.Rank}) libera armas até o nível {cap}. Subir de rank libera mais dois níveis.";
        AddStatRow("Dano", current.Damage, check.Next.Damage, "0");
        AddStatRow("Crítico", current.CritPct, check.Next.CritPct, "0.#", "%");
        if (check.Next.TierRank != current.TierRank)
        {
            AddTextRow("Novo patamar", current.DisplayName, check.Next.DisplayName, RankColors.Of(check.Next.TierRank));
        }

        BuildCost(check, engine);
        _reason.AddThemeColorOverride("font_color", Bad);
        _reason.Text = ReasonFor(check, engine);
        SetForgeEnabled(check.CanUpgrade);
    }

    private void SetSeal(ProgressionEngine engine, WeaponStats current)
    {
        _seal?.QueueFree();
        _seal = new ItemIconSlot(SealSize, ItemIcons.Gear(engine.ClassId, current.TierRank), current.TierRank)
        {
            Position = new Vector2((SealArea - SealSize) / 2f, (SealArea - SealSize) / 2f),
            PivotOffset = new Vector2(SealSize / 2f, SealSize / 2f),
        };
        _sealHolder.AddChild(_seal);
        _sealHolder.MoveChild(_flash, -1);
    }

    private static string ReasonFor(ForgeCheck check, ProgressionEngine engine)
    {
        if (check.CanUpgrade)
        {
            return string.Empty;
        }

        if (check.Block != ForgeBlock.RankTooLow)
        {
            return check.Reason;
        }

        var pending = engine.RankStatus.Missing.Select(r => r.Text);
        return check.Reason + "\n" + string.Join("\n", pending);
    }

    private void BuildCost(ForgeCheck check, ProgressionEngine engine)
    {
        var cost = check.Cost!;
        AddCostRow("Fragmentos", Palette.Amber, engine.Progress.Fragmentos, cost.Fragmentos, ItemIcons.Fragmentos);
        foreach (var (id, need) in cost.Materials)
        {
            var material = engine.Catalog.Materials[id];
            var name = material.Kind == MaterialKind.Guardian ? $"{material.Name} (Guardião)" : material.Name;
            AddCostRow(name, RankColors.Of(material.Rank), engine.Progress.MaterialCount(id), need, ItemIcons.Material(material));
        }
    }

    private void AddCostRow(string name, Color color, int have, int need, Texture2D? icon)
    {
        _cost.AddChild(new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        var label = UiKit.MakeLabel(name);
        label.AddThemeColorOverride("font_color", color);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var amount = UiKit.MakeLabel($"{have} / {need}", UiTheme.HeadingVariation, HorizontalAlignment.Right);
        amount.AddThemeColorOverride("font_color", have >= need ? Good : Bad);
        _cost.AddChild(label);
        _cost.AddChild(amount);
        _cost.AddChild(new CheckMark(have >= need, 30));
    }

    private void AddStatRow(string name, double now, double next, string format, string suffix = "")
    {
        _stats.AddChild(UiKit.MakeLabel(name));
        _stats.AddChild(UiKit.MakeLabel(now.ToString(format, Pt) + suffix, null, HorizontalAlignment.Right));
        _stats.AddChild(UiKit.MakeLabel("→", UiTheme.DimVariation, HorizontalAlignment.Center));
        var delta = next - now;
        var value = UiKit.MakeLabel($"{next.ToString(format, Pt)}{suffix}  ({(delta >= 0 ? "+" : string.Empty)}{delta.ToString(format, Pt)})", UiTheme.HeadingVariation);
        value.AddThemeColorOverride("font_color", Good);
        _stats.AddChild(value);
    }

    private void AddTextRow(string name, string now, string next, Color nextColor)
    {
        _stats.AddChild(UiKit.MakeLabel(name));
        _stats.AddChild(UiKit.MakeLabel(now, UiTheme.DimVariation, HorizontalAlignment.Right));
        _stats.AddChild(UiKit.MakeLabel("→", UiTheme.DimVariation, HorizontalAlignment.Center));
        var value = UiKit.MakeLabel(next);
        value.AddThemeColorOverride("font_color", nextColor);
        _stats.AddChild(value);
    }

    private void SetForgeEnabled(bool enabled)
    {
        _forge.Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.55f);
    }

    private void TryForge()
    {
        var before = LastCheck;
        var result = Progression.Forge.Upgrade();
        if (!result.Done)
        {
            UiSfx.Play(UiSound.Deny);
            Shake(_forge);
            return;
        }

        HunterSfx.Play(HunterSound.Forge);
        PlayFeedback(result.Check.Next!.Level, before?.Next?.TierRank != before?.Current.TierRank);
    }

    private void PlayFeedback(int newLevel, bool newTier)
    {
        _tween?.Kill();
        _glow.Burst();
        _flash.Text = newTier ? "Novo patamar!" : $"Nível {newLevel}";
        var pulse = CreateTween().SetParallel(false);
        _tween = pulse;
        pulse.TweenMethod(Callable.From<float>(v => _glow.Intensity = v), 0f, 1f, 0.14);
        pulse.Parallel().TweenProperty(_seal, "scale", new Vector2(1.18f, 1.18f), 0.14).From(Vector2.One);
        pulse.Parallel().TweenProperty(_flash, "modulate:a", 1f, 0.14);
        pulse.TweenMethod(Callable.From<float>(v => _glow.Intensity = v), 1f, 0f, 0.9).SetEase(Tween.EaseType.Out);
        pulse.Parallel().TweenProperty(_seal, "scale", Vector2.One, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        pulse.Parallel().TweenProperty(_flash, "modulate:a", 0f, 0.9).SetDelay(0.3);
    }

    private void Shake(Control target)
    {
        var origin = target.Position;
        var tween = CreateTween();
        for (var i = 0; i < 3; i++)
        {
            tween.TweenProperty(target, "position:x", origin.X + 10, 0.04);
            tween.TweenProperty(target, "position:x", origin.X - 10, 0.04);
        }

        tween.TweenProperty(target, "position:x", origin.X, 0.04);
    }

    private static void Clear(Container container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
