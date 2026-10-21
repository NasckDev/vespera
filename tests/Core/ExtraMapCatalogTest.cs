namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ExtraMapCatalogTest
{
    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", name));

    private static readonly Lazy<MapCatalog> Maps = new(() => MapCatalog.Parse(Data("maps.json")));

    private static readonly Lazy<ExtraMapCatalog> Real = new(() => ExtraMapCatalog.Parse(Data("maps_extra.json"), Maps.Value));

    [TestCase]
    public void EveryCurrentBrechaHasAnExtraRoom()
    {
        foreach (var id in new[] { "fish2", "crypt2", "mine", "garden", "cath" })
        {
            AssertThat(Real.Value.RoomsOf(id).Any()).OverrideFailureMessage($"{id} sem sala extra").IsTrue();
        }
    }

    [TestCase]
    public void NewBrechasSixToTenHaveRooms()
    {
        AssertThat(Real.Value.Brechas.Keys.OrderBy(k => k).ToArray()).IsEqual(new[] { 6, 7, 8, 9, 10 });
        AssertThat(Real.Value.Brechas[10]).IsEqual("Observatório Astral");
        for (var n = 6; n <= 10; n++)
        {
            AssertThat(Real.Value.RoomsOfBrecha(n).Count() >= 2).OverrideFailureMessage($"Brecha {n} com menos de 2 salas").IsTrue();
        }
    }

    [TestCase]
    public void PairsAreSplitAndNothingHasCollisionYet()
    {
        var maps = Real.Value.Maps;
        AssertThat(maps.Count).IsEqual(20);
        AssertThat(maps.All(m => !m.HasCollision)).IsTrue();
        // Pares 1774×887 cortados no vão entre os dois mapas: as metades somam a largura original.
        foreach (var source in new[] { "58", "60", "62", "64", "66" })
        {
            var halves = maps.Where(m => m.Source.StartsWith(source, StringComparison.Ordinal)).ToList();
            AssertThat(halves.Count).IsEqual(2);
            AssertThat(halves.Sum(m => m.ImageWidth)).IsEqual(1774);
            AssertThat(halves.All(m => m.ImageHeight == 887 && m.ImageWidth is > 700 and < 1100)).IsTrue();
        }

        AssertThat(maps.Count(m => m.Source is "58L" or "58R")).IsEqual(2);
    }

    [TestCase]
    public void ImagesExist()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vespera.sln")))
        {
            dir = dir.Parent;
        }

        AssertThat(dir).IsNotNull();
        foreach (var m in Real.Value.Maps)
        {
            AssertThat(File.Exists(Path.Combine(dir!.FullName, m.Image["res://".Length..]))).OverrideFailureMessage(m.Image).IsTrue();
        }
    }

    [TestCase]
    public void RejectsRoomForUnknownMap()
    {
        var broken = Data("maps_extra.json").Replace("\"map\": \"fish2\"", "\"map\": \"atlantis\"", StringComparison.Ordinal);
        try
        {
            ExtraMapCatalog.Parse(broken, Maps.Value);
        }
        catch (MapFormatException ex)
        {
            AssertThat(ex.Message).Contains("atlantis");
            return;
        }

        throw new InvalidOperationException("sala extra para mapa inexistente deveria ser recusada");
    }
}
