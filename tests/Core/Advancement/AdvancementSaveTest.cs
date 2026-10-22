namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Save versionado com o bloco da progressão da v2 e a migração 2→3.</summary>
[TestSuite]
public class AdvancementSaveTest
{
    [TestCase]
    public void NewGameHasAnEmptyHunterBlock()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "bow");
        AssertThat(save.SaveVersion).IsEqual(3);
        AssertThat(save.Hunter.Xp).IsEqual(0);
        AssertThat(save.Hunter.Items).IsEmpty();
    }

    [TestCase]
    public void HunterBlockSurvivesARoundTrip()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "hammer");
        var engine = new ProgressionEngine(AdvTestData.Catalog, save.Hunter, save.ClassId, new Vespera.Core.Common.SeededRandom(5));
        engine.AddXp(200);
        engine.RecordGuardianKill("lodo_mae");
        foreach (var piece in AdvTestData.Catalog.Sets["conjunto_lodo_mae"].Pieces.Take(2))
        {
            var item = LootRoller.NewSetPiece(AdvTestData.Catalog.Sets["conjunto_lodo_mae"], piece);
            item.Id = save.Hunter.NextItemId++;
            save.Hunter.Items.Add(item);
            engine.Equip(item.Id);
        }

        engine.Roller.RollRareGear(Rank.D, engine.Random).Item!.Id = 0;
        engine.Progress.Fragmentos = 77;
        engine.BeginRun("pomar_alto");
        engine.Progress.RunFragmentos = 9;
        engine.EndRun(false);

        var json = SaveSerializer.ToJson(save);
        var loaded = SaveSerializer.FromJson(json, TestDb.Instance);
        var again = new ProgressionEngine(AdvTestData.Catalog, loaded.Hunter, loaded.ClassId, new Vespera.Core.Common.SeededRandom(5));

        AssertThat(again.Rank).IsEqual(Rank.D);
        AssertThat(loaded.Hunter.Fragmentos).IsEqual(77);
        AssertThat(loaded.Hunter.Xp).IsEqual(200);
        AssertThat(loaded.Hunter.GuardiansDefeated).ContainsExactly("lodo_mae");
        AssertThat(loaded.Hunter.Echoes).ContainsExactly("eco_viscoso");
        AssertThat(again.EchoIn(0)).IsEqual("eco_viscoso");
        AssertThat(again.LanternFragmentos("pomar_alto")).IsEqual(9);
        AssertThat(again.Equipment().SetBonuses.Hp).IsEqual(20);
        AssertThat(again.EquippedItems.Count()).IsEqual(2);
        AssertThat(again.EquippedItems.First().Rank).IsEqual(Rank.E);
        AssertThat(json).Contains("\"rank\": \"E\"");
    }

    [TestCase]
    public void SaveVersion2MigratesKeepingXpAndLumes()
    {
        const string v2 = """
            {"saveVersion": 2, "name": "Lia", "classId": "bow", "currentWeapon": "bow", "xp": 410, "lumes": 33,
             "weapons": {"bow": 3}, "savedAt": "2026-10-21T10:00:00+00:00"}
            """;

        var save = SaveSerializer.FromJson(v2, TestDb.Instance);

        AssertThat(save.SaveVersion).IsEqual(3);
        AssertThat(save.Hunter.Xp).IsEqual(410);
        AssertThat(save.Hunter.Lumes).IsEqual(33);
        AssertThat(save.Hunter.Fragmentos).IsEqual(0);
        AssertThat(save.Hunter.GuardiansDefeated).IsEmpty();
        AssertThat(save.Name).IsEqual("Lia");
        var engine = new ProgressionEngine(AdvTestData.Catalog, save.Hunter, save.ClassId, new Vespera.Core.Common.SeededRandom(1));
        AssertThat(engine.Rank).IsEqual(Rank.E);
        AssertThat(engine.WeaponStats.Level).IsEqual(1);
    }

    [TestCase]
    public void SaveVersion1MigratesAllTheWayToVersion3()
    {
        const string v1 = """{"saveVersion": 1, "totalXp": 320, "fragments": 42, "weaponLevel": 2, "day": 5}""";
        var save = SaveSerializer.FromJson(v1, TestDb.Instance);
        AssertThat(save.SaveVersion).IsEqual(3);
        AssertThat(save.Hunter.Xp).IsEqual(320);
        AssertThat(save.Hunter.Lumes).IsEqual(60);
    }

    [TestCase]
    public void Version3WithoutHunterBlockGetsAnEmptyOne()
    {
        var save = SaveSerializer.FromJson("""{"saveVersion": 3, "name": "Ana", "classId": "sword"}""", TestDb.Instance);
        AssertThat(save.Hunter).IsNotNull();
        AssertThat(save.Hunter.Materials).IsEmpty();
    }

    [TestCase]
    public void NullHunterBlockIsRepaired()
    {
        var save = SaveSerializer.FromJson("""{"saveVersion": 3, "classId": "sword", "hunter": null}""", TestDb.Instance);
        AssertThat(save.Hunter).IsNotNull();
    }
}
