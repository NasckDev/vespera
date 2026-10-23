using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vespera.Core.Advancement;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>Textos (BBCode) dos detalhes de peça, material, Eco e arma, no mesmo estilo em todas as telas.</summary>
public static class HunterText
{
    private const string Dim = "#B7B2C9";
    private const string Good = "#7CC47F";
    private const string Bad = "#E4737A";
    private const string Gold = "#F2A541";

    public static string Color(string hex, string text) => $"[color={hex}]{text}[/color]";

    public static string Dimmed(string text) => Color(Dim, text);

    public static string Item(ItemInstance item, ProgressionEngine engine)
    {
        var step = engine.Catalog.Step(item.Rank);
        var slot = engine.Catalog.GearData.Slots.First(s => s.Id == item.Slot).Name;
        var sb = new StringBuilder();
        sb.AppendLine(Color(RankColors.Hex(item.Rank), item.Name));
        sb.AppendLine(Dimmed($"{slot} · Rank {item.Rank} · {step.Rarity}"));
        sb.AppendLine();
        foreach (var line in item.Stats.Lines())
        {
            sb.AppendLine(Color(Good, line));
        }

        if (item.IsRare)
        {
            sb.AppendLine(Color(Gold, "Peça rara com afixo"));
        }

        if (item.SetId is not null && engine.Catalog.Sets.TryGetValue(item.SetId, out var set))
        {
            sb.AppendLine();
            AppendSet(sb, engine.SetOverview(set.Id));
        }

        if (engine.LockReason(item) is { } lockReason)
        {
            sb.AppendLine();
            sb.AppendLine(Color(Bad, lockReason));
        }

        return sb.ToString().TrimEnd();
    }

    public static void AppendSet(StringBuilder sb, SetStatus status)
    {
        var total = status.Set.Pieces.Count;
        sb.AppendLine(Color(Gold, $"{status.Set.Name} ({status.Pieces}/{total})"));
        foreach (var bonus in status.Bonuses)
        {
            var color = bonus.Active ? Good : Dim;
            sb.AppendLine(Color(color, $"({bonus.Bonus.Pieces}) {bonus.Bonus.Name}: {bonus.Bonus.Description}"));
        }
    }

    public static string Weapon(WeaponStats stats, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Color(RankColors.Hex(stats.TierRank), stats.DisplayName));
        sb.AppendLine(Dimmed($"{className} · Nível {stats.Level} · Rank {stats.TierRank}"));
        sb.AppendLine();
        sb.AppendLine($"Dano {Color(Good, stats.Damage.ToString())}");
        sb.AppendLine($"Ritmo {Color(Good, stats.AttacksPerSecond.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "/s")}");
        sb.AppendLine($"Crítico {Color(Good, stats.CritPct.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "%")}");
        sb.AppendLine($"Quebra de partes {Color(Good, stats.PartBreakPct.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "%")}");
        return sb.ToString().TrimEnd();
    }

    public static string Material(HunterMaterialDef material, int owned)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Color(RankColors.Hex(material.Rank), material.Name));
        var kind = material.Kind == MaterialKind.Guardian ? "Material de Guardião" : "Material";
        sb.AppendLine(Dimmed($"{kind} · Rank {material.Rank}"));
        sb.AppendLine();
        sb.AppendLine(material.Desc);
        sb.AppendLine();
        sb.AppendLine(Dimmed($"Origem: {material.Source}"));
        sb.AppendLine($"Você tem {Color(Gold, owned.ToString())}");
        sb.AppendLine(Dimmed($"Vale {material.Value} Lumes"));
        return sb.ToString().TrimEnd();
    }

    public static string Echo(EchoDef echo, AdvancementCatalog catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Color(RankColors.Hex(echo.Rank), echo.Name));
        sb.AppendLine(Dimmed($"Eco · {catalog.Guardians[echo.Guardian].Name}"));
        sb.AppendLine();
        sb.AppendLine(echo.Description);
        return sb.ToString().TrimEnd();
    }

    public static string EmptySlot(string slotName, string hint) => $"{Color(Dim, slotName)}\n\n{Dimmed(hint)}";

    /// <summary>"+12" em verde ou "-3" em vermelho ao lado de um atributo que mudou.</summary>
    public static string Delta(double delta, string format = "0.#")
    {
        var text = (delta >= 0 ? "+" : string.Empty) + delta.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        return Color(delta >= 0 ? Good : Bad, text);
    }

    public static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
}
