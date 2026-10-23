namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>O motor de progressão: loot aplicado, Guardiões, equipamento, conjuntos, Ecos e a Lanterna.</summary>
[TestSuite]
public class ProgressionEngineTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    private static ItemInstance Piece(ProgressionEngine engine, string pieceId)
    {
        var set = Catalog.Sets["conjunto_lodo_mae"];
        var def = set.Pieces.First(p => p.Id == pieceId);
        var item = LootRoller.NewSetPiece(set, def);
        item.Id = engine.Progress.NextItemId++;
        engine.Progress.Items.Add(item);
        return item;
    }

    // ---------- loot ----------

    [TestCase]
    public void GrantLootAddsMaterialsAndFragmentosToTheWalletOutsideARun()
    {
        var engine = AdvTestData.NewEngine(seed: 3);
        var grant = engine.GrantLoot(Rank.E, LootSource.Chest);
        AssertThat(grant.FragmentosHeldInRun).IsFalse();
        AssertThat(engine.Progress.Fragmentos).IsEqual(grant.FragmentosGained);
        AssertThat(grant.FragmentosGained).IsGreater(0);
        var total = engine.Progress.Materials.Values.Sum() + engine.Items.Count;
        AssertThat(total).IsEqual(grant.Loot.Entries.Sum(e => e.Amount));
    }

    [TestCase]
    public void LootPiecesGetUniqueIds()
    {
        var engine = AdvTestData.NewEngine();
        var rare1 = engine.Roller.RollRareGear(Rank.C, engine.Random);
        var rare2 = engine.Roller.RollRareGear(Rank.C, engine.Random);
        engine.Apply(new LootResult(Rank.C, LootSource.Chest, [rare1, rare2], 0));
        AssertThat(engine.Items.Select(i => i.Id).Distinct().Count()).IsEqual(2);
        AssertThat(engine.Items.All(i => i.Id > 0)).IsTrue();
    }

    [TestCase]
    public void FragmentosBonusFromGearRaisesFragmentos()
    {
        var engine = AdvTestData.NewEngine(seed: 9);
        engine.Progress.Items.Add(new ItemInstance { Id = 1, DefId = "x", Slot = "cabeca", Rank = Rank.E, Name = "Capuz", AffixId = "fartura", Stats = new StatBlock { FragmentosPct = 100 } });
        engine.Progress.NextItemId = 2;
        engine.Equip(1);
        var rolled = new LootResult(Rank.E, LootSource.Chest, [], 20);
        var grant = engine.Apply(rolled);
        AssertThat(grant.FragmentosGained).IsEqual(40);
        AssertThat(engine.Progress.Fragmentos).IsEqual(40);
    }

    [TestCase]
    public void SellingMaterialsGivesLumes()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.Materials["gel_de_lodo"] = 5;
        AssertThat(engine.SellMaterial("gel_de_lodo", 3)).IsEqual(6);
        AssertThat(engine.Progress.Lumes).IsEqual(6);
        AssertThat(engine.Progress.MaterialCount("gel_de_lodo")).IsEqual(2);
        AssertThat(engine.SellMaterial("gel_de_lodo", 9)).IsEqual(0);
        AssertThat(engine.SellMaterial("nao_existe", 1)).IsEqual(0);
    }

    [TestCase]
    public void SpendingLumesNeverGoesNegative()
    {
        var engine = AdvTestData.NewEngine();
        engine.AddLumes(10);
        AssertThat(engine.TrySpendLumes(11)).IsFalse();
        AssertThat(engine.TrySpendLumes(10)).IsTrue();
        AssertThat(engine.Progress.Lumes).IsEqual(0);
    }

    // ---------- Guardiões ----------

    [TestCase]
    public void LodoMaeKillGivesMaterialEchoAndAutoEquipsIt()
    {
        var engine = AdvTestData.NewEngine(seed: 5);
        var kill = engine.RecordGuardianKill("lodo_mae");
        AssertThat(kill.FirstKill).IsTrue();
        AssertThat(engine.Progress.MaterialCount("nucleo_lodo_mae")).IsGreaterEqual(1);
        AssertThat(kill.EchoGained!.Id).IsEqual("eco_viscoso");
        AssertThat(engine.Progress.Echoes).ContainsExactly("eco_viscoso");
        AssertThat(engine.EchoIn(0)).IsEqual("eco_viscoso");
        AssertThat(engine.EchoIn(1)).IsNull();
        AssertThat(engine.RecordGuardianKill("lodo_mae").EchoGained).IsNull();
    }

    [TestCase]
    public void UnknownGuardianIsRejected()
    {
        var engine = AdvTestData.NewEngine();
        try
        {
            engine.RecordGuardianKill("dragao");
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("esperava ArgumentException");
    }

    // ---------- equipamento ----------

    [TestCase]
    public void EquippingSwapsThePieceInTheSlot()
    {
        var engine = AdvTestData.NewEngine();
        var capuz = Piece(engine, "capuz_louca_trincada");
        var rare = engine.Roller.RollRareGear(Rank.E, engine.Random);
        var other = rare.Item!;
        other.Slot = "cabeca";
        other.Id = engine.Progress.NextItemId++;
        engine.Progress.Items.Add(other);

        AssertThat(engine.Equip(capuz.Id).Ok).IsTrue();
        AssertThat(engine.EquippedIn("cabeca")!.Id).IsEqual(capuz.Id);
        AssertThat(engine.Equip(other.Id).Ok).IsTrue();
        AssertThat(engine.EquippedIn("cabeca")!.Id).IsEqual(other.Id);
        AssertThat(engine.BackpackItems.Select(i => i.Id).ToArray()).ContainsExactly(capuz.Id);
        AssertThat(engine.Unequip("cabeca")).IsTrue();
        AssertThat(engine.EquippedIn("cabeca")).IsNull();
    }

    [TestCase]
    public void RankLocksPiecesAboveTheHunter()
    {
        var engine = AdvTestData.NewEngine();
        var item = new ItemInstance { Id = 7, DefId = "rara:peito", Slot = "peito", Rank = Rank.B, Name = "Gibão do Vigor", AffixId = "vigor", Stats = new StatBlock { Hp = 40 } };
        engine.Progress.Items.Add(item);
        var result = engine.Equip(7);
        AssertThat(result.Ok).IsFalse();
        AssertThat(result.Reason).Contains("rank B");
        AssertThat(engine.EquippedIn("peito")).IsNull();
        AssertThat(engine.Equip(999).Ok).IsFalse();
    }

    [TestCase]
    public void SetBonusesActivateAtTwoAndFourPiecesCumulatively()
    {
        var engine = AdvTestData.NewEngine();
        var ids = Catalog.Sets["conjunto_lodo_mae"].Pieces.Select(p => Piece(engine, p.Id).Id).ToArray();

        engine.Equip(ids[0]);
        var one = engine.Equipment();
        AssertThat(one.SetBonuses.IsZero).IsTrue();
        AssertThat(one.Sets[0].Pieces).IsEqual(1);

        engine.Equip(ids[1]);
        var two = engine.Equipment();
        AssertThat(two.SetBonuses.Hp).IsEqual(20);
        AssertThat(two.SetBonuses.Defense).IsEqual(0);
        AssertThat(two.Sets[0].Bonuses.Select(b => b.Active).ToArray()).ContainsExactly(true, false);

        engine.Equip(ids[2]);
        AssertThat(engine.Equipment().SetBonuses.Hp).IsEqual(20);

        engine.Equip(ids[3]);
        var four = engine.Equipment();
        AssertThat(four.SetBonuses.Hp).IsEqual(20);
        AssertThat(four.SetBonuses.Defense).IsEqual(3);
        AssertThat(four.SetBonuses.FragmentosPct).IsEqual(10.0);
        AssertThat(four.Items.Hp).IsEqual(10 + 16 + 6 + 8);
        AssertThat(four.Total.Hp).IsEqual(four.Items.Hp + 20);
        AssertThat(four.Sets[0].Bonuses.All(b => b.Active)).IsTrue();
    }

    [TestCase]
    public void MaxHpBonusCombinesRankAndGear()
    {
        var engine = AdvTestData.HunterAt(Rank.D);
        var capuz = Piece(engine, "capuz_louca_trincada");
        engine.Equip(capuz.Id);
        AssertThat(engine.MaxHpBonus).IsEqual(20 + 10);
    }

    // ---------- Ecos ----------

    [TestCase]
    public void TwoEchoSlotsAndSwapping()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.Echoes.AddRange(["eco_viscoso", "eco_semente", "eco_farol"]);
        AssertThat(engine.EchoSlotCount).IsEqual(2);
        AssertThat(engine.EquipEcho(0, "eco_viscoso").Ok).IsTrue();
        AssertThat(engine.EquipEcho(1, "eco_semente").Ok).IsTrue();
        AssertThat(engine.EquipEcho(0, "eco_semente").Ok).IsTrue();
        AssertThat(engine.EchoIn(0)).IsEqual("eco_semente");
        AssertThat(engine.EchoIn(1)).IsEqual("eco_viscoso");
        AssertThat(engine.EquipEcho(1, "eco_farol").Ok).IsTrue();
        AssertThat(engine.EquipEcho(2, "eco_farol").Ok).IsFalse();
        AssertThat(engine.EquipEcho(0, "eco_sino").Ok).IsFalse();
        AssertThat(engine.UnequipEcho(0)).IsTrue();
        AssertThat(engine.UnequipEcho(0)).IsFalse();
    }

    // ---------- Lanterna ----------

    [TestCase]
    public void FragmentosOfARunStayInThePotUntilYouLeaveAlive()
    {
        var engine = AdvTestData.NewEngine(seed: 2);
        engine.BeginRun("bairro_das_forjas");
        var grant = engine.GrantLoot(Rank.E, LootSource.Chest);
        AssertThat(grant.FragmentosHeldInRun).IsTrue();
        AssertThat(engine.Progress.Fragmentos).IsEqual(0);
        AssertThat(engine.RunFragmentos).IsEqual(grant.FragmentosGained);

        var end = engine.EndRun(survived: true)!;
        AssertThat(end.FragmentosBanked).IsEqual(grant.FragmentosGained);
        AssertThat(engine.Progress.Fragmentos).IsEqual(grant.FragmentosGained);
        AssertThat(engine.InRun).IsFalse();
    }

    [TestCase]
    public void DyingLeavesFragmentosInTheRiftButKeepsMaterialsAndGear()
    {
        var engine = AdvTestData.NewEngine(seed: 2);
        engine.BeginRun("bairro_das_forjas");
        var grant = engine.GrantLoot(Rank.E, LootSource.Chest);
        var materials = engine.Progress.Materials.Values.Sum();
        var end = engine.EndRun(survived: false)!;

        AssertThat(end.Survived).IsFalse();
        AssertThat(end.FragmentosLeft).IsEqual(grant.FragmentosGained);
        AssertThat(engine.Progress.Fragmentos).IsEqual(0);
        AssertThat(engine.LanternFragmentos("bairro_das_forjas")).IsEqual(grant.FragmentosGained);
        AssertThat(engine.Progress.Materials.Values.Sum()).IsEqual(materials);
    }

    [TestCase]
    public void LanternIsRecoveredOnTheNextEntryInTheSameNeighborhoodOnly()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.Fragmentos = 5;
        engine.BeginRun("bairro_das_forjas");
        engine.Progress.RunFragmentos = 33;
        engine.EndRun(survived: false);

        AssertThat(engine.BeginRun("pomar_alto")).IsEqual(0);
        engine.EndRun(survived: true);
        AssertThat(engine.LanternFragmentos("bairro_das_forjas")).IsEqual(33);

        AssertThat(engine.BeginRun("bairro_das_forjas")).IsEqual(33);
        AssertThat(engine.LanternFragmentos("bairro_das_forjas")).IsEqual(0);
        AssertThat(engine.RunFragmentos).IsEqual(33);
        engine.EndRun(survived: true);
        AssertThat(engine.Progress.Fragmentos).IsEqual(38);
    }

    [TestCase]
    public void DyingTwiceBeforeRecoveringAddsUp()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.LostFragmentos["pomar_alto"] = 10;
        engine.BeginRun("bairro_das_forjas");
        engine.Progress.RunFragmentos = 7;
        engine.EndRun(false);
        engine.BeginRun("bairro_das_forjas");
        engine.Progress.RunFragmentos += 4;
        engine.EndRun(false);
        AssertThat(engine.LanternFragmentos("bairro_das_forjas")).IsEqual(11);
        AssertThat(engine.TotalLostFragmentos).IsEqual(21);
    }

    [TestCase]
    public void EndRunWithoutARunDoesNothing()
    {
        AssertThat(AdvTestData.NewEngine().EndRun(true)).IsNull();
    }

    [TestCase]
    public void LoadingASaveInTheMiddleOfARunCountsAsAbandoned()
    {
        var progress = new HunterProgress { RunNeighborhood = "cais", RunFragmentos = 12 };
        var engine = AdvTestData.NewEngine(progress: progress);
        AssertThat(engine.InRun).IsFalse();
        AssertThat(engine.LanternFragmentos("cais")).IsEqual(12);
        AssertThat(engine.Warnings.Any(w => w.Contains("abandonada"))).IsTrue();
    }

    // ---------- sanitização ----------

    [TestCase]
    public void SanitizeDropsUnknownIdsAndFixesEcoSlots()
    {
        var progress = new HunterProgress
        {
            Xp = -5,
            Materials = new() { ["gel_de_lodo"] = 2, ["dragao"] = 1, ["veu_de_espectro"] = 0 },
            GuardiansDefeated = ["lodo_mae", "lodo_mae", "fantasma"],
            Echoes = ["eco_viscoso", "eco_inexistente"],
            EchoSlots = ["eco_viscoso", "eco_viscoso", "eco_sino"],
            WeaponLevels = new() { ["sword"] = 99, ["staff"] = 3 },
            Items = [new ItemInstance { Id = 3, Slot = "cabeca", Rank = Rank.E, Name = "Capuz" }, new ItemInstance { Id = 4, Slot = "cintura", Rank = Rank.E, Name = "?" }],
            Equipped = new() { ["cabeca"] = 3, ["peito"] = 3, ["botas"] = 77 },
        };

        var engine = AdvTestData.NewEngine(progress: progress);

        AssertThat(progress.Xp).IsEqual(0);
        AssertThat(progress.Materials.Keys.ToArray()).ContainsExactly("gel_de_lodo");
        AssertThat(progress.GuardiansDefeated).ContainsExactly("lodo_mae");
        AssertThat(progress.Echoes).ContainsExactly("eco_viscoso");
        AssertThat(progress.EchoSlots.ToArray()).ContainsExactly("eco_viscoso", null);
        AssertThat(progress.WeaponLevels).ContainsKeyValue("sword", 18);
        AssertThat(progress.WeaponLevels.ContainsKey("staff")).IsFalse();
        AssertThat(progress.Items.Count).IsEqual(1);
        AssertThat(progress.Equipped.Keys.ToArray()).ContainsExactly("cabeca");
        AssertThat(progress.NextItemId).IsGreater(3);
        AssertThat(engine.Warnings.Count).IsGreaterEqual(5);
    }

    [TestCase]
    public void UnknownClassFallsBackToTheFirstWeapon()
    {
        var engine = AdvTestData.NewEngine("staff");
        AssertThat(engine.ClassId).IsEqual("sword");
        AssertThat(engine.WeaponStats.Level).IsEqual(1);
    }

    [TestCase]
    public void NewEngineStartsTheClassWeaponAtLevelOne()
    {
        foreach (var classId in new[] { "sword", "bow", "hammer" })
        {
            var engine = AdvTestData.NewEngine(classId);
            AssertThat(engine.Progress.WeaponLevels[classId]).IsEqual(1);
            AssertThat(engine.WeaponStats.Name).IsEqual(Catalog.Weapons[classId].Tiers[0].Name);
        }
    }

    [TestCase]
    public void FullFlowFromFirstRunToRankD()
    {
        var engine = AdvTestData.NewEngine(seed: 12);
        engine.BeginRun("bairro_das_forjas");
        for (var i = 0; i < 40; i++)
        {
            engine.GrantLoot(Rank.E, LootSource.Monster);
        }

        engine.GrantLoot(Rank.E, LootSource.Chest);
        engine.GrantLoot(Rank.E, LootSource.Chest);
        engine.AddXp(130);
        var kill = engine.RecordGuardianKill("lodo_mae");
        engine.EndRun(survived: true);

        AssertThat(kill.RankChange!.To).IsEqual(Rank.D);
        AssertThat(engine.Progress.Fragmentos).IsGreater(24);
        AssertThat(engine.CanUpgrade().CanUpgrade).IsTrue();
        engine.Upgrade();
        AssertThat(engine.WeaponStats.Level).IsEqual(2);
    }

    [TestCase]
    public void AddXpRejectsNegativeAmounts()
    {
        var engine = AdvTestData.NewEngine();
        try
        {
            engine.AddXp(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException("esperava ArgumentOutOfRangeException");
    }
}
