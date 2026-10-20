namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Rules;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Estações, relógio e regras de Brecha (spec 01 §1.9, §7; spec 02 §3.14; §6.3 itens 23, 25, 26).</summary>
[TestSuite]
public class CalendarAndBrechaTest
{
    private static SeasonCalendar Calendar => new(TestDb.Instance.SeasonData);

    private static BrechaRules Brecha => new(TestDb.Instance);

    [TestCase(1, "Outono")]
    [TestCase(3, "Outono")]
    [TestCase(4, "Inverno")]
    [TestCase(7, "Primavera")]
    [TestCase(10, "Verão")]
    [TestCase(12, "Verão")]
    [TestCase(13, "Outono")]
    public void SeasonOfDay(int day, string season)
        => AssertThat(Calendar.SeasonOf(day).Name).IsEqual(season);

    [TestCase]
    public void ClockAdvancesAndRollsDay()
    {
        AssertThat(Calendar.Advance(990, 30)).IsEqual(1020.0);
        AssertThat(Calendar.IsNewDayDue(1799.9)).IsFalse();
        AssertThat(Calendar.IsNewDayDue(1800)).IsTrue();
        AssertThat(Calendar.NewDayClock).IsEqual(960.0);
    }

    [TestCase]
    public void VesperaAndClockFormat()
    {
        AssertThat(Calendar.IsVespera(990)).IsTrue();      // 16:30
        AssertThat(Calendar.IsVespera(1139)).IsTrue();     // 18:59
        AssertThat(Calendar.IsVespera(1140)).IsFalse();    // 19:00
        AssertThat(SeasonCalendar.FormatClock(990)).IsEqual("16:30");
        AssertThat(SeasonCalendar.FormatClock(1500.7)).IsEqual("01:00");
        AssertThat(SeasonCalendar.FormatTimer(61.2)).IsEqual("1:02");
        AssertThat(SeasonCalendar.FormatTimer(300)).IsEqual("5:00");
        AssertThat(SeasonCalendar.HourOfDay(1500)).IsEqual(1);
        AssertThat(SeasonCalendar.RawHour(1500)).IsEqual(25);
    }

    [TestCase]
    public void WalkerScheduleUsesRawHours()
    {
        var lauro = TestDb.Instance.Npcs.Walkers.Single(w => w.Id == "lauro"); // 17–27h
        AssertThat(SeasonCalendar.IsWalkerPresent(lauro, 16 * 60)).IsFalse();
        AssertThat(SeasonCalendar.IsWalkerPresent(lauro, 17 * 60)).IsTrue();
        AssertThat(SeasonCalendar.IsWalkerPresent(lauro, 26 * 60)).IsTrue();
        AssertThat(SeasonCalendar.IsWalkerPresent(lauro, 27 * 60)).IsFalse();
    }

    [TestCase]
    public void FirstBrechaIsAlwaysFishWithoutBonus()
    {
        var rng = new FixedRandom();
        var theme = Brecha.ChooseTheme(0, [], first: true, rng);
        AssertThat(theme.Id).IsEqual("fish");
        AssertThat(Brecha.RollRank(theme, 0, first: true, rng)).IsEqual(0);
        AssertThat(Brecha.RollTimer(first: true, rng)).IsEqual(300.0);
        AssertThat(Brecha.RollNpcCloseAt(first: true, rng)).IsNull();
        AssertThat(rng.Consumed).IsEqual(0);
    }

    [TestCase]
    public void RankIsClampedMaxOfThemeAndSealPlusBonus()
    {
        var db = TestDb.Instance;
        AssertThat(Brecha.RollRank(db.Themes["cathedral"], 0, first: false, new ConstantRandom(0.5))).IsEqual(4);
        AssertThat(Brecha.RollRank(db.Themes["fish"], 2, first: false, new ConstantRandom(0.24))).IsEqual(3);
        AssertThat(Brecha.RollRank(db.Themes["fish"], 9, first: false, new ConstantRandom(0))).IsEqual(9);
    }

    [TestCase]
    public void ThemesUnlockOneSealAhead()
    {
        AssertThat(Brecha.AvailableThemes(0).Select(t => t.Id)).ContainsExactly("fish", "crypt");
        AssertThat(Brecha.AvailableThemes(3).Select(t => t.Id)).ContainsExactly("fish", "crypt", "mines", "garden", "cathedral");

        var theme = Brecha.ChooseTheme(0, ["fish"], first: false, new ConstantRandom(0));
        AssertThat(theme.Id).IsEqual("crypt");
        var anyWhenAllOpen = Brecha.ChooseTheme(0, ["fish", "crypt"], first: false, new ConstantRandom(0));
        AssertThat(anyWhenAllOpen.Id).IsEqual("fish");
    }

    [TestCase]
    public void TimerAndNpcClose()
    {
        AssertThat(Brecha.RollTimer(first: false, new ConstantRandom(0.5))).IsEqual(240.0);
        AssertThat(Brecha.RollNpcCloseAt(first: false, new FixedRandom(0.54, 0))!.Value).IsEqual(25.0);
        AssertThat(Brecha.RollNpcCloseAt(first: false, new FixedRandom(0.55))).IsNull();
        AssertThat(Brecha.InitialOpenDelay(newGame: true)).IsEqual(16.0);
        AssertThat(Brecha.InitialOpenDelay(newGame: false)).IsEqual(6.0);
        AssertThat(Brecha.RollOpenDelay(new ConstantRandom(0))).IsEqual(80.0);
    }

    [TestCase]
    public void EntryAllowedUpToOneSealAbove()
    {
        AssertThat(Brecha.CanEnter(1, 0)).IsTrue();
        AssertThat(Brecha.CanEnter(2, 0)).IsFalse();
        AssertThat(Brecha.CanEnter(0, 3)).IsTrue();
    }

    [TestCase]
    public void BossBonusAndInvasion()
    {
        AssertThat(Brecha.BossBonusLumes(0)).IsEqual(40);
        AssertThat(Brecha.BossBonusLumes(3)).IsEqual(115);
        AssertThat(Brecha.BossXp).IsEqual(60);
        AssertThat(Brecha.Invasion(0)).IsEqual(new InvasionSize(5, true, "wolfE"));
        AssertThat(Brecha.Invasion(2).Mobs).IsEqual(7);
        AssertThat(Brecha.Invasion(9).Mobs).IsEqual(9);
        AssertThat(Brecha.InvasionReward).IsEqual((30, 30));
    }

    [TestCase]
    public void EasyWavesAreTwoAndThree()
    {
        var fish = TestDb.Instance.Themes["fish"];
        var waves = Brecha.GenerateWaves(fish, 3, easy: true, new ConstantRandom(0.1));
        AssertThat(waves.Count).IsEqual(2);
        AssertThat(waves[0]).ContainsExactly("fishm", "fishm");
        AssertThat(waves[1]).ContainsExactly("fishm", "fishm", "fishm");
    }

    [TestCase]
    public void NormalWavesGrowAndGetExtraAndElite()
    {
        var fish = TestDb.Instance.Themes["fish"];
        // rk 0 → 2 ondas. Onda 0: RI(0,1)=0 → 3 mobs. Onda 1: RI=1 → 5 mobs + extra; elite com 0.49.
        var rng = new FixedRandom(0.0, 0.1, 0.9, 0.55, 0.99, 0.1, 0.1, 0.56, 0.1, 0.1, 0.49);
        var waves = Brecha.GenerateWaves(fish, 0, easy: false, rng);
        AssertThat(waves.Count).IsEqual(2);
        AssertThat(waves[0]).ContainsExactly("fishm", "wolf", "fishm");
        AssertThat(waves[1]).ContainsExactly("fishm", "fishm", "wolf", "fishm", "fishm", "fishm", "wolfE");
        AssertThat(rng.Consumed).IsEqual(11);

        var three = Brecha.GenerateWaves(fish, 1, easy: false, new ConstantRandom(0.6));
        AssertThat(three.Count).IsEqual(3);
        AssertThat(three[2].Count).IsEqual(3 + 2 + 1 + 1); // 3+i+RI(0,1)=1, + extra; 0.6 ≥ 0.5 → sem elite
        AssertThat(three[2][^1]).IsEqual("fishm");
    }

    [TestCase]
    public void PickMobFallsBackToLastMobOfTheme()
    {
        var crypt = TestDb.Instance.Themes["crypt"];
        AssertThat(BrechaRules.PickMob(crypt, new ConstantRandom(0.4))).IsEqual("cnev");
        AssertThat(BrechaRules.PickMob(crypt, new ConstantRandom(0.41))).IsEqual("coss");
        AssertThat(BrechaRules.PickMob(crypt, new ConstantRandom(0.9999999999))).IsEqual("cvel");
    }

    [TestCase]
    public void TitleBySeason()
        => AssertThat(BrechaRules.Title(TestDb.Instance.Themes["fish"], "inverno")).IsEqual("Maré Congelada");
}
