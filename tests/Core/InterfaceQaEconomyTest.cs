namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// QA da frente B3 (Interface e economia): todo preço que a UI mostra em loja, forja, serviço, infusão e relíquia
/// é exatamente o que cobra, na borda (Lumes iguais ao preço compram e zeram; um a menos não compra nada), em
/// tempo normal e em invasão (×1,35 com arredondamento JS). Varre o banco real, então pega item novo sem teste.
/// </summary>
[TestSuite]
public class InterfaceQaEconomyTest
{
    private static GameDatabase Db => TestDb.Instance;

    private static PriceCalculator Prices => new(Db);

    private static UpgradeCosts Upgrades => new(Db);

    /// <summary>Todos os custos que a UI oferece: serviços, relíquias do Abel, receitas, melhorias, desbloqueios e infusões.</summary>
    private static IEnumerable<(string Name, Cost Cost)> AllShopCosts()
    {
        foreach (var (id, service) in Db.Services)
        {
            yield return ($"serviço {id}", service.Cost);
        }

        foreach (var offer in Db.Shops.Relics)
        {
            yield return ($"relíquia {offer.Gear}", offer.Cost);
        }

        foreach (var recipe in Db.Recipes)
        {
            yield return ($"receita {recipe.Gear}", recipe.Cost);
        }

        foreach (var weapon in Db.WeaponOrder)
        {
            yield return ($"desbloqueio {weapon.Id}", Upgrades.WeaponUnlock(weapon.Id));
            for (var level = 1; level < 20 && Upgrades.WeaponUpgrade(weapon.Id, level) is { } upgrade; level++)
            {
                yield return ($"melhoria {weapon.Id} +{level}", upgrade);
            }
        }

        foreach (var (id, _) in Db.Elements)
        {
            yield return ($"infusão {id}", Upgrades.Infusion(id));
        }
    }

    private static SaveData WalletFor(Cost cost, int lumes)
    {
        var save = SaveData.NewGame(Db, "Qa", "sword");
        save.Lumes = lumes;
        save.Materials.Clear();
        foreach (var (id, amount) in cost.Mats)
        {
            save.Materials[id] = amount;
        }

        return save;
    }

    [TestCase]
    public void TheShopCatalogIsNotEmpty() => AssertThat(AllShopCosts().Count()).IsGreater(20);

    [TestCase]
    public void ExactLumesBuyAndLeaveZeroInNormalAndInvasion()
    {
        foreach (var invasion in new[] { false, true })
        {
            foreach (var (name, cost) in AllShopCosts())
            {
                var price = Prices.CostLumes(cost.Lumes, invasion);
                var save = WalletFor(cost, price);
                AssertThat(Prices.CanPay(save, cost, invasion)).OverrideFailureMessage($"{name} (invasão={invasion}): {price} Lumes deveriam bastar").IsTrue();
                AssertThat(Prices.TryPay(save, cost, invasion)).OverrideFailureMessage($"{name} (invasão={invasion}): compra recusada").IsTrue();
                AssertThat(save.Lumes).OverrideFailureMessage($"{name} (invasão={invasion}): sobrou {save.Lumes} de {price}").IsEqual(0);
                AssertThat(save.Materials.Count).OverrideFailureMessage($"{name}: materiais deveriam zerar").IsEqual(0);
            }
        }
    }

    [TestCase]
    public void OneLumeShortBuysNothingAndChargesNothing()
    {
        foreach (var invasion in new[] { false, true })
        {
            foreach (var (name, cost) in AllShopCosts())
            {
                var price = Prices.CostLumes(cost.Lumes, invasion);
                if (price <= 0)
                {
                    continue;
                }

                var save = WalletFor(cost, price - 1);
                var materials = save.Materials.Values.Sum();
                AssertThat(Prices.CanPay(save, cost, invasion)).OverrideFailureMessage($"{name} (invasão={invasion}): {price - 1} Lumes não deveriam bastar").IsFalse();
                AssertThat(Prices.TryPay(save, cost, invasion)).IsFalse();
                AssertThat(save.Lumes).OverrideFailureMessage($"{name}: compra recusada não pode cobrar").IsEqual(price - 1);
                AssertThat(save.Materials.Values.Sum()).OverrideFailureMessage($"{name}: compra recusada não pode tirar material").IsEqual(materials);
            }
        }
    }

    [TestCase]
    public void DescribedPriceIsTheChargedPrice()
    {
        foreach (var invasion in new[] { false, true })
        {
            foreach (var (name, cost) in AllShopCosts())
            {
                var save = WalletFor(cost, 10_000);
                var text = Prices.Describe(cost, save, invasion);
                if (cost.Lumes > 0)
                {
                    var price = Prices.CostLumes(cost.Lumes, invasion);
                    AssertThat(text.StartsWith($"{price} Lumes", StringComparison.Ordinal)).OverrideFailureMessage($"{name} (invasão={invasion}): mostra '{text}', cobra {price}").IsTrue();
                    Prices.TryPay(save, cost, invasion);
                    AssertThat(save.Lumes).OverrideFailureMessage($"{name}: '{text}' cobrou {10_000 - save.Lumes}").IsEqual(10_000 - price);
                }
                else
                {
                    AssertThat(text.Contains("Lumes", StringComparison.Ordinal)).OverrideFailureMessage($"{name}: custo sem Lumes não deveria citar Lumes ('{text}')").IsFalse();
                }
            }
        }
    }

    [TestCase]
    public void InvasionNeverMakesAnythingCheaper()
    {
        foreach (var (name, cost) in AllShopCosts())
        {
            var normal = Prices.CostLumes(cost.Lumes, invasion: false);
            var invasion = Prices.CostLumes(cost.Lumes, invasion: true);
            AssertThat(invasion >= normal).OverrideFailureMessage($"{name}: invasão {invasion} < normal {normal}").IsTrue();
            AssertThat(invasion).OverrideFailureMessage($"{name}: invasão deveria ser JS-round(base × 1,35)").IsEqual((int)Math.Floor((cost.Lumes * (1.5 * 0.9)) + 0.5));
        }
    }
}
