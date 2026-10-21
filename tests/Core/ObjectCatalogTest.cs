namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ObjectCatalogTest
{
    private const string Sample = """
    {
      "objects": {
        "lampiao_ferro": {
          "name": "Lampião de ferro", "sprite": "res://assets/sprites/objects/city/lampiao_ferro.png", "ppu": 3,
          "anchor": [24, 140], "footprint": {"ellipse": [5, 2.5]}, "occluder": true,
          "light": {"kind": "a", "r": 40, "y": -38}, "interact": {"kind": "lamp", "label": "Apagar lampião"}
        },
        "barril": {"sprite": "atlas:pr_barrel", "breakable": "barrel", "footprint": {"circle": [0, 0, 6]}},
        "bau": {"sprite": "atlas:pr_chest1", "chest": 1},
        "salgueiro": {"sprite": "res://x.png", "sway": true, "footprint": {"rect": [-6, -3, 12, 4]}}
      }
    }
    """;

    [TestCase]
    public void ParsesEveryField()
    {
        var catalog = ObjectCatalog.Parse(Sample);
        AssertThat(catalog.Count).IsEqual(4);
        AssertThat(catalog.TryGet("lampiao_ferro", out var lamp)).IsTrue();
        AssertThat(lamp.Name).IsEqual("Lampião de ferro");
        AssertThat(lamp.PixelsPerUnit).IsEqual(3f);
        AssertThat(lamp.Anchor).IsEqual(new MapPoint(24, 140));
        AssertThat(lamp.Footprint!.Bounds).IsEqual(new MapRect(-5, -2.5f, 10, 5));
        AssertThat(lamp.Occluder).IsTrue();
        AssertThat(lamp.Light).IsEqual(new ObjectLight("a", 40, 0, -38));
        AssertThat(lamp.Interact!.Kind).IsEqual("lamp");
        AssertThat(lamp.Interact.Radius).IsEqual(MapLayout.DefaultDecorRadius);
        AssertThat(lamp.IsAtlasSprite).IsFalse();

        catalog.TryGet("barril", out var barrel);
        AssertThat(barrel.IsAtlasSprite).IsTrue();
        AssertThat(barrel.AtlasKey).IsEqual("pr_barrel");
        AssertThat(barrel.Breakable).IsEqual("barrel");
        AssertThat(barrel.PixelsPerUnit).IsEqual(1f);
        AssertThat(barrel.Anchor).IsNull();

        catalog.TryGet("bau", out var chest);
        AssertThat(chest.Chest ?? -1).IsEqual(1);
        AssertThat(chest.Footprint).IsNull();
        catalog.TryGet("salgueiro", out var willow);
        AssertThat(willow.Sway).IsTrue();
        AssertThat(willow.Footprint!.Bounds).IsEqual(new MapRect(-6, -3, 12, 4));
    }

    [TestCase]
    public void AcceptsListsAndBareDictionaries()
    {
        AssertThat(ObjectCatalog.Parse("""[ {"id": "a", "sprite": "atlas:pr_urn"} ]""").TryGet("a", out _)).IsTrue();
        AssertThat(ObjectCatalog.Parse("""{"objects": [ {"id": "a", "sprite": "atlas:pr_urn"} ]}""").Count).IsEqual(1);
        AssertThat(ObjectCatalog.Parse("""{"a": {"sprite": "atlas:pr_urn"}, "version": 2}""").Count).IsEqual(1);
    }

    [TestCase]
    public void RejectsObjectsWithoutSprite()
    {
        AssertThat(ThrowsFormat(() => ObjectCatalog.Parse("""{"objects": {"a": {"name": "sem sprite"}}}"""))).IsTrue();
        AssertThat(ThrowsFormat(() => ObjectCatalog.Parse("""[ {"sprite": "atlas:pr_urn"} ]"""))).IsTrue();
    }

    [TestCase]
    public void MergedCatalogPrefersTheSecond()
    {
        var game = ObjectCatalog.Parse("""{"a": {"sprite": "atlas:pr_urn"}, "b": {"sprite": "atlas:pr_sack"}}""");
        var test = ObjectCatalog.Parse("""{"b": {"sprite": "atlas:pr_barrel"}}""");
        var merged = game.With(test);
        AssertThat(merged.Count).IsEqual(2);
        merged.TryGet("b", out var b);
        AssertThat(b.AtlasKey).IsEqual("pr_barrel");
    }

    [TestCase]
    public void SmokeObjectFixtureParsesAndIsUsedByTheLayoutFixture()
    {
        var catalog = ObjectCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "objects_fixture.json")));
        var layout = MapLayout.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Layouts", "fixture_forge.json")));
        AssertThat(layout.Objects.Count).IsGreater(0);
        foreach (var o in layout.Objects)
        {
            AssertThat(catalog.TryGet(o.Id, out _)).OverrideFailureMessage($"fixture usa objeto '{o.Id}' fora do catálogo de teste").IsTrue();
        }
    }

    /// <summary>O GdUnit desembrulha a InnerException no AssertThrown; aqui conferimos o tipo lançado de fato.</summary>
    private static bool ThrowsFormat(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (MapFormatException)
        {
            return true;
        }
    }
}
