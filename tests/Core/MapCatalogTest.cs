namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class MapCatalogTest
{
    private static readonly Lazy<MapCatalog> Real = new(() =>
        MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json"))));

    private static MapCatalog Catalog => Real.Value;

    [TestCase]
    public void LoadsTheThirteenV2Maps()
    {
        string[] expected = ["guild", "forge", "inn", "fish2", "crypt2", "mine", "garden", "cath", "core", "shop_arm", "shop_mag", "shop_alq", "shop_rel"];
        AssertThat(Catalog.Count).IsEqual(expected.Length);
        foreach (var id in expected)
        {
            AssertThat(Catalog.TryGet(id, out var map)).IsTrue();
            AssertThat(map.Id).IsEqual(id);
        }

        // Legados da v1 (spec 01 §2.1) não entram.
        AssertThat(Catalog.TryGet("fish", out _)).IsFalse();
        AssertThat(Catalog.TryGet("crypt", out _)).IsFalse();
    }

    [TestCase]
    public void GuildMatchesSpec()
    {
        var guild = Catalog["guild"];
        AssertThat(guild.Name).IsEqual("Guilda dos Lanterneiros");
        AssertThat(guild.Indoor).IsTrue();
        AssertThat(guild.Hd).IsFalse();
        AssertThat(guild.HasMask).IsFalse();
        AssertThat(guild.ImageWidth).IsEqual(500);
        AssertThat(guild.ImageHeight).IsEqual(896);
        AssertThat(guild.Spawn).IsEqual(new MapPoint(250, 560));
        AssertThat(guild.Exits.Count).IsEqual(1);
        AssertThat(guild.Exits[0]).IsEqual(new MapExit(new MapRect(200, 872, 100, 24), "forge", new MapPoint(1500, 250)));
        AssertThat(guild.Walk.Count).IsEqual(5);
        AssertThat(guild.Block.Count).IsEqual(15);
        AssertThat(guild.Arrivals["continuar"]).IsEqual(new MapPoint(250, 330));
        AssertThat(guild.Arrivals["daRuaDaForja"]).IsEqual(new MapPoint(250, 840));
        AssertThat(guild.Interactables[0]).IsEqual(new MapInteractable("odete", "Falar com Odete", 247, 250, 32, "talkClerk", null));
        AssertThat(guild.Npcs[0].Path.Count).IsEqual(3);
        AssertThat(guild.ViewHeight).IsEqual(300f);
    }

    [TestCase]
    public void ForgeMatchesSpec()
    {
        var forge = Catalog["forge"];
        AssertThat(forge.Hd).IsTrue();
        AssertThat(forge.Outdoor).IsTrue();
        AssertThat(forge.Mask).IsEqual("res://assets/maps/city/walk_forge.png"); // layout_forge.json (B4b)
        AssertThat(forge.MaskCell).IsEqual(4);
        AssertThat(forge.LightsKey).IsEqual("city");
        AssertThat(forge.Spawn).IsEqual(new MapPoint(1500, 250));
        AssertThat(forge.Exits[0]).IsEqual(new MapExit(new MapRect(250, 268, 40, 25), "inn", new MapPoint(250, 760)));
        AssertThat(forge.Exits[1]).IsEqual(new MapExit(new MapRect(1460, 128, 80, 22), "guild", new MapPoint(250, 840)));
        AssertThat(forge.Exits.Select(e => e.To).ToArray()).IsEqual(new[] { "inn", "guild", "shop_arm", "shop_rel", "shop_alq", "shop_mag" });
        AssertThat(forge.PortalSpots.Count).IsEqual(2);
        AssertThat(forge.PortalSpots[1]).IsEqual(new MapPoint(1174, 534));
        AssertThat(forge.Interactables.Count).IsEqual(4);
        AssertThat(forge.Interactables.Single(i => i.Id == "portal0").StartsEnabled).IsFalse();
        AssertThat(forge.Interactables.Single(i => i.Id == "bras").StartsEnabled).IsTrue();
        AssertThat(forge.ViewHeight).IsEqual(400f);
        AssertThat(forge.Arrivals.ContainsKey("voltaDaBrecha")).IsFalse(); // texto descritivo, não coordenada
    }

    [TestCase]
    public void DungeonsHaveDoorsBackAndBossPoints()
    {
        AssertThat(Catalog["fish2"].Door).IsEqual(new MapRect(740, 60, 120, 40));
        AssertThat(Catalog["fish2"].Back).IsEqual(new MapRect(790, 890, 90, 30));
        AssertThat(Catalog["crypt2"].Door).IsEqual(new MapRect(180, 110, 60, 40));
        AssertThat(Catalog["mine"].Back).IsEqual(new MapRect(300, 790, 80, 20));
        AssertThat(Catalog["garden"].Spawn).IsEqual(new MapPoint(1040, 895));
        AssertThat(Catalog["cath"].Door).IsNull();
        AssertThat(Catalog["cath"].BossHere).IsEqual(new MapPoint(835, 380));
        AssertThat(Catalog["cath"].ExitAt).IsEqual(new MapPoint(835, 330));
        AssertThat(Catalog["core"].Back).IsEqual(new MapRect(213, 1000, 74, 14));
        AssertThat(Catalog["core"].BossDefault).IsEqual(new MapPoint(250, 500));
        AssertThat(Catalog["core"].ExitDefault).IsEqual(new MapPoint(250, 228));
        foreach (var id in new[] { "fish2", "crypt2", "mine", "garden", "cath", "core" })
        {
            AssertThat(Catalog[id].Dungeon).IsTrue();
            AssertThat(Catalog[id].Exits.Count).IsEqual(0);
            // Colisão redesenhada sobre a arte (tools/maps/layout.py, frente B4c): máscara de células de 4 u.
            AssertThat(Catalog[id].Mask).IsEqual($"res://assets/maps/{id}/walk_{id}.png");
            AssertThat(Catalog[id].MaskCell).IsEqual(4);
        }
    }

    [TestCase]
    public void CoreArenaIsACircle()
    {
        var core = Catalog["core"];
        AssertThat(core.Walk[0].IsCircle).IsTrue();
        AssertThat(core.Walk[0].Circle!.Value).IsEqual(new MapCircle(250, 505, 142));
        AssertThat(core.Hd).IsFalse();
    }

    [TestCase]
    public void ShopsAreIndoorShopsThatExitToTheirStreetDoor()
    {
        var forge = Catalog["forge"];
        foreach (var id in new[] { "shop_arm", "shop_mag", "shop_alq", "shop_rel" })
        {
            var shop = Catalog[id];
            AssertThat(shop.Shop).IsTrue();
            AssertThat(shop.Indoor).IsTrue();
            AssertThat(shop.Exits.Single().To).IsEqual("forge");
            AssertThat(shop.Interactables.Count).IsEqual(1);

            // Sair da loja põe o jogador na frente da porta da rua que leva a ela (B4b), não na placa dos Ofícios.
            var door = forge.Exits.Single(e => e.To == id).Area;
            var at = shop.Exits.Single().At;
            var dx = at.X - (door.X + (door.Width / 2));
            var dy = at.Y - (door.Y + (door.Height / 2));
            AssertThat(MathF.Sqrt((dx * dx) + (dy * dy)) < 40f).IsTrue();
        }
    }

    [TestCase]
    public void ArrivalsNeverLandInsideAnExitOfTheDestination()
    {
        foreach (var map in Catalog.Maps)
        {
            foreach (var exit in map.Exits)
            {
                AssertThat(Catalog[exit.To].FindExit(exit.At.X, exit.At.Y)).IsNull();
            }
        }
    }

    [TestCase]
    public void SpawnsAndArrivalsOfShapeMapsAreStandable()
    {
        var failures = new List<string>();
        foreach (var map in Catalog.Maps.Where(m => !m.HasMask))
        {
            var area = map.CreateShapeWalkArea();
            Check(area, map.Id, "spawn", map.Spawn, failures);
            foreach (var (name, point) in map.Arrivals)
            {
                Check(area, map.Id, name, point, failures);
            }
        }

        foreach (var map in Catalog.Maps)
        {
            foreach (var exit in map.Exits.Where(e => !Catalog[e.To].HasMask))
            {
                Check(Catalog[exit.To].CreateShapeWalkArea(), exit.To, $"chegada de {map.Id}", exit.At, failures);
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void EveryExitOfAShapeMapCanBeStoodIn()
    {
        foreach (var map in Catalog.Maps.Where(m => !m.HasMask))
        {
            var area = map.CreateShapeWalkArea();
            foreach (var exit in map.Exits)
            {
                AssertThat(AnyStandable(area, exit.Area)).OverrideFailureMessage($"{map.Id}: saída {exit.Area} inalcançável").IsTrue();
            }

            if (map.Back is { } back)
            {
                AssertThat(AnyStandable(area, back)).OverrideFailureMessage($"{map.Id}: back {back} inalcançável").IsTrue();
            }
        }
    }

    [TestCase]
    public void FindExitUsesInclusiveRectangles()
    {
        var guild = Catalog["guild"];
        AssertThat(guild.FindExit(200, 872)!.To).IsEqual("forge");
        AssertThat(guild.FindExit(300, 896)!.To).IsEqual("forge");
        AssertThat(guild.FindExit(250, 871.9f)).IsNull();
    }

    [TestCase]
    public void MaskMapsCannotCreateShapeAreas()
        => AssertThrown(() => Catalog["forge"].CreateShapeWalkArea()).IsInstanceOf<InvalidOperationException>();

    [TestCase]
    public void RejectsExitToUnknownMap()
        => AssertThrown(() => MapCatalog.Parse(
                "{\"a\":{\"image\":\"x\",\"imageSize\":[10,10],\"name\":\"A\",\"spawn\":[5,5],\"walk\":[[0,0,10,10]],\"exits\":[{\"r\":[0,0,1,1],\"to\":\"b\",\"at\":[1,1]}]}}"))
            .IsInstanceOf<MapFormatException>();

    [TestCase]
    public void RejectsMapWithoutWalkableArea()
        => AssertThrown(() => MapCatalog.Parse("{\"a\":{\"image\":\"x\",\"imageSize\":[10,10],\"name\":\"A\",\"spawn\":[5,5]}}"))
            .IsInstanceOf<MapFormatException>();

    private static void Check(IWalkArea area, string mapId, string what, MapPoint p, List<string> failures)
    {
        if (!area.CanStand(p.X, p.Y))
        {
            failures.Add($"{mapId}.{what} ({p.X}, {p.Y})");
        }
    }

    private static bool AnyStandable(IWalkArea area, MapRect rect)
    {
        for (var y = rect.Y; y <= rect.Bottom; y += 1)
        {
            for (var x = rect.X; x <= rect.Right; x += 1)
            {
                if (area.CanStand(x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
