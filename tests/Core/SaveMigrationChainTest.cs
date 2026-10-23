namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// Cadeia única de migração do save depois da integração do Ato I: um só número de versão (3) com os dois blocos novos
/// (<c>hunter</c> da progressão e <c>story</c> da história). v1 e v2 chegam à versão atual pelo mesmo caminho.
/// </summary>
[TestSuite]
public class SaveMigrationChainTest
{
    [TestCase]
    public void CurrentVersionIsThreeAndCarriesHunterAndStory()
    {
        AssertThat(SaveData.CurrentVersion).IsEqual(3);
        var data = SaveData.NewGame(TestDb.Instance, "Ana", "sword");
        data.Hunter.Xp = 129;
        data.Hunter.GuardiansDefeated.Add("lodo_mae");
        data.Story.Add("starter_blade");
        data.Story.ObjectiveId = "ato2";

        var back = SaveSerializer.FromJson(SaveSerializer.ToJson(data), TestDb.Instance);

        AssertThat(back.SaveVersion).IsEqual(3);
        AssertThat(back.Hunter.Xp).IsEqual(129);
        AssertThat(back.Hunter.GuardiansDefeated).ContainsExactly("lodo_mae");
        AssertThat(back.Story.Flags).ContainsExactly("starter_blade");
        AssertThat(back.Story.ObjectiveId).IsEqual("ato2");
    }

    [TestCase]
    public void Version1ReachesCurrentWithHunterAndEmptyStory()
    {
        var save = SaveSerializer.FromJson("""{"saveVersion": 1, "totalXp": 320, "fragments": 42, "weaponLevel": 2, "day": 5}""", TestDb.Instance);

        AssertThat(save.SaveVersion).IsEqual(SaveData.CurrentVersion);
        AssertThat(save.Hunter.Xp).IsEqual(320);
        AssertThat(save.Story).IsNotNull();
        AssertThat(save.Story.Flags).IsEmpty();
    }

    [TestCase]
    public void Version2WithoutStoryReachesCurrentWithEmptyStory()
    {
        const string v2 = """{"saveVersion": 2, "name": "Lia", "classId": "sword", "xp": 50, "lumes": 7}""";
        var save = SaveSerializer.FromJson(v2, TestDb.Instance);

        AssertThat(save.SaveVersion).IsEqual(SaveData.CurrentVersion);
        AssertThat(save.Hunter.Xp).IsEqual(50);
        AssertThat(save.Hunter.Lumes).IsEqual(7);
        AssertThat(save.Story.Flags).IsEmpty();
    }

    [TestCase]
    public void Version2FromTheStoryBuildKeepsItsStoryBlock()
    {
        // A ramificação da história gravava o bloco "story" ainda na versão 2: ele sobrevive à migração para a 3.
        const string v2 = """
            {"saveVersion": 2, "name": "Lia", "classId": "sword", "xp": 12, "lumes": 0,
             "story": {"flags": ["arrival_done", "registered", "met_ravi"], "objectiveId": "odete", "levelId": "forjas"}}
            """;
        var save = SaveSerializer.FromJson(v2, TestDb.Instance);

        AssertThat(save.SaveVersion).IsEqual(SaveData.CurrentVersion);
        AssertThat(save.Story.Flags).ContainsExactly("arrival_done", "registered", "met_ravi");
        AssertThat(save.Story.ObjectiveId).IsEqual("odete");
        AssertThat(save.Story.LevelId).IsEqual("forjas");
        AssertThat(save.Hunter.Xp).IsEqual(12);
    }
}
