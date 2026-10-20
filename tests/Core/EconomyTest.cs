namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Preços, custos de forja/afinação e venda de sobras (spec 02 §1.6, §1.12, §2.5; §6.3 itens 15-16).</summary>
[TestSuite]
public class EconomyTest
{
    private static PriceCalculator Prices => new(TestDb.Instance);

    private static UpgradeCosts Upgrades => new(TestDb.Instance);

    // Tabela da spec 02 §2.5 (arredondamento JS).
    [TestCase(6, 5, 8)]
    [TestCase(10, 9, 14)]
    [TestCase(22, 20, 30)]
    [TestCase(30, 27, 41)]
    [TestCase(40, 36, 54)]
    [TestCase(50, 45, 68)]
    [TestCase(60, 54, 81)]
    [TestCase(70, 63, 95)]
    [TestCase(80, 72, 108)]
    [TestCase(90, 81, 122)]
    [TestCase(100, 90, 135)]
    [TestCase(110, 99, 149)]
    [TestCase(120, 108, 162)]
    [TestCase(130, 117, 176)]
    [TestCase(140, 126, 189)]
    [TestCase(150, 135, 203)]
    [TestCase(160, 144, 216)]
    public void CostLumesMatchesPrototypeTable(int baseLumes, int normal, int invasion)
    {
        AssertThat(Prices.CostLumes(baseLumes, invasion: false)).IsEqual(normal);
        AssertThat(Prices.CostLumes(baseLumes, invasion: true)).IsEqual(invasion);
    }

    [TestCase]
    public void PriceMultIsDiscountTimesInvasion()
    {
        AssertThat(Prices.PriceMult(false)).IsEqualApprox(0.9, 1e-12);
        AssertThat(Prices.PriceMult(true)).IsEqualApprox(1.35, 1e-12);
    }

    [TestCase]
    public void PayChargesMultipliedLumesAndRawMaterials()
    {
        var s = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        s.Lumes = 100;
        s.Materials["minerio"] = 4;
        s.Materials["presa"] = 2;
        var luva2 = TestDb.Instance.Recipes.Single(r => r.Gear == "luva2").Cost; // 60 L, 4 minério, 2 presa

        AssertThat(Prices.TryPay(s, luva2, invasion: false)).IsTrue();
        AssertThat(s.Lumes).IsEqual(46);
        AssertThat(s.Materials.ContainsKey("minerio")).IsFalse();
        AssertThat(s.Materials.ContainsKey("presa")).IsFalse();
    }

    [TestCase]
    public void PayFailsWithoutSpendingWhenShort()
    {
        var s = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        s.Lumes = 40;
        var potion = Cost.Of(30);
        AssertThat(Prices.CanPay(s, potion, invasion: false)).IsTrue();
        AssertThat(Prices.TryPay(s, potion, invasion: true)).IsFalse(); // 41 > 40
        AssertThat(s.Lumes).IsEqual(40);

        var mats = Cost.Of(0, new Dictionary<string, int> { ["erva"] = 2 });
        s.Materials["erva"] = 1;
        AssertThat(Prices.TryPay(s, mats, invasion: false)).IsFalse();
        AssertThat(s.MaterialCount("erva")).IsEqual(1);
    }

    [TestCase]
    public void DescribeListsLumesAndOwnedMaterials()
    {
        var s = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        s.Materials["presa"] = 3;
        var text = Prices.Describe(Upgrades.WeaponUpgrade("sword", 2)!, s, invasion: false);
        AssertThat(text).IsEqual("72 Lumes · 2× Presa Rubra (3)");
    }

    [TestCase("sword", 1, 40, "presa", 1)]
    [TestCase("sword", 4, 160, "presa", 4)]
    [TestCase("staff", 1, 40, "estilhaco", 2)]
    [TestCase("staff", 3, 120, "estilhaco", 6)]
    [TestCase("bow", 2, 80, "pavio", 2)]
    [TestCase("hammer", 3, 120, "minerio", 3)]
    [TestCase("dag", 4, 160, "osso", 4)]
    public void WeaponUpgradeCostByLevel(string weapon, int level, int lumes, string material, int amount)
    {
        var cost = Upgrades.WeaponUpgrade(weapon, level)!;
        AssertThat(cost.Lumes).IsEqual(lumes);
        AssertThat(cost.Mats.GetValueOrDefault(material)).IsEqual(amount);
        AssertThat(cost.Mats.Count).IsEqual(1);
    }

    [TestCase]
    public void WeaponUpgradeStopsAtMaxLevel()
        => AssertThat(Upgrades.WeaponUpgrade("sword", 5)).IsNull();

    [TestCase]
    public void EcoTuningCostsAndReducesEssence()
    {
        var first = Upgrades.EcoTuning(0)!;
        AssertThat(first.Lumes).IsEqual(70);
        AssertThat(first.Mats.GetValueOrDefault("estilhaco")).IsEqual(4);
        var second = Upgrades.EcoTuning(1)!;
        AssertThat(second.Lumes).IsEqual(130);
        AssertThat(second.Mats.GetValueOrDefault("estilhaco")).IsEqual(6);
        AssertThat(Upgrades.EcoTuning(2)).IsNull();

        AssertThat(Upgrades.EcoCost("abissal", 0)).IsEqual(55);
        AssertThat(Upgrades.EcoCost("abissal", 1)).IsEqual(45);
        AssertThat(Upgrades.EcoCost("abissal", 2)).IsEqual(35);
    }

    [TestCase]
    public void UnlockAndInfusionComeFromData()
    {
        AssertThat(Upgrades.WeaponUnlock("hammer").Lumes).IsEqual(80);
        AssertThat(Upgrades.WeaponUnlock("hammer").Mats.GetValueOrDefault("minerio")).IsEqual(3);
        AssertThat(Upgrades.Infusion("vazio").Lumes).IsEqual(100);
        AssertThat(Upgrades.Infusion("vazio").Mats.GetValueOrDefault("carapaca")).IsEqual(1);
    }

    [TestCase]
    public void ScrapSaleIgnoresMaterialsInUse()
    {
        var sale = new MaterialSale(TestDb.Instance);
        AssertThat(sale.UsedMaterials.Contains("estilhaco")).IsTrue();
        AssertThat(sale.UsedMaterials.Contains("pavio")).IsTrue();  // upgrade do arco
        AssertThat(sale.UsedMaterials.Contains("halo")).IsTrue();   // loja do Abel
        AssertThat(sale.UsedMaterials.Contains("lamento")).IsTrue(); // Cristal do Vazio

        var quote = sale.QuoteScraps(new Dictionary<string, int> { ["estilhaco"] = 5, ["presa"] = 2, ["lapide"] = 3, ["medalha"] = 1, ["placa"] = 0 });
        AssertThat(quote.MaterialIds).ContainsExactlyInAnyOrder("lapide", "medalha");
        AssertThat(quote.Count).IsEqual(4);
        AssertThat(quote.Lumes).IsEqual(22); // 3×4 + 1×10
        AssertThat(sale.ShardSaleLumes(5)).IsEqual(20);
    }
}
