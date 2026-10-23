using System;
using System.Linq;
using Godot;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.UI;

namespace Vespera.Game.Hunt;

/// <summary>
/// Tela de resultado ao voltar da Brecha (playtest 23/10: "dá vontade de melhorar"): como a caçada foi (tempo, salas,
/// monstros, dano levado), o espólio com ícone e cor do rank, os contratos do mural (cumpridos ou não), a barra de XP
/// até o próximo rank e o próximo passo da história. Teclado, controle e mouse; fecha com Continuar/Esc/botão direito.
/// </summary>
public partial class RunResultScreen(RunSummary summary, bool brechaClosed) : HunterScreen
{
    private const int IconSize = 64;
    private static readonly Color Good = new("8fe39a");
    private static readonly Color Bad = new("ff8a7a");

    private Button _continue = null!;

    /// <summary>Tela aberta agora (testes).</summary>
    public static RunResultScreen? Active { get; private set; }

    public RunSummary Summary => summary;

    public string Headline { get; private set; } = string.Empty;

    public static void Show(RunSummary summary, bool brechaClosed) => Open(new RunResultScreen(summary, brechaClosed));

    protected override Control? DefaultFocus() => _continue;

    public override void _EnterTree() => Active = this;

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Active == this)
        {
            Active = null;
        }
    }

    protected override void Build()
    {
        var report = summary.Report;
        Headline = !report.Survived ? "Você caiu" : brechaClosed ? "Brecha fechada!" : "Caçada concluída!";
        var panel = BuildPage(Headline, out var column, sideMargin: 180);
        var body = UiKit.MakeRow(60);
        panel.AddChild(UiKit.PadForGlow(body));
        body.AddChild(BuildStats(report));
        body.AddChild(BuildLoot(report));
        body.AddChild(BuildContracts());
        column.AddChild(BuildXp());
        if (StoryService.Engine.CurrentObjective is { Locked: false } objective)
        {
            column.AddChild(UiKit.MakeLabel($"Próximo passo: {objective.Title}", UiTheme.HeadingVariation, HorizontalAlignment.Center));
        }

        _continue = UiKit.MakeButton("Voltar à Praça", Close, 520);
        _continue.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_continue);
    }

    private Control BuildStats(RunReport r)
    {
        var box = Section("A caçada");
        var time = TimeSpan.FromSeconds(r.Seconds);
        Line(box, $"Tempo: {(int)time.TotalMinutes}:{time.Seconds:00}");
        Line(box, $"Salas: {r.RoomsCleared}/{r.RoomCount}");
        Line(box, $"Monstros vencidos: {r.Kills}" + (r.EliteKills > 0 ? $" ({r.EliteKills} de elite)" : string.Empty));
        Line(box, $"Golpes levados: {r.HitsTaken} ({r.DamageTaken} de vida)", r.HitsTaken == 0 ? Good : null);
        var frag = r.Survived ? $"Fragmentos trazidos: {r.Fragmentos}" : "Fragmentos: ficaram na Lanterna, dentro da Brecha";
        Line(box, frag + (summary.BonusFragmentos > 0 ? $" (+{summary.BonusFragmentos} do poço)" : string.Empty), r.Survived ? null : Bad);
        if (!r.Survived)
        {
            Line(box, "Entre de novo para buscar a Lanterna. Armas e peças nunca se perdem.", Palette.CreamDim);
        }

        return box;
    }

    private static Control BuildLoot(RunReport r)
    {
        var box = Section("Espólio");
        var catalog = Progression.Catalog.Materials;
        foreach (var (id, count) in r.Materials.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!catalog.TryGetValue(id, out var def))
            {
                continue;
            }

            var rank = def.Rank;
            box.AddChild(IconLine(ItemIcons.Material(def), rank, $"{def.Name} ×{count}", rank > RiftRank ? "  (rank acima!)" : string.Empty));
        }

        foreach (var item in r.Items)
        {
            var rank = Enum.TryParse<Rank>(item.Rank, out var parsed) ? parsed : Rank.E;
            box.AddChild(IconLine(ItemIcons.Gear("peito", rank), rank, item.Name, item.Rare ? "  (rara!)" : string.Empty));
        }

        if (r.Materials.Count == 0 && r.Items.Count == 0)
        {
            Line(box, "Nada desta vez.", Palette.CreamDim);
        }

        return box;
    }

    private Control BuildContracts()
    {
        var box = Section("Contratos do mural");
        foreach (var c in summary.Contracts)
        {
            var mark = c.Done ? "✔" : "✘";
            Line(box, $"{mark} {c.Title}: {c.Have}/{c.Need}", c.Done ? Good : Palette.CreamDim);
            if (c.Done)
            {
                Line(box, $"   +{c.Reward.Xp} XP  +{c.Reward.Fragmentos} Fragmentos  +{c.Reward.Lumes} Lumes", Palette.Amber);
            }
        }

        if (summary.ExamJustReady)
        {
            Line(box, "O Mestre Benício quer ver você: está quase pronto para o exame.", Palette.Amber);
        }

        return box;
    }

    private Control BuildXp()
    {
        var after = summary.After;
        var gained = summary.Report.Xp;
        var row = UiKit.MakeColumn(4);
        var next = after.Next is { } n ? $"rank {n}" : "o topo";
        var text = after.Progress >= 1f && after.Next is not null
            ? $"+{gained} XP · XP do rank {after.Next} completo. Falta: {string.Join(", ", after.Missing.Select(m => m.Text))}"
            : $"+{gained} XP · {after.XpIntoRank}/{after.XpSpan} até {next}";
        row.AddChild(UiKit.MakeLabel(text, UiTheme.ReadingVariation, HorizontalAlignment.Center));
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            Value = after.Progress,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(900, 22),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("9fd8ff") });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.5f) });
        row.AddChild(bar);
        return row;
    }

    private static Rank RiftRank => World.Rift.RiftSession.BreachRank;

    private static VBoxContainer Section(string title)
    {
        var box = UiKit.MakeColumn(10);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(UiKit.MakeLabel(title, UiTheme.HeadingVariation));
        return box;
    }

    private static void Line(VBoxContainer box, string text, Color? color = null)
    {
        var label = UiKit.MakeWrapped(text, UiTheme.ReadingVariation);
        if (color is { } c)
        {
            label.AddThemeColorOverride("font_color", c);
        }

        box.AddChild(label);
    }

    private static Control IconLine(Texture2D? icon, Rank rank, string text, string tag)
    {
        var row = UiKit.MakeRow(14);
        row.AddChild(new ItemIconSlot(IconSize, icon, rank) { CustomMinimumSize = new Vector2(IconSize, IconSize) });
        var label = UiKit.MakeLabel($"{text}{tag}", UiTheme.ReadingVariation);
        label.AddThemeColorOverride("font_color", RankColors.Of(rank).Lerp(Palette.Cream, 0.35f));
        label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(label);
        return row;
    }
}
