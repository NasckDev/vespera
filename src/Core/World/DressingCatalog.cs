using System.Text.Json;

namespace Vespera.Core.World;

/// <summary>Luz de um objeto do catálogo: tipo de <c>LIGHTC</c> (<c>a</c> âmbar, <c>f</c> fogo, <c>m</c>, <c>c</c>, <c>g</c>), raio e posição relativa ao pé (u).</summary>
public sealed record DressingLight(string Kind, float Radius, float X, float Y);

/// <summary>Interação de um objeto do catálogo: os mesmos tipos de <c>decor</c> do layout (<c>lamp</c>, <c>sign</c>, <c>bench</c>…).</summary>
public sealed record DressingInteraction(string Kind, string? Label, string? Text, float Radius);

/// <summary>
/// Um objeto do catálogo (frente B5, <c>src/Data/objects.json</c>; contrato em <c>docs/design/api-mundo.md</c>). O sprite é
/// um caminho <c>res://</c> ou <c>atlas:&lt;chave&gt;</c>; a âncora é o pé (px do sprite; null = centro da base do recorte
/// opaco, como D10). <see cref="Footprint"/> é a área sólida em u relativa ao pé (null = não bloqueia).
/// </summary>
public sealed record DressingDef
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

    public DressingLight? Light { get; init; }

    public DressingInteraction? Interact { get; init; }

    /// <summary>Tipo de quebrável (<c>barrel</c>, <c>crate</c>…: vida em <c>rules.world.props.hp</c>), ou null.</summary>
    public string? Breakable { get; init; }

    /// <summary>Baú deste tier (0–4), ou null.</summary>
    public int? Chest { get; init; }

    /// <summary>Camada de desenho (biblioteca B5): pisos ficam por baixo do y-sort, <c>Overhead</c> por cima.</summary>
    public ObjectLayer Layer { get; init; } = ObjectLayer.Entity;

    public bool IsAtlasSprite => Sprite.StartsWith(DressingCatalog.AtlasPrefix, StringComparison.Ordinal);

    public string AtlasKey => IsAtlasSprite ? Sprite[DressingCatalog.AtlasPrefix.Length..] : string.Empty;
}

/// <summary>
/// Catálogo de objetos com transparência (frente B5). Aceita <c>{"objects": {id: def}}</c>, <c>{"objects": [def]}</c>,
/// <c>{id: def}</c> ou <c>[def]</c>, para o motor não quebrar enquanto o formato do catálogo se firma. Campos desconhecidos
/// são ignorados; <c>sprite</c> é obrigatório.
/// </summary>
public sealed class DressingCatalog
{
    public const string AtlasPrefix = "atlas:";

    private readonly Dictionary<string, DressingDef> _objects;

    private DressingCatalog(Dictionary<string, DressingDef> objects) => _objects = objects;

    public static DressingCatalog Empty { get; } = new(new Dictionary<string, DressingDef>(StringComparer.Ordinal));

    public int Count => _objects.Count;

    public IEnumerable<string> Ids => _objects.Keys;

    public bool TryGet(string id, out DressingDef def) => _objects.TryGetValue(id, out def!);

    /// <summary>Junta dois catálogos (o segundo vence nos ids repetidos): catálogo do jogo + fixtures do smoke.</summary>
    public DressingCatalog With(DressingCatalog other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var merged = new Dictionary<string, DressingDef>(_objects, StringComparer.Ordinal);
        foreach (var (id, def) in other._objects)
        {
            merged[id] = def;
        }

        return new DressingCatalog(merged);
    }

    public static DressingCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("reference", out _))
            {
                // Formato gerado pela frente B5 (tools/assets/build_objects.py): lido pelo ObjectCatalog e convertido.
                return FromLibrary(ObjectCatalog.Parse(json));
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("objects", out var inner))
            {
                root = inner;
            }

            var objects = new Dictionary<string, DressingDef>(StringComparer.Ordinal);
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

            return new DressingCatalog(objects);
        }
        catch (ObjectFormatException ex)
        {
            throw new MapFormatException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new MapFormatException($"catálogo de objetos inválido: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converte a biblioteca da frente B5 (<see cref="ObjectCatalog"/>, <c>objects.json</c> gerado) para o formato do
    /// motor: <c>file</c> → sprite, <c>density</c> → ppu, âncora em px do PNG, footprint retangular, cor da luz → tipo de
    /// <c>LIGHTC</c> mais próximo, interação <c>break</c> → quebrável do <c>kind</c>, <c>chest</c> → baú de tier 0 e as
    /// outras interações viram decoração do mesmo nome.
    /// </summary>
    public static DressingCatalog FromLibrary(ObjectCatalog library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var objects = new Dictionary<string, DressingDef>(StringComparer.Ordinal);
        foreach (var o in library.Objects)
        {
            objects[o.Id] = new DressingDef
            {
                Id = o.Id,
                Sprite = o.File,
                PixelsPerUnit = o.Density,
                Anchor = new MapPoint(o.AnchorX, o.AnchorY),
                Footprint = o.Footprint is { } f ? RectShape(f) : null,
                Occluder = o.Occluder,
                Sway = o.Sway,
                Layer = o.Layer,
                Light = o.Light is { } l ? new DressingLight(NearestLightKind(l.Color), l.Radius, l.Offset.X, l.Offset.Y) : null,
                Breakable = o.Interaction == "break" ? o.Kind : null,
                Chest = o.Interaction == "chest" ? 0 : null,
                Interact = o.Interaction is { } i && i != "break" && i != "chest"
                    ? new DressingInteraction(i, null, null, MapLayout.DefaultDecorRadius)
                    : null,
            };
        }

        return new DressingCatalog(objects);
    }

    private static LayoutShape RectShape(MapRect r)
    {
        var points = new[] { new MapPoint(r.X, r.Y), new MapPoint(r.X + r.Width, r.Y), new MapPoint(r.X + r.Width, r.Y + r.Height), new MapPoint(r.X, r.Y + r.Height) };
        return new LayoutShape { Polygon = points, Bounds = r };
    }

    /// <summary>Cores de <c>LIGHTC</c> (as mesmas do <c>LightGlows</c>), para achar o tipo mais próximo de uma cor #rrggbb.</summary>
    private static readonly (string Kind, int R, int G, int B)[] LightColors =
    [
        ("a", 242, 165, 65), ("f", 255, 120, 40), ("m", 230, 74, 142), ("c", 95, 208, 230), ("g", 110, 230, 90),
    ];

    public static string NearestLightKind(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return "a";
        }

        var v = Convert.ToInt32(hex[1..], 16);
        var (r, g, b) = ((v >> 16) & 255, (v >> 8) & 255, v & 255);
        return LightColors.MinBy(c => ((c.R - r) * (c.R - r)) + ((c.G - g) * (c.G - g)) + ((c.B - b) * (c.B - b))).Kind;
    }

    private static DressingDef ParseDef(string id, JsonElement e)
    {
        var sprite = Str(e, "sprite") ?? throw new FormatException($"objeto '{id}' sem 'sprite'");
        return new DressingDef
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
                ? new DressingLight(Str(l, "kind") ?? "a", Num(l, "r") ?? 46f, Num(l, "x") ?? 0f, Num(l, "y") ?? 0f)
                : null,
            Interact = e.TryGetProperty("interact", out var i) && i.ValueKind == JsonValueKind.Object
                ? new DressingInteraction(Str(i, "kind") ?? "inspect", Str(i, "label"), Str(i, "text"), Num(i, "r") ?? MapLayout.DefaultDecorRadius)
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
