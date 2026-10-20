using System.Text.Json;

namespace Vespera.Core.World;

public sealed class MapFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Catálogo dos mapas lido de <c>src/Data/maps.json</c> (spec 01 §9.3, DECISIONS D5). O Godot lê o arquivo e passa o
/// texto; o Core não conhece <c>res://</c>.
/// </summary>
public sealed class MapCatalog
{
    private readonly Dictionary<string, MapDef> _maps;

    private MapCatalog(Dictionary<string, MapDef> maps) => _maps = maps;

    public IReadOnlyCollection<MapDef> Maps => _maps.Values;

    public IEnumerable<string> Ids => _maps.Keys;

    public int Count => _maps.Count;

    public MapDef this[string id] =>
        _maps.TryGetValue(id, out var map) ? map : throw new KeyNotFoundException($"Mapa '{id}' não existe em maps.json.");

    public bool TryGet(string id, out MapDef map) => _maps.TryGetValue(id, out map!);

    public static MapCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var maps = new Dictionary<string, MapDef>(StringComparer.Ordinal);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                maps[entry.Name] = ParseMap(entry.Name, entry.Value);
            }

            var catalog = new MapCatalog(maps);
            catalog.Validate();
            return catalog;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new MapFormatException($"maps.json inválido: {ex.Message}", ex);
        }
    }

    private void Validate()
    {
        foreach (var map in _maps.Values)
        {
            foreach (var exit in map.Exits)
            {
                if (!_maps.ContainsKey(exit.To))
                {
                    throw new MapFormatException($"Mapa '{map.Id}' tem saída para '{exit.To}', que não existe.");
                }
            }

            if (!map.HasMask && map.Walk.Count == 0)
            {
                throw new MapFormatException($"Mapa '{map.Id}' não tem máscara nem formas 'walk'.");
            }
        }
    }

    private static MapDef ParseMap(string key, JsonElement e)
    {
        var size = e.GetProperty("imageSize");
        return new MapDef
        {
            Id = e.TryGetProperty("id", out var id) ? id.GetString()! : key,
            Image = e.GetProperty("image").GetString()!,
            ImageWidth = size[0].GetInt32(),
            ImageHeight = size[1].GetInt32(),
            Mask = OptString(e, "mask"),
            MaskCell = e.TryGetProperty("maskCell", out var cell) ? cell.GetInt32() : MaskWalkArea.DefaultCellSize,
            Name = e.GetProperty("name").GetString()!,
            Indoor = Flag(e, "indoor"),
            Outdoor = Flag(e, "outdoor"),
            Hd = Flag(e, "hd"),
            Dungeon = Flag(e, "dungeon"),
            Shop = Flag(e, "shop"),
            Theme = OptString(e, "theme"),
            LightsKey = OptString(e, "lightsKey"),
            Spawn = Point(e.GetProperty("spawn")),
            Exits = List(e, "exits", x => new MapExit(Rect(x.GetProperty("r")), x.GetProperty("to").GetString()!, Point(x.GetProperty("at")))),
            Walk = List(e, "walk", Shape),
            Block = List(e, "block", Rect),
            Lights = e.TryGetProperty("lights", out var lights) && lights.ValueKind == JsonValueKind.Array
                ? [.. lights.EnumerateArray().Select(l => new MapLight(l[0].GetSingle(), l[1].GetSingle(), l[2].GetSingle(), l[3].GetString()!))]
                : [],
            Interactables = List(e, "interactables", x => new MapInteractable(
                x.GetProperty("id").GetString()!,
                x.GetProperty("label").GetString()!,
                x.GetProperty("x").GetSingle(),
                x.GetProperty("y").GetSingle(),
                x.GetProperty("r").GetSingle(),
                x.GetProperty("action").GetString()!,
                OptString(x, "cond"))),
            Arrivals = Arrivals(e),
            Door = OptRect(e, "door"),
            Back = OptRect(e, "back"),
            BossHere = OptPoint(e, "bossHere"),
            ExitAt = OptPoint(e, "exitAt"),
            BossDefault = OptPoint(e, "bossDefault"),
            ExitDefault = OptPoint(e, "exitDefault"),
            PortalSpots = List(e, "portalSpots", Point),
            Dummies = List(e, "dummies", Point),
            CrystalProps = List(e, "crystalProps", Point),
            Npcs = List(e, "npcs", x => new NpcRoute(
                x.GetProperty("id").GetString()!,
                x.GetProperty("name").GetString()!,
                x.GetProperty("hours")[0].GetInt32(),
                x.GetProperty("hours")[1].GetInt32(),
                x.GetProperty("spd").GetSingle(),
                [.. x.GetProperty("path").EnumerateArray().Select(Point)])),
            Clerk = e.TryGetProperty("clerkSprite", out var clerk)
                ? new ClerkSprite(clerk.GetProperty("sprite").GetString()!, clerk.GetProperty("x").GetSingle(), clerk.GetProperty("y").GetSingle(), clerk.GetProperty("scale").GetSingle(), Rect(clerk.GetProperty("foregroundSlice")))
                : null,
            Patch = e.TryGetProperty("imagePatch", out var patch)
                ? new ImagePatch(Rect(patch.GetProperty("copyFrom")), Point(patch.GetProperty("pasteAt")))
                : null,
        };
    }

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.Number && v.GetInt32() != 0));

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static MapPoint Point(JsonElement v) => new(v[0].GetSingle(), v[1].GetSingle());

    private static MapRect Rect(JsonElement v) => new(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle(), v[3].GetSingle());

    private static WalkShape Shape(JsonElement v) =>
        v.ValueKind == JsonValueKind.Object
            ? WalkShape.FromCircle(new MapCircle(v.GetProperty("c")[0].GetSingle(), v.GetProperty("c")[1].GetSingle(), v.GetProperty("c")[2].GetSingle()))
            : WalkShape.FromRect(Rect(v));

    private static MapRect? OptRect(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? Rect(v) : null;

    private static MapPoint? OptPoint(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? Point(v) : null;

    private static IReadOnlyList<T> List<T>(JsonElement e, string name, Func<JsonElement, T> parse) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray().Select(parse)] : [];

    /// <summary>Só entradas que são coordenadas; textos descritivos (ex.: "PORTAL_SPOTS[spot] + [0,24]") ficam de fora.</summary>
    private static Dictionary<string, MapPoint> Arrivals(JsonElement e)
    {
        var result = new Dictionary<string, MapPoint>(StringComparer.Ordinal);
        if (e.TryGetProperty("arrivals", out var arrivals))
        {
            foreach (var a in arrivals.EnumerateObject())
            {
                if (a.Value.ValueKind == JsonValueKind.Array)
                {
                    result[a.Name] = Point(a.Value);
                }
            }
        }

        return result;
    }
}
