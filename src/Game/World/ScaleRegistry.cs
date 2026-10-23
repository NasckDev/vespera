using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Vespera.Game.World;

/// <summary>Um ator do contrato de escala: altura-alvo em múltiplos da altura humana, folha medida e flutuação.</summary>
/// <param name="Height">Altura do corpo (pés à cabeça, sem arma nem efeito) ÷ altura humana do mapa.</param>
/// <param name="Sheet">JSON da folha (res://) com "standing_height"; vazio = ainda sem folha.</param>
/// <param name="Hover">Altura em que flutua acima do chão ÷ altura humana (Espectro 0,15).</param>
public sealed record ScaleEntry(float Height, string Sheet, float Hover = 0f);

/// <summary>
/// Registro único do contrato de escala (PLANO-V2 §2.1): escala do sprite = altura-alvo no mapa ÷ altura opaca em pé
/// medida na folha (<c>standing_height</c>, gravada por <c>tools/art/medir_altura_em_pe.py</c>). A altura-alvo é a
/// altura humana do mapa (0,75 × <see cref="LevelData.DoorHeight"/>) × a altura da tabela. Ator novo = uma linha aqui.
/// Lógica pura: quem lê o arquivo é <see cref="ReadText"/> (FileAccess no jogo, disco nos testes).
/// </summary>
public static class ScaleRegistry
{
    private const string Sprites = "res://assets/v2/sprites/";

    private static readonly Dictionary<string, ScaleEntry> Table = new()
    {
        ["lamina"] = new(1.00f, Sprites + "lamina/lamina_e1.json"),
        ["arco"] = new(1.00f, Sprites + "arco/arco_e1.json"),
        ["martelo"] = new(1.00f, Sprites + "martelo/martelo_e1.json"),
        ["odete"] = new(1.00f, string.Empty),
        ["ravi"] = new(1.00f, string.Empty),
        ["nilza"] = new(1.00f, string.Empty),
        ["benicio"] = new(0.95f, string.Empty),
        ["lis"] = new(0.75f, string.Empty),
        ["lodo"] = new(0.35f, Sprites + "monstros/lodo/lodo.json"),
        ["espectro"] = new(1.00f, Sprites + "monstros/espectro/espectro.json", Hover: 0.15f),
        ["lodo_mae"] = new(3.00f, Sprites + "monstros/lodo_mae/lodo_mae.json"),
    };

    private static readonly Dictionary<string, float> Standing = [];

    /// <summary>Lê um arquivo pelo caminho res:// (o jogo liga em FileAccess; os testes, no disco).</summary>
    public static Func<string, string?> ReadText { get; set; } = _ => null;

    public static IReadOnlyDictionary<string, ScaleEntry> Actors => Table;

    public static ScaleEntry Get(string actorId) =>
        Table.TryGetValue(actorId, out var entry) ? entry : throw new ArgumentException($"ator '{actorId}' fora do contrato de escala", nameof(actorId));

    /// <summary>Altura-alvo do ator em px de mundo num mapa com esta altura humana.</summary>
    public static float TargetHeight(string actorId, float humanHeight) => Get(actorId).Height * humanHeight;

    /// <summary>Quanto o ator flutua acima do chão (px de mundo).</summary>
    public static float HoverHeight(string actorId, float humanHeight) => Get(actorId).Hover * humanHeight;

    /// <summary>Altura opaca em pé gravada na folha (px da folha).</summary>
    public static float StandingHeight(string actorId)
    {
        if (Standing.TryGetValue(actorId, out var cached))
        {
            return cached;
        }

        var sheet = Get(actorId).Sheet;
        var json = ReadText(sheet) ?? throw new InvalidOperationException($"folha de '{actorId}' ausente: {sheet}");
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("standing_height", out var h) || h.GetSingle() <= 0)
        {
            throw new InvalidOperationException($"{sheet} sem standing_height (rode tools/art/medir_altura_em_pe.py)");
        }

        Standing[actorId] = h.GetSingle();
        return Standing[actorId];
    }

    /// <summary>Escala do sprite: alvo × <paramref name="sizeFactor"/> ÷ standing_height.</summary>
    public static float SpriteScale(string actorId, float humanHeight, float sizeFactor = 1f) =>
        TargetHeight(actorId, humanHeight) * sizeFactor / StandingHeight(actorId);

    /// <summary>Verdadeiro se a altura medida está dentro de ±8% do alvo.</summary>
    public static bool WithinTolerance(float measured, float target) => MathF.Abs(measured - target) <= target * ScaleRules.Tolerance;

    public static void ClearCache() => Standing.Clear();
}
