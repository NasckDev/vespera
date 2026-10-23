namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Loot por rank da Brecha: 80% rank R, 15% rank R+1, 5% raro de R com afixo; Guardião garante material e 30% de peça.</summary>
[TestSuite]
public class LootRollerTest
{
    private const int Rolls = 10_000;

    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    private static LootRoller Roller => new(Catalog);

    [TestCase(Rank.E)]
    [TestCase(Rank.C)]
    [TestCase(Rank.SSS)]
    public void RankSplitIsEightyFifteenFiveWithinTolerance(Rank rank)
    {
        var random = new SeededRandom(2026);
        var next = Roller.NextRank(rank);
        int same = 0, ahead = 0, rare = 0;
        for (var i = 0; i < Rolls; i++)
        {
            var entry = Roller.RollOne(rank, random);
            if (entry.Rare)
            {
                rare++;
                AssertThat(entry.Item!.Rank).IsEqual(rank);
                AssertThat(entry.Item.AffixId).IsNotNull();
            }
            else if (entry.Rank == next)
            {
                ahead++;
            }
            else
            {
                same++;
                AssertThat(entry.Rank).IsEqual(rank);
            }
        }

        // Tolerância de ~4 desvios-padrão para 10k sorteios (5% => sigma ~0.22%; 80% => ~0.4%).
        AssertThat(same / (double)Rolls).IsBetween(0.78, 0.82);
        AssertThat(ahead / (double)Rolls).IsBetween(0.135, 0.165);
        AssertThat(rare / (double)Rolls).IsBetween(0.04, 0.06);
    }

    [TestCase]
    public void TopRankNextTierFallsBackToTheSameRank()
    {
        AssertThat(Roller.NextRank(Rank.Alvor)).IsEqual(Rank.Alvor);
        var random = new SeededRandom(5);
        for (var i = 0; i < 500; i++)
        {
            var entry = Roller.RollOne(Rank.Alvor, random);
            AssertThat(entry.Rank).IsEqual(Rank.Alvor);
        }
    }

    [TestCase]
    public void SameSeedGivesSameLoot()
    {
        var a = Roller.Roll(Rank.D, LootSource.Chest, new SeededRandom(9));
        var b = Roller.Roll(Rank.D, LootSource.Chest, new SeededRandom(9));
        AssertThat(a.Fragmentos).IsEqual(b.Fragmentos);
        AssertThat(a.Entries.Select(e => $"{e.Id}x{e.Amount}").ToArray()).ContainsExactly(b.Entries.Select(e => $"{e.Id}x{e.Amount}").ToArray());
    }

    [TestCase]
    public void CommonMaterialsComeFromTheRankPoolOnly()
    {
        var random = new SeededRandom(11);
        for (var i = 0; i < 2000; i++)
        {
            var entry = Roller.RollMaterial(Rank.C, random);
            var material = Catalog.Materials[entry.Id];
            AssertThat(material.Rank).IsEqual(Rank.C);
            AssertThat(material.Kind).IsEqual(MaterialKind.Common);
        }
    }

    [TestCase]
    public void MonsterRollsFollowTheDropChance()
    {
        var random = new SeededRandom(3);
        var drops = Enumerable.Range(0, Rolls).Count(_ => !Roller.Roll(Rank.E, LootSource.Monster, random).IsEmpty || false);
        // dropChance 0.45; um monstro pode render 0 Fragmentos mas sempre rende 1 sorteio quando dropa.
        AssertThat(drops / (double)Rolls).IsBetween(0.42, 0.48);
    }

    [TestCase]
    public void FragmentosGrowByRankSoTheForgeStaysReachable()
    {
        double Average(Rank rank)
        {
            var random = new SeededRandom(1);
            return Enumerable.Range(0, 4000).Average(_ => Roller.Roll(rank, LootSource.Chest, random).Fragmentos);
        }

        var e = Average(Rank.E);
        var d = Average(Rank.D);
        var c = Average(Rank.C);
        AssertThat(d / e).IsBetween(1.55, 1.85);
        AssertThat(c / d).IsBetween(1.55, 1.85);
    }

    [TestCase]
    public void GuardianAlwaysDropsItsMaterialAndFragmentos()
    {
        var random = new SeededRandom(21);
        for (var i = 0; i < 500; i++)
        {
            var result = Roller.Roll(Rank.E, LootSource.Guardian("lodo_mae"), random);
            AssertThat(result.Entries.Any(e => e.Id == "nucleo_lodo_mae" && e.Amount >= 1)).IsTrue();
            AssertThat(result.Fragmentos).IsGreaterEqual(14);
        }
    }

    [TestCase]
    public void LodoMaeAlwaysLeavesGelEnoughForTheFirstUpgrade()
    {
        // Espólio garantido (guardians.json "spoils"): a Lodo-Mãe sozinha já paga o material primário do nível 1 -> 2.
        var cost = new WeaponForge(Catalog, new RankLadder(Catalog)).Cost(1)!;
        var needed = cost.Materials.GetValueOrDefault("gel_de_lodo");
        AssertThat(needed).IsGreater(0);
        var random = new SeededRandom(7);
        for (var i = 0; i < 200; i++)
        {
            var gel = Roller.Roll(Rank.E, LootSource.Guardian("lodo_mae"), random).Entries.Where(e => e.Id == "gel_de_lodo").Sum(e => e.Amount);
            AssertThat(gel).IsGreaterEqual(needed);
        }
    }

    [TestCase]
    public void GuardianDropsASetPieceThirtyPercentOfTheTime()
    {
        var random = new SeededRandom(77);
        var withPiece = 0;
        for (var i = 0; i < Rolls; i++)
        {
            var result = Roller.Roll(Rank.E, LootSource.Guardian("lodo_mae"), random);
            var pieces = result.Entries.Where(e => e.SetPiece).ToList();
            AssertThat(pieces.Count).IsLessEqual(1);
            if (pieces.Count == 1)
            {
                withPiece++;
                AssertThat(pieces[0].Item!.SetId).IsEqual("conjunto_lodo_mae");
            }
        }

        AssertThat(withPiece / (double)Rolls).IsBetween(0.27, 0.33);
    }

    [TestCase]
    public void GuardianPrefersPiecesTheHunterStillLacks()
    {
        var set = Catalog.Sets["conjunto_lodo_mae"];
        var owned = set.Pieces.Take(3).Select(p => p.Id).ToHashSet();
        var missing = set.Pieces[3].Id;
        var random = new SeededRandom(8);
        for (var i = 0; i < 3000; i++)
        {
            var result = Roller.Roll(Rank.E, LootSource.Guardian("lodo_mae"), random, owned);
            foreach (var piece in result.Entries.Where(e => e.SetPiece))
            {
                AssertThat(piece.Id).IsEqual(missing);
            }
        }
    }

    [TestCase]
    public void GuardianWithoutASetDropsNoPiece()
    {
        var random = new SeededRandom(8);
        for (var i = 0; i < 1000; i++)
        {
            AssertThat(Roller.Roll(Rank.D, LootSource.Guardian("macieira_oca"), random).Entries.Any(e => e.SetPiece)).IsFalse();
        }
    }

    [TestCase]
    public void RareGearScalesWithRankAndAlwaysHasAnAffix()
    {
        var low = new List<int>();
        var high = new List<int>();
        var random = new SeededRandom(4);
        for (var i = 0; i < 300; i++)
        {
            var e = Roller.RollRareGear(Rank.E, random).Item!;
            var s = Roller.RollRareGear(Rank.S, random).Item!;
            low.Add(e.Stats.Hp + e.Stats.Defense);
            high.Add(s.Stats.Hp + s.Stats.Defense);
            AssertThat(e.Name).IsNotEmpty();
            AssertThat(s.IsRare).IsTrue();
        }

        AssertThat(high.Average()).IsGreater(low.Average() * 2);
    }

    [TestCase]
    public void AffixStatsAreScaledByRankIndex()
    {
        var vigor = Catalog.GearData.Affixes.First(a => a.Id == "vigor");
        AssertThat(LootRoller.AffixStats(vigor, 0).Hp).IsEqual(10);
        AssertThat(LootRoller.AffixStats(vigor, 3).Hp).IsEqual(28);
    }

    [TestCase]
    public void GuardianLootWithoutAnIdIsRejected()
    {
        try
        {
            Roller.Roll(Rank.E, new LootSource(LootSourceKind.Guardian), new SeededRandom(1));
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("esperava ArgumentException");
    }
}
