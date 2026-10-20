using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Core.Rules;

/// <summary>
/// Preços em Lumes (<c>priceMult</c>/<c>costLumes</c>/<c>canPay</c>/<c>pay</c>, <c>core.js:233-237</c>).
/// Todo Lume de loja ou serviço passa por <see cref="CostLumes"/>; materiais nunca são multiplicados.
/// </summary>
public sealed class PriceCalculator(GameDatabase db)
{
    private readonly EconomyRules _economy = db.Rules.Economy;

    /// <summary><c>(invasão ? 1.5 : 1) * 0.9</c>: 0.9 normal, 1.35 em invasão. Mesma ordem de operações do JS.</summary>
    public double PriceMult(bool invasion) => (invasion ? _economy.InvasionMult : 1) * _economy.PriceMult;

    /// <summary>Lumes cobrados por um preço base, arredondados como no JS (D7).</summary>
    public int CostLumes(int baseLumes, bool invasion) => JsMath.Round(baseLumes * PriceMult(invasion));

    public bool CanPay(SaveData save, Cost cost, bool invasion)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(cost);
        if (cost.Lumes > 0 && save.Lumes < CostLumes(cost.Lumes, invasion))
        {
            return false;
        }

        foreach (var (id, amount) in cost.Mats)
        {
            if (save.MaterialCount(id) < amount)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Desconta o custo. Devolve false (sem gastar nada) se faltar Lumes ou material.</summary>
    public bool TryPay(SaveData save, Cost cost, bool invasion)
    {
        if (!CanPay(save, cost, invasion))
        {
            return false;
        }

        if (cost.Lumes > 0)
        {
            save.Lumes -= CostLumes(cost.Lumes, invasion);
        }

        foreach (var (id, amount) in cost.Mats)
        {
            var left = save.MaterialCount(id) - amount;
            if (left > 0)
            {
                save.Materials[id] = left;
            }
            else
            {
                save.Materials.Remove(id);
            }
        }

        return true;
    }

    /// <summary><c>costStr</c>: "54 Lumes · 2× Presa Rubra (3)" (quantidade possuída entre parênteses).</summary>
    public string Describe(Cost cost, SaveData save, bool invasion)
    {
        var parts = new List<string>();
        if (cost.Lumes > 0)
        {
            parts.Add($"{CostLumes(cost.Lumes, invasion)} Lumes");
        }

        foreach (var (id, amount) in cost.Mats)
        {
            parts.Add($"{amount}× {db.Materials[id].Name} ({save.MaterialCount(id)})");
        }

        return string.Join(" · ", parts);
    }
}
