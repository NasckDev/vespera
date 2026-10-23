using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>Medidas de uma folha de monstro (JSON ao lado do .tres): célula, pé e altura em pé.</summary>
public sealed record MonsterSheet(SpriteFrames Frames, Vector2 Offset, float StandHeight, float SpriteScale);

/// <summary>
/// Folhas dos monstros do Ato I (assets/v2/sprites/monstros/&lt;id&gt;/): vista de lado olhando para a direita (a
/// esquerda espelha). A escala põe cada um na proporção com o caçador (GDD/QA): Lodo na altura do joelho,
/// Espectro da altura do caçador, Lodo-Mãe com ~3 caçadores.
/// </summary>
public static class MonsterSprites
{
    private static readonly Dictionary<string, (SpriteFrames Frames, Vector2 Offset, float Stand)> Cache = [];

    public static void ClearCache() => Cache.Clear();

    /// <summary>Folha do monstro com a escala para a sala (<paramref name="actorScale"/>) e um fator extra de tamanho.</summary>
    public static MonsterSheet Load(string id, float actorScale, float sizeFactor = 1f)
    {
        if (!Cache.TryGetValue(id, out var entry))
        {
            var folder = $"res://assets/v2/sprites/monstros/{id}";
            var frames = GD.Load<SpriteFrames>($"{folder}/{id}.tres");
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString($"{folder}/{id}.json"));
            var root = doc.RootElement;
            var cell = root.GetProperty("cell");
            var anchor = root.GetProperty("anchor");
            var offset = new Vector2((cell[0].GetSingle() / 2f) - anchor[0].GetSingle(), (cell[1].GetSingle() / 2f) - anchor[1].GetSingle());
            entry = (frames, offset, root.GetProperty("stand_height_px").GetSingle());
            Cache[id] = entry;
        }

        // Contrato de escala (PLANO-V2 §2.1): alvo da tabela × altura humana do mapa ÷ standing_height da folha.
        var scale = ScaleRegistry.SpriteScale(id, actorScale * ScaleRules.ReferenceHuman, sizeFactor);
        return new MonsterSheet(entry.Frames, entry.Offset, ScaleRegistry.StandingHeight(id), scale);
    }
}
