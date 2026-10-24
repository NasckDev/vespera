using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Common;
using Vespera.Core.Hunt;

namespace Vespera.Tests.Game;

/// <summary>Bênçãos da Lanterna (ROTEIRO-V2 §5.1): 5 famílias, 3 cartas por sala limpa, efeitos que somam.</summary>
[TestSuite]
public class BlessingsTest
{
    private static BlessingCatalog Catalog => BlessingCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cacada", "bencaos.json")));

    [TestCase]
    public void FiveFamiliesWithAtLeastFourBlessingsEach()
    {
        var catalog = Catalog;
        AssertThat(catalog.Families.Count).IsEqual(5);
        foreach (var family in catalog.Families.Keys)
        {
            AssertThat(catalog.Blessings.Count(b => b.Family == family)).IsGreaterEqual(4);
        }
    }

    [TestCase]
    public void OfferIsThreeDistinctNotOwnedAndFromDifferentFamilies()
    {
        var catalog = Catalog;
        for (var seed = 1; seed <= 100; seed++)
        {
            var owned = new List<string> { "brasa", "passo_longo" };
            var offer = catalog.Offer(new SeededRandom(seed), owned);
            AssertThat(offer.Count).IsEqual(3);
            AssertThat(offer.Select(b => b.Id).Distinct().Count()).IsEqual(3);
            AssertThat(offer.Select(b => b.Family).Distinct().Count()).IsEqual(3);
            AssertThat(offer.Any(b => owned.Contains(b.Id))).IsFalse();
        }
    }

    [TestCase]
    public void EffectsAddUp()
    {
        var catalog = Catalog;
        var total = catalog.Total(["brasa", "tempera", "mao_pesada", "caldo_forte", "teimosia"]);
        AssertThat(total.DamagePct).IsEqual(15 + 8 + 35 + 10);
        AssertThat(total.MaxHp).IsEqual(25 - 20);
        AssertThat(total.DamageMultiplier(lowHp: false)).IsEqualApprox(1.68, 0.001);
        var gamble = catalog.Total(["tudo_ou_nada"]);
        AssertThat(gamble.DamageMultiplier(lowHp: true)).IsEqualApprox(2.0, 0.001);
        AssertThat(gamble.DamageMultiplier(lowHp: false)).IsEqualApprox(1.0, 0.001);
    }

    [TestCase]
    public void DeathLinesComeInOrderThenAtRandom()
    {
        var lines = ReturnLines.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cacada", "falas.json")));
        AssertThat(lines.Death.Count).IsEqual(6);
        AssertThat(lines.ForDeath(1, new SeededRandom(1))!.Who).IsEqual("Nilza");
        AssertThat(lines.ForDeath(2, new SeededRandom(1))!.Who).IsEqual("Ravi");
        AssertThat(lines.ForDeath(9, new SeededRandom(1))).IsNotNull();
    }
}
