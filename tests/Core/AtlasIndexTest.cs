namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Atlas;
using static GdUnit4.Assertions;

[TestSuite]
public class AtlasIndexTest
{
    private static string RealAtlasJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "atlas.json"));

    [TestCase]
    public void ParsesFrameFields()
    {
        var atlas = AtlasIndex.Parse("{\"shadow\":[546,1938,102,25,50,25,5]}");
        AssertThat(atlas.Count).IsEqual(1);
        AssertThat(atlas["shadow"]).IsEqual(new AtlasFrame(546, 1938, 102, 25, 50, 25, 5));
    }

    [TestCase]
    public void MissingOrZeroDensityMeansOne()
    {
        var atlas = AtlasIndex.Parse("{\"a\":[0,0,10,10,5,10],\"b\":[0,0,10,10,5,10,0]}");
        AssertThat(atlas["a"].Density).IsEqual(1.0);
        AssertThat(atlas["b"].Density).IsEqual(1.0);
    }

    [TestCase]
    public void AnchorXOverrideIsInWorldUnitsTimesDensity()
    {
        var atlas = AtlasIndex.Parse("{\"c_sword_atk\":[1037,1392,295,159,159,159,5]}");
        AssertThat(atlas["c_sword_atk"].AnchorX).IsEqual(90.0); // AXO 18 u × k 5
        AssertThat(atlas["c_sword_atk"].AnchorY).IsEqual(159.0);
    }

    [TestCase]
    public void CustomOverridesReplacePrototypeOnes()
    {
        var atlas = AtlasIndex.Parse("{\"c_sword_atk\":[0,0,10,10,3,10,2]}", new Dictionary<string, double>());
        AssertThat(atlas["c_sword_atk"].AnchorX).IsEqual(3.0);
    }

    [TestCase]
    public void UnknownKeyThrowsAndTryGetFails()
    {
        var atlas = AtlasIndex.Parse("{\"a\":[0,0,1,1,0,1,1]}");
        AssertThat(atlas.TryGet("nope", out _)).IsFalse();
        AssertThrown(() => _ = atlas["nope"]).IsInstanceOf<KeyNotFoundException>();
    }

    [TestCase]
    public void RealAtlasHasAllV2Frames()
    {
        var atlas = AtlasIndex.Parse(RealAtlasJson());
        AssertThat(atlas.Count).IsEqual(217);
        AssertThat(atlas["c_sword_front"]).IsEqual(new AtlasFrame(2877, 1218, 193, 162, 88, 162, 5.0025));
        foreach (var cls in new[] { "sword", "bow", "staff", "hammer", "dag" })
        {
            foreach (var pose in new[] { "front", "back", "side", "atk" })
            {
                AssertThat(atlas.TryGet($"c_{cls}_{pose}", out _)).IsTrue();
            }
        }

        // Todas as âncoras da v2 ficam no pé (spec 01 §4.1).
        foreach (var key in atlas.Keys)
        {
            AssertThat(atlas[key].AnchorY).IsEqual((double)atlas[key].Height);
        }
    }

    [TestCase]
    public void WorldSizeMatchesSpecExamples()
    {
        var atlas = AtlasIndex.Parse(RealAtlasJson());
        AssertThat(Math.Round(atlas["shadow"].WorldWidth, 1)).IsEqual(20.4);
        AssertThat(Math.Round(atlas["shadow"].WorldHeight, 1)).IsEqual(5.0);
        AssertThat(Math.Round(atlas["c_sword_front"].WorldWidth, 1)).IsEqual(38.6);
        AssertThat(Math.Round(atlas["c_sword_front"].WorldHeight, 1)).IsEqual(32.4);
    }

    [TestCase]
    public void LooseDensitiesAndBaseCenterAnchor()
    {
        var k = AtlasIndex.ParseLooseDensities("{\"cnev_f\":[129,108,3],\"d_osso\":[40,40,1]}");
        AssertThat(k["cnev_f"]).IsEqual(3.0);
        AssertThat(k["d_osso"]).IsEqual(1.0);
        AssertThat(AtlasIndex.BaseCenterAnchor(10, 4, 20, 30)).IsEqual((20.0, 34.0));
    }
}
