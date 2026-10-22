using System.Collections.Generic;
using System.Globalization;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Data;
using Vespera.Core.State;

namespace Vespera.UI.Common;

/// <summary>Uma linha de atributo do detalhe do item ("Defesa", "+6") e a diferença para o equipado (0 = sem comparação).</summary>
public readonly record struct StatLine(string Label, string Value, double Delta = 0, string? DeltaText = null);

/// <summary>Tipo de item da grade do inventário (<c>invItems</c>, <c>ui.js:83-90</c>).</summary>
public enum ItemKind
{
    Weapon,
    Gear,
    Material,
    Potion,
    Lantern,
}

/// <summary>Item mostrável (grade, tooltip): o que é e de onde veio (índice na mochila para equipamento).</summary>
public sealed record ItemRef(ItemKind Kind, string Id, int Index = -1, int Count = 1);

/// <summary>Descrição completa do item para o tooltip/detalhe (<c>itemInfo</c> efetivo, <c>ui.js:93-102</c> + <c>extra.js:84-88</c>).</summary>
public sealed class ItemInfo
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Icon { get; init; }

    public required string Desc { get; init; }

    public required string RarityName { get; init; }

    public required Color RarityColor { get; init; }

    public List<StatLine> Stats { get; } = [];

    /// <summary>Motivo do bloqueio (classe/Selo), ou null.</summary>
    public string? Locked { get; init; }

    /// <summary>"Empunhada" etc.</summary>
    public string? Note { get; init; }

    /// <summary>"Comparado com: X" (equipamento do mesmo slot).</summary>
    public string? CompareWith { get; set; }
}

/// <summary>Nomes, ícones e detalhes de armas/equipamentos/materiais como a v2 mostra.</summary>
public static class UiItems
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary><c>wName</c>: nome da arma + " · Elemento" se houver infusão ativa (<c>core.js:216</c>).</summary>
    public static string WeaponName(GameState state, string weaponId)
    {
        var db = Database.Db;
        var name = db.Weapons[weaponId].Name;
        return state.Save.ActiveInfusion.TryGetValue(weaponId, out var el) && db.Elements.TryGetValue(el, out var e) ? $"{name} · {e.Name}" : name;
    }

    /// <summary><c>wIcon</c>: <c>el_{arma}_{índice}</c> com infusão ativa (<c>core.js:217</c>).</summary>
    public static string WeaponIcon(GameState state, string weaponId)
    {
        var weapon = Database.Db.Weapons[weaponId];
        if (state.Save.ActiveInfusion.TryGetValue(weaponId, out var el))
        {
            var index = IndexOf(weapon.Elements, el);
            if (index >= 0)
            {
                return ElementIcon(weaponId, index);
            }
        }

        return weapon.Icon;
    }

    public static string ElementIcon(string weaponId, int index) => $"el_{weaponId}_{index}";

    public static RarityDef Rarity(string id) => Database.Db.Rarities.TryGetValue(id, out var r) ? r : Database.Db.Rarities["comum"];

    public static Color RarityColor(string id) => UiPalette.FromHex(Rarity(id).Color, UiPalette.Creme);

    public static string Percent(double fraction) => $"{(int)System.Math.Floor((fraction * 100) + 0.5)}%";

    /// <summary><c>statRows</c> (<c>ui.js:92</c>).</summary>
    public static List<StatLine> StatRows(GearDef g, GearDef? compare = null)
    {
        var rows = new List<StatLine>();
        void Add(string label, double value, double other, string text, string deltaFormat)
        {
            if (value == 0 && other == 0)
            {
                return;
            }

            var delta = value - other;
            rows.Add(new StatLine(label, value == 0 ? "—" : text, compare is null ? 0 : delta, compare is null || delta == 0 ? null : string.Format(PtBr, deltaFormat, delta)));
        }

        Add("Ataque", g.Atk, compare?.Atk ?? 0, $"+{g.Atk}", "{0:+0;-0}");
        Add("Defesa", g.Def, compare?.Def ?? 0, $"+{g.Def}", "{0:+0;-0}");
        Add("Vida", g.Hp, compare?.Hp ?? 0, $"+{g.Hp}", "{0:+0;-0}");
        Add("Crítico", g.Crit, compare?.Crit ?? 0, $"+{g.Crit}%", "{0:+0;-0}%");
        Add("Velocidade", g.Spd * 100, (compare?.Spd ?? 0) * 100, $"+{Percent(g.Spd)}", "{0:+0;-0}%");
        Add("Luz", g.Light ?? 0, compare?.Light ?? 0, $"×{(g.Light ?? 0).ToString("0.##", PtBr)}", "{0:+0.##;-0.##}");
        Add("Essência", g.Ess * 100, (compare?.Ess ?? 0) * 100, $"+{Percent(g.Ess)}", "{0:+0;-0}%");
        Add("Regeneração", g.Regen, compare?.Regen ?? 0, $"{g.Regen.ToString("0.#", PtBr)}/s", "{0:+0.#;-0.#}/s");
        return rows;
    }

    /// <summary>Detalhe de um equipamento pelo id (lojas, receitas, mochila), comparado com o que está no slot.</summary>
    public static ItemInfo Gear(GameState state, string gearId, bool compareEquipped = true)
    {
        var db = Database.Db;
        var g = db.Gear[gearId];
        var slotName = SlotName(g.Slot);
        GearDef? equipped = null;
        if (compareEquipped && state.Save.Equipment.TryGetValue(g.Slot, out var eqId) && eqId != gearId && db.Gear.TryGetValue(eqId, out var eq))
        {
            equipped = eq;
        }

        var rarity = Rarity(g.Rarity);
        var info = new ItemInfo
        {
            Name = g.Name,
            Type = $"Equipamento · {slotName}",
            Icon = g.Icon,
            Desc = g.Desc,
            RarityName = rarity.Name,
            RarityColor = RarityColor(g.Rarity),
            Locked = state.EquipLockReason(gearId),
            CompareWith = equipped is null ? null : $"Comparado com: {equipped.Name}",
        };
        info.Stats.AddRange(StatRows(g, equipped));
        info.Stats.Add(new StatLine("Classe", state.EquipmentRules.ClassesLabel(g)));
        var minRank = db.Ranks[state.EquipmentRules.MinRankIndex(g)];
        info.Stats.Add(new StatLine("Selo mínimo", $"{minRank.Id} · {minRank.Title}")); // a letra sozinha em negrito parece "0"
        return info;
    }

    /// <summary>Detalhe de arma (<c>itemInfo</c> efetivo): ataque base, nível, elemento, partes; trava fora da classe.</summary>
    public static ItemInfo Weapon(GameState state, string weaponId)
    {
        var db = Database.Db;
        var w = db.Weapons[weaponId];
        var level = state.Save.WeaponLevel(weaponId);
        var rarity = level >= 4 ? "raro" : level >= 2 ? "incomum" : "comum";
        var element = state.Save.ActiveInfusion.TryGetValue(weaponId, out var el) && db.Elements.TryGetValue(el, out var e) ? e.Name : "Nenhum";
        var cls = state.Save.ClassId;
        var info = new ItemInfo
        {
            Name = WeaponName(state, weaponId),
            Type = $"Arma ({w.Type})",
            Icon = WeaponIcon(state, weaponId),
            Desc = w.Desc,
            RarityName = Rarity(rarity).Name,
            RarityColor = RarityColor(rarity),
            Locked = weaponId == cls ? null : $"Arma da classe {state.EquipmentRules.ClassName(weaponId)}. Só a arma da sua classe pode ser empunhada.",
            Note = weaponId == cls ? "Empunhada" : null,
        };
        info.Stats.Add(new StatLine("Ataque base", state.StatsCalculator.WeaponDamage(weaponId, System.Math.Max(1, level)).ToString()));
        info.Stats.Add(new StatLine("Nível", $"+{level}"));
        info.Stats.Add(new StatLine("Elemento", element));
        info.Stats.Add(new StatLine("Partes", $"×{w.PartMult.ToString("0.##", PtBr)}"));
        return info;
    }

    public static ItemInfo Material(string materialId, int count)
    {
        var m = Database.Db.Materials[materialId];
        var info = new ItemInfo
        {
            Name = m.Name,
            Type = "Material",
            Icon = m.Icon,
            Desc = m.Desc,
            RarityName = Rarity(m.Rarity).Name,
            RarityColor = RarityColor(m.Rarity),
        };
        info.Stats.Add(new StatLine("Quantidade", count.ToString()));
        info.Stats.Add(new StatLine("Valor", $"{m.Value} Lumes"));
        return info;
    }

    public static ItemInfo Potion(int count)
    {
        var info = new ItemInfo
        {
            Name = "Poção",
            Type = "Consumível",
            Icon = "ic_potion",
            Desc = "Recupera 45% da vida. Ivo destila com Erva-de-Véspera.",
            RarityName = Rarity("comum").Name,
            RarityColor = RarityColor("comum"),
        };
        info.Stats.Add(new StatLine("Quantidade", count.ToString()));
        return info;
    }

    public static ItemInfo Lantern(string owner) => new()
    {
        Name = $"Lanterna de {owner}",
        Type = "Lanterna perdida",
        Icon = "i_lantern",
        Desc = $"Ainda está morna. Leve até Odete, na Guilda: {owner} está esperando.",
        RarityName = Rarity("epico").Name,
        RarityColor = RarityColor("epico"),
    };

    public static ItemInfo Info(GameState state, ItemRef item) => item.Kind switch
    {
        ItemKind.Weapon => Weapon(state, item.Id),
        ItemKind.Gear => Gear(state, item.Id),
        ItemKind.Material => Material(item.Id, item.Count),
        ItemKind.Potion => Potion(item.Count),
        _ => Lantern(item.Id),
    };

    public static string SlotName(string slotId)
    {
        foreach (var slot in Database.Db.GearSlots)
        {
            if (slot.Id == slotId)
            {
                return slot.Name;
            }
        }

        return slotId;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}
