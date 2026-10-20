namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Core.State;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Operações de estado do jogador e eventos (§6.3 itens 3, 15, 16, 20, 24, 30 no fluxo completo).</summary>
[TestSuite]
public class GameStateTest
{
    private static GameState NewState(string cls = "sword")
        => new(TestDb.Instance, SaveData.NewGame(TestDb.Instance, "Rui", cls), new SeededRandom(1));

    [TestCase]
    public void StartsWithFullHealth()
    {
        var gs = NewState();
        AssertThat(gs.Hp).IsEqual(100);
        AssertThat(gs.MaxHp).IsEqual(100);
        AssertThat(gs.Rank).IsEqual(Rank.E);
        AssertThat(gs.Level.Level).IsEqual(1);
        AssertThat(gs.Lumes).IsEqual(60);
        AssertThat(gs.Season.Id).IsEqual("outono");
    }

    [TestCase]
    public void GainXpRaisesSealLevelAndHp()
    {
        var gs = NewState();
        var ranks = new List<(Rank, Rank)>();
        var levels = new List<(int, int)>();
        var health = new List<(int, int)>();
        gs.RankChanged += (a, b) => ranks.Add((a, b));
        gs.LevelChanged += (a, b) => levels.Add((a, b));
        gs.HealthChanged += (hp, max) => health.Add((hp, max));
        gs.Damage(50);

        var result = gs.GainXp(100);

        AssertThat(result.RankedUp).IsTrue();
        AssertThat(ranks).ContainsExactly((Rank.E, Rank.D));
        AssertThat(levels).ContainsExactly((1, 3));
        AssertThat(gs.MaxHp).IsEqual(124);
        AssertThat(gs.Hp).IsEqual(74); // 50 + 24 (combat.js:217)
        AssertThat(health[^1]).IsEqual((74, 124));
        AssertThat(gs.Save.Xp).IsEqual(100);
    }

    [TestCase]
    public void GainXpWithoutThresholdDoesNotTouchStats()
    {
        var gs = NewState();
        var statsChanged = 0;
        gs.StatsChanged += _ => statsChanged++;
        gs.GainXp(10);
        AssertThat(statsChanged).IsEqual(0);
        AssertThat(gs.MaxHp).IsEqual(100);
    }

    [TestCase]
    public void PaysWithJsRoundingAndInvasion()
    {
        var gs = NewState();
        var lumesEvents = new List<int>();
        gs.LumesChanged += lumesEvents.Add;

        AssertThat(gs.CostLumes(22)).IsEqual(20);
        gs.Invasion = true;
        AssertThat(gs.CostLumes(30)).IsEqual(41);
        AssertThat(gs.TryPay(Cost.Of(30))).IsTrue();
        AssertThat(gs.Lumes).IsEqual(19);
        AssertThat(gs.TryPay(Cost.Of(30))).IsFalse();
        AssertThat(gs.Lumes).IsEqual(19);
        AssertThat(lumesEvents).ContainsExactly(19);
    }

    [TestCase]
    public void LumesAndMaterials()
    {
        var gs = NewState();
        var mats = new List<(string, int)>();
        gs.MaterialChanged += (id, n) => mats.Add((id, n));
        gs.AddLumes(15);
        AssertThat(gs.TrySpendLumes(100)).IsFalse();
        AssertThat(gs.TrySpendLumes(75)).IsTrue();
        AssertThat(gs.Lumes).IsEqual(0);

        gs.AddMaterial("presa", 3);
        AssertThat(gs.TryRemoveMaterial("presa", 4)).IsFalse();
        AssertThat(gs.TryRemoveMaterial("presa", 3)).IsTrue();
        AssertThat(gs.Save.Materials.ContainsKey("presa")).IsFalse();
        AssertThat(mats).ContainsExactly(("presa", 3), ("presa", 0));
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void RejectsUnknownMaterial()
        => NewState().AddMaterial("ouro", 1);

    [TestCase]
    public void EquipSwapsAndAdjustsHp()
    {
        var gs = NewState();
        var slots = new List<string>();
        gs.EquipmentChanged += slots.Add;

        AssertThat(gs.EquipById("capuz")).IsNull();
        AssertThat(gs.MaxHp).IsEqual(110);
        AssertThat(gs.Hp).IsEqual(110);
        AssertThat(gs.Stats.Def).IsEqual(8);
        AssertThat(gs.Save.Backpack).ContainsExactly("luva");

        AssertThat(gs.EquipById("luva")).IsNull();
        AssertThat(gs.Stats.Atk).IsEqual(15);

        gs.AddGear("lant1");
        AssertThat(gs.EquipById("lant1")).IsEqual("Requer Selo D (Vigia). Seu Selo é E.");
        gs.GainXp(100);
        AssertThat(gs.EquipById("lant1")).IsNull();
        AssertThat(gs.Save.Backpack).Contains("lant0"); // a antiga volta para a mochila
        AssertThat(slots).ContainsExactly("cabeca", "maos", "lanterna");

        gs.Damage(gs.Hp - 5);
        AssertThat(gs.Unequip("cabeca")).IsTrue();
        AssertThat(gs.Hp).IsEqual(5);       // desequipar só trunca
        AssertThat(gs.Unequip("cabeca")).IsFalse();
    }

    [TestCase]
    public void ClassLockedGearCannotBeEquipped()
    {
        var gs = NewState("bow");
        gs.AddGear("escudo");
        AssertThat(gs.EquipById("escudo")).IsEqual("Exclusivo de Lâmina / Martelo. Sua classe é Arco.");
        AssertThat(gs.Save.Equipment.ContainsKey("reliquia")).IsFalse();
    }

    [TestCase]
    public void DismantleGivesTwoShards()
    {
        var gs = NewState();
        AssertThat(gs.Dismantle(0)).IsTrue();
        AssertThat(gs.Shards).IsEqual(2);
        AssertThat(gs.Save.Backpack).ContainsExactly("luva");
        AssertThat(gs.Dismantle(5)).IsFalse();
    }

    [TestCase]
    public void UpgradeWeaponPaysAndRaisesAttack()
    {
        var gs = NewState();
        AssertThat(gs.TryUpgradeWeapon("sword")).IsEqual(PurchaseResult.CannotAfford);
        gs.AddMaterial("presa", 1);
        var levels = new List<(string, int)>();
        gs.WeaponLevelChanged += (w, lv) => levels.Add((w, lv));

        AssertThat(gs.TryUpgradeWeapon("sword")).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.Lumes).IsEqual(24); // 60 - 36
        AssertThat(gs.Save.Weapons).ContainsKeyValue("sword", 2);
        AssertThat(gs.Stats.Atk).IsEqual(15);
        AssertThat(levels).ContainsExactly(("sword", 2));
        AssertThat(gs.TryUpgradeWeapon("bow")).IsEqual(PurchaseResult.NotAvailable);

        gs.Save.Weapons["sword"] = 5;
        AssertThat(gs.TryUpgradeWeapon("sword")).IsEqual(PurchaseResult.MaxLevel);
    }

    [TestCase]
    public void OtherClassWeaponsAreLocked()
    {
        var gs = NewState();
        gs.AddLumes(500);
        gs.AddMaterial("estilhaco", 10);
        AssertThat(gs.TryUnlockWeapon("bow")).IsEqual(PurchaseResult.Locked);
        AssertThat(gs.TryUnlockWeapon("sword")).IsEqual(PurchaseResult.AlreadyOwned);
    }

    [TestCase]
    public void ForgeRelicsAndServices()
    {
        var db = TestDb.Instance;
        var gs = NewState();
        gs.AddLumes(1000);
        gs.AddMaterial("minerio", 4);
        gs.AddMaterial("presa", 2);

        AssertThat(gs.TryForge(db.Recipes.Single(r => r.Gear == "luva2"))).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.Save.Backpack).Contains("luva2");
        AssertThat(gs.TryForge(db.Recipes.Single(r => r.Gear == "luva2"))).IsEqual(PurchaseResult.CannotAfford);

        var lunar = db.Shops.Relics.Single(r => r.Gear == "ln_lunar");
        AssertThat(gs.TryBuyRelic(lunar)).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.TryBuyRelic(lunar)).IsEqual(PurchaseResult.AlreadyOwned);

        var lumes = gs.Lumes;
        AssertThat(gs.TryBuyService(db.Services["ivo_box"])).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.Save.Potions).IsEqual(6);
        AssertThat(gs.Lumes).IsEqual(lumes - 54);

        AssertThat(gs.TryBuyService(db.Services["vesna_book"])).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.TryBuyService(db.Services["vesna_book"])).IsEqual(PurchaseResult.AlreadyOwned);
        AssertThat(gs.TryBuyService(db.Services["odete_rescue"])).IsEqual(PurchaseResult.NotAvailable);

        gs.Damage(30);
        AssertThat(gs.TryBuyService(db.Services["inn_rest"])).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.Save.Day).IsEqual(2);
        AssertThat(gs.Save.ClockMinutes).IsEqual(960.0);
        AssertThat(gs.Hp).IsEqual(gs.MaxHp);
    }

    [TestCase]
    public void SellScrapsAndShards()
    {
        var gs = NewState();
        gs.AddMaterial("lapide", 3);
        gs.AddMaterial("presa", 2);
        gs.AddMaterial("estilhaco", 5);
        AssertThat(gs.SellScraps()).IsEqual(12);
        AssertThat(gs.MaterialCount("lapide")).IsEqual(0);
        AssertThat(gs.MaterialCount("presa")).IsEqual(2);
        AssertThat(gs.SellShards()).IsEqual(20);
        AssertThat(gs.Shards).IsEqual(0);
        AssertThat(gs.Lumes).IsEqual(92);
        AssertThat(gs.SellShards()).IsEqual(0);
    }

    [TestCase]
    public void PotionHealsWithCooldownAndBlocks()
    {
        var gs = NewState();
        AssertThat(gs.TryUsePotion(0, out _)).IsEqual(PotionResult.FullHealth);

        gs.Damage(60);
        AssertThat(gs.TryUsePotion(10, out var healed)).IsEqual(PotionResult.Used);
        AssertThat(healed).IsEqual(45);
        AssertThat(gs.Hp).IsEqual(85);
        AssertThat(gs.Save.Potions).IsEqual(2);
        AssertThat(gs.TryUsePotion(10.5, out _)).IsEqual(PotionResult.OnCooldown);
        AssertThat(gs.TryUsePotion(11, out _)).IsEqual(PotionResult.Used);
        AssertThat(gs.Hp).IsEqual(100);

        gs.Damage(10);
        gs.Save.Potions = 0;
        AssertThat(gs.TryUsePotion(20, out _)).IsEqual(PotionResult.NoPotions);
    }

    [TestCase]
    public void DamageKillsAtZero()
    {
        var gs = NewState();
        AssertThat(gs.Damage(99)).IsFalse();
        AssertThat(gs.Damage(5)).IsTrue();
        AssertThat(gs.Hp).IsEqual(0);
        AssertThat(gs.IsDead).IsTrue();
        AssertThat(gs.Damage(5)).IsFalse();
    }

    [TestCase]
    public void EcosAbsorbEquipAndTune()
    {
        var gs = NewState();
        AssertThat(gs.AbsorbEco("uivo")).IsTrue();
        AssertThat(gs.Save.EquippedEco).IsEqual("uivo");
        AssertThat(gs.AbsorbEco("abissal")).IsTrue();
        AssertThat(gs.Save.EquippedEco).IsEqual("uivo");
        AssertThat(gs.AbsorbEco("uivo")).IsFalse();
        AssertThat(gs.EquipEco("abissal")).IsTrue();
        AssertThat(gs.EquipEco("arauto")).IsFalse();

        AssertThat(gs.EcoCost("abissal")).IsEqual(55);
        gs.AddLumes(200);
        gs.AddMaterial("estilhaco", 10);
        AssertThat(gs.TryTuneEco("abissal")).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.EcoCost("abissal")).IsEqual(45);
        AssertThat(gs.Lumes).IsEqual(260 - 63);
        AssertThat(gs.TryTuneEco("arauto")).IsEqual(PurchaseResult.NotAvailable);
    }

    [TestCase]
    public void InfusionIsBoughtActivatedAndSwapped()
    {
        var gs = NewState();
        gs.AddLumes(200);
        gs.AddMaterial("estilhaco", 3);
        gs.AddMaterial("erva", 2);
        AssertThat(gs.TryBuyInfusion("sword", "solar")).IsEqual(PurchaseResult.Ok);
        AssertThat(gs.Lumes).IsEqual(260 - 72);
        AssertThat(gs.Stats.Element).IsEqual("solar");
        AssertThat(gs.TryBuyInfusion("sword", "solar")).IsEqual(PurchaseResult.AlreadyOwned);
        AssertThat(gs.TryBuyInfusion("bow", "solar")).IsEqual(PurchaseResult.NotAvailable);
        AssertThat(gs.SetActiveInfusion("sword", null)).IsTrue();
        AssertThat(gs.Stats.Element).IsNull();
        AssertThat(gs.SetActiveInfusion("sword", "lunar")).IsFalse();
    }

    [TestCase]
    public void DyingInBrechaLeavesLanternUntilRecovered()
    {
        var gs = NewState();
        var lantern = new List<bool>();
        gs.LanternChanged += lantern.Add;
        gs.Damage(100);
        var bag = new RunBag { Lumes = 30, Materials = { ["isca"] = 2 }, Gear = { "bota" } };

        gs.DieInBrecha(new LostLantern { BrechaId = 1, Bairro = "Mercado de Peixes", Bag = bag, Room = "theme" });
        AssertThat(gs.MaxHp).IsEqual(60);
        AssertThat(gs.Hp).IsEqual(60);
        AssertThat(gs.Stats.Light).IsEqualApprox(0.45, 1e-9);
        AssertThat(gs.HasLostLantern).IsTrue();

        gs.Damage(10);
        AssertThat(gs.RecoverLantern()).IsTrue();
        AssertThat(gs.MaxHp).IsEqual(100);
        AssertThat(gs.Hp).IsEqual(90);
        AssertThat(gs.Lumes).IsEqual(90);
        AssertThat(gs.MaterialCount("isca")).IsEqual(2);
        AssertThat(gs.Save.Backpack).Contains("bota");
        AssertThat(gs.RecoverLantern()).IsFalse();
        AssertThat(lantern).ContainsExactly(true, false);
    }

    [TestCase]
    public void DyingInCityCostsLumesAndHalfHp()
    {
        var gs = NewState();
        gs.Damage(100);
        gs.DieInCity();
        AssertThat(gs.Hp).IsEqual(50);
        AssertThat(gs.Lumes).IsEqual(50);
        AssertThat(gs.HasLostLantern).IsFalse();
    }

    [TestCase]
    public void DeliverLanternsPaysLumesAndXp()
    {
        var gs = NewState();
        gs.Save.CarriedLanterns.AddRange(["Nina", "Bento"]);
        AssertThat(gs.DeliverLanterns()).IsEqual((70, 40));
        AssertThat(gs.Lumes).IsEqual(130);
        AssertThat(gs.Save.Xp).IsEqual(40);
        AssertThat(gs.Save.CarriedLanterns).IsEmpty();
        AssertThat(gs.DeliverLanterns()).IsEqual((0, 0));
    }

    [TestCase]
    public void ClockRollsIntoNewDayAtSixAm()
    {
        var gs = NewState();
        var days = new List<int>();
        gs.DayChanged += days.Add;
        AssertThat(gs.AdvanceClock(809)).IsFalse();
        AssertThat(gs.AdvanceClock(1)).IsTrue(); // 990 + 810 = 1800
        AssertThat(gs.Save.Day).IsEqual(2);
        AssertThat(gs.Save.ClockMinutes).IsEqual(960.0);
        AssertThat(days).ContainsExactly(2);
    }

    [TestCase]
    public void QuestCompletesWithRewardAndEvent()
    {
        var gs = NewState();
        var done = new List<string>();
        gs.QuestCompleted += q => done.Add(q.Id);
        AssertThat(gs.TryCompleteQuest()).IsNull();
        gs.CountQuest(QuestCounters.Odete);
        AssertThat(gs.TryCompleteQuest()!.Id).IsEqual("primeiros_passos");
        AssertThat(gs.Lumes).IsEqual(80);
        AssertThat(gs.CurrentQuest!.Id).IsEqual("bonecos");
        gs.CountQuest(QuestCounters.Dummy, 8);
        AssertThat(gs.TryCompleteQuest()!.Id).IsEqual("bonecos");
        AssertThat(gs.Save.Potions).IsEqual(4);
        AssertThat(done).ContainsExactly("primeiros_passos", "bonecos");
    }

    [TestCase]
    public void BossAndInvasionCountTowardsBrechasClosed()
    {
        var gs = NewState();
        gs.RecordBossDefeated("fish");
        AssertThat(gs.Save.BrechasClosed).IsEqual(1);
        AssertThat(gs.Save.Quests.Counter(QuestCounters.Bosses)).IsEqual(1);
        AssertThat(gs.Save.Quests.ClosedThemes).ContainsExactly("fish");
        gs.RecordInvasionResolved();
        AssertThat(gs.Save.BrechasClosed).IsEqual(2);
        AssertThat(gs.Lumes).IsEqual(90);
        AssertThat(gs.Save.Xp).IsEqual(30);
    }

    [TestCase]
    public void MergeBagFromFinishedRun()
    {
        var gs = NewState();
        gs.MergeBag(new RunBag { Lumes = 5, Lanterns = { "Caio" } });
        AssertThat(gs.Lumes).IsEqual(65);
        AssertThat(gs.Save.CarriedLanterns).ContainsExactly("Caio");
    }
}
