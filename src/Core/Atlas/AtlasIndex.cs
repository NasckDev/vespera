using System.Text.Json;

namespace Vespera.Core.Atlas;

/// <summary>
/// Um recorte do atlas. <see cref="AnchorX"/>/<see cref="AnchorY"/> são o pivô em px do recorte (na v2, sempre o pé:
/// <c>AnchorY == Height</c>). <see cref="Density"/> é px de atlas por u: o tamanho no mundo é <c>Width / Density</c>.
/// </summary>
public readonly record struct AtlasFrame(int X, int Y, int Width, int Height, double AnchorX, double AnchorY, double Density)
{
    public double WorldWidth => Width / Density;

    public double WorldHeight => Height / Density;

    /// <summary>Escala a aplicar no recorte para desenhá-lo em u (vezes a escala extra do objeto).</summary>
    public double ScaleFor(double extraScale = 1.0) => extraScale / Density;
}

/// <summary>
/// Índice do <c>atlas.json</c> da v2: <c>{nome: [x, y, w, h, ax, ay, k]}</c> (spec 01 §4.1), com os overrides de
/// âncora X do protótipo (<c>AXO</c>, <c>core.js:5</c>, em u, convertidos para px multiplicando por k).
/// </summary>
public sealed class AtlasIndex
{
    /// <summary><c>AXO</c> de <c>core.js:5</c>: âncora X em u para poses de ataque cujo recorte é assimétrico.</summary>
    public static readonly IReadOnlyDictionary<string, double> PrototypeAnchorXOverrides = new Dictionary<string, double>
    {
        ["c_sword_atk"] = 18,
        ["c_bow_atk"] = 17,
        ["c_staff_atk"] = 21,
        ["c_hammer_atk"] = 20,
        ["c_dag_atk"] = 22,
        ["wolf_a"] = 18,
        ["fishm_a"] = 19,
        ["anglerB_a"] = 36,
    };

    private readonly Dictionary<string, AtlasFrame> _frames;

    private AtlasIndex(Dictionary<string, AtlasFrame> frames) => _frames = frames;

    public int Count => _frames.Count;

    public IEnumerable<string> Keys => _frames.Keys;

    public AtlasFrame this[string key] =>
        _frames.TryGetValue(key, out var frame) ? frame : throw new KeyNotFoundException($"Recorte '{key}' não existe no atlas.");

    public bool TryGet(string key, out AtlasFrame frame) => _frames.TryGetValue(key, out frame);

    /// <summary>Lê o <c>atlas.json</c>. Sem <paramref name="anchorXOverrides"/>, aplica os da v2.</summary>
    public static AtlasIndex Parse(string json, IReadOnlyDictionary<string, double>? anchorXOverrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        anchorXOverrides ??= PrototypeAnchorXOverrides;

        using var doc = JsonDocument.Parse(json);
        var frames = new Dictionary<string, AtlasFrame>(StringComparer.Ordinal);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var v = entry.Value;
            if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() < 6)
            {
                throw new FormatException($"Recorte '{entry.Name}' do atlas deve ser [x, y, w, h, ax, ay, k].");
            }

            // core.js:60 — k ausente ou 0 vale 1.
            var k = v.GetArrayLength() > 6 ? v[6].GetDouble() : 0;
            if (k <= 0)
            {
                k = 1;
            }

            var ax = anchorXOverrides.TryGetValue(entry.Name, out var axo) ? axo * k : v[4].GetDouble();
            frames[entry.Name] = new AtlasFrame(v[0].GetInt32(), v[1].GetInt32(), v[2].GetInt32(), v[3].GetInt32(), ax, v[5].GetDouble(), k);
        }

        return new AtlasIndex(frames);
    }

    /// <summary>
    /// Lê o <c>meta.json</c> dos sprites soltos de <c>assets/src</c> (<c>{nome: [w, h, k]}</c>) como densidade por nome.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ParseLooseDensities(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var k = entry.Value.GetArrayLength() > 2 ? entry.Value[2].GetDouble() : 1;
            result[entry.Name] = k > 0 ? k : 1;
        }

        return result;
    }

    /// <summary>
    /// Pivô de um sprite solto (sem âncora no protótipo): centro da base do recorte não transparente (DECISIONS D10).
    /// Recebe o retângulo usado (alpha &gt; 0) em px da imagem.
    /// </summary>
    public static (double AnchorX, double AnchorY) BaseCenterAnchor(int usedX, int usedY, int usedWidth, int usedHeight) =>
        (usedX + (usedWidth / 2.0), usedY + usedHeight);
}
