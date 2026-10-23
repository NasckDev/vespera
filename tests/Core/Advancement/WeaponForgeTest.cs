namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Forja: curvas de dano, teto por rank, custos justos e o gasto de recursos.</summary>
[TestSuite]
public class WeaponForgeTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    private static WeaponForge Forge => new(Catalog, new RankLadder(Catalog));

    [TestCase]
    public void MaxLevelIsTwoPerRank()
    {
        AssertThat(Forge.MaxLevel).IsEqual(18);
        AssertThat(Forge.LevelCap(Rank.E)).IsEqual(2);
        AssertThat(Forge.LevelCap(Rank.D)).IsEqual(4);
        AssertThat(Forge.LevelCap(Rank.Alvor)).IsEqual(18);
        AssertThat(Forge.RankRequiredFor(3)).IsEqual(Rank.D);
        AssertThat(Forge.RankRequiredFor(18)).IsEqual(Rank.Alvor);
    }

    [TestCase("sword")]
    [TestCase("bow")]
    [TestCase("hammer")]
    public void DamageHitsTheCatalogAnchorOnEachTierStartAndNeverDrops(string classId)
    {
        var weapon = Catalog.Weapons[classId];
        var previous = 0;
        for (var level = 1; level <= Forge.MaxLevel; level++)
        {
            var stats = Forge.Stats(classId, level);
            AssertThat(stats.Damage).IsGreater(previous);
            previous = stats.Damage;
            if (level % 2 == 1)
            {
                AssertThat(stats.Damage).IsEqual(weapon.Tiers[(level - 1) / 2].Damage);
            }
        }
    }

    [TestCase]
    public void IntermediateLevelIsBetweenTheTwoAnchors()
    {
        var stats = Forge.Stats("sword", 2);
        AssertThat(stats.Damage).IsBetween(12, 23);
        AssertThat(stats.DisplayName).IsEqual("Lâmina de Ferro Frio +1");
        AssertThat(Forge.Stats("sword", 3).DisplayName).IsEqual("Espada das Marés");
        AssertThat(Forge.Stats("sword", 3).TierRank).IsEqual(Rank.D);
    }

    [TestCase]
    public void ClassesKeepTheirIdentityInAttackSpeedAndPartBreaking()
    {
        AssertThat(Forge.Stats("hammer", 1).Damage).IsGreater(Forge.Stats("sword", 1).Damage);
        AssertThat(Forge.Stats("hammer", 1).AttacksPerSecond).IsLess(Forge.Stats("sword", 1).AttacksPerSecond);
        AssertThat(Forge.Stats("hammer", 1).PartBreakPct).IsGreater(Forge.Stats("sword", 1).PartBreakPct);
        AssertThat(Forge.Stats("bow", 1).AttacksPerSecond).IsGreater(Forge.Stats("sword", 1).AttacksPerSecond);
    }

    [TestCase]
    public void FirstUpgradeCostsTwentyFourFragmentosAndERankMaterials()
    {
        var cost = Forge.Cost(1)!;
        AssertThat(cost.Fragmentos).IsEqual(24);
        AssertThat(cost.Materials["gel_de_lodo"]).IsEqual(4);
        AssertThat(cost.Materials["veu_de_espectro"]).IsEqual(1);
        AssertThat(cost.Materials.Count).IsEqual(2);
    }

    [TestCase]
    public void CrossingToTheNextTierAlsoNeedsTheGuardianMaterial()
    {
        var cost = Forge.Cost(2)!;
        AssertThat(cost.Materials["nucleo_lodo_mae"]).IsEqual(1);
        AssertThat(Forge.Cost(1)!.Materials.ContainsKey("nucleo_lodo_mae")).IsFalse();
        AssertThat(Forge.Cost(4)!.Materials["galho_oco"]).IsEqual(1);
    }

    [TestCase]
    public void CostGrowsAtEveryLevelAndEndsAtTheMax()
    {
        var previous = 0;
        for (var level = 1; level < Forge.MaxLevel; level++)
        {
            var cost = Forge.Cost(level)!;
            AssertThat(cost.Fragmentos).IsGreater(previous);
            previous = cost.Fragmentos;
        }

        AssertThat(Forge.Cost(Forge.MaxLevel)).IsNull();
    }

    [TestCase]
    public void AnERunYieldsEnoughForOneUpgradeButNotTwo()
    {
        // Corrida de referência no rank E: 24 monstros, 3 partes quebradas, 2 baús, sem o Guardião (estimativa conservadora).
        const int runs = 400;
        double fragments = 0, gel = 0, veil = 0;
        var roller = new LootRoller(Catalog);
        for (var seed = 0; seed < runs; seed++)
        {
            var random = new SeededRandom(seed);
            var results = new List<LootResult>();
            results.AddRange(Enumerable.Range(0, 24).Select(_ => roller.Roll(Rank.E, LootSource.Monster, random)));
            results.AddRange(Enumerable.Range(0, 3).Select(_ => roller.Roll(Rank.E, LootSource.Part, random)));
            results.AddRange(Enumerable.Range(0, 2).Select(_ => roller.Roll(Rank.E, LootSource.Chest, random)));
            fragments += results.Sum(r => r.Fragmentos);
            gel += results.Sum(r => r.Entries.Where(e => e.Id == "gel_de_lodo").Sum(e => e.Amount));
            veil += results.Sum(r => r.Entries.Where(e => e.Id == "veu_de_espectro").Sum(e => e.Amount));
        }

        fragments /= runs;
        gel /= runs;
        veil /= runs;
        var first = Forge.Cost(1)!;
        var firstTwo = first.Fragmentos + Forge.Cost(2)!.Fragmentos;
        AssertThat(fragments).IsGreater(first.Fragmentos);
        AssertThat(fragments).IsLess(firstTwo);
        AssertThat(gel).IsGreater(first.Materials["gel_de_lodo"]);
        AssertThat(veil).IsGreater(first.Materials["veu_de_espectro"]);
    }

    [TestCase]
    public void CheckReportsMissingResources()
    {
        var progress = new HunterProgress { Fragmentos = 10 };
        progress.Materials["gel_de_lodo"] = 1;
        var check = Forge.Check(progress, "sword");
        AssertThat(check.CanUpgrade).IsFalse();
        AssertThat(check.Block).IsEqual(ForgeBlock.Missing);
        AssertThat(check.Missing.Select(m => m.Id).ToArray()).ContainsExactlyInAnyOrder("fragmentos", "gel_de_lodo", "veu_de_espectro");
        var frag = check.Missing.First(m => m.Id == "fragmentos");
        AssertThat(frag.Have).IsEqual(10);
        AssertThat(frag.Need).IsEqual(24);
        AssertThat(check.Next!.Level).IsEqual(2);
    }

    [TestCase]
    public void UpgradeSpendsExactlyTheCostAndRaisesTheLevel()
    {
        var progress = new HunterProgress { Fragmentos = 30 };
        progress.Materials["gel_de_lodo"] = 5;
        progress.Materials["veu_de_espectro"] = 1;
        var result = Forge.Upgrade(progress, "sword");
        AssertThat(result.CanUpgrade).IsTrue();
        AssertThat(progress.WeaponLevels["sword"]).IsEqual(2);
        AssertThat(progress.Fragmentos).IsEqual(6);
        AssertThat(progress.MaterialCount("gel_de_lodo")).IsEqual(1);
        AssertThat(progress.Materials.ContainsKey("veu_de_espectro")).IsFalse();
    }

    [TestCase]
    public void FailedUpgradeSpendsNothing()
    {
        var progress = new HunterProgress { Fragmentos = 23 };
        progress.Materials["gel_de_lodo"] = 9;
        progress.Materials["veu_de_espectro"] = 9;
        var result = Forge.Upgrade(progress, "sword");
        AssertThat(result.CanUpgrade).IsFalse();
        AssertThat(progress.Fragmentos).IsEqual(23);
        AssertThat(progress.MaterialCount("gel_de_lodo")).IsEqual(9);
        AssertThat(progress.WeaponLevels.ContainsKey("sword")).IsFalse();
    }

    [TestCase]
    public void LevelCapBlocksUntilTheRankIsReached()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.WeaponLevels["sword"] = 2;
        engine.Progress.Fragmentos = 9999;
        foreach (var id in new[] { "gel_de_lodo", "veu_de_espectro", "nucleo_lodo_mae" })
        {
            engine.Progress.Materials[id] = 50;
        }

        var check = engine.CanUpgrade();
        AssertThat(check.Block).IsEqual(ForgeBlock.RankTooLow);
        AssertThat(check.RequiredRank).IsEqual(Rank.D);
        AssertThat(check.Reason).Contains("rank D");

        engine.AddXp(500);
        engine.RecordGuardianKill("lodo_mae");
        AssertThat(engine.Rank).IsEqual(Rank.D);
        AssertThat(engine.CanUpgrade().CanUpgrade).IsTrue();
    }

    [TestCase]
    public void MaxLevelCannotBeUpgraded()
    {
        var engine = AdvTestData.HunterAt(Rank.Alvor);
        engine.Progress.WeaponLevels["sword"] = 18;
        var check = engine.CanUpgrade();
        AssertThat(check.Block).IsEqual(ForgeBlock.MaxLevel);
        AssertThat(check.Next).IsNull();
        AssertThat(check.Cost).IsNull();
    }

    [TestCase]
    public void EveryLevelOfEveryClassUsesMaterialsThatExist()
    {
        foreach (var classId in Catalog.Weapons.Keys)
        {
            for (var level = 1; level < Forge.MaxLevel; level++)
            {
                foreach (var id in Forge.Cost(level)!.Materials.Keys)
                {
                    AssertThat(Catalog.Materials.ContainsKey(id)).IsTrue();
                }

                AssertThat(Forge.Stats(classId, level + 1).Damage).IsGreater(Forge.Stats(classId, level).Damage);
            }
        }
    }
}
