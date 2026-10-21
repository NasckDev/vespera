using System.Text.Json;

namespace Vespera.Core.World;

/// <summary>Forma de um layout: polígono, retângulo ou círculo (coordenadas em u, como a pintura do mapa).</summary>
public sealed record LayoutShape
{
    /// <summary>Vértices do polígono (retângulo e círculo também são guardados como polígono).</summary>
    public required IReadOnlyList<MapPoint> Polygon { get; init; }

    public required MapRect Bounds { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Recorte da arte desenhado no y-sort das entidades com origem em <see cref="BaseY"/>: quem tem o pé acima de
/// <see cref="BaseY"/> fica atrás dele. <see cref="Sway"/> balança com o vento (árvores, bandeiras, toldos).
/// </summary>
public sealed record LayoutOccluder(IReadOnlyList<MapPoint> Polygon, MapRect Bounds, float BaseY, bool Sway, string? Note);

/// <summary>Som ambiente posicional (<c>water, fire, forge, crowd, wind, bells, birds</c>) audível até <see cref="Radius"/> u.</summary>
public sealed record LayoutAmbient(string Kind, float X, float Y, float Radius);

/// <summary>Objeto quebrável (atlas <c>pr_{kind}</c>).</summary>
public sealed record LayoutProp(string Kind, float X, float Y);

/// <summary>Baú (atlas <c>pr_chest{tier}</c>).</summary>
public sealed record LayoutChest(float X, float Y, int Tier);

/// <summary>Decoração com interação (lampião, flor, balsa, pesca, placa, banco, fonte, barraca, inspecionar).</summary>
public sealed record LayoutDecor(string Kind, float X, float Y, float Radius, string? Label, string? Text, MapPoint? To);

/// <summary>
/// Objeto do catálogo (<c>src/Data/objects.json</c>, frente B5) posto no mapa: <see cref="Id"/> é a chave do catálogo,
/// (<see cref="X"/>, <see cref="Y"/>) é o pé do objeto em u e <see cref="Flip"/> espelha na horizontal.
/// </summary>
public sealed record LayoutObject(string Id, float X, float Y, bool Flip);

/// <summary>Efeito de ambiente sem interação (<c>smoke</c> de chaminé, <c>fireflies</c>, <c>embers</c>), com raio de espalhamento.</summary>
public sealed record LayoutFx(string Kind, float X, float Y, float Radius);

/// <summary>
/// Layout de um mapa (<c>assets/maps/&lt;pasta&gt;/layout_&lt;id&gt;.json</c>, formato na docstring de
/// <c>tools/maps/layout.py</c>): colisão redesenhada (gerada em <c>walk_&lt;id&gt;.png</c> pela ferramenta), oclusores,
/// água, sons ambiente, quebráveis, baús, decoração e efeitos. O Core só lê o texto; quem desenha é o World.
/// </summary>
public sealed class MapLayout
{
    public const int DefaultCell = 4;
    public const float DefaultAmbientRadius = 160f;
    public const float DefaultDecorRadius = 24f;
    public const float DefaultFxRadius = 24f;
    private const int CircleSegments = 24;

    public static MapLayout Empty { get; } = new();

    public int Cell { get; init; } = DefaultCell;

    /// <summary><c>mask</c>, <c>shapes</c> ou <c>none</c> (base da colisão; o motor usa o <c>walk_*.png</c> gerado).</summary>
    public string Base { get; init; } = "mask";

    public IReadOnlyList<LayoutShape> Walk { get; init; } = [];

    public IReadOnlyList<LayoutShape> Block { get; init; } = [];

    public IReadOnlyList<LayoutOccluder> Occluders { get; init; } = [];

    public IReadOnlyList<LayoutShape> Water { get; init; } = [];

    public IReadOnlyList<LayoutAmbient> Ambient { get; init; } = [];

    public IReadOnlyList<LayoutProp> Props { get; init; } = [];

    public IReadOnlyList<LayoutChest> Chests { get; init; } = [];

    public IReadOnlyList<LayoutDecor> Decor { get; init; } = [];

    public IReadOnlyList<LayoutFx> Fx { get; init; } = [];

    /// <summary>Objetos do catálogo (<c>objects</c>: <c>{"id", "x", "y", "flip"}</c>).</summary>
    public IReadOnlyList<LayoutObject> Objects { get; init; } = [];

    /// <summary>Lê o JSON do layout. Campos ausentes viram listas vazias; formas inválidas lançam <see cref="MapFormatException"/>.</summary>
    public static MapLayout Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var e = doc.RootElement;
            return new MapLayout
            {
                Cell = e.TryGetProperty("cell", out var cell) ? cell.GetInt32() : DefaultCell,
                Base = e.TryGetProperty("base", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString()! : "mask",
                Walk = List(e, "walk", Shape),
                Block = List(e, "block", Shape),
                Occluders = List(e, "occluders", Occluder),
                Water = List(e, "water", Shape),
                Ambient = List(e, "ambient", x => new LayoutAmbient(ReqStr(x, "kind"), Num(x, "x"), Num(x, "y"), OptNum(x, "r") ?? DefaultAmbientRadius)),
                Props = List(e, "props", x => new LayoutProp(ReqStr(x, "kind"), Num(x, "x"), Num(x, "y"))),
                Chests = List(e, "chests", x => new LayoutChest(Num(x, "x"), Num(x, "y"), x.TryGetProperty("tier", out var t) ? t.GetInt32() : 1)),
                Decor = List(e, "decor", x => new LayoutDecor(
                    ReqStr(x, "kind"),
                    Num(x, "x"),
                    Num(x, "y"),
                    OptNum(x, "r") ?? DefaultDecorRadius,
                    Str(x, "label"),
                    Str(x, "text"),
                    x.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array ? new MapPoint(to[0].GetSingle(), to[1].GetSingle()) : null)),
                Fx = List(e, "fx", x => new LayoutFx(ReqStr(x, "kind"), Num(x, "x"), Num(x, "y"), OptNum(x, "r") ?? DefaultFxRadius)),
                Objects = List(e, "objects", x => new LayoutObject(ReqStr(x, "id"), Num(x, "x"), Num(x, "y"), x.TryGetProperty("flip", out var f) && f.ValueKind == JsonValueKind.True)),
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new MapFormatException($"layout inválido: {ex.Message}", ex);
        }
    }

    /// <summary>Polígono de um círculo com <paramref name="segments"/> lados (vértice inicial à direita, sentido horário na tela).</summary>
    public static IReadOnlyList<MapPoint> CirclePolygon(float cx, float cy, float r, int segments = CircleSegments)
    {
        var points = new MapPoint[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = MathF.Tau * i / segments;
            points[i] = new MapPoint(cx + (MathF.Cos(a) * r), cy + (MathF.Sin(a) * r));
        }

        return points;
    }

    public static IReadOnlyList<MapPoint> RectPolygon(MapRect r) =>
        [new(r.X, r.Y), new(r.Right, r.Y), new(r.Right, r.Bottom), new(r.X, r.Bottom)];

    /// <summary>Lê uma forma <c>poly</c>/<c>rect</c>/<c>circle</c> (também usada pelo <see cref="ObjectCatalog"/>).</summary>
    internal static LayoutShape Shape(JsonElement x)
    {
        var note = Str(x, "note");
        if (x.TryGetProperty("poly", out var poly))
        {
            var points = Points(poly);
            return new LayoutShape { Polygon = points, Bounds = PolygonMath.Bounds(points), Note = note };
        }

        if (x.TryGetProperty("rect", out var rect))
        {
            var r = new MapRect(rect[0].GetSingle(), rect[1].GetSingle(), rect[2].GetSingle(), rect[3].GetSingle());
            return new LayoutShape { Polygon = RectPolygon(r), Bounds = r, Note = note };
        }

        if (x.TryGetProperty("circle", out var circle))
        {
            var (cx, cy, r) = (circle[0].GetSingle(), circle[1].GetSingle(), circle[2].GetSingle());
            return new LayoutShape { Polygon = CirclePolygon(cx, cy, r), Bounds = new MapRect(cx - r, cy - r, r * 2, r * 2), Note = note };
        }

        throw new FormatException("forma sem 'poly', 'rect' ou 'circle'");
    }

    private static LayoutOccluder Occluder(JsonElement x)
    {
        var shape = Shape(x);
        var baseY = OptNum(x, "baseY") ?? shape.Bounds.Bottom;
        return new LayoutOccluder(shape.Polygon, shape.Bounds, baseY, x.TryGetProperty("sway", out var s) && s.ValueKind == JsonValueKind.True, shape.Note);
    }

    private static IReadOnlyList<MapPoint> Points(JsonElement poly)
    {
        var points = new List<MapPoint>(poly.GetArrayLength());
        foreach (var p in poly.EnumerateArray())
        {
            points.Add(new MapPoint(p[0].GetSingle(), p[1].GetSingle()));
        }

        if (points.Count < 3)
        {
            throw new FormatException($"polígono com {points.Count} vértice(s)");
        }

        return points;
    }

    private static float Num(JsonElement e, string name) => e.GetProperty(name).GetSingle();

    private static string ReqStr(JsonElement e, string name) =>
        Str(e, name) ?? throw new FormatException($"campo '{name}' ausente");

    private static float? OptNum(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static IReadOnlyList<T> List<T>(JsonElement e, string name, Func<JsonElement, T> parse) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray().Select(parse)] : [];
}
