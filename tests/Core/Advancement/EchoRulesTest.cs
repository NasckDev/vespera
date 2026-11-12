namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Os 7 Ecos de Guardião têm números de combate nos dados e as contas puras batem com eles.</summary>
[TestSuite]
public class EchoRulesTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    [TestCase]
    public void EveryEchoHasPositiveCooldownDurationAndRadius()
    {
        AssertThat(Catalog.Echoes.Count).IsEqual(7);
        foreach (var echo in Catalog.Echoes.Values)
        {
            AssertThat(echo.Cooldown).IsGreater(0);
            AssertThat(echo.Duration).IsGreater(0);
            AssertThat(echo.Radius).IsGreater(0);
        }
    }

    [TestCase]
    public void SeedFiresThornsForItsWholeLifeAtAFractionOfTheAttack()
    {
        var seed = Catalog.Echoes["eco_semente"];
        AssertThat(EchoRules.ThornCount(seed)).IsEqual(12);
        AssertThat(EchoRules.Damage(seed, 100)).IsEqual(70.0);
    }

    [TestCase]
    public void VelaHealsItsFractionOfMaxHpOverItsDuration()
    {
        var vela = Catalog.Echoes["eco_vela"];
        var total = EchoRules.HealPerSecond(vela, 120) * vela.Duration;
        AssertThat(total).IsEqual(60.0);
    }

    [TestCase]
    public void BellStunsBossesForOnlyAFractionOfTheTime()
    {
        var bell = Catalog.Echoes["eco_sino"];
        AssertThat(EchoRules.StunSeconds(bell, false)).IsEqual(2.0);
        AssertThat(EchoRules.StunSeconds(bell, true)).IsLess(1.0);
    }

    [TestCase]
    public void VelaCostsOneRankOfHpExceptAtRankE()
    {
        AssertThat(EchoRules.VelaHpPenalty(Catalog, Rank.E)).IsEqual(0);
        AssertThat(EchoRules.VelaHpPenalty(Catalog, Rank.B)).IsEqual(Catalog.RankData.HpPerRank);
    }

    [TestCase]
    public void CooldownCountsDownAndReportsTheFraction()
    {
        var cooldown = new EchoCooldown();
        AssertThat(cooldown.Ready).IsTrue();
        cooldown.Start(10);
        AssertThat(cooldown.Ready).IsFalse();
        cooldown.Tick(5);
        AssertThat(cooldown.Fraction).IsEqual(0.5f);
        cooldown.Tick(6);
        AssertThat(cooldown.Ready).IsTrue();
        AssertThat(cooldown.Fraction).IsEqual(0f);
    }
}
