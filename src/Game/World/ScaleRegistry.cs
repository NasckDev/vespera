using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Vespera.Game.World;

/// <summary>Um ator do contrato de escala: altura-alvo em múltiplos da altura humana, folha medida e flutuação.</summary>
/// <param name="Height">Altura do corpo (pés à cabeça, sem arma nem efeito) ÷ altura humana do mapa.</param>
/// <param name="Sheet">JSON da folha (res://) com "standing_height"; vazio = ainda sem folha.</param>
/// <param name="Hover">Altura em que flutua acima do chão ÷ altura humana (Espectro 0,15).</param>
/// <param name="Key">Se preenchido, o JSON guarda vários atores e a medida fica em <c>{Key: {standing_height}}</c> (npcs.json).</param>
public sealed record ScaleEntry(float Height, string Sheet, float Hover = 0f, string Key = "");

/// <summary>
/// Registro único do contrato de escala (PLANO-V2 §2.1). O mapa declara <c>doorHeight</c> (px de mundo, medido numa
/// porta pintada); a altura humana do mapa é 0,75 × porta (<see cref="ScaleRules.HumanHeight"/>); cada ator tem uma
/// altura-alvo em múltiplos dela (tabela abaixo); a escala do sprite = altura-alvo ÷ altura opaca em pé medida na folha
/// (<c>standing_height</c>, gravada por <c>tools/art/medir_altura_em_pe.py</c> e <c>tools/art/import_npcs.py</c>).
/// Ator novo = uma linha aqui. Lógica pura: quem lê o arquivo é <see cref="ReadText"/> (FileAccess no jogo, disco nos testes).
/// </summary>
public static class ScaleRegistry
{
    public const string Hunter = "lamina";

    private const string Sprites = "res://assets/v2/sprites/";
    private const string Npcs = Sprites + "npcs/npcs.json";

    private static readonly Dictionary<string, ScaleEntry> Table = new()
    {
        ["lamina"] = new(1.00f, Sprites + "lamina/lamina_e1.json"),
        ["arco"] = new(1.00f, Sprites + "arco/arco_e1.json"),
        ["martelo"] = new(1.00f, Sprites + "martelo/martelo_e1.json"),
        ["odete"] = new(1.00f, Npcs, Key: "odete"),
        ["ravi"] = new(1.00f, Npcs, Key: "ravi"),
        ["nilza"] = new(1.00f, Npcs, Key: "nilza"),
        ["benicio"] = new(0.95f, Npcs, Key: "benicio"),
        ["lis"] = new(0.75f, Npcs, Key: "lis"),
        ["lodo"] = new(0.35f, Sprites + "monstros/lodo/lodo.json"),
        ["espectro"] = new(1.00f, Sprites + "monstros/espectro/espectro.json", Hover: 0.15f),
        ["lodo_mae"] = new(3.00f, Sprites + "monstros/lodo_mae/lodo_mae.json"),
    };

    private static readonly Dictionary<string, float> Standing = [];

    /// <summary>Lê um arquivo pelo caminho res:// (o jogo liga em FileAccess; os testes, no disco).</summary>
    public static Func<string, string?> ReadText { get; set; } = _ => null;

    public static IReadOnlyDictionary<string, ScaleEntry> Actors => Table;

    public static ScaleEntry Get(string actorId) =>
        Table.TryGetValue(actorId, out var entry) ? entry : throw new KeyNotFoundException($"ator '{actorId}' fora do contrato de escala");

    /// <summary>Altura humana do mapa (px de mundo) para a porta dada.</summary>
    public static float HumanHeight(float doorHeight) => ScaleRules.HumanHeight(doorHeight);

    /// <summary>
    /// Escala de mundo dos atores num mapa (velocidades, pés, alcances e efeitos são dados na escala 1 = humano de
    /// <see cref="ScaleRules.ReferenceHuman"/> px). É o <see cref="LevelData.ActorScale"/>.
    /// </summary>
    public static float HunterScale(float doorHeight) => HumanHeight(doorHeight) / ScaleRules.ReferenceHuman;

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

        var entry = Get(actorId);
        if (entry.Sheet.Length == 0)
        {
            throw new InvalidOperationException($"'{actorId}' ainda não tem folha medida");
        }

        var json = ReadText(entry.Sheet) ?? throw new InvalidOperationException($"folha de '{actorId}' ausente: {entry.Sheet}");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (entry.Key.Length > 0 && !root.TryGetProperty(entry.Key, out root))
        {
            throw new InvalidOperationException($"{entry.Sheet} sem a chave '{entry.Key}'");
        }

        if (!root.TryGetProperty("standing_height", out var h) || h.GetSingle() <= 0)
        {
            throw new InvalidOperationException($"{entry.Sheet} sem standing_height para '{actorId}'");
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
