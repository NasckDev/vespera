using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>O que o Brás pagaria pelas sobras: materiais fora de uso, quantidade total e Lumes.</summary>
public sealed record SaleQuote(IReadOnlyList<string> MaterialIds, int Count, int Lumes)
{
    public bool IsEmpty => Count == 0;
}

/// <summary>Venda de sobras de Brecha e de Estilhaços no Brás (<c>usedMats</c>/<c>sellInfo</c>, <c>world.js:228-229,317-319</c>).</summary>
public sealed class MaterialSale
{
    private readonly GameDatabase _db;

    public MaterialSale(GameDatabase db)
    {
        _db = db;
        UsedMaterials = ComputeUsedMaterials(db);
    }

    /// <summary>
    /// "Materiais em uso" (não vendidos como sobra): Estilhaço + materiais de upgrade + de desbloqueio de arma
    /// + receitas + infusões + loja de relíquias.
    /// </summary>
    public IReadOnlySet<string> UsedMaterials { get; }

    /// <summary>Sobras vendáveis: material fora de uso, qtd &gt; 0, a <c>Value</c> Lumes cada (sem multiplicador de preço).</summary>
    public SaleQuote QuoteScraps(IReadOnlyDictionary<string, int> materials)
    {
        var ids = new List<string>();
        int count = 0, lumes = 0;
        foreach (var (id, amount) in materials)
        {
            if (amount <= 0 || UsedMaterials.Contains(id) || !_db.Materials.TryGetValue(id, out var def))
            {
                continue;
            }

            ids.Add(id);
            count += amount;
            lumes += amount * def.Value;
        }

        return new SaleQuote(ids, count, lumes);
    }

    /// <summary>Estilhaços vendidos a 4 Lumes cada (não passa pelo multiplicador de preço).</summary>
    public int ShardSaleLumes(int shards) => Math.Max(0, shards) * _db.Rules.Economy.ShardSellPrice;

    private static HashSet<string> ComputeUsedMaterials(GameDatabase db)
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { db.Rules.Economy.ShardMaterial };
        foreach (var weapon in db.WeaponOrder)
        {
            used.Add(weapon.UpgradeMaterial);
            used.UnionWith(weapon.Unlock.Mats.Keys);
        }

        foreach (var recipe in db.Recipes)
        {
            used.UnionWith(recipe.Cost.Mats.Keys);
        }

        foreach (var element in db.Elements.Values)
        {
            used.UnionWith(element.Infusion.Mats.Keys);
        }

        foreach (var offer in db.Shops.Relics)
        {
            used.UnionWith(offer.Cost.Mats.Keys);
        }

        return used;
    }
}
