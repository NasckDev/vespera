using System.Text.Json;

namespace Vespera.Core.World;

/// <summary>Cor de luz em 0–1 (<c>LIGHTC</c>, <c>data.js:4</c>).</summary>
public readonly record struct LightColor(float R, float G, float B);

/// <summary>
/// Regras de iluminação da v2 (spec 01 §4.5, <c>render.js:2-8,55-83</c>): escuridão por mapa e hora, formato das
/// luzes, cintilação, lanterna do jogador e as lâmpadas da rua que acendem ao escurecer. Tudo puro e testável;
/// quem desenha é o shader do Godot.
/// </summary>
public static class CityLighting
{
    /// <summary>Furo de luz na escuridão: raio na tela = 1,6 × raio da luz (<c>render.js:78</c>).</summary>
    public const float HoleRadiusScale = 1.6f;

    /// <summary>Glow aditivo colorido: raio = 0,7 × raio da luz (<c>drawGlows</c>).</summary>
    public const float GlowRadiusScale = 0.7f;

    public const float PlayerHoleAlpha = 0.95f;

    public const float HoleAlpha = 0.85f;

    /// <summary>
    /// Lâmpadas da Rua da Forja (B2-D51): furo menor (1,3× o raio em vez de 1,6×) e mais fraco (0,7). Com dezenas de luzes
    /// na tela, os furos da v2 apagavam a noite inteira; assim cada lâmpada vira uma poça de luz e a rua fica escura entre elas.
    /// </summary>
    public const float StreetLampHoleScale = 1.3f;

    public const float StreetLampHoleAlpha = 0.7f;

    public const float MagentaGlowAlpha = 0.22f;

    public const float GlowAlpha = 0.14f;

    /// <summary>Alpha do centro da textura colorida (<c>makeGlow(cor, .9)</c>).</summary>
    public const float GlowTextureAlpha = 0.9f;

    /// <summary>
    /// <c>core.js:29</c>: toda luz do <c>lights.json</c> vira <c>[x, y, 46, 'a']</c> (o raio 48 e o tipo do arquivo
    /// são ignorados pela v2).
    /// </summary>
    public const float LightsJsonRadius = 46f;

    public const string AmberKind = "a";

    public const string MagentaKind = "m";

    /// <summary>Cor da camada de escuridão <c>rgba(9,7,22,dk)</c>.</summary>
    public static readonly LightColor DarkColor = new(9 / 255f, 7 / 255f, 22 / 255f);

    /// <summary>Abaixo disso a v2 não desenha a escuridão.</summary>
    public const float MinDarkness = 0.02f;

    /// <summary>Escurecimento extra com a Lanterna perdida dentro da Brecha (<c>render.js:82</c>).</summary>
    public const float LostLanternExtra = 0.15f;

    /// <summary>
    /// Escuridão base da cidade pela hora (<c>h = clock/60</c>, sem módulo): <c>h &lt; 17</c> 0,16; 17–19 sobe a 0,50;
    /// 19–22 sobe a 0,62; 22–29 noite fechada 0,62; 29–30 desce a 0,50. DECISIONS D39: a v2 subia até 0,66 às 21:59 e
    /// saltava para 0,62 às 22:00 (<c>render.js:5</c>); aqui a curva é contínua e para no valor da noite.
    /// </summary>
    public static float BaseDarkness(double clockMinutes)
    {
        var h = clockMinutes / 60;
        if (h < 17)
        {
            return 0.16f;
        }

        if (h < 19)
        {
            return Lerp(0.16f, 0.5f, (h - 17) / 2);
        }

        if (h < 22)
        {
            return Lerp(0.5f, 0.62f, (h - 19) / 3);
        }

        if (h < DayPhases.DawnHour)
        {
            return 0.62f;
        }

        return Lerp(0.62f, 0.5f, Math.Min(1, h - DayPhases.DawnHour));
    }

    /// <summary>
    /// <c>darkness()</c>: Brechas têm escuridão fixa (hd 0,26; Núcleo 0,40; outras 0,46); na cidade, a base da hora
    /// com o ajuste do tipo de mapa (loja <c>d·0,45+0,12</c>, interior <c>d·0,55+0,14</c>, Rua da Forja <c>d·0,6</c>).
    /// </summary>
    public static float Darkness(MapDef map, double clockMinutes)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Dungeon)
        {
            return map.Hd ? 0.26f : (map.Theme == "core" ? 0.40f : 0.46f);
        }

        var d = BaseDarkness(clockMinutes);
        if (map.Shop)
        {
            return (d * 0.45f) + 0.12f;
        }

        if (map.Indoor)
        {
            return (d * 0.55f) + 0.14f;
        }

        return map.Hd ? d * StreetFactor(d) : d;
    }

    /// <summary>
    /// Fator da escuridão na Rua da Forja (B2-D51): a v2 usava <c>d·0,6</c> o tempo todo e a noite mal escurecia
    /// (0,37). Aqui o fator sobe com a escuridão base, de 0,6 (dia, igual à v2: 0,096 às 16h) a 1,15 (noite fechada:
    /// 0,71), então a véspera vira noite de verdade e as lâmpadas e a lanterna passam a desenhar a rua.
    /// </summary>
    public static float StreetFactor(float baseDarkness) =>
        0.6f + (0.55f * Math.Clamp((baseDarkness - 0.16f) / (0.62f - 0.16f), 0f, 1f));

    /// <summary>
    /// Cor da hora na rua (B2-D51), multiplicada sobre o mundo antes da escuridão: tarde neutra, véspera dourada e rosada
    /// (pico às 18h), crepúsculo violeta às 19h, noite azulada a partir das 20h e aurora quente antes das 6h.
    /// Interiores e Brechas não usam (cor branca).
    /// </summary>
    public static LightColor StreetGrade(double clockMinutes)
    {
        var h = clockMinutes / 60;
        for (var i = 1; i < GradeKeys.Length; i++)
        {
            if (h <= GradeKeys[i].Hour)
            {
                var (h0, c0) = GradeKeys[i - 1];
                var (h1, c1) = GradeKeys[i];
                var t = Math.Clamp((h - h0) / (h1 - h0), 0, 1);
                return new LightColor(Lerp(c0.R, c1.R, t), Lerp(c0.G, c1.G, t), Lerp(c0.B, c1.B, t));
            }
        }

        return GradeKeys[^1].Color;
    }

    private static readonly (double Hour, LightColor Color)[] GradeKeys =
    [
        (0, new LightColor(1f, 1f, 1f)),
        (16.5, new LightColor(1f, 1f, 1f)),
        (17.25, new LightColor(1f, 0.92f, 0.84f)),
        (18.25, new LightColor(1f, 0.82f, 0.8f)),
        (19.25, new LightColor(0.86f, 0.8f, 0.94f)),
        (20.25, new LightColor(0.78f, 0.84f, 1f)),
        (29, new LightColor(0.78f, 0.84f, 1f)),
        (29.6, new LightColor(0.9f, 0.84f, 0.94f)),
        (30, new LightColor(1f, 0.93f, 0.88f)),
    ];

    /// <summary>Cintilação das luzes do mapa: <c>r · (1 + 0,05 sin(t·(7 + i%3) + i))</c>.</summary>
    public static float Flicker(int index, double seconds) =>
        1f + (0.05f * (float)Math.Sin((seconds * (7 + (index % 3))) + index));

    /// <summary>Raio da lanterna do jogador: <c>(morto ? 40 : 72) · luz</c>.</summary>
    public static float PlayerLightRadius(bool dead, double lightMultiplier) => (dead ? 40f : 72f) * (float)lightMultiplier;

    /// <summary>Alpha do furo: 0,95 para o jogador, 0,85 para o resto.</summary>
    public static float HoleAlphaFor(bool player) => player ? PlayerHoleAlpha : HoleAlpha;

    /// <summary>Alpha do glow aditivo: 0,22 magenta, 0,14 os outros.</summary>
    public static float GlowAlphaFor(string kind) => kind == MagentaKind ? MagentaGlowAlpha : GlowAlpha;

    /// <summary>Cores de <c>LIGHTC</c>; tipo desconhecido usa o âmbar (<c>glowCol[l[3]] || glowCol.a</c>).</summary>
    public static LightColor ColorOf(string kind) => kind switch
    {
        "f" => new LightColor(1f, 120 / 255f, 40 / 255f),
        "m" => new LightColor(230 / 255f, 74 / 255f, 142 / 255f),
        "c" => new LightColor(95 / 255f, 208 / 255f, 230 / 255f),
        "g" => new LightColor(110 / 255f, 230 / 255f, 90 / 255f),
        "w" => new LightColor(1f, 240 / 255f, 220 / 255f),
        _ => new LightColor(242 / 255f, 165 / 255f, 65 / 255f),
    };

    /// <summary>
    /// Perfil radial da textura de luz (<c>makeGlow</c>): 1 no centro, 0,55 a 40% do raio, 0 na borda.
    /// <paramref name="d"/> = distância / raio.
    /// </summary>
    public static float Profile(float d)
    {
        if (d >= 1f)
        {
            return 0f;
        }

        return d < 0.4f ? Lerp(1f, 0.55f, d / 0.4f) : Lerp(0.55f, 0f, (d - 0.4f) / 0.6f);
    }

    /// <summary>Primeiro minuto em que as lâmpadas da rua começam a acender (17:00, quando a cidade escurece).</summary>
    public const double LampsFromMinutes = 17 * 60;

    /// <summary>Janela em que todas as lâmpadas acendem, uma a uma (17:00–18:00).</summary>
    public const double LampsWindowMinutes = 60;

    /// <summary>Quanto tempo de jogo cada lâmpada leva para acender por inteiro.</summary>
    public const double LampFadeMinutes = 3;

    /// <summary>
    /// Nível (0–1) da lâmpada <paramref name="index"/> da rua (luzes do <c>lights.json</c> em mapas sem teto).
    /// DECISIONS D40: na v2 as luzes do mapa ficam sempre acesas; aqui cada lâmpada acende num minuto próprio entre
    /// 17:00 e 18:00 (ordem espalhada pela razão áurea, como o Faísca andando pela rua) e fica acesa até o dia virar.
    /// </summary>
    public static float LampLevel(int index, double clockMinutes)
    {
        var order = (index * 0.6180339887) % 1.0;
        var onAt = LampsFromMinutes + (order * (LampsWindowMinutes - LampFadeMinutes));
        return (float)Math.Clamp((clockMinutes - onAt) / LampFadeMinutes, 0, 1);
    }

    /// <summary>
    /// Luzes do <c>assets/maps/lights.json</c> por chave (<c>city</c>, <c>fish2</c>…), já como a v2 usa:
    /// raio 46 e tipo âmbar.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<MapLight>> ParseLightsJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, IReadOnlyList<MapLight>>(StringComparer.Ordinal);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var list = new List<MapLight>(entry.Value.GetArrayLength());
            foreach (var l in entry.Value.EnumerateArray())
            {
                list.Add(new MapLight(l[0].GetSingle(), l[1].GetSingle(), LightsJsonRadius, AmberKind));
            }

            result[entry.Name] = list;
        }

        return result;
    }

    private static float Lerp(float a, float b, double t) => a + ((b - a) * (float)t);
}
