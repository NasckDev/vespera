namespace Vespera.Tests.Core.Coop;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Coop;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Saque individual (sorteio e dono por jogador), XP compartilhado e a Lanterna de cada um na queda geral.</summary>
[TestSuite]
public class CoopLootTest
{
    private const string Hood = "mercado";

    private static List<ProgressionEngine> Party() => [AdvTestData.NewEngine("sword", seed: 3), AdvTestData.NewEngine("bow", seed: 77)];

    [TestCase]
    public void OnlyTheOwnerCollects()
    {
        AssertThat(CoopLoot.CanCollect(0, 0)).IsTrue();
        AssertThat(CoopLoot.CanCollect(1, 1)).IsTrue();
        AssertThat(CoopLoot.CanCollect(0, 1)).IsFalse();
        AssertThat(CoopLoot.CanCollect(1, 0)).IsFalse();
    }

    [TestCase]
    public void EachPlayerGetsASeparateRollIntoTheirOwnProgress()
    {
        var party = Party();
        foreach (var e in party)
        {
            e.BeginRun(Hood);
        }

        var grants = new List<OwnedLoot>();
        for (var i = 0; i < 30; i++)
        {
            grants.AddRange(CoopLoot.GrantEach(party, Rank.E, LootSource.Chest));
        }

        AssertThat(grants.Count).IsEqual(60);
        for (var slot = 0; slot < party.Count; slot++)
        {
            var mine = grants.Where(g => g.OwnerSlot == slot).ToList();
            AssertThat(mine.Count).IsEqual(30);
            AssertThat(party[slot].RunFragmentos).IsEqual(mine.Sum(g => g.Grant.FragmentosGained));
            var materials = mine.SelectMany(g => g.Grant.Loot.Entries).Where(e => e.Kind == LootEntryKind.Material).Sum(e => e.Amount);
            AssertThat(party[slot].Progress.Materials.Values.Sum()).IsEqual(materials);
        }

        // Sementes diferentes: os sorteios dos dois não são o mesmo saque copiado.
        var p1 = grants.Where(g => g.OwnerSlot == 0).Select(g => g.Grant.FragmentosGained).ToList();
        var p2 = grants.Where(g => g.OwnerSlot == 1).Select(g => g.Grant.FragmentosGained).ToList();
        AssertThat(p1.SequenceEqual(p2)).IsFalse();
    }

    [TestCase]
    public void XpIsSharedInFull()
    {
        var party = Party();
        var results = CoopLoot.ShareXp(party, 40);
        AssertThat(results.Count).IsEqual(2);
        AssertThat(party[0].Progress.Xp).IsEqual(40);
        AssertThat(party[1].Progress.Xp).IsEqual(40);
        AssertThat(results.All(r => r.Result.Amount == 40)).IsTrue();
    }

    [TestCase]
    public void WipeLeavesEachPlayersFragmentsInTheirOwnLantern()
    {
        var party = Party();
        foreach (var e in party)
        {
            e.BeginRun(Hood);
        }

        party[0].Progress.RunFragmentos = 7;
        party[1].Progress.RunFragmentos = 3;
        var ends = CoopLoot.EndRunEach(party, survived: false);
        AssertThat(ends.All(e => e is { Survived: false })).IsTrue();
        AssertThat(party[0].LanternFragmentos(Hood)).IsEqual(7);
        AssertThat(party[1].LanternFragmentos(Hood)).IsEqual(3);
        AssertThat(party[0].Progress.Fragmentos).IsEqual(0);
        AssertThat(party[1].Progress.Fragmentos).IsEqual(0);

        // Na próxima entrada cada um recupera só a própria Lanterna.
        AssertThat(party[0].BeginRun(Hood)).IsEqual(7);
        AssertThat(party[1].BeginRun(Hood)).IsEqual(3);
        CoopLoot.EndRunEach(party, survived: true);
        AssertThat(party[0].Progress.Fragmentos).IsEqual(7);
        AssertThat(party[1].Progress.Fragmentos).IsEqual(3);
    }
}
