namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Lanterna, travas de equipamento e missões (spec 02 §1.9, §3.13; extra.js; §6.3 itens 24 e 30).</summary>
[TestSuite]
public class LanternEquipmentQuestTest
{
    private static LanternRules Lantern => new(TestDb.Instance);

    private static EquipmentRules Equipment => new(TestDb.Instance);

    private static QuestRules Quests => new(TestDb.Instance);

    private static SaveData NewSave(string cls = "sword") => SaveData.NewGame(TestDb.Instance, "Rui", cls);

    [TestCase]
    public void LanternPenaltiesAndRescue()
    {
        AssertThat(Lantern.PenalizedHp(100)).IsEqual(60);
        AssertThat(Lantern.PenalizedHp(124)).IsEqual(74);
        AssertThat(Lantern.PenalizedLight(1.35)).IsEqualApprox(0.6075, 1e-9);
        AssertThat(Lantern.RollRescueDelay(new ConstantRandom(0))).IsEqual(55.0);
        AssertThat(Lantern.RollRescueDelay(new ConstantRandom(0.5))).IsEqual(70.0);
        AssertThat(Lantern.OdeteRescueDelay).IsEqual(12.0);
        AssertThat(Lantern.RescueOnLoadDelay).IsEqual(20.0);
        AssertThat(Lantern.DeliveryReward(2)).IsEqual((70, 40));
        AssertThat(Lantern.RollFoundLantern(new ConstantRandom(0.44))).IsTrue();
        AssertThat(Lantern.RollFoundLantern(new ConstantRandom(0.45))).IsFalse();
    }

    [TestCase]
    public void CityDeathCostsTenLumesAndHalfHp()
    {
        AssertThat(Lantern.CityRespawnHp(125)).IsEqual(63);
        AssertThat(Lantern.LumesAfterCityDeath(60)).IsEqual(50);
        AssertThat(Lantern.LumesAfterCityDeath(4)).IsEqual(0);
    }

    [TestCase]
    public void LanternIsFoundOnlyInSameBrechaAndRoom()
    {
        var s = NewSave();
        AssertThat(LanternRules.IsLanternHere(s, 3, "theme")).IsFalse();
        s.LostLantern = new LostLantern { BrechaId = 3, Room = "core" };
        AssertThat(LanternRules.IsLanternHere(s, 3, "core")).IsTrue();
        AssertThat(LanternRules.IsLanternHere(s, 3, "theme")).IsFalse();
        AssertThat(LanternRules.IsLanternHere(s, 4, "core")).IsFalse();
    }

    [TestCase]
    public void MergeBagAddsEverything()
    {
        var s = NewSave();
        s.Materials["presa"] = 1;
        var bag = new RunBag { Lumes = 15, Materials = { ["presa"] = 2, ["isca"] = 1 }, Gear = { "bota" }, Lanterns = { "Nina" } };
        LanternRules.MergeBag(s, bag);
        AssertThat(s.Lumes).IsEqual(75);
        AssertThat(s.Materials).ContainsKeyValue("presa", 3);
        AssertThat(s.Materials).ContainsKeyValue("isca", 1);
        AssertThat(s.Backpack).Contains("bota");
        AssertThat(s.CarriedLanterns).ContainsExactly("Nina");
    }

    [TestCase]
    public void ClassLockUsesPrototypeText()
    {
        AssertThat(Equipment.LockReason("luva2", "bow", 9)).IsEqual("Exclusivo de Lâmina / Martelo / Adagas. Sua classe é Arco.");
        AssertThat(Equipment.LockReason("capuz", "bow", 0)).IsNull();
    }

    [TestCase]
    public void RankLockComesFromRarity()
    {
        AssertThat(Equipment.LockReason("couraca2", "sword", 0)).IsEqual("Requer Selo C (Lanterneiro). Seu Selo é E.");
        AssertThat(Equipment.LockReason("couraca2", "sword", 2)).IsNull();
        AssertThat(Equipment.LockReason("gibao", "dag", 0)).IsEqual("Requer Selo D (Vigia). Seu Selo é E.");
        AssertThat(Equipment.CanEquip("gibao", "dag", 1)).IsTrue();
        AssertThat(Equipment.MinRankIndex(TestDb.Instance.Gear["capuz"])).IsEqual(0);
        AssertThat(Equipment.MinRankIndex(TestDb.Instance.Gear["rl_silva"])).IsEqual(2);
    }

    [TestCase]
    public void ClassIsCheckedBeforeSeal()
        => AssertThat(Equipment.LockReason("couraca2", "bow", 0)).StartsWith("Exclusivo de Lâmina / Martelo.");

    [TestCase]
    public void ClassLabelAndWield()
    {
        AssertThat(Equipment.ClassesLabel(TestDb.Instance.Gear["capuz"])).IsEqual("Todas");
        AssertThat(Equipment.ClassesLabel(TestDb.Instance.Gear["livro"])).IsEqual("Cajado, Arco");
        AssertThat(EquipmentRules.CanWield("sword", "sword")).IsTrue();
        AssertThat(EquipmentRules.CanWield("bow", "sword")).IsFalse();
    }

    [TestCase]
    public void QuestChainStartsWithOdete()
    {
        var s = NewSave();
        var first = Quests.Current(s)!;
        AssertThat(first.Name).IsEqual("Primeiros passos");
        AssertThat(Quests.IsComplete(first, s)).IsFalse();
        s.Quests.Counters[QuestCounters.Odete] = 1;
        AssertThat(Quests.IsComplete(first, s)).IsTrue();

        Quests.Complete(first, s);
        AssertThat(s.Lumes).IsEqual(80);
        AssertThat(Quests.Current(s)!.Name).IsEqual("Bonecos de treino");
    }

    [TestCase]
    public void CounterQuestShowsProgress()
    {
        var s = NewSave();
        var dummies = TestDb.Instance.Quests[1];
        s.Quests.Counters[QuestCounters.Dummy] = 5;
        AssertThat(Quests.ProgressText(dummies, s)).IsEqual("5/8");
        s.Quests.Counters[QuestCounters.Dummy] = 11;
        AssertThat(Quests.ProgressText(dummies, s)).IsEqual("8/8");
        AssertThat(Quests.IsComplete(dummies, s)).IsTrue();
        AssertThat(Quests.ProgressText(TestDb.Instance.Quests[0], s)).IsNull();
    }

    [TestCase]
    public void OtherConditionKinds()
    {
        var db = TestDb.Instance;
        var s = NewSave("staff");
        var forge = db.Quests.Single(q => q.Id == "fogo_bras");
        var sealD = db.Quests.Single(q => q.Id == "selo_d");
        var crypt = db.Quests.Single(q => q.Id == "cemiterio");
        var alvor = db.Quests[^1];

        AssertThat(Quests.IsComplete(forge, s)).IsFalse();
        s.Weapons["staff"] = 2;
        AssertThat(Quests.IsComplete(forge, s)).IsTrue();

        AssertThat(Quests.IsComplete(sealD, s)).IsFalse();
        s.Xp = 100;
        AssertThat(Quests.IsComplete(sealD, s)).IsTrue();

        AssertThat(Quests.IsComplete(crypt, s)).IsFalse();
        s.Quests.ClosedThemes.Add("crypt");
        AssertThat(Quests.IsComplete(crypt, s)).IsTrue();

        s.Xp = 7499;
        AssertThat(Quests.IsComplete(alvor, s)).IsFalse();
        s.Xp = 7500;
        AssertThat(Quests.IsComplete(alvor, s)).IsTrue();
    }

    [TestCase]
    public void RewardTextAndMaterials()
    {
        var s = NewSave();
        var close = TestDb.Instance.Quests.Single(q => q.Id == "feche_brecha");
        AssertThat(Quests.RewardText(close)).IsEqual("+60 Lumes · +3 Estilhaço de Brecha");
        AssertThat(Quests.RewardText(TestDb.Instance.Quests[1])).IsEqual("+1 Poção");
        s.Quests.Index = 4;
        Quests.Complete(close, s);
        AssertThat(s.Materials).ContainsKeyValue("estilhaco", 3);
        AssertThat(s.Quests.Index).IsEqual(5);
    }

    [TestCase]
    public void AllQuestsDoneMeansNoCurrent()
    {
        var s = NewSave();
        s.Quests.Index = TestDb.Instance.Quests.Count;
        AssertThat(Quests.Current(s)).IsNull();
    }
}
