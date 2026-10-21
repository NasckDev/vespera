namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Incursão na Brecha (spec 01 §1.9, spec 02 §3.14), drops com ímã (§3.12) e Ecos/Supremos/Lanterna.</summary>
[TestSuite]
public class BrechaRunTest
{
    private static BrechaRun NewRun(bool easy, int waves = 2)
    {
        var theme = TestDb.Instance.Themes["fish"];
        var list = new List<IReadOnlyList<string>>();
        for (var i = 0; i < waves; i++)
        {
            list.Add(["wolf", "fishm"]);
        }

        return new BrechaRun(1, 0, theme, 0, "primavera", list, easy);
    }

    [TestCase]
    public void FirstWaveAfterFourSecondsOnTheEasyBrechaThenOnePointSixBetween()
    {
        var run = NewRun(easy: true);
        AssertThat(run.Update(3.9, 0, false)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, false)).IsEqual(BrechaRunEvent.SpawnWave);
        AssertThat(run.Wave).IsEqual(1);
        AssertThat(run.Update(5, 3, false)).IsEqual(BrechaRunEvent.None); // monstros vivos: espera
        AssertThat(run.Update(1.5, 0, false)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, false)).IsEqual(BrechaRunEvent.SpawnWave);
        AssertThat(run.Wave).IsEqual(2);
        AssertThat(run.Update(0.1, 0, false)).IsEqual(BrechaRunEvent.DoorOpened);
        AssertThat(run.Cleared).IsTrue();
        AssertThat(run.Update(0.1, 0, false)).IsEqual(BrechaRunEvent.None);
    }

    [TestCase]
    public void NormalBrechaStartsAfterTwoAndAHalfSeconds()
    {
        var run = NewRun(easy: false);
        AssertThat(run.Update(2.4, 0, false)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, false)).IsEqual(BrechaRunEvent.SpawnWave);
    }

    [TestCase]
    public void CoreChamberSpawnsTheBossAfterOnePointEightAndExitPortalAfterTheKill()
    {
        var run = NewRun(easy: false, waves: 0);
        AssertThat(run.Update(0.1, 0, false)).IsEqual(BrechaRunEvent.DoorOpened);
        run.EnterCore();
        AssertThat(run.Room).IsEqual(BrechaRooms.Core);
        AssertThat(run.Update(1.7, 0, false)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, false)).IsEqual(BrechaRunEvent.SpawnBoss);
        AssertThat(run.Update(5, 1, false)).IsEqual(BrechaRunEvent.None);
        run.DefeatBoss();
        AssertThat(run.Update(1.3, 0, false)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, false)).IsEqual(BrechaRunEvent.ExitPortal);
    }

    [TestCase]
    public void CathedralAwakensTheAltarInPlace()
    {
        var run = NewRun(easy: false, waves: 0);
        AssertThat(run.Update(0.1, 0, true)).IsEqual(BrechaRunEvent.AltarAwakens);
        AssertThat(run.Room).IsEqual(BrechaRooms.Core);
        AssertThat(run.Update(2.1, 0, true)).IsEqual(BrechaRunEvent.None);
        AssertThat(run.Update(0.2, 0, true)).IsEqual(BrechaRunEvent.SpawnBoss);
    }

    [TestCase]
    public void SummaryMatchesThePrototypeText()
    {
        var bag = new RunBag { Lumes = 12 };
        bag.Materials["presa"] = 2;
        bag.Materials["estilhaco"] = 1;
        bag.Gear.Add("capuz");
        AssertThat(BrechaRun.Summary(bag)).IsEqual("+12 Lumes · 3 materiais · 1 equipamentos");
        bag.Lanterns.Add("Nina");
        AssertThat(BrechaRun.Summary(bag)).IsEqual("+12 Lumes · 3 materiais · 1 equipamentos · 1 lanterna(s)");
    }

    [TestCase]
    public void DropBouncesThenMagnetPullsAndCollects()
    {
        var k = PickupMotion.Drop(100, 100, new ConstantRandom(0.5));
        AssertThat(k.Y).IsEqual(106.0);
        AssertThat(k.Z).IsEqual(8.0);
        AssertThat(k.Vz).IsEqual(70.0);
        for (var i = 0; i < 60; i++)
        {
            k.Step(1.0 / 60);
        }

        AssertThat(k.Z).IsEqual(0.0);

        // antes de 0,45 s não atrai
        var early = PickupMotion.Drop(0, 0, new ConstantRandom(0.5));
        AssertThat(early.Magnet(20, 6, 0.016)).IsFalse();
        AssertThat(early.X).IsEqual(0.0);

        // jogador a 30 u: puxa até coletar (< 9 u)
        var collected = false;
        var px = k.X + 30;
        for (var i = 0; i < 120 && !collected; i++)
        {
            k.Step(1.0 / 60);
            collected = k.Magnet(px, k.Y, 1.0 / 60);
        }

        AssertThat(collected).IsTrue();
    }

    [TestCase]
    public void PickupOutsideMagnetRadiusStays()
    {
        var k = PickupMotion.Still(0, 0);
        k.Step(1);
        AssertThat(k.Magnet(60, 0, 0.016)).IsFalse();
        AssertThat(k.X).IsEqual(0.0);
    }

    [TestCase]
    public void AbilityFormulasFollowThePrototype()
    {
        AssertThat(EcoNumbers.VotiveDamage(12)).IsEqual(11); // round(12×0.6 + 4) = round(11.2)
        AssertThat(EcoNumbers.LureDamage(10)).IsEqual(34.0);
        AssertThat(EcoNumbers.ChainDamage(10)).IsEqual(23.0);
        AssertThat(EcoNumbers.ChargeDamage(10)).IsEqual(30.0);
        AssertThat(UltimateNumbers.SwordDamage(10)).IsEqual(34.0);
        AssertThat(UltimateNumbers.BowDamage(10)).IsEqual(10.5);
        AssertThat(UltimateNumbers.StaffDamage(10)).IsEqual(60.0);
        AssertThat(UltimateNumbers.HammerDamage(10)).IsEqual(24.0);
        AssertThat(UltimateNumbers.DagDamage(10)).IsEqual(34.0);
        AssertThat(UltimateNumbers.HammerWaveRadiusAt(0, 0.45)!.Value).IsEqualApprox(90.0, 1e-9);
        AssertThat(UltimateNumbers.HammerWaveRadiusAt(1, 0.2) is null).IsTrue();
        AssertThat(UltimateNumbers.HammerWaveRadiusAt(2, 1.3) is null).IsTrue(); // 0,6/0,45×90 = 120 > 95
    }

    [TestCase]
    public void EcoSlotsAreEquippedThenTheFirstOther()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        AssertThat(EcoSlots.Slot(save, 0)).IsNull();
        AssertThat(EcoSlots.Slot(save, 1)).IsNull();
        save.Ecos.Add("abissal");
        save.Ecos.Add("uivo");
        save.EquippedEco = "uivo";
        AssertThat(EcoSlots.Slot(save, 0)).IsEqual("uivo");
        AssertThat(EcoSlots.Slot(save, 1)).IsEqual("abissal");
    }

    [TestCase]
    public void LostLanternWaitsInTheSameRoomOfTheSameBrechaOrTheme()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        save.LostLantern = new LostLantern { BrechaId = 3, ThemeId = "fish", Room = BrechaRooms.Theme };
        AssertThat(LanternPlacement.IsHere(save, 3, "crypt", BrechaRooms.Theme)).IsTrue();
        AssertThat(LanternPlacement.IsHere(save, 9, "fish", BrechaRooms.Theme)).IsTrue();
        AssertThat(LanternPlacement.IsHere(save, 9, "fish", BrechaRooms.Core)).IsFalse();
        AssertThat(LanternPlacement.IsHere(save, 9, "crypt", BrechaRooms.Theme)).IsFalse();
    }
}
