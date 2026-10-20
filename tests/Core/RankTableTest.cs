namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Ranks;
using static GdUnit4.Assertions;

[TestSuite]
public class RankTableTest
{
    [TestCase(0, Rank.E)]
    [TestCase(99, Rank.E)]
    [TestCase(100, Rank.D)]
    [TestCase(299, Rank.D)]
    [TestCase(300, Rank.C)]
    [TestCase(700, Rank.B)]
    [TestCase(1500, Rank.A)]
    [TestCase(3000, Rank.S)]
    [TestCase(6000, Rank.SS)]
    [TestCase(12000, Rank.SSS)]
    [TestCase(25000, Rank.Alvor)]
    [TestCase(999999, Rank.Alvor)]
    public void RankForUsesClaudeMdThresholds(int totalXp, Rank expected)
        => AssertThat(RankTable.Default.RankFor(totalXp)).IsEqual(expected);

    [TestCase]
    public void XpToNextCountsDownToNextRank()
    {
        AssertThat(RankTable.Default.XpToNext(0)!.Value).IsEqual(100);
        AssertThat(RankTable.Default.XpToNext(250)!.Value).IsEqual(50);
    }

    [TestCase]
    public void XpToNextIsNullAtAlvor()
        => AssertThat(RankTable.Default.XpToNext(25000)).IsNull();

    [TestCase]
    public void ProgressInRankIsFractionOfCurrentRank()
    {
        AssertThat(RankTable.Default.ProgressInRank(150)).IsEqual(0.25f);
        AssertThat(RankTable.Default.ProgressInRank(30000)).IsEqual(1f);
    }

    [TestCase]
    public void MaxHpGrowsTwentyPerRank()
    {
        AssertThat(RankTable.Default.MaxHpFor(Rank.E, 100)).IsEqual(100);
        AssertThat(RankTable.Default.MaxHpFor(Rank.C, 100)).IsEqual(140);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void RejectsTableWithWrongRankCount()
        => _ = new RankTable([0, 100, 300]);

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void RejectsNonIncreasingThresholds()
        => _ = new RankTable([0, 100, 100, 700, 1500, 3000, 6000, 12000, 25000]);

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void RejectsTableNotStartingAtZero()
        => _ = new RankTable([10, 100, 300, 700, 1500, 3000, 6000, 12000, 25000]);
}
