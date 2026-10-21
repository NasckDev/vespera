using System.Text.Json;

namespace Vespera.Core.World;

/// <summary>Luz de um objeto do catálogo: tipo de <c>LIGHTC</c> (<c>a</c> âmbar, <c>f</c> fogo, <c>m</c>, <c>c</c>, <c>g</c>), raio e posição relativa ao pé (u).</summary>
public sealed record ObjectLight(string Kind, float Radius, float X, float Y);

/// <summary>Interação de um objeto do catálogo: os mesmos tipos de <c>decor</c> do layout (<c>lamp</c>, <c>sign</c>, <c>bench</c>…).</summary>
public sealed record ObjectInteraction(string Kind, string? Label, string? Text, float Radius);

/// <summary>
/// Um objeto do catálogo (frente B5, <c>src/Data/objects.json</c>; contrato em <c>docs/design/api-mundo.md</c>). O sprite é
/// um caminho <c>res://</c> ou <c>atlas:&lt;chave&gt;</c>; a âncora é o pé (px do sprite; null = centro da base do recorte
/// opaco, como D10). <see cref="Footprint"/> é a área sólida em u relativa ao pé (null = não bloqueia).
/// </summary>
public sealed record ObjectDef
{
    public required string Id { get; init; }

    public string? Name { get; init; }

    public required string Sprite { get; init; }

    /// <summary>px do sprite por u (1 = desenhado na escala da pintura do mapa).</summary>
    public float PixelsPerUnit { get; init; } = 1f;

    public MapPoint? Anchor { get; init; }

    public LayoutShape? Footprint { get; init; }

    /// <summary>Fica translúcido quando cobre o jogador que passa por trás.</summary>
    public bool Occluder { get; init; }

    /// <summary>Balança com o vento (plantas, bandeiras, toldos).</summary>
    public bool Sway { get; init; }

    public ObjectLight? Light { get; init; }

    public ObjectInteraction? Interact { get; init; }

    /// <summary>Tipo de quebrável (<c>barrel</c>, <c>crate</c>…: vida em <c>rules.world.props.hp</c>), ou null.</summary>
    public string? Breakable { get; init; }

    /// <summary>Baú deste tier (0–4), ou null.</summary>
    public int? Chest { get; init; }

    public bool IsAtlasSprite => Sprite.StartsWith(ObjectCatalog.AtlasPrefix, StringComparison.Ordinal);

    public string AtlasKey => IsAtlasSprite ? Sprite[ObjectCatalog.AtlasPrefix.Length..] : string.Empty;
}

/// <summary>
/// Catálogo de objetos com transparência (frente B5). Aceita <c>{"objects": {id: def}}</c>, <c>{"objects": [def]}</c>,
/// <c>{id: def}</c> ou <c>[def]</c>, para o motor não quebrar enquanto o formato do catálogo se firma. Campos desconhecidos
/// são ignorados; <c>sprite</c> é obrigatório.
/// </summary>
public sealed class ObjectCatalog
{
    public const string AtlasPrefix = "atlas:";

    private readonly Dictionary<string, ObjectDef> _objects;

    private ObjectCatalog(Dictionary<string, ObjectDef> objects) => _objects = objects;

    public static ObjectCatalog Empty { get; } = new(new Dictionary<string, ObjectDef>(StringComparer.Ordinal));

    public int Count => _objects.Count;

    public IEnumerable<string> Ids => _objects.Keys;

    public bool TryGet(string id, out ObjectDef def) => _objects.TryGetValue(id, out def!);

    /// <summary>Junta dois catálogos (o segundo vence nos ids repetidos): catálogo do jogo + fixtures do smoke.</summary>
    public ObjectCatalog With(ObjectCatalog other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var merged = new Dictionary<string, ObjectDef>(_objects, StringComparer.Ordinal);
        foreach (var (id, def) in other._objects)
        {
            merged[id] = def;
        }

        return new ObjectCatalog(merged);
    }

    public static ObjectCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("objects", out var inner))
            {
                root = inner;
            }

            var objects = new Dictionary<string, ObjectDef>(StringComparer.Ordinal);
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in root.EnumerateArray())
                {
                    var id = Str(e, "id") ?? throw new FormatException("objeto sem 'id'");
                    objects[id] = ParseDef(id, e);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in root.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Object)
                    {
                        objects[p.Name] = ParseDef(p.Name, p.Value);
                    }
                }
            }

            return new ObjectCatalog(objects);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new MapFormatException($"catálogo de objetos inválido: {ex.Message}", ex);
        }
    }

    private static ObjectDef ParseDef(string id, JsonElement e)
    {
        var sprite = Str(e, "sprite") ?? throw new FormatException($"objeto '{id}' sem 'sprite'");
        return new ObjectDef
        {
            Id = id,
            Name = Str(e, "name"),
            Sprite = sprite,
            PixelsPerUnit = Num(e, "ppu") is { } ppu && ppu > 0 ? ppu : 1f,
            Anchor = e.TryGetProperty("anchor", out var a) && a.ValueKind == JsonValueKind.Array ? new MapPoint(a[0].GetSingle(), a[1].GetSingle()) : null,
            Footprint = Footprint(e),
            Occluder = Bool(e, "occluder"),
            Sway = Bool(e, "sway"),
            Light = e.TryGetProperty("light", out var l) && l.ValueKind == JsonValueKind.Object
                ? new ObjectLight(Str(l, "kind") ?? "a", Num(l, "r") ?? 46f, Num(l, "x") ?? 0f, Num(l, "y") ?? 0f)
                : null,
            Interact = e.TryGetProperty("interact", out var i) && i.ValueKind == JsonValueKind.Object
                ? new ObjectInteraction(Str(i, "kind") ?? "inspect", Str(i, "label"), Str(i, "text"), Num(i, "r") ?? MapLayout.DefaultDecorRadius)
                : null,
            Breakable = Str(e, "breakable"),
            Chest = e.TryGetProperty("chest", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null,
        };
    }

    /// <summary><c>footprint</c>: forma do layout (<c>rect</c>/<c>circle</c>/<c>poly</c>) ou <c>{"ellipse": [rx, ry]}</c>, em u relativos ao pé.</summary>
    private static LayoutShape? Footprint(JsonElement e)
    {
        if (!e.TryGetProperty("footprint", out var f) || f.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (f.TryGetProperty("ellipse", out var el))
        {
            var (rx, ry) = (el[0].GetSingle(), el[1].GetSingle());
            var points = new MapPoint[16];
            for (var k = 0; k < points.Length; k++)
            {
                var ang = MathF.Tau * k / points.Length;
                points[k] = new MapPoint(MathF.Cos(ang) * rx, MathF.Sin(ang) * ry);
            }

            return new LayoutShape { Polygon = points, Bounds = new MapRect(-rx, -ry, rx * 2, ry * 2) };
        }

        return MapLayout.Shape(f);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static float? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : null;

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
