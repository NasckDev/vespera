namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Save;
using static GdUnit4.Assertions;

[TestSuite]
public class SaveSerializerTest
{
    [TestCase]
    public void RoundTripKeepsAllFields()
    {
        var original = new SaveData
        {
            TotalXp = 320,
            Fragments = 42,
            WeaponLevel = 2,
            Day = 5,
            Lantern = new LanternData { DistrictId = "forjas", Fragments = 12 },
        };

        var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(original));

        AssertThat(loaded.TotalXp).IsEqual(320);
        AssertThat(loaded.Fragments).IsEqual(42);
        AssertThat(loaded.WeaponLevel).IsEqual(2);
        AssertThat(loaded.Day).IsEqual(5);
        AssertThat(loaded.Lantern!.DistrictId).IsEqual("forjas");
        AssertThat(loaded.Lantern.Fragments).IsEqual(12);
    }

    [TestCase]
    public void JsonUsesCamelCaseSaveVersion()
        => AssertThat(SaveSerializer.ToJson(new SaveData())).Contains("\"saveVersion\": 1");

    [TestCase]
    [ThrowsException(typeof(SaveFormatException))]
    public void RejectsSaveFromNewerGame()
        => SaveSerializer.FromJson("{\"saveVersion\": 999}");

    [TestCase]
    [ThrowsException(typeof(SaveFormatException))]
    public void RejectsCorruptedJson()
        => SaveSerializer.FromJson("{ not json");
}
