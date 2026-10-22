namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class MapLayoutTest
{
    private const string Sample = """
    {
      "cell": 4, "base": "mask",
      "walk": [ {"rect": [10, 20, 30, 40], "note": "escada"} ],
      "block": [ {"circle": [100, 100, 10]} ],
      "occluders": [
        {"poly": [[570, 420], [598, 420], [598, 516], [570, 516]], "baseY": 515, "note": "lampião"},
        {"poly": [[0, 0], [40, 0], [20, 60]], "sway": true}
      ],
      "water": [ {"poly": [[700, 100], [760, 100], [760, 300], [700, 300]]} ],
      "ambient": [ {"kind": "water", "x": 730, "y": 200, "r": 200}, {"kind": "birds", "x": 1, "y": 2} ],
      "props": [ {"kind": "barrel", "x": 300, "y": 400} ],
      "chests": [ {"x": 320, "y": 410, "tier": 3}, {"x": 1, "y": 1} ],
      "decor": [
        {"kind": "boat", "x": 700, "y": 600, "r": 30, "label": "Atravessar", "to": [900, 600]},
        {"kind": "sign", "x": 10, "y": 10, "text": "Rua dos Ofícios"}
      ],
      "fx": [ {"kind": "smoke", "x": 260, "y": 60} ],
      "objects": [ {"id": "lampiao_ferro", "x": 400, "y": 500, "flip": true}, {"id": "banca", "x": 1, "y": 2, "text": "Preços do dia.", "label": "Ler"} ]
    }
    """;

    [TestCase]
    public void ParsesEveryList()
    {
        var layout = MapLayout.Parse(Sample);
        AssertThat(layout.Cell).IsEqual(4);
        AssertThat(layout.Walk.Count).IsEqual(1);
        AssertThat(layout.Walk[0].Bounds).IsEqual(new MapRect(10, 20, 30, 40));
        AssertThat(layout.Walk[0].Note).IsEqual("escada");
        AssertThat(layout.Block[0].Polygon.Count).IsEqual(24);
        AssertThat(layout.Occluders.Count).IsEqual(2);
        AssertThat(layout.Water.Count).IsEqual(1);
        AssertThat(layout.Props[0]).IsEqual(new LayoutProp("barrel", 300, 400));
        AssertThat(layout.Chests[0].Tier).IsEqual(3);
        AssertThat(layout.Decor[0].To).IsEqual(new MapPoint(900, 600));
        AssertThat(layout.Decor[0].Label).IsEqual("Atravessar");
        AssertThat(layout.Decor[1].Text).IsEqual("Rua dos Ofícios");
        AssertThat(layout.Fx[0].Kind).IsEqual("smoke");
        AssertThat(layout.Objects[0]).IsEqual(new LayoutObject("lampiao_ferro", 400, 500, true));
        AssertThat(layout.Objects[1].Flip).IsFalse();
        AssertThat(layout.Objects[0].Text).IsNull();
        AssertThat(layout.Objects[1].Text).IsEqual("Preços do dia.");
        AssertThat(layout.Objects[1].Label).IsEqual("Ler");
    }

    [TestCase]
    public void OccluderKeepsBaseYAndSway()
    {
        var layout = MapLayout.Parse(Sample);
        var lamp = layout.Occluders[0];
        AssertThat(lamp.BaseY).IsEqual(515f);
        AssertThat(lamp.Sway).IsFalse();
        AssertThat(lamp.Bounds).IsEqual(new MapRect(570, 420, 28, 96));
        AssertThat(lamp.Note).IsEqual("lampião");

        // Sem baseY, a base é o fundo do recorte; "sway": true balança.
        var tree = layout.Occluders[1];
        AssertThat(tree.BaseY).IsEqual(60f);
        AssertThat(tree.Sway).IsTrue();
    }

    [TestCase]
    public void DefaultsForOptionalFields()
    {
        var layout = MapLayout.Parse(Sample);
        AssertThat(layout.Ambient[1].Radius).IsEqual(MapLayout.DefaultAmbientRadius);
        AssertThat(layout.Chests[1].Tier).IsEqual(1);
        AssertThat(layout.Decor[1].Radius).IsEqual(MapLayout.DefaultDecorRadius);
        AssertThat(layout.Decor[1].To).IsNull();
        AssertThat(layout.Fx[0].Radius).IsEqual(MapLayout.DefaultFxRadius);
    }

    [TestCase]
    public void EmptyLayoutHasNoObjects()
    {
        var layout = MapLayout.Parse("""{"cell": 4, "base": "shapes", "walk": [], "block": [], "occluders": [], "props": [], "chests": [], "decor": []}""");
        AssertThat(layout.Base).IsEqual("shapes");
        AssertThat(layout.Occluders.Count + layout.Props.Count + layout.Chests.Count + layout.Decor.Count + layout.Water.Count + layout.Ambient.Count + layout.Fx.Count).IsEqual(0);
    }

    [TestCase]
    public void InvalidShapesAreRejected()
    {
        AssertThat(ThrowsFormat(() => MapLayout.Parse("""{"water": [ {"poly": [[0,0],[1,1]]} ]}"""))).IsTrue();
        AssertThat(ThrowsFormat(() => MapLayout.Parse("""{"water": [ {"note": "sem forma"} ]}"""))).IsTrue();
        AssertThat(ThrowsFormat(() => MapLayout.Parse("""{"props": [ {"x": 1, "y": 2} ]}"""))).IsTrue();
        AssertThat(ThrowsFormat(() => MapLayout.Parse("""{"objects": [ {"x": 1, "y": 2} ]}"""))).IsTrue();
    }

    [TestCase]
    public void EveryLayoutInTheRepositoryParses()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Layouts"), "*.json");
        AssertThat(files.Length).IsGreater(12); // 13 mapas + fixtures do smoke
        foreach (var file in files)
        {
            var layout = MapLayout.Parse(File.ReadAllText(file));
            AssertThat(layout.Cell).IsGreater(0);
        }
    }

    [TestCase]
    public void SmokeFixturesHaveEveryObjectKind()
    {
        var layout = MapLayout.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Layouts", "fixture_forge.json")));
        AssertThat(layout.Occluders.Count).IsGreater(0);
        AssertThat(layout.Water.Count).IsGreater(0);
        AssertThat(layout.Props.Count).IsGreater(0);
        AssertThat(layout.Chests.Count).IsGreater(0);
        AssertThat(layout.Ambient.Count).IsGreater(0);
        foreach (var kind in new[] { "lamp", "flower", "boat", "fishing", "sign" })
        {
            AssertThat(layout.Decor.Any(d => d.Kind == kind)).OverrideFailureMessage($"fixture sem decor '{kind}'").IsTrue();
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
