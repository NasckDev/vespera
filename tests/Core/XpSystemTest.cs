namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Ranks;
using static GdUnit4.Assertions;

[TestSuite]
public class XpSystemTest
{
    [TestCase]
    public void StartsAtRankE()
    {
        var xp = new XpSystem(RankTable.Default);
        AssertThat(xp.TotalXp).IsEqual(0);
        AssertThat(xp.Rank).IsEqual(Rank.E);
    }

    [TestCase]
    public void GainWithinRankDoesNotRankUp()
    {
        var result = new XpSystem(RankTable.Default).Add(40);
        AssertThat(result.TotalXp).IsEqual(40);
        AssertThat(result.RankedUp).IsFalse();
    }

    [TestCase]
    public void CrossingThresholdRanksUp()
    {
        var xp = new XpSystem(RankTable.Default, totalXp: 90);
        var result = xp.Add(20);
        AssertThat(result.PreviousRank).IsEqual(Rank.E);
        AssertThat(result.NewRank).IsEqual(Rank.D);
        AssertThat(result.RankedUp).IsTrue();
    }

    [TestCase]
    public void BigGainCanSkipRanks()
    {
        var result = new XpSystem(RankTable.Default).Add(700);
        AssertThat(result.NewRank).IsEqual(Rank.B);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void RejectsNegativeXp()
        => new XpSystem(RankTable.Default).Add(-1);
}
