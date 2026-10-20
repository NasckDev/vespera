namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Ranks;
using Vespera.Core.Rules;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Selo e Nível da v2 (spec 02 §1.2-1.3; §6.3 itens 1-3). Substitui os testes do RankTable de 9 ranks.</summary>
[TestSuite]
public class ProgressionTest
{
    private static Progression P => TestDb.Instance.Progression;

    [TestCase(0, Rank.E)]
    [TestCase(99, Rank.E)]
    [TestCase(100, Rank.D)]
    [TestCase(259, Rank.D)]
    [TestCase(260, Rank.C)]
    [TestCase(519, Rank.C)]
    [TestCase(520, Rank.B)]
    [TestCase(900, Rank.A)]
    [TestCase(1500, Rank.S)]
    [TestCase(2400, Rank.SS)]
    [TestCase(3600, Rank.SSS)]
    [TestCase(5200, Rank.SSSS)]
    [TestCase(7499, Rank.SSSS)]
    [TestCase(7500, Rank.Alvor)]
    [TestCase(999999, Rank.Alvor)]
    public void RankIndexUsesV2Thresholds(int totalXp, Rank expected)
        => AssertThat(P.RankEnumOf(totalXp)).IsEqual(expected);

    // Tabela da spec 02 §1.3: XP acumulado para atingir o nível e XP para o próximo.
    [TestCase(0, 1, 40)]
    [TestCase(40, 2, 47)]
    [TestCase(87, 3, 55)]
    [TestCase(142, 4, 65)]
    [TestCase(207, 5, 77)]
    [TestCase(284, 6, 91)]
    [TestCase(375, 7, 107)]
    [TestCase(482, 8, 126)]
    [TestCase(608, 9, 149)]
    [TestCase(757, 10, 176)]
    [TestCase(933, 11, 208)]
    [TestCase(1141, 12, 245)]
    [TestCase(1386, 13, 289)]
    [TestCase(1675, 14, 341)]
    [TestCase(2016, 15, 402)]
    [TestCase(2418, 16, 474)]
    [TestCase(2892, 17, 559)]
    [TestCase(3451, 18, 660)]
    [TestCase(4111, 19, 779)]
    [TestCase(4890, 20, 919)]
    [TestCase(5809, 21, 1084)]
    [TestCase(6893, 22, 1279)]
    public void LevelCurveMatchesSpecTable(int totalXp, int level, int need)
    {
        var info = P.LevelOf(totalXp);
        AssertThat(info.Level).IsEqual(level);
        AssertThat(info.Current).IsEqual(0);
        AssertThat(info.Need).IsEqual(need);
    }

    [TestCase]
    public void LevelKeepsLeftoverXp()
    {
        var info = P.LevelOf(86);
        AssertThat(info.Level).IsEqual(2);
        AssertThat(info.Current).IsEqual(46);
        AssertThat(info.Need).IsEqual(47);
        AssertThat(info.Progress).IsEqualApprox(46f / 47f, 1e-6f);
    }

    // HP base no limiar de cada Selo (spec 02 §1.3).
    [TestCase(0, 100)]
    [TestCase(100, 124)]
    [TestCase(260, 148)]
    [TestCase(520, 178)]
    [TestCase(900, 202)]
    [TestCase(1500, 232)]
    [TestCase(2400, 256)]
    [TestCase(3600, 286)]
    [TestCase(5200, 310)]
    [TestCase(7500, 334)]
    public void BaseMaxHpAddsRankAndLevel(int totalXp, int expectedHp)
        => AssertThat(P.BaseMaxHp(totalXp)).IsEqual(expectedHp);

    [TestCase]
    public void XpToNextRankCountsDown()
    {
        AssertThat(P.XpToNextRank(0)!.Value).IsEqual(100);
        AssertThat(P.XpToNextRank(250)!.Value).IsEqual(10);
        AssertThat(P.XpToNextRank(7500)).IsNull();
        AssertThat(P.NextRank(5200)!.Id).IsEqual("ALVOR");
    }

    [TestCase]
    public void ProgressToNextRankIsFractionOfCurrentSeal()
    {
        AssertThat(P.ProgressToNextRank(180)).IsEqual(0.5f);
        AssertThat(P.ProgressToNextRank(9000)).IsEqual(1f);
    }

    [TestCase]
    public void GainReportsRankAndLevelChanges()
    {
        var crossing = P.Gain(90, 20);
        AssertThat(crossing.TotalXp).IsEqual(110);
        AssertThat(crossing.PreviousRank).IsEqual(Rank.E);
        AssertThat(crossing.NewRank).IsEqual(Rank.D);
        AssertThat(crossing.RankedUp).IsTrue();
        AssertThat(crossing.LeveledUp).IsFalse();

        var levelOnly = P.Gain(30, 20);
        AssertThat(levelOnly.RankedUp).IsFalse();
        AssertThat(levelOnly.LeveledUp).IsTrue();
        AssertThat(levelOnly.Level).IsEqual(2);
    }

    [TestCase]
    public void BigGainCanSkipSeals()
        => AssertThat(P.Gain(0, 900).NewRank).IsEqual(Rank.A);

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void RejectsNegativeXp()
        => P.Gain(0, -1);
}
