namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

/// <summary>Layouts reais dos mapas com a arte aprovada do designer (frente C2, rodada de correção 1).</summary>
[TestSuite]
public class ArteLayoutTest
{
    private static MapLayout Load(string id) =>
        MapLayout.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Layouts", $"layout_{id}.json")));

    private static readonly string[] MapIds = ["guild", "forge", "inn", "fish2", "crypt2", "mine", "garden", "cath", "core", "shop_arm", "shop_mag", "shop_alq", "shop_rel"];

    /// <summary>ARTE-2: ids do catálogo que têm a arte de estados do designer (<c>PropArt</c>) e onde cada um aparece no jogo real.</summary>
    [TestCase]
    public void EveryStatefulObjectFamilyIsPlacedInARealMap()
    {
        var placed = MapIds.SelectMany(id => Load(id).Objects.Select(o => o.Id)).ToHashSet(StringComparer.Ordinal);
        string[][] families =
        [
            ["lamp_post_common"],
            ["brazier_stand"],
            ["flowers_blue", "flowers_yellow", "flower_violet_glow"],
            ["lever_red"],
            ["door_arched"],
            ["notice_board"],
        ];
        foreach (var family in families)
        {
            AssertThat(family.Any(placed.Contains)).IsTrue();
        }
    }

    /// <summary>ARTE-2: todo objeto posto nos layouts reais existe no catálogo de objetos.</summary>
    [TestCase]
    public void PlacedObjectsExistInTheCatalog()
    {
        var catalog = DressingCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "objects.json")));
        var unknown = MapIds.SelectMany(id => Load(id).Objects.Select(o => o.Id)).Where(i => !catalog.TryGet(i, out _)).ToList();
        AssertThat(unknown.Count).IsEqual(0);
    }

    /// <summary>
    /// ARTE-7: o recorte do lampião da ponte grande acompanha a silhueta (poste, lanterna e base). Um retângulo cheio
    /// repintava o chão por cima do monstro que passa atrás dele.
    /// </summary>
    [TestCase]
    public void BigBridgeLampOccludersFollowTheLampSilhouette()
    {
        var lamps = Load("forge").Occluders.Where(o => o.Note is { } n && n.Contains("lampião", StringComparison.Ordinal) && n.Contains("ponte grande", StringComparison.Ordinal)).ToList();
        AssertThat(lamps.Count).IsEqual(2);
        foreach (var lamp in lamps)
        {
            var fill = PolygonMath.Area(lamp.Polygon) / (lamp.Bounds.Width * lamp.Bounds.Height);
            AssertThat(lamp.Polygon.Count >= 8).IsTrue();
            AssertThat(fill < 0.8f).IsTrue();
        }
    }
}
