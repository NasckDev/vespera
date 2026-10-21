namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ObjectCatalogTest
{
    private static readonly Lazy<string> RealJson = new(() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "objects.json")));

    private static readonly Lazy<ObjectCatalog> Real = new(() => ObjectCatalog.Parse(RealJson.Value));

    private static ObjectCatalog Catalog => Real.Value;

    [TestCase]
    public void LoadsTheNineThemes()
    {
        string[] themes = ["cemetery", "market", "docks", "astral", "clockwork", "cathedral", "greenhouse", "mine", "common"];
        AssertThat(Catalog.Themes.Keys.ToArray()).IsEqual(themes);
        foreach (var theme in themes)
        {
            AssertThat(Catalog.InTheme(theme).Count > 0).IsTrue();
            AssertThat(Catalog.InTheme(theme).All(o => o.Theme == theme)).IsTrue();
        }

        AssertThat(Catalog.Count > 400).IsTrue();
        AssertThat(Catalog.InTheme("nao_existe").Count).IsEqual(0);
    }

    [TestCase]
    public void ReferenceIsPlayerAboutDoorHeight()
    {
        // c_sword_front: 162 px / k 5,0025 (atlas.json) = 32,4 u, × characterScale 1,75 do motor (B4a-D41) = 56,7 u.
        AssertThat(Catalog.PlayerHeight).IsEqualApprox(56.7f, 0.1f);
        var ratio = Catalog.PlayerHeight / Catalog.DoorHeight;
        AssertThat(ratio is > 0.85f and <= 1f).IsTrue();
        var door = Catalog["door_arched"];
        AssertThat(door.WorldHeight).IsEqualApprox(Catalog.DoorHeight, 0.5f);
        AssertThat(door.Layer).IsEqual(ObjectLayer.Wall);
    }

    [TestCase]
    public void CalibrationFollowsTheTargets()
    {
        var h = Catalog.PlayerHeight;
        foreach (var id in new[] { "barrel_fish", "barrel_wood", "barrel_common", "barrel_mine" })
        {
            AssertThat(Catalog[id].HeightRatio(h)).IsEqualApprox(0.45f, 0.02f);
        }

        foreach (var id in new[] { "lamp_post_common", "lamp_mine" })
        {
            AssertThat(Catalog[id].HeightRatio(h)).IsEqualApprox(1.6f, 0.02f);
        }

        foreach (var stall in Catalog.Objects.Where(o => o.Kind == "stall"))
        {
            AssertThat(stall.HeightRatio(h) is >= 1.85f and <= 2.15f).IsTrue();
        }
    }

    [TestCase]
    public void NothingIsAbsurdlyBig()
    {
        var h = Catalog.PlayerHeight;
        string[] plants = ["plant", "bush", "flowers", "planter", "mushroom", "topiary"];
        foreach (var o in Catalog.Objects)
        {
            if (plants.Contains(o.Kind))
            {
                AssertThat(o.HeightRatio(h) <= 2f).OverrideFailureMessage($"{o.Id}: planta com {o.HeightRatio(h):0.00}× o jogador").IsTrue();
            }

            var big = o.Kind is "building" or "mausoleum" or "crane" or "tree" or "furnace" or "boat";
            AssertThat(o.HeightRatio(h) <= (big ? 3.5f : 2.5f) || o.Layer == ObjectLayer.Floor)
                .OverrideFailureMessage($"{o.Id}: {o.HeightRatio(h):0.00}× o jogador").IsTrue();
        }
    }

    [TestCase]
    public void TexturesAreSmallAndDenseEnough()
    {
        foreach (var o in Catalog.Objects)
        {
            // Sem texturas gigantes: a densidade passa de 4 px/u só pelo arredondamento da redução. Piso de ~1 px/u: com o
            // jogador em 56,7 u (characterScale do motor), as peças grandes das folhas (mausoléus, portais) não têm mais
            // resolução que isso; abaixo de 1 px/u o recorte seria ampliado mesmo na escala 1 da pintura.
            AssertThat(o.Density is > 0.95f and <= 4.5f).OverrideFailureMessage($"{o.Id}: {o.Density} px/u").IsTrue();
            AssertThat(o.PixelWidth <= 512 && o.PixelHeight <= 512).OverrideFailureMessage($"{o.Id}: {o.PixelWidth}×{o.PixelHeight}").IsTrue();
            AssertThat(o.WorldHeight * o.Density <= o.PixelHeight + 1).IsTrue();
        }
    }

    [TestCase]
    public void SuggestionsAreConsistentWithKinds()
    {
        foreach (var o in Catalog.Objects)
        {
            if (o.Kind is "lamp" or "lantern" or "candle" or "candles" or "candelabra" or "chandelier" or "brazier" or "furnace" or "portal")
            {
                AssertThat(o.Light).OverrideFailureMessage($"{o.Id} deveria ter luz").IsNotNull();
            }

            if (o.Interaction == "break")
            {
                AssertThat(o.Solid).OverrideFailureMessage($"{o.Id}: quebrável sem corpo").IsTrue();
            }

            if (o.Layer != ObjectLayer.Entity)
            {
                AssertThat(o.Solid).IsFalse();
            }

            if (o.Layer is ObjectLayer.Floor or ObjectLayer.Wall or ObjectLayer.Fx)
            {
                AssertThat(o.Occluder).OverrideFailureMessage($"{o.Id}: {o.Layer} não esconde o jogador").IsFalse();
            }

            if (o.Footprint is { } f)
            {
                AssertThat(f.Bottom <= 0.05f && f.Y < 0).IsTrue();
                AssertThat(Math.Abs(f.X + (f.Width / 2)) < 0.2f).OverrideFailureMessage($"{o.Id}: footprint fora do centro").IsTrue();
            }
        }

        var lamp = Catalog["lamp_gallows"];
        AssertThat(lamp.Interaction).IsEqual("lamp");
        AssertThat(lamp.Occluder).IsTrue();
        AssertThat(lamp.Light!.Offset.Y < -10).IsTrue(); // a lanterna fica no alto do poste
        AssertThat(Catalog["tree_dead"].Sway).IsTrue();
        AssertThat(Catalog["fountain_tiered"].AmbientSound).IsEqual("water");
        AssertThat(Catalog["dock_corner"].Layer).IsEqual(ObjectLayer.Floor);
    }

    [TestCase]
    public void EveryFileExists()
    {
        var root = RepoRoot();
        foreach (var o in Catalog.Objects)
        {
            var path = Path.Combine(root, o.File["res://".Length..]);
            AssertThat(File.Exists(path)).OverrideFailureMessage($"{o.Id}: {path} não existe").IsTrue();
        }
    }

    [TestCase]
    public void RejectsBrokenData()
    {
        var json = RealJson.Value;
        AssertRejected(json.Replace("\"id\": \"barrel_fish\"", "\"id\": \"barrel_wood\"", StringComparison.Ordinal), "id repetido");
        AssertRejected(json.Replace("\"interaction\": \"lamp\"", "\"interaction\": \"dance\"", StringComparison.Ordinal), "interação desconhecida");
        AssertRejected(json.Replace("\"layer\": \"floor\"", "\"layer\": \"roof\"", StringComparison.Ordinal), "camada desconhecida");
        AssertRejected(json.Replace("\"theme\": \"mine\"", "\"theme\": \"moon\"", StringComparison.Ordinal), "não está em 'themes'");
        AssertRejected("{}", "objects.json inválido");
    }

    private static void AssertRejected(string json, string reason)
    {
        try
        {
            ObjectCatalog.Parse(json);
        }
        catch (ObjectFormatException ex)
        {
            AssertThat(ex.Message).Contains(reason);
            return;
        }

        throw new InvalidOperationException($"objects.json alterado deveria ser recusado ({reason}).");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vespera.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Vespera.sln não encontrado acima da pasta do teste.");
    }
}
