using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

public sealed partial class ProgressionEngine
{
    /// <summary>
    /// Sorteia e entrega o saque de uma fonte numa Brecha de rank <paramref name="breachRank"/>. Materiais e peças entram na hora;
    /// os Fragmentos vão para a caçada em andamento (e só chegam à carteira se você sair vivo) ou direto para a carteira fora de Brecha.
    /// Peças de equipamento ficam no inventário e nunca se perdem na morte.
    /// </summary>
    public LootGrant GrantLoot(Rank breachRank, LootSource source)
    {
        var owned = Progress.Items.Where(i => i.SetId is not null).Select(i => i.DefId).ToHashSet();
        var rolled = Roller.Roll(breachRank, source, Random, owned);
        return Apply(rolled);
    }

    /// <summary>Entrega um saque já sorteado (útil para repetir/mostrar o mesmo resultado).</summary>
    public LootGrant Apply(LootResult rolled)
    {
        var entries = new List<LootEntry>(rolled.Entries.Count);
        foreach (var entry in rolled.Entries)
        {
            if (entry.Kind == LootEntryKind.Material)
            {
                Progress.Materials[entry.Id] = Progress.MaterialCount(entry.Id) + entry.Amount;
                entries.Add(entry);
                continue;
            }

            var item = entry.Item!;
            item.Id = Progress.NextItemId++;
            Progress.Items.Add(item);
            entries.Add(entry);
        }

        var bonus = Equipment().Total.FragmentosPct;
        var fragmentos = (int)Math.Round(rolled.Fragmentos * (1 + (bonus / 100.0)), MidpointRounding.AwayFromZero);
        var held = Progress.RunNeighborhood is not null;
        if (held)
        {
            Progress.RunFragmentos += fragmentos;
        }
        else
        {
            Progress.Fragmentos += fragmentos;
        }

        return new LootGrant(new LootResult(rolled.BreachRank, rolled.Source, entries, fragmentos), fragmentos, held);
    }

    /// <summary>Vende materiais por Lumes (valor do catálogo). Devolve os Lumes ganhos, 0 se não há o bastante.</summary>
    public int SellMaterial(string materialId, int amount)
    {
        if (amount <= 0 || !Catalog.Materials.TryGetValue(materialId, out var material) || Progress.MaterialCount(materialId) < amount)
        {
            return 0;
        }

        var left = Progress.MaterialCount(materialId) - amount;
        if (left > 0)
        {
            Progress.Materials[materialId] = left;
        }
        else
        {
            Progress.Materials.Remove(materialId);
        }

        var lumes = material.Value * amount;
        Progress.Lumes += lumes;
        return lumes;
    }
}
