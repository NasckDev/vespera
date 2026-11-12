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
    public void FiveFamiliesWithSixBlessingsEachThirtyInAll()
    {
        var catalog = Catalog;
        AssertThat(catalog.Families.Count).IsEqual(5);
        foreach (var family in catalog.Families.Keys)
        {
            AssertThat(catalog.Blessings.Count(b => b.Family == family)).IsEqual(6);
        }

        AssertThat(catalog.Blessings.Count).IsEqual(30);
        AssertThat(catalog.Blessings.Select(b => b.Id).Distinct().Count()).IsEqual(30);
    }

    [TestCase]
    public void EveryBlessingHasATextAndAtLeastOneEffect()
    {
        foreach (var b in Catalog.Blessings)
        {
            AssertThat(b.Text.Length).IsGreaterEqual(10);
            var effect = b.DamagePct != 0 || b.CritPct != 0 || b.PartPct != 0 || b.DodgeDistancePct != 0 || b.DodgeInvulnerable != 0
                || b.DodgeCostPct != 0 || b.HurtInvulnerable != 0 || b.MaxHp != 0 || b.HealOnClearPct != 0 || b.HealOnKill != 0
                || b.PotionPct != 0 || b.FragmentPct != 0 || b.LumesPerBreakable != 0 || b.SpeedPct != 0 || b.LowHpDamagePct != 0;
            AssertThat(effect).IsTrue();
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
        var brasaViva = catalog.Combos.Single(c => c.Id == "brasa_viva");
        AssertThat(total.DamagePct).IsEqual(15 + 8 + 35 + 10 + brasaViva.DamagePct);
        AssertThat(total.MaxHp).IsEqual(25 - 20);
        AssertThat(total.DamageMultiplier(lowHp: false)).IsEqualApprox(1.80, 0.001);
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

    [TestCase]
    public void CombosNeedEveryFamilyOfTheListAndTheTeimosiaDoesNotCount()
    {
        var catalog = Catalog;
        AssertThat(catalog.Combos.Count).IsEqual(7);
        AssertThat(catalog.Combos.Count(c => c.Families.Count == 3)).IsEqual(2);
        AssertThat(catalog.ActiveCombos(["brasa", "tempera"]).Count).IsEqual(0);
        AssertThat(catalog.ActiveCombos(["teimosia", "tudo_ou_nada"]).Count).IsEqual(0);
        AssertThat(catalog.ActiveCombos(["brasa", "tudo_ou_nada"]).Select(c => c.Id).Single()).IsEqual("brasa_viva");
        var triple = catalog.ActiveCombos(["brasa", "passo_longo", "caldo_forte"]).Select(c => c.Id).ToList();
        AssertThat(triple.Contains("fogo_de_casa")).IsTrue();
        AssertThat(triple.Contains("ferro_e_compasso")).IsTrue();
        AssertThat(triple.Contains("panela_de_pressao")).IsTrue();
    }

    [TestCase]
    public void ComboBonusSumsIntoTheTotalAndCardsAnnounceWhatTheyComplete()
    {
        var catalog = Catalog;
        var alone = catalog.Total(["brasa"]);
        var together = catalog.Total(["brasa", "cartada"]);
        var brasaViva = catalog.Combos.Single(c => c.Id == "brasa_viva");
        AssertThat(together.CritPct).IsEqual(20 + brasaViva.CritPct);
        AssertThat(together.DamagePct).IsEqual(alone.DamagePct + brasaViva.DamagePct);
        AssertThat(catalog.CompletedBy(["brasa"], "cartada").Single().Id).IsEqual("brasa_viva");
        AssertThat(catalog.CompletedBy(["brasa", "cartada"], "faisca").Count).IsEqual(0);
        AssertThat(catalog.CompletedBy(["brasa"], "tempera").Count).IsEqual(0);
    }

    [TestCase]
    public void PendingCombosListWhatIsStillMissing()
    {
        var catalog = Catalog;
        AssertThat(catalog.PendingCombos([]).Count).IsEqual(0);
        var pending = catalog.PendingCombos(["brasa"]);
        AssertThat(pending.Count).IsEqual(3);
        var chamaAposta = pending.Single(c => c.Id == "brasa_viva");
        AssertThat(catalog.MissingFamilies(chamaAposta, ["brasa"]).Single()).IsEqual("aposta");
        AssertThat(catalog.PendingCombos(["brasa", "cartada"]).Any(c => c.Id == "brasa_viva")).IsFalse();
    }

    [TestCase]
    public void ComboFamiliesAreKnownAndBadCombosAreRejected()
    {
        foreach (var combo in Catalog.Combos)
        {
            AssertThat(combo.Text.Length).IsGreaterEqual(10);
            AssertThat(combo.Families.All(f => Catalog.Families.ContainsKey(f))).IsTrue();
        }

        const string bad = "{\"families\":{\"a\":{}},\"blessings\":[],\"combos\":[{\"id\":\"x\",\"families\":[\"a\",\"b\"]}]}";
        var threw = false;
        try
        {
            BlessingCatalog.Parse(bad);
        }
        catch (FormatException)
        {
            threw = true;
        }

        AssertThat(threw).IsTrue();
    }
}
