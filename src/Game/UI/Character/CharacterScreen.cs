using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>
/// Ficha do caçador: selo do rank com estágio visual, barra de XP, o que falta para o próximo rank (XP, Guardião, exame),
/// atributos do equipamento, conjuntos, Lanterna e as conquistas já feitas. Só leitura; os botões levam ao inventário.
/// </summary>
public partial class CharacterScreen : HunterScreen
{
    private const int IdentityWidth = 760;
    private const int BarWidth = 560;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    private RankBadge _seal = null!;
    private Control? _first;
    private Button _inventoryButton = null!;
    private float _sealPulse;

    public static void Open() => Open(new CharacterScreen());

    /// <summary>Texto simples das exigências mostradas (para testes e leitores de tela).</summary>
    public IReadOnlyList<string> RequirementTexts { get; private set; } = [];

    protected override Control? DefaultFocus() => _first ?? _inventoryButton;

    public override void _Process(double delta)
    {
        _sealPulse += (float)delta;
        _seal.Glow = 0.55f + (0.35f * Mathf.Sin(_sealPulse * 2.2f));
    }

    protected override void Build()
    {
        var panel = BuildPage("Caçador", out var column, sideMargin: 40);
        var content = UiKit.MakeRow(30);
        panel.AddChild(content);
        content.AddChild(BuildIdentity());
        content.AddChild(BuildDetails());

        var footer = MakeFooter();
        _inventoryButton = UiKit.MakeButton("Inventário", () => InventoryScreen.Open(), 380);
        footer.AddChild(_inventoryButton);
        footer.AddChild(UiKit.MakeButton("Voltar", Close, 300));
        column.AddChild(footer);
    }

    private Control BuildIdentity()
    {
        var engine = Progression.Engine;
        var status = engine.RankStatus;
        var step = engine.RankStep;

        var box = UiKit.MakeColumn(10);
        box.CustomMinimumSize = new Vector2(IdentityWidth, 0);
        box.Alignment = BoxContainer.AlignmentMode.Begin;

        var name = GameSession.Data?.Name ?? string.Empty;
        box.AddChild(UiKit.MakeLabel(name, UiTheme.ReadingVariation, HorizontalAlignment.Center));
        var className = engine.Catalog.Weapons[engine.ClassId].Name;
        box.AddChild(UiKit.MakeLabel($"{className} · Dia {GameSession.Data?.Day ?? 1}", UiTheme.DimVariation, HorizontalAlignment.Center));

        _seal = new RankBadge(status.Current, 250) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        box.AddChild(_seal);
        var rankLabel = UiKit.MakeLabel($"Rank {status.Current}: {step.Title}", UiTheme.HeadingVariation, HorizontalAlignment.Center);
        rankLabel.AddThemeColorOverride("font_color", RankColors.Of(status.Current));
        box.AddChild(rankLabel);
        box.AddChild(UiKit.MakeLabel($"Estágio {engine.Stage + 1} de 5: {engine.StageName}", UiTheme.DimVariation, HorizontalAlignment.Center));

        box.AddChild(BuildXpBar(status));
        box.AddChild(BuildRequirements(status));
        return box;
    }

    private static Control BuildXpBar(RankStatus status)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(BarWidth, 60), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var bar = UiAssets.MakeBar(UiAssets.BarKind.Xp);
        bar.Value = status.Progress * 100f;
        bar.Position = new Vector2(0, 6);
        holder.AddChild(bar);
        var text = status.AtTop ? $"{status.Xp} XP · rank máximo" : $"{status.Xp} / {status.Xp - status.XpIntoRank + status.XpSpan} XP";
        var label = UiKit.MakeLabel(text, null, HorizontalAlignment.Center);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.06f, 0.18f));
        label.AddThemeConstantOverride("outline_size", 8);
        holder.AddChild(label);
        return holder;
    }

    private Control BuildRequirements(RankStatus status)
    {
        var box = UiKit.MakeColumn(8);
        box.AddChild(new HSeparator());
        if (status.AtTop)
        {
            box.AddChild(UiKit.MakeWrapped("Você alcançou o Alvor. A manhã voltou para Lumeira.", UiTheme.DimVariation));
            RequirementTexts = [];
            return box;
        }

        box.AddChild(UiKit.MakeLabel($"Para o rank {status.Next}", UiTheme.HeadingVariation));
        foreach (var requirement in status.Requirements)
        {
            var line = UiKit.MakeRow(14);
            line.AddChild(new CheckMark(requirement.Done));
            var text = UiKit.MakeWrapped(requirement.Text, requirement.Done ? UiTheme.DimVariation : null);
            line.AddChild(text);
            box.AddChild(line);
        }

        RequirementTexts = [.. status.Requirements.Select(r => r.Text)];
        return box;
    }

    private Control BuildDetails()
    {
        var engine = Progression.Engine;
        var scroll = UiKit.MakeScroll();
        var box = UiKit.MakeColumn(10);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(box));

        AddAttributes(box, engine);
        AddWeapon(box, engine);
        AddSets(box, engine);
        AddLantern(box, engine);
        AddAchievements(box, engine);
        return scroll;
    }

    private static void AddAttributes(VBoxContainer box, ProgressionEngine engine)
    {
        var gear = engine.Equipment().Total;
        AddHeading(box, "Atributos", first: true);
        AddStat(box, "Vida máxima extra", $"+{engine.MaxHpBonus}", $"rank +{engine.Ladder.MaxHpBonus(engine.Rank)} · peças +{gear.Hp}");
        AddStat(box, "Defesa", gear.Defense.ToString(Pt), string.Empty);
        if (gear.CritPct != 0)
        {
            AddStat(box, "Crítico das peças", $"+{gear.CritPct.ToString("0.#", Pt)}%", string.Empty);
        }

        if (gear.SpeedPct != 0)
        {
            AddStat(box, "Velocidade das peças", $"+{gear.SpeedPct.ToString("0.#", Pt)}%", string.Empty);
        }

        if (gear.FragmentosPct != 0)
        {
            AddStat(box, "Fragmentos encontrados", $"+{gear.FragmentosPct.ToString("0.#", Pt)}%", string.Empty);
        }
    }

    private static void AddWeapon(VBoxContainer box, ProgressionEngine engine)
    {
        var weapon = engine.WeaponStats;
        AddHeading(box, "Arma");
        var row = UiKit.MakeRow(18);
        row.AddChild(new ItemIconSlot(104, ItemIcons.Gear(engine.ClassId, weapon.TierRank), weapon.TierRank));
        var text = UiKit.MakeColumn(2);
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var name = UiKit.MakeLabel(weapon.DisplayName);
        name.AddThemeColorOverride("font_color", RankColors.Of(weapon.TierRank));
        text.AddChild(name);
        text.AddChild(UiKit.MakeLabel($"Nível {weapon.Level} de {engine.Forge.MaxLevel} · dano {weapon.Damage}", UiTheme.DimVariation));
        text.AddChild(UiKit.MakeLabel($"crítico {weapon.CritPct.ToString("0.#", Pt)}% · {weapon.AttacksPerSecond.ToString("0.##", Pt)} golpes/s", UiTheme.DimVariation));
        row.AddChild(text);
        box.AddChild(row);
    }

    private static void AddSets(VBoxContainer box, ProgressionEngine engine)
    {
        AddHeading(box, "Conjuntos de Guardião");
        foreach (var set in engine.Catalog.Sets.Values)
        {
            var status = engine.SetOverview(set.Id);
            var owned = engine.Progress.Items.Count(i => i.SetId == set.Id);
            box.AddChild(UiKit.MakeLabel($"{set.Name}: {status.Pieces} vestidas de {set.Pieces.Count} ({owned} possuídas)"));
            foreach (var bonus in status.Bonuses)
            {
                var line = UiKit.MakeWrapped($"({bonus.Bonus.Pieces}) {bonus.Bonus.Name}: {bonus.Bonus.Description}", bonus.Active ? null : UiTheme.DimVariation);
                if (bonus.Active)
                {
                    line.AddThemeColorOverride("font_color", new Color("7CC47F"));
                }

                box.AddChild(line);
            }
        }
    }

    private static void AddLantern(VBoxContainer box, ProgressionEngine engine)
    {
        if (engine.TotalLostFragmentos == 0 && !engine.InRun)
        {
            return;
        }

        AddHeading(box, "Lanterna");
        foreach (var (neighborhood, amount) in engine.Progress.LostFragmentos)
        {
            box.AddChild(UiKit.MakeWrapped($"{amount} Fragmentos ficaram na Brecha de {neighborhood}. Volte a ela para recuperar.", UiTheme.DimVariation));
        }

        if (engine.InRun)
        {
            box.AddChild(UiKit.MakeLabel($"Nesta caçada: {engine.RunFragmentos} Fragmentos", UiTheme.DimVariation));
        }
    }

    private void AddAchievements(VBoxContainer box, ProgressionEngine engine)
    {
        AddHeading(box, "Conquistas");
        foreach (var step in engine.Catalog.Ranks.Where(s => s.Guardian is not null))
        {
            var guardian = engine.Catalog.Guardians[step.Guardian!];
            var done = engine.Progress.GuardiansDefeated.Contains(guardian.Id);
            var row = new SlotRow($"Guardião · Rank {step.Id}", guardian.Name, done ? RankColors.Of(step.Id) : Palette.CreamDim, done ? "Derrotado" : "Pendente", new RankBadge(step.Id, 64), dimmed: !done);
            _first ??= row;
            box.AddChild(row);
            if (step.Exam && engine.Catalog.ExamFor(step.Id) is { } exam)
            {
                var passed = engine.Progress.ExamsPassed.Contains(exam.Id);
                var examRow = new SlotRow($"Exame da Ordem · Rank {step.Id}", exam.Name, passed ? RankColors.Of(step.Id) : Palette.CreamDim, passed ? "Aprovado" : "Pendente", new RankBadge(step.Id, 64), dimmed: !passed);
                box.AddChild(examRow);
            }
        }
    }

    private static void AddStat(VBoxContainer box, string caption, string value, string note)
    {
        var row = UiKit.MakeRow(16);
        var name = UiKit.MakeLabel(caption);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);
        row.AddChild(UiKit.MakeLabel(value, UiTheme.HeadingVariation, HorizontalAlignment.Right));
        box.AddChild(row);
        if (note.Length > 0)
        {
            box.AddChild(UiKit.MakeLabel(note, UiTheme.DimVariation));
        }
    }

    private static void AddHeading(VBoxContainer box, string text, bool first = false)
    {
        if (!first)
        {
            box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        }

        box.AddChild(UiKit.MakeLabel(text, UiTheme.HeadingVariation));
    }
}
