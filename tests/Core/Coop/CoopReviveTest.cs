namespace Vespera.Tests.Core.Coop;

using GdUnit4;
using Vespera.Core.Coop;
using static GdUnit4.Assertions;

/// <summary>Reanimar (segurar 2 s perto do caído) e a regra do grupo (todos caídos = Lanterna).</summary>
[TestSuite]
public class CoopReviveTest
{
    private static ReviveTuning Tuning => new() { HoldSeconds = 2.0, DecayPerSecond = 1.0 };

    [TestCase]
    public void HoldingForTwoSecondsRevives()
    {
        var tracker = new ReviveTracker(Tuning);
        var completedAt = -1;
        for (var frame = 1; frame <= 200; frame++)
        {
            if (tracker.Tick(1.0 / 60.0, helperHolding: true))
            {
                completedAt = frame;
                break;
            }
        }

        AssertThat(completedAt).IsEqual(120);
        AssertThat(tracker.Completed).IsTrue();
        AssertThat(tracker.Fraction).IsEqualApprox(1.0, 1e-9);
        AssertThat(tracker.Tick(1.0, true)).IsFalse();
    }

    [TestCase]
    public void LettingGoDecaysTheProgress()
    {
        var tracker = new ReviveTracker(Tuning);
        tracker.Tick(1.5, helperHolding: true);
        AssertThat(tracker.Fraction).IsEqualApprox(0.75, 1e-9);
        tracker.Tick(1.0, helperHolding: false);
        AssertThat(tracker.Seconds).IsEqualApprox(0.5, 1e-9);
        tracker.Tick(5.0, helperHolding: false);
        AssertThat(tracker.Seconds).IsEqualApprox(0.0, 1e-9);
        AssertThat(tracker.Completed).IsFalse();
        tracker.Tick(1.9, true);
        AssertThat(tracker.Completed).IsFalse();
        tracker.Reset();
        AssertThat(tracker.Seconds).IsEqualApprox(0.0, 1e-9);
    }

    [TestCase]
    public void PartyWipesOnlyWhenEveryoneIsDown()
    {
        AssertThat(ReviveRules.Evaluate([HunterLife.Up, HunterLife.Down])).IsEqual(PartyStatus.Fighting);
        AssertThat(ReviveRules.Evaluate([HunterLife.Down, HunterLife.Up])).IsEqual(PartyStatus.Fighting);
        AssertThat(ReviveRules.Evaluate([HunterLife.Down, HunterLife.Down])).IsEqual(PartyStatus.Wiped);
        AssertThat(ReviveRules.Evaluate([HunterLife.Down])).IsEqual(PartyStatus.Wiped);
        AssertThat(ReviveRules.Evaluate([])).IsEqual(PartyStatus.Fighting);
    }

    [TestCase]
    public void OnlyAStandingHunterInReachCanRevive()
    {
        AssertThat(ReviveRules.CanRevive(HunterLife.Up, HunterLife.Down, 50, 80)).IsTrue();
        AssertThat(ReviveRules.CanRevive(HunterLife.Up, HunterLife.Down, 81, 80)).IsFalse();
        AssertThat(ReviveRules.CanRevive(HunterLife.Down, HunterLife.Down, 10, 80)).IsFalse();
        AssertThat(ReviveRules.CanRevive(HunterLife.Up, HunterLife.Up, 10, 80)).IsFalse();
    }
}
