using System.Globalization;
using System.Text.Json;

namespace Vespera.Core.World;

public sealed class ObjectFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Biblioteca de objetos do mundo lida de <c>src/Data/objects.json</c> (DECISIONS B5-1). O arquivo é gerado por
/// <c>tools/assets/build_objects.py</c> a partir da curadoria; o Godot lê o texto e passa para cá (o Core não conhece
/// <c>res://</c>). Ordem dos objetos = ordem do arquivo (tema, depois id).
/// </summary>
public sealed class ObjectCatalog
{
    /// <summary>Interações sugeridas aceitas: as do formato de layout dos mapas (<c>tools/maps/layout.py</c>) mais baú e quebrável.</summary>
    public static readonly IReadOnlySet<string> Interactions = new HashSet<string>(StringComparer.Ordinal)
    {
        "break", "chest", "lamp", "flower", "boat", "fishing", "sign", "bench", "fountain", "stall", "inspect",
    };

    /// <summary>Sons ambiente aceitos (<c>ambient.kind</c> do layout dos mapas).</summary>
    public static readonly IReadOnlySet<string> AmbientSounds = new HashSet<string>(StringComparer.Ordinal)
    {
        "water", "fire", "forge", "crowd", "wind", "bells", "birds",
    };

    private readonly Dictionary<string, ObjectDef> _byId;
    private readonly List<ObjectDef> _objects;
    private readonly Dictionary<string, List<ObjectDef>> _byTheme;

    private ObjectCatalog(List<ObjectDef> objects, IReadOnlyDictionary<string, string> themes, float playerHeight, float doorWidth, float doorHeight)
    {
        _objects = objects;
        Themes = themes;
        PlayerHeight = playerHeight;
        DoorWidth = doorWidth;
        DoorHeight = doorHeight;
        _byId = new Dictionary<string, ObjectDef>(StringComparer.Ordinal);
        _byTheme = new Dictionary<string, List<ObjectDef>>(StringComparer.Ordinal);
        foreach (var theme in themes.Keys)
        {
            _byTheme[theme] = [];
        }

        foreach (var o in objects)
        {
            if (!_byId.TryAdd(o.Id, o))
            {
                throw new ObjectFormatException($"objects.json: id repetido '{o.Id}'.");
            }

            if (!_byTheme.TryGetValue(o.Theme, out var list))
            {
                throw new ObjectFormatException($"objects.json: '{o.Id}' tem tema '{o.Theme}', que não está em 'themes'.");
            }

            list.Add(o);
        }
    }

    /// <summary>Altura do jogador em u usada na calibração (recorte <c>c_sword_front</c>).</summary>
    public float PlayerHeight { get; }

    /// <summary>Largura da porta de referência em u.</summary>
    public float DoorWidth { get; }

    /// <summary>Altura da porta de referência em u (jogador ≈ altura de porta).</summary>
    public float DoorHeight { get; }

    /// <summary>Tema → nome de exibição, na ordem do arquivo.</summary>
    public IReadOnlyDictionary<string, string> Themes { get; }

    public IReadOnlyList<ObjectDef> Objects => _objects;

    public int Count => _objects.Count;

    public ObjectDef this[string id] =>
        _byId.TryGetValue(id, out var o) ? o : throw new KeyNotFoundException($"Objeto '{id}' não existe em objects.json.");

    public bool TryGet(string id, out ObjectDef obj) => _byId.TryGetValue(id, out obj!);

    /// <summary>Objetos de um tema (vazio se o tema não existe).</summary>
    public IReadOnlyList<ObjectDef> InTheme(string theme) => _byTheme.TryGetValue(theme, out var list) ? list : [];

    public static ObjectCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            var reference = root.GetProperty("reference");
            var themes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var t in root.GetProperty("themes").EnumerateObject())
            {
                themes[t.Name] = t.Value.GetString()!;
            }

            var objects = root.GetProperty("objects").EnumerateArray().Select(ParseObject).ToList();
            var catalog = new ObjectCatalog(
                objects,
                themes,
                reference.GetProperty("playerHeight").GetSingle(),
                reference.GetProperty("doorWidth").GetSingle(),
                reference.GetProperty("doorHeight").GetSingle());
            catalog.Validate();
            return catalog;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or ArgumentException)
        {
            throw new ObjectFormatException($"objects.json inválido: {ex.Message}", ex);
        }
    }

    private void Validate()
    {
        if (PlayerHeight <= 0 || DoorHeight <= 0 || DoorWidth <= 0)
        {
            throw new ObjectFormatException("objects.json: referência (jogador/porta) precisa ser positiva.");
        }

        foreach (var o in _objects)
        {
            string Where() => $"objects.json: '{o.Id}'";
            if (o.Id.Length == 0 || o.Id.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_')))
            {
                throw new ObjectFormatException($"{Where()}: id precisa ser snake_case ASCII.");
            }

            if (!o.File.StartsWith("res://", StringComparison.Ordinal) || !o.File.EndsWith($"/{o.Theme}/{o.Id}.png", StringComparison.Ordinal))
            {
                throw new ObjectFormatException($"{Where()}: arquivo '{o.File}' fora de res://…/<tema>/<id>.png.");
            }

            if (o.PixelWidth <= 0 || o.PixelHeight <= 0 || o.WorldWidth <= 0 || o.WorldHeight <= 0 || o.Density <= 0)
            {
                throw new ObjectFormatException($"{Where()}: tamanhos e densidade precisam ser positivos.");
            }

            if (o.AnchorX < 0 || o.AnchorX > o.PixelWidth || o.AnchorY < 0 || o.AnchorY > o.PixelHeight)
            {
                throw new ObjectFormatException($"{Where()}: âncora ({o.AnchorX}, {o.AnchorY}) fora da textura {o.PixelWidth}×{o.PixelHeight}.");
            }

            if (o.Footprint is { } f)
            {
                if (o.Layer != ObjectLayer.Entity)
                {
                    throw new ObjectFormatException($"{Where()}: só objetos da camada Entity podem ter footprint.");
                }

                if (f.Width <= 0 || f.Height <= 0 || f.Bottom > 0.05f || f.Width > o.WorldWidth + 0.5f)
                {
                    throw new ObjectFormatException($"{Where()}: footprint [{f.X}, {f.Y}, {f.Width}, {f.Height}] precisa ficar atrás do pé e dentro da largura.");
                }
            }

            if (o.Interaction is { } i && !Interactions.Contains(i))
            {
                throw new ObjectFormatException($"{Where()}: interação desconhecida '{i}'.");
            }

            if (o.AmbientSound is { } s && !AmbientSounds.Contains(s))
            {
                throw new ObjectFormatException($"{Where()}: som ambiente desconhecido '{s}'.");
            }

            if (o.Light is { } l && (l.Radius <= 0 || !IsHexColor(l.Color)))
            {
                throw new ObjectFormatException($"{Where()}: luz inválida ({l.Color}, raio {l.Radius}).");
            }
        }
    }

    private static bool IsHexColor(string s) =>
        s.Length == 7 && s[0] == '#' && int.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

    private static ObjectDef ParseObject(JsonElement e)
    {
        var px = e.GetProperty("px");
        var size = e.GetProperty("worldSize");
        var anchor = e.GetProperty("anchor");
        var sound = e.TryGetProperty("sound", out var snd) && snd.ValueKind == JsonValueKind.Object ? snd : default;
        return new ObjectDef
        {
            Id = e.GetProperty("id").GetString()!,
            File = e.GetProperty("file").GetString()!,
            Theme = e.GetProperty("theme").GetString()!,
            Kind = e.GetProperty("kind").GetString()!,
            Layer = ParseLayer(e.GetProperty("layer").GetString()!),
            Source = e.GetProperty("source").GetString()!,
            PixelWidth = px[0].GetInt32(),
            PixelHeight = px[1].GetInt32(),
            WorldWidth = size[0].GetSingle(),
            WorldHeight = size[1].GetSingle(),
            Density = e.GetProperty("density").GetSingle(),
            AnchorX = anchor[0].GetSingle(),
            AnchorY = anchor[1].GetSingle(),
            Footprint = e.TryGetProperty("footprint", out var fp) && fp.ValueKind == JsonValueKind.Array
                ? new MapRect(fp[0].GetSingle(), fp[1].GetSingle(), fp[2].GetSingle(), fp[3].GetSingle())
                : null,
            Occluder = e.GetProperty("occluder").GetBoolean(),
            Sway = e.GetProperty("sway").GetBoolean(),
            Light = e.TryGetProperty("light", out var light) && light.ValueKind == JsonValueKind.Object
                ? new ObjectLight(
                    light.GetProperty("color").GetString()!,
                    light.GetProperty("radius").GetSingle(),
                    new MapPoint(light.GetProperty("offset")[0].GetSingle(), light.GetProperty("offset")[1].GetSingle()))
                : null,
            Interaction = OptString(e, "interaction"),
            AmbientSound = sound.ValueKind == JsonValueKind.Object ? OptString(sound, "ambient") : null,
            Material = sound.ValueKind == JsonValueKind.Object ? OptString(sound, "material") : null,
        };
    }

    private static ObjectLayer ParseLayer(string s) => s switch
    {
        "entity" => ObjectLayer.Entity,
        "floor" => ObjectLayer.Floor,
        "wall" => ObjectLayer.Wall,
        "overhead" => ObjectLayer.Overhead,
        "fx" => ObjectLayer.Fx,
        _ => throw new FormatException($"camada desconhecida '{s}'"),
    };

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
