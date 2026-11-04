namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// Ato VI (ROTEIRO-V2 §13, "A Sopa da Véspera"): a Sopa da Véspera levanta o caçador uma vez por caçada e a Odete dá a
/// arma de Duas Vésperas da classe (evento <c>GiveLegendaryWeapon</c>).
/// </summary>
[TestSuite]
public class ActSixGiftsTest
{
    private static WeaponForge Forge => new(AdvTestData.Catalog, new RankLadder(AdvTestData.Catalog));

    [TestCase("sword", "Lâmina de Duas Vésperas")]
    [TestCase("bow", "Arco de Duas Vésperas")]
    [TestCase("hammer", "Martelo de Duas Vésperas")]
    public void OdeteGivesTheTwoEvesWeaponOfTheClass(string classId, string name)
    {
        var progress = new HunterProgress();
        progress.WeaponLevels[classId] = 9;

        var weapon = Forge.Grant(progress, classId, Rank.SS);

        AssertThat(weapon.Name).IsEqual(name);
        AssertThat(weapon.TierRank).IsEqual(Rank.SS);
        AssertThat(progress.WeaponLevels[classId]).IsEqual(Forge.FirstLevelOf(Rank.SS));
        AssertThat(weapon.Damage).IsGreater(Forge.Stats(classId, 9).Damage);
    }

    [TestCase]
    public void TheGiftNeverLowersAStrongerWeapon()
    {
        var progress = new HunterProgress();
        progress.WeaponLevels["sword"] = 16;

        var weapon = Forge.Grant(progress, "sword", Rank.SS);

        AssertThat(weapon.Level).IsEqual(16);
        AssertThat(progress.WeaponLevels["sword"]).IsEqual(16);
    }

    [TestCase]
    public void TheGiftCostsNothing()
    {
        var progress = new HunterProgress { Fragmentos = 5 };

        Forge.Grant(progress, "hammer", Rank.SS);

        AssertThat(progress.Fragmentos).IsEqual(5);
        AssertThat(progress.Materials).IsEmpty();
    }

    [TestCase]
    public void SoupRaisesOnlyOncePerHuntAndOnlyAfterNilzaServesIt()
    {
        AssertThat(RunPerks.CanRise(unlocked: false, usedThisRun: false)).IsFalse();
        AssertThat(RunPerks.CanRise(unlocked: true, usedThisRun: false)).IsTrue();
        AssertThat(RunPerks.CanRise(unlocked: true, usedThisRun: true)).IsFalse();
    }

    [TestCase]
    public void SoupRaisesWithHalfTheLife()
    {
        AssertThat(RunPerks.SecondLifeHp(200)).IsEqual(100);
        AssertThat(RunPerks.SecondLifeHp(1)).IsEqual(1);
        AssertThat(RunPerks.SecondLifeHp(0)).IsEqual(0);
    }
}
