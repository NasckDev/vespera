namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using static GdUnit4.Assertions;

/// <summary>Golpe combinado dos Guardiões em dupla (ROTEIRO-V2 §13, ato VII).</summary>
[TestSuite]
public class DuoStrikeTest
{
    private static readonly DuoInput Close = new(true, true, true, 300, true);

    private static DuoEvent RunUntilEvent(DuoStrike duo, DuoInput input, double maxSeconds = 40)
    {
        for (var t = 0.0; t < maxSeconds; t += 0.05)
        {
            var e = duo.Tick(0.05, input);
            if (e != DuoEvent.None)
            {
                return e;
            }
        }

        return DuoEvent.None;
    }

    [TestCase]
    public void WaitsTheFirstDelayThenWarnsLongerThanTheSingleWarning()
    {
        var duo = new DuoStrike();
        AssertThat(duo.Tick(DuoStrike.FirstDelay - 0.5, Close)).IsEqual(DuoEvent.None);
        AssertThat(duo.Warning).IsFalse();
        AssertThat(duo.Tick(0.6, Close)).IsEqual(DuoEvent.WarningStarted);
        AssertThat(duo.Warning).IsTrue();
        AssertThat(DuoStrike.WarnSeconds).IsGreater(0.4 * 2);
    }

    [TestCase]
    public void StrikesOnlyAfterTheFullWarningWindow()
    {
        var duo = new DuoStrike();
        duo.SkipDelay();
        AssertThat(duo.Tick(0.016, Close)).IsEqual(DuoEvent.WarningStarted);
        AssertThat(duo.Tick(DuoStrike.WarnSeconds - 0.1, Close)).IsEqual(DuoEvent.None);
        AssertThat(duo.Strikes).IsEqual(0);
        AssertThat(duo.Tick(0.2, Close)).IsEqual(DuoEvent.Strike);
        AssertThat(duo.Strikes).IsEqual(1);
        AssertThat(duo.Warning).IsFalse();
    }

    [TestCase]
    public void NeverWarnsWhenOneIsDeadFarApartNotReadyOrNoHunterStands()
    {
        foreach (var input in new[]
        {
            new DuoInput(true, false, false, 300, true),
            new DuoInput(true, true, false, 300, true),
            new DuoInput(true, true, true, DuoStrike.MaxGap + 1, true),
            new DuoInput(true, true, true, 300, false),
        })
        {
            var duo = new DuoStrike();
            AssertThat(RunUntilEvent(duo, input, 60)).IsEqual(DuoEvent.None);
        }
    }

    [TestCase]
    public void ClockStopsWhileTheLinkIsNotPossible()
    {
        var duo = new DuoStrike();
        var far = new DuoInput(true, true, true, DuoStrike.MaxGap + 50, true);
        duo.Tick(30, far);
        AssertThat(duo.Remaining).IsEqual(DuoStrike.FirstDelay);
    }

    [TestCase]
    public void CancelsWhenOneFallsDuringTheWarningAndWaitsTheCooldown()
    {
        var duo = new DuoStrike();
        duo.SkipDelay();
        duo.Tick(0.016, Close);
        AssertThat(duo.Warning).IsTrue();
        AssertThat(duo.Tick(0.3, new DuoInput(true, false, false, 300, true))).IsEqual(DuoEvent.Cancelled);
        AssertThat(duo.Warning).IsFalse();
        AssertThat(duo.Strikes).IsEqual(0);
        AssertThat(duo.Remaining).IsEqual(DuoStrike.Cooldown);
    }

    [TestCase]
    public void CooldownSeparatesTwoStrikes()
    {
        var duo = new DuoStrike();
        duo.SkipDelay();
        AssertThat(RunUntilEvent(duo, Close)).IsEqual(DuoEvent.WarningStarted);
        AssertThat(RunUntilEvent(duo, Close)).IsEqual(DuoEvent.Strike);
        var seconds = 0.0;
        var next = DuoEvent.None;
        while (next == DuoEvent.None && seconds < 60)
        {
            next = duo.Tick(0.05, Close);
            seconds += 0.05;
        }

        AssertThat(next).IsEqual(DuoEvent.WarningStarted);
        AssertThat(seconds).IsGreaterEqual(DuoStrike.Cooldown - 0.1);
    }

    [TestCase]
    public void DamageIsTheAverageTimesTheMultiplierAndStaysWithinTheBossStrikeRange()
    {
        AssertThat(DuoStrike.Damage(10, 20)).IsEqualApprox(15 * DuoStrike.DamageMult, 0.0001);
        AssertThat(DuoStrike.Damage(10, 10)).IsEqualApprox(10 * DuoStrike.DamageMult, 0.0001);

        // Os golpes de chefe do jogo vão de x0,6 a x1,3 do dano base: o combinado não passa do teto.
        AssertThat(DuoStrike.DamageMult).IsLessEqual(1.3);
        AssertThat(DuoStrike.DamageMult).IsGreaterEqual(1.0);
    }

    [TestCase]
    public void HitZoneIsTheLaneBetweenThemAndThePulseAtEachFoot()
    {
        // A em (0,0), B em (300,0); faixa de 90 (meia 45), pulso de 120.
        AssertThat(DuoStrike.Hits(150, 0, 0, 0, 300, 0, 0, 1)).IsTrue();
        AssertThat(DuoStrike.Hits(150, 44, 0, 0, 300, 0, 0, 1)).IsTrue();
        AssertThat(DuoStrike.Hits(150, 60, 0, 0, 300, 0, 0, 1)).IsFalse();
        AssertThat(DuoStrike.Hits(0, 110, 0, 0, 300, 0, 0, 1)).IsTrue();
        AssertThat(DuoStrike.Hits(300, 130, 0, 0, 300, 0, 0, 1)).IsFalse();
        AssertThat(DuoStrike.Hits(-100, 0, 0, 0, 300, 0, 0, 1)).IsTrue();
        AssertThat(DuoStrike.Hits(-200, 0, 0, 0, 300, 0, 0, 1)).IsFalse();
        // O corpo do caçador alarga a zona; a escala do ator a escala.
        AssertThat(DuoStrike.Hits(150, 60, 0, 0, 300, 0, 16, 1)).IsTrue();
        AssertThat(DuoStrike.Hits(150, 60, 0, 0, 300, 0, 0, 2)).IsTrue();
    }
}
