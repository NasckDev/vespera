using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>Bônus de um conjunto e se já está ativo com as peças vestidas.</summary>
public sealed record SetBonusStatus(SetBonusDef Bonus, bool Active);

/// <summary>Quantas peças do conjunto estão vestidas e quais bônus valem.</summary>
public sealed record SetStatus(ArmorSetDef Set, int Pieces, IReadOnlyList<SetBonusStatus> Bonuses);

/// <summary>Atributos que o equipamento soma ao caçador (sem a arma, que vem do <see cref="WeaponForge"/>).</summary>
public sealed record EquipmentSummary(StatBlock Items, StatBlock SetBonuses, StatBlock Total, IReadOnlyList<SetStatus> Sets);

public sealed record EquipResult(bool Ok, string Reason)
{
    public static readonly EquipResult Success = new(true, string.Empty);

    public static EquipResult Fail(string reason) => new(false, reason);
}

public sealed partial class ProgressionEngine
{
    public IReadOnlyList<ItemInstance> Items => Progress.Items;

    public ItemInstance? EquippedIn(string slot) =>
        Progress.Equipped.TryGetValue(slot, out var id) ? Progress.Items.FirstOrDefault(i => i.Id == id) : null;

    public IEnumerable<ItemInstance> EquippedItems => Catalog.GearData.Slots.Select(s => EquippedIn(s.Id)).OfType<ItemInstance>();

    /// <summary>Peças no inventário que não estão vestidas.</summary>
    public IEnumerable<ItemInstance> BackpackItems => Progress.Items.Where(i => !IsEquipped(i));

    public bool IsEquipped(ItemInstance item) => Progress.Equipped.TryGetValue(item.Slot, out var id) && id == item.Id;

    /// <summary>Veste a peça no slot dela, trocando a que estava (que volta ao inventário). Exige rank igual ou maior que o da peça.</summary>
    public EquipResult Equip(int itemId)
    {
        var item = Progress.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return EquipResult.Fail("Você não possui esta peça.");
        }

        var reason = LockReason(item);
        if (reason is not null)
        {
            return EquipResult.Fail(reason);
        }

        Progress.Equipped[item.Slot] = item.Id;
        return EquipResult.Success;
    }

    /// <summary>Texto do motivo de não poder vestir, ou null se pode.</summary>
    public string? LockReason(ItemInstance item) =>
        Catalog.RankIndex(Rank) < Catalog.RankIndex(item.Rank) ? $"Requer o rank {item.Rank}. Seu rank é {Rank}." : null;

    public bool Unequip(string slot) => Progress.Equipped.Remove(slot);

    /// <summary>Atributos do que está vestido e bônus dos conjuntos (2 e 4 peças valem juntos).</summary>
    public EquipmentSummary Equipment()
    {
        var items = StatBlock.Zero;
        var sets = new List<SetStatus>();
        var bonuses = StatBlock.Zero;
        var worn = EquippedItems.ToList();
        foreach (var item in worn)
        {
            items += item.Stats;
        }

        foreach (var group in worn.Where(i => i.SetId is not null).GroupBy(i => i.SetId!))
        {
            if (!Catalog.Sets.TryGetValue(group.Key, out var set))
            {
                continue;
            }

            var count = group.Count();
            var statuses = set.Bonuses.OrderBy(b => b.Pieces).Select(b => new SetBonusStatus(b, count >= b.Pieces)).ToList();
            foreach (var status in statuses.Where(s => s.Active))
            {
                bonuses += status.Bonus.Stats;
            }

            sets.Add(new SetStatus(set, count, statuses));
        }

        return new EquipmentSummary(items, bonuses, items + bonuses, sets);
    }

    /// <summary>Estado de um conjunto mesmo sem peças vestidas (para a tela de personagem listar os conjuntos conhecidos).</summary>
    public SetStatus SetOverview(string setId)
    {
        var set = Catalog.Sets[setId];
        var count = EquippedItems.Count(i => i.SetId == setId);
        return new SetStatus(set, count, [.. set.Bonuses.OrderBy(b => b.Pieces).Select(b => new SetBonusStatus(b, count >= b.Pieces))]);
    }

    public int EchoSlotCount => Progress.EchoSlots.Count;

    public string? EchoIn(int slot) => slot >= 0 && slot < Progress.EchoSlots.Count ? Progress.EchoSlots[slot] : null;

    /// <summary>Põe um Eco já absorvido no slot (0 ou 1). Se ele estava no outro slot, troca de lugar.</summary>
    public EquipResult EquipEcho(int slot, string echoId)
    {
        if (slot < 0 || slot >= Progress.EchoSlots.Count)
        {
            return EquipResult.Fail("Slot de Eco inexistente.");
        }

        if (!Progress.Echoes.Contains(echoId))
        {
            return EquipResult.Fail("Você ainda não absorveu este Eco.");
        }

        var other = Progress.EchoSlots.IndexOf(echoId);
        if (other >= 0)
        {
            Progress.EchoSlots[other] = Progress.EchoSlots[slot];
        }

        Progress.EchoSlots[slot] = echoId;
        return EquipResult.Success;
    }

    public bool UnequipEcho(int slot)
    {
        if (slot < 0 || slot >= Progress.EchoSlots.Count || Progress.EchoSlots[slot] is null)
        {
            return false;
        }

        Progress.EchoSlots[slot] = null;
        return true;
    }

    /// <summary>Eco recém-absorvido vai para o primeiro slot livre (se houver).</summary>
    private void AutoEquipEcho(string echoId)
    {
        var free = Progress.EchoSlots.IndexOf(null);
        if (free >= 0)
        {
            Progress.EchoSlots[free] = echoId;
        }
    }

    // Forja: delegação fina, para quem chama não precisar conhecer o WeaponForge.

    public WeaponStats WeaponStats => Forge.Stats(ClassId, Progress.WeaponLevels.GetValueOrDefault(ClassId, 1));

    public ForgeCheck CanUpgrade() => Forge.Check(Progress, ClassId);

    public ForgeResult Upgrade() => new(Forge.Upgrade(Progress, ClassId));
}
