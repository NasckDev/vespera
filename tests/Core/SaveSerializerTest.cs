namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class SaveSerializerTest
{
    [TestCase]
    public void NewGameUsesPrototypeDefaults()
    {
        var s = SaveData.NewGame(TestDb.Instance, "  Ana  ", "hammer");
        AssertThat(s.Name).IsEqual("Ana");
        AssertThat(s.ClassId).IsEqual("hammer");
        AssertThat(s.CurrentWeapon).IsEqual("hammer");
        AssertThat(s.Weapons).ContainsKeyValue("hammer", 1);
        AssertThat(s.Weapons.Count).IsEqual(1);
        AssertThat(s.Lumes).IsEqual(60);
        AssertThat(s.Potions).IsEqual(3);
        AssertThat(s.Backpack).ContainsExactly("capuz", "luva");
        AssertThat(s.Equipment).ContainsKeyValue("lanterna", "lant0");
        AssertThat(s.Equipment).ContainsKeyValue("peito", "couraca");
        AssertThat(s.ClockMinutes).IsEqual(990.0);
        AssertThat(s.Day).IsEqual(1);
        AssertThat(s.LostLantern).IsNull();
    }

    [TestCase]
    public void NewGameCleansName()
    {
        AssertThat(SaveData.NewGame(TestDb.Instance, "   ", "sword").Name).IsEqual("Rui");
        AssertThat(SaveData.NewGame(TestDb.Instance, "Maximiliano Ferreira", "sword").Name).IsEqual("Maximiliano Fe");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void NewGameRejectsUnknownClass()
        => SaveData.NewGame(TestDb.Instance, "Rui", "lança");

    [TestCase]
    public void RoundTripKeepsAllFields()
    {
        var original = SaveData.NewGame(TestDb.Instance, "Rui", "dag");
        original.Xp = 320;
        original.Lumes = 77;
        original.Materials["presa"] = 4;
        original.Backpack.Add("capa");
        original.Equipment["maos"] = "luva";
        original.Weapons["dag"] = 3;
        original.Ecos.Add("uivo");
        original.EquippedEco = "uivo";
        original.EcoLevels["uivo"] = 1;
        original.Infusions["dag"] = ["solar"];
        original.ActiveInfusion["dag"] = "solar";
        original.Potions = 5;
        original.CarriedLanterns.Add("Nina");
        original.Day = 5;
        original.ClockMinutes = 1234.5;
        original.TutorialStep = 3;
        original.BrechasClosed = 2;
        original.FirstBrechaDone = true;
        original.Quests.Index = 4;
        original.Quests.Counters["dummy"] = 8;
        original.Quests.ClosedThemes.Add("fish");
        original.LostLantern = new LostLantern
        {
            BrechaId = 7, Bairro = "Mercado de Peixes", ThemeId = "fish", MapId = "fish2", Room = "core", X = 250, Y = 960,
            Bag = new RunBag { Lumes = 12, Materials = { ["isca"] = 2 }, Gear = { "bota" }, Lanterns = { "Bento" } },
        };

        var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(original), TestDb.Instance);

        AssertThat(loaded.Xp).IsEqual(320);
        AssertThat(loaded.Lumes).IsEqual(77);
        AssertThat(loaded.Materials).ContainsKeyValue("presa", 4);
        AssertThat(loaded.Backpack).ContainsExactly("capuz", "luva", "capa");
        AssertThat(loaded.Equipment).ContainsKeyValue("maos", "luva");
        AssertThat(loaded.Weapons).ContainsKeyValue("dag", 3);
        AssertThat(loaded.EquippedEco).IsEqual("uivo");
        AssertThat(loaded.EcoLevels).ContainsKeyValue("uivo", 1);
        AssertThat(loaded.Infusions["dag"]).ContainsExactly("solar");
        AssertThat(loaded.ActiveInfusion).ContainsKeyValue("dag", "solar");
        AssertThat(loaded.Potions).IsEqual(5);
        AssertThat(loaded.CarriedLanterns).ContainsExactly("Nina");
        AssertThat(loaded.Day).IsEqual(5);
        AssertThat(loaded.ClockMinutes).IsEqual(1234.5);
        AssertThat(loaded.TutorialStep).IsEqual(3);
        AssertThat(loaded.BrechasClosed).IsEqual(2);
        AssertThat(loaded.FirstBrechaDone).IsTrue();
        AssertThat(loaded.Quests.Index).IsEqual(4);
        AssertThat(loaded.Quests.Counter("dummy")).IsEqual(8);
        AssertThat(loaded.Quests.ClosedThemes).ContainsExactly("fish");
        var lost = loaded.LostLantern!;
        AssertThat(lost.BrechaId).IsEqual(7);
        AssertThat(lost.Room).IsEqual("core");
        AssertThat(lost.Bag.Lumes).IsEqual(12);
        AssertThat(lost.Bag.Materials).ContainsKeyValue("isca", 2);
        AssertThat(lost.Bag.Gear).ContainsExactly("bota");
        AssertThat(lost.Bag.Lanterns).ContainsExactly("Bento");
    }

    [TestCase]
    public void JsonUsesCamelCaseAndCurrentVersion()
    {
        var json = SaveSerializer.ToJson(SaveData.NewGame(TestDb.Instance, "Rui", "sword"));
        AssertThat(json).Contains("\"saveVersion\": 3");
        AssertThat(json).Contains("\"classId\": \"sword\"");
        AssertThat(json).NotContains("lostLantern");
    }

    [TestCase]
    public void MigratesFoundationSaveV1()
    {
        const string v1 = """
            {"saveVersion": 1, "totalXp": 320, "fragments": 42, "weaponLevel": 2, "day": 5,
             "lantern": {"districtId": "forjas", "fragments": 12}, "savedAt": "2026-10-20T10:00:00+00:00"}
            """;

        var s = SaveSerializer.FromJson(v1, TestDb.Instance);

        AssertThat(s.SaveVersion).IsEqual(3);
        AssertThat(s.Xp).IsEqual(320);
        AssertThat(s.Materials).ContainsKeyValue("estilhaco", 42);
        AssertThat(s.ClassId).IsEqual("sword");
        AssertThat(s.Weapons).ContainsKeyValue("sword", 2);
        AssertThat(s.Day).IsEqual(5);
        AssertThat(s.Lumes).IsEqual(60);
        AssertThat(s.Potions).IsEqual(3);
        AssertThat(s.Equipment).ContainsKeyValue("peito", "couraca");
        AssertThat(s.LostLantern!.Bairro).IsEqual("forjas");
        AssertThat(s.LostLantern.Bag.Materials).ContainsKeyValue("estilhaco", 12);
        AssertThat(s.SavedAt.Year).IsEqual(2026);
    }

    [TestCase]
    public void MigratesV1WithoutLantern()
    {
        var s = SaveSerializer.FromJson("""{"saveVersion": 1, "totalXp": 0, "fragments": 0}""", TestDb.Instance);
        AssertThat(s.LostLantern).IsNull();
        AssertThat(s.Materials.ContainsKey("estilhaco")).IsFalse();
        AssertThat(s.Weapons).ContainsKeyValue("sword", 1);
    }

    [TestCase]
    public void SanitizesMissingCollectionsAndUnknownIds()
    {
        const string json = """
            {"saveVersion": 2, "name": "Lia", "classId": "bow", "currentWeapon": "sword", "xp": 10,
             "materials": {"presa": 2, "dragao": 1}, "backpack": ["capuz", "espada_lendaria"],
             "equipment": {"peito": "capuz"}, "weapons": null, "activeInfusion": {"bow": "solar"}}
            """;
        var warnings = new List<string>();

        var s = SaveSerializer.FromJson(json, TestDb.Instance, warnings);

        AssertThat(s.CurrentWeapon).IsEqual("bow");
        AssertThat(s.Weapons).ContainsKeyValue("bow", 1);
        AssertThat(s.Materials.ContainsKey("dragao")).IsFalse();
        AssertThat(s.Backpack).ContainsExactly("capuz");
        AssertThat(s.Equipment.Count).IsEqual(0);
        AssertThat(s.ActiveInfusion.Count).IsEqual(0);
        AssertThat(s.Ecos).IsEmpty();
        AssertThat(s.Quests.Counters).IsEmpty();
        AssertThat(warnings.Count).IsGreaterEqual(4);
    }

    [TestCase]
    [ThrowsException(typeof(SaveFormatException))]
    public void RejectsSaveFromNewerGame()
        => SaveSerializer.FromJson("{\"saveVersion\": 999}", TestDb.Instance);

    [TestCase]
    [ThrowsException(typeof(SaveFormatException))]
    public void RejectsSaveWithoutVersion()
        => SaveSerializer.FromJson("{\"xp\": 10}", TestDb.Instance);

    [TestCase]
    [ThrowsException(typeof(SaveFormatException))]
    public void RejectsCorruptedJson()
        => SaveSerializer.FromJson("{ not json", TestDb.Instance);
}
