namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Hunt;
using static GdUnit4.Assertions;

/// <summary>Sopa da Nilza, mapa da Lis e suportes de arma (ROTEIRO-V2 §3 e §4.3).</summary>
[TestSuite]
public class RunPerksTest
{
    [TestCase]
    public void SoupAddsTenPercentOfMaxHp()
    {
        AssertThat(RunPerks.SoupBonus(100)).IsEqual(10);
        AssertThat(RunPerks.SoupBonus(125)).IsEqual(13);
        AssertThat(RunPerks.SoupBonus(4)).IsEqual(1);
        AssertThat(RunPerks.SoupBonus(0)).IsEqual(0);
    }

    [TestCase]
    public void StandsOfferBowThenHammerOnlyBeforeTheExam()
    {
        AssertThat(RunPerks.StandWeapon(0, guardianHunt: false)).IsEqual("bow");
        AssertThat(RunPerks.StandWeapon(1, guardianHunt: false)).IsEqual("hammer");
        AssertThat(RunPerks.StandWeapon(2, guardianHunt: false)).IsNull();
        AssertThat(RunPerks.StandWeapon(1, guardianHunt: true)).IsNull();
    }

    [TestCase]
    public void MapShowsEveryRoomAndMarksTreasureAndCurrent()
    {
        var line = RunPerks.MapLine([RoomKind.Combat, RoomKind.Treasure, RoomKind.Rest, RoomKind.Guardian], 1);
        AssertThat(line).IsEqual("Mapa da Lis: 1 Combate · [2 TESOURO] · 3 Descanso · 4 Guardião");
    }
}
