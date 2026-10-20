namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Escala de monstros e saque (spec 02 §3.9, §3.12; §6.3 itens 14, 17-19).</summary>
[TestSuite]
public class LootAndScalingTest
{
    private static LootTables Loot => new(TestDb.Instance);

    private static MonsterScaling Scaling => new(TestDb.Instance);

    [TestCase(0, 1.0, 1.0)]
    [TestCase(1, 1.4, 1.25)]
    [TestCase(2, 1.8, 1.5)]
    [TestCase(3, 2.2, 1.75)]
    [TestCase(4, 2.6, 2.0)]
    [TestCase(5, 3.0, 2.25)]
    public void ScalingTableByBrechaRank(int rank, double hp, double damage)
    {
        AssertThat(Scaling.HpMult(rank)).IsEqualApprox(hp, 1e-9);
        AssertThat(Scaling.DamageMult(rank, null)).IsEqualApprox(damage, 1e-9);
        AssertThat(Scaling.DamageMult(rank, "outono")).IsEqualApprox(damage, 1e-9);
        AssertThat(Scaling.DamageMult(rank, "verao")).IsEqualApprox(damage * 1.15, 1e-9);
    }

    [TestCase]
    public void ScaleAppliesToHpPartsAndDamageNotSpeed()
    {
        var wolf = TestDb.Instance.Monsters["wolf"];
        var scaled = Scaling.Scale(wolf, 2, "verao");
        AssertThat(scaled.Hp).IsEqualApprox(72.0, 1e-9);
        AssertThat(scaled.PartHp[0]).IsEqualApprox(28.8, 1e-9);
        AssertThat(scaled.PartHp[1]).IsEqualApprox(32.4, 1e-9);
        AssertThat(scaled.Damage).IsEqualApprox(12 * 1.5 * 1.15, 1e-9);
        AssertThat(scaled.Def.Speed).IsEqual(58.0);
    }

    [TestCase]
    public void RollGearUsesRarityWeightsInDataOrder()
    {
        // Pool sem lant0: comum 4×3, incomum 8×2, raro 12×0.6 = 35.2 (chefe: 12×3 → 64).
        AssertThat(Loot.RollGear(false, new ConstantRandom(0))).IsEqual("capuz");
        AssertThat(Loot.RollGear(false, new ConstantRandom(0.9999))).IsEqual("rl_silva");
        AssertThat(Loot.RollGear(false, new ConstantRandom(8.3 / 35.2))).IsEqual("couraca2"); // 3+3+2 < 8.3 ≤ 8.6
        AssertThat(Loot.RollGear(false, new ConstantRandom(8.7 / 35.2))).IsEqual("capa");
        AssertThat(Loot.RollGear(true, new ConstantRandom(10.0 / 64))).IsEqual("couraca2"); // 8 < 10 ≤ 11
    }

    [TestCase]
    public void RollGearNeverDropsHeirloomLanternAndFavorsRareForBosses()
    {
        var db = TestDb.Instance;
        var random = new SeededRandom(7);
        int rareNormal = 0, rareBoss = 0;
        const int n = 20000;
        for (var i = 0; i < n; i++)
        {
            var normal = Loot.RollGear(false, random);
            var boss = Loot.RollGear(true, random);
            AssertThat(normal).IsNotEqual("lant0");
            AssertThat(boss).IsNotEqual("lant0");
            rareNormal += db.Gear[normal].Rarity == "raro" ? 1 : 0;
            rareBoss += db.Gear[boss].Rarity == "raro" ? 1 : 0;
        }

        AssertThat((double)rareNormal / n).IsBetween(7.2 / 35.2 - 0.02, 7.2 / 35.2 + 0.02);
        AssertThat((double)rareBoss / n).IsBetween(36.0 / 64 - 0.02, 36.0 / 64 + 0.02);
    }

    [TestCase]
    public void CommonKillDropsShardAtSeventyPercentAndCoins()
    {
        var wolf = TestDb.Instance.Monsters["wolf"];
        // 0.69 < 0.7 → Estilhaço; RI(1,3) com 0.99 → 3 montes; montes RI(1,3): 0 → 1, 0.5 → 2, 0.99 → 3
        var drops = Loot.KillDrops(wolf, invasion: false, new FixedRandom(0.69, 0.99, 0, 0.5, 0.99));
        AssertThat(drops.Count).IsEqual(4);
        AssertThat(drops[0]).IsEqual(LootDrop.Material("estilhaco", 1));
        AssertThat(drops.Skip(1).Select(d => d.Amount)).ContainsExactly(1, 2, 3);

        var noShard = Loot.KillDrops(wolf, invasion: false, new FixedRandom(0.7, 0, 0));
        AssertThat(noShard.Count).IsEqual(1);
        AssertThat(noShard[0].Kind).IsEqual(LootKind.Lumes);
    }

    [TestCase]
    public void InvasionMobAlwaysDropsShard()
    {
        var wolf = TestDb.Instance.Monsters["wolf"];
        var drops = Loot.KillDrops(wolf, invasion: true, new FixedRandom(0, 0));
        AssertThat(drops[0]).IsEqual(LootDrop.Material("estilhaco", 1));
    }

    [TestCase]
    public void BossDropsFourShardsCoinsAndGear()
    {
        var angler = TestDb.Instance.Monsters["angler"];
        // RI(6,9) com 0 → 6 montes de RI(4,7) com 0.99 → 7; rollGear(true) com 0 → capuz
        var random = new FixedRandom(0, 0.99, 0.99, 0.99, 0.99, 0.99, 0.99, 0);
        var drops = Loot.KillDrops(angler, invasion: false, random);
        AssertThat(drops[0]).IsEqual(LootDrop.Material("estilhaco", 4));
        AssertThat(drops.Count(d => d.Kind == LootKind.Lumes)).IsEqual(6);
        AssertThat(drops.Where(d => d.Kind == LootKind.Lumes).Sum(d => d.Amount)).IsEqual(42);
        AssertThat(drops[^1]).IsEqual(LootDrop.Gear("capuz"));
        AssertThat(random.Consumed).IsEqual(8);
    }

    [TestCase]
    public void EcoChanceForBossesAndAlpha()
    {
        var db = TestDb.Instance;
        AssertThat(Loot.EcoChance(db.Monsters["angler"], alreadyOwned: false)).IsEqual(0.75);
        AssertThat(Loot.EcoChance(db.Monsters["knight"], alreadyOwned: false)).IsEqual(0.75);
        AssertThat(Loot.EcoChance(db.Monsters["angler"], alreadyOwned: true)).IsEqual(0.0);
        AssertThat(Loot.EcoChance(db.Monsters["wolfE"], alreadyOwned: false)).IsEqual(0.25);
        AssertThat(Loot.EcoChance(db.Monsters["wolf"], alreadyOwned: false)).IsEqual(0.0);

        AssertThat(Loot.RollEco(db.Monsters["wolfE"], false, new ConstantRandom(0.24))).IsEqual("uivo");
        AssertThat(Loot.RollEco(db.Monsters["wolfE"], false, new ConstantRandom(0.25))).IsNull();
        AssertThat(Loot.RollEco(db.Monsters["reaper"], true, new ConstantRandom(0))).IsNull();
    }

    [TestCase]
    public void PartDropsRepeatMaterial()
    {
        var parts = TestDb.Instance.Monsters["angler"].Parts;
        AssertThat(Loot.PartDrops(parts[1])).ContainsExactly(LootDrop.Material("escama", 1), LootDrop.Material("escama", 1), LootDrop.Material("escama", 1));
    }

    [TestCase]
    public void ChestDropsFollowPrototypeOdds()
    {
        // 3 montes (RI(3,5) com 0) de RI(2,5); RI(1,2) Estilhaços; material básico; gear 0.29 < 0.3; rollGear; poção 0.34 < 0.35
        var random = new FixedRandom(0, 0, 0.5, 0.99, 0.99, 0.5, 0.29, 0, 0.34);
        var drops = Loot.ChestDrops(purple: false, random);
        AssertThat(drops.Where(d => d.Kind == LootKind.Lumes).Select(d => d.Amount)).ContainsExactly(2, 4, 5);
        AssertThat(drops).Contains(LootDrop.Material("estilhaco", 2), LootDrop.Material("erva", 1), LootDrop.Gear("capuz"), LootDrop.Potion(1));

        var plain = Loot.ChestDrops(purple: false, new FixedRandom(0, 0, 0, 0, 0, 0, 0.3, 0.35));
        AssertThat(plain.Any(d => d.Kind is LootKind.Gear or LootKind.Potion)).IsFalse();

        var purple = Loot.ChestDrops(purple: true, new FixedRandom(0, 0, 0, 0, 0, 0, 0.84, 0, 0.9));
        AssertThat(purple.Count(d => d.Kind == LootKind.Gear)).IsEqual(1);
    }

    [TestCase]
    public void ChestCountAndPurple()
    {
        var db = TestDb.Instance;
        var spring = db.SeasonData.Seasons.Single(s => s.Id == "primavera");
        var autumn = db.SeasonData.Seasons.Single(s => s.Id == "outono");
        AssertThat(Loot.ChestCount(spring, new ConstantRandom(0.29))).IsEqual(3);
        AssertThat(Loot.ChestCount(autumn, new ConstantRandom(0.3))).IsEqual(1);
        AssertThat(Loot.RollPurpleChest(new ConstantRandom(0.24))).IsTrue();
        AssertThat(Loot.RollPurpleChest(new ConstantRandom(0.25))).IsFalse();
    }

    [TestCase]
    public void PropDropBands()
    {
        var crypt = TestDb.Instance.Themes["crypt"];
        AssertThat(Loot.PropDrop(crypt, new FixedRandom(0.44, 0.99))!.Value).IsEqual(LootDrop.Lumes(3));
        AssertThat(Loot.PropDrop(crypt, new FixedRandom(0.59))!.Value).IsEqual(LootDrop.Material("cera", 1));
        AssertThat(Loot.PropDrop(crypt, new FixedRandom(0.6))!.Value).IsEqual(LootDrop.Material("erva", 1));
        AssertThat(Loot.PropDrop(crypt, new FixedRandom(0.81))!.Value).IsEqual(LootDrop.Potion(1));
        AssertThat(Loot.PropDrop(crypt, new FixedRandom(0.82))).IsNull();
    }
}
