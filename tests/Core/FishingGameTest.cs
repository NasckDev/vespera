namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class FishingGameTest
{
    private static FishingGame Game(params double[] rolls) =>
        new(new NumRange(1, 3), zoneWidth: 0.2, sweepSeconds: 1, maxSweeps: 3, new FixedRandom(rolls));

    [TestCase]
    public void BitesAfterTheRolledDelay()
    {
        var game = Game(0.5, 0.0); // mordida em 2 s; faixa em 0,08
        AssertThat(game.BiteAt).IsEqual(2.0);
        AssertThat(game.ZoneStart).IsEqual(0.08);
        AssertThat(game.Update(1.9)).IsFalse();
        AssertThat(game.State).IsEqual(FishingState.Waiting);
        AssertThat(game.Update(0.2)).IsTrue();
        AssertThat(game.State).IsEqual(FishingState.Biting);
    }

    [TestCase]
    public void PullingEarlyScaresTheFish()
    {
        var game = Game(0.5, 0.0);
        game.Update(1);
        AssertThat(game.Pull()).IsEqual(FishingState.Escaped);
        AssertThat(game.IsOver).IsTrue();
    }

    [TestCase]
    public void PullingWithTheNeedleInTheZoneCatches()
    {
        var game = Game(0.0, 0.5); // mordida em 1 s; faixa em 0,08 + 0,5·(0,72−0,08) = 0,4
        AssertThat(game.ZoneStart).IsEqualApprox(0.4, 1e-9);
        game.Update(1.0);
        game.Update(0.5); // agulha em 0,5 (dentro de 0,4–0,6)
        AssertThat(game.Needle).IsEqualApprox(0.5, 1e-9);
        AssertThat(game.NeedleInZone).IsTrue();
        AssertThat(game.Pull()).IsEqual(FishingState.Caught);
    }

    [TestCase]
    public void NeedleGoesBackAndForthAndTheFishLeaves()
    {
        var game = Game(0.0, 0.0);
        game.Update(1.0);
        game.Update(1.5); // ida + meia volta → 0,5
        AssertThat(game.Needle).IsEqualApprox(0.5, 1e-9);
        AssertThat(game.Pull()).IsEqual(FishingState.Escaped); // faixa em 0,08–0,28

        var slow = Game(0.0, 0.0);
        slow.Update(1.0);
        AssertThat(slow.Update(3.01)).IsTrue();
        AssertThat(slow.State).IsEqual(FishingState.Escaped);
    }
}
