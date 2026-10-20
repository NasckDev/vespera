using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>
/// Travas de equipamento por classe e Selo (<c>gearLock</c>, <c>extra.js:75-89</c>) e regra de classe permanente
/// (só a arma da classe é empunhada, <c>extra.js:90-93</c>).
/// </summary>
public sealed class EquipmentRules(GameDatabase db)
{
    /// <summary>Selo mínimo: o explícito do item ou o da raridade (comum E, incomum D, raro C, épico B).</summary>
    public int MinRankIndex(GearDef gear) => db.RankIndexOf(gear.MinRank ?? db.Rarities[gear.Rarity].MinRank);

    /// <summary>
    /// Motivo de não poder equipar, com o texto do protótipo, ou null se pode. Classe é checada antes do Selo.
    /// </summary>
    public string? LockReason(string gearId, string classId, int playerRankIndex)
    {
        var gear = db.Gear[gearId];
        if (gear.Classes is { } classes && !classes.Contains(classId))
        {
            return db.GearData.LockClassText
                .Replace("{classes}", string.Join(" / ", classes.Select(ClassName)), StringComparison.Ordinal)
                .Replace("{class}", ClassName(classId), StringComparison.Ordinal);
        }

        var min = MinRankIndex(gear);
        if (playerRankIndex < min)
        {
            return db.GearData.LockRankText
                .Replace("{rank}", db.Ranks[min].Id, StringComparison.Ordinal)
                .Replace("{title}", db.Ranks[min].Title, StringComparison.Ordinal)
                .Replace("{current}", db.Ranks[Math.Clamp(playerRankIndex, 0, db.Ranks.Count - 1)].Id, StringComparison.Ordinal);
        }

        return null;
    }

    public bool CanEquip(string gearId, string classId, int playerRankIndex) => LockReason(gearId, classId, playerRankIndex) is null;

    /// <summary>Linha "Classe" do detalhe do item: "Todas" ou "Lâmina, Martelo".</summary>
    public string ClassesLabel(GearDef gear) => gear.Classes is { } classes ? string.Join(", ", classes.Select(ClassName)) : "Todas";

    /// <summary>Só a arma da classe pode ser empunhada.</summary>
    public static bool CanWield(string weaponId, string classId) => weaponId == classId;

    /// <summary>Nome exibido da classe (<c>CLS_NAME</c>).</summary>
    public string ClassName(string classId) => db.Classes.TryGetValue(classId, out var c) ? c.Name : classId;
}
