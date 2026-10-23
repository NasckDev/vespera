using System.Collections.Generic;

namespace Vespera.Game.World;

/// <summary>
/// Registro único de escala (PLANO-V2 §2.1). O mapa declara <c>doorHeight</c> (px de mundo, medido numa porta pintada);
/// a altura humana do mapa é <c>0,75 x doorHeight</c>; cada ator tem uma altura-alvo em múltiplos dela; a escala do
/// sprite é altura-alvo ÷ altura opaca da tira em pé (<c>standing_height</c>). Lógica pura, sem Godot: testada fora da engine.
/// </summary>
public static class ScaleRegistry
{
    /// <summary>Altura humana / altura da porta pintada.</summary>
    public const float HumanOverDoor = 0.75f;

    /// <summary>Altura opaca (px) do caçador na célula 192 das tiras de andar de frente (mediana dos quadros).</summary>
    public const float HunterStandingHeight = 123f;

    public const string Hunter = "hunter";

    /// <summary>Altura-alvo de cada ator, em múltiplos da altura humana do mapa (tabela do PLANO-V2 §2.1).</summary>
    public static readonly IReadOnlyDictionary<string, float> Targets = new Dictionary<string, float>
    {
        [Hunter] = 1.00f,
        ["odete"] = 1.00f,
        ["ravi"] = 1.00f,
        ["nilza"] = 1.00f,
        ["benicio"] = 0.95f,
        ["lis"] = 0.75f,
        ["lodo"] = 0.35f,
        ["espectro"] = 1.00f,
        ["lodo_mae"] = 3.00f,
    };

    public static float HumanHeight(float doorHeight) => HumanOverDoor * doorHeight;

    /// <summary>Escala do caçador (e do que é medido por ele: pé, câmera) num mapa com a porta dada.</summary>
    public static float HunterScale(float doorHeight) => HumanHeight(doorHeight) / HunterStandingHeight;

    /// <summary>Altura-alvo em px de mundo do ator <paramref name="id"/> no mapa.</summary>
    public static float TargetHeight(string id, float doorHeight) => Target(id) * HumanHeight(doorHeight);

    /// <summary>Escala do sprite do ator: altura-alvo ÷ altura opaca em pé (<paramref name="standingHeight"/>, px da tira).</summary>
    public static float ActorScale(string id, float doorHeight, float standingHeight) => TargetHeight(id, doorHeight) / standingHeight;

    /// <summary>Altura de porta equivalente a uma escala de caçador (mapas antigos que só tinham actorScale).</summary>
    public static float DoorHeightFor(float hunterScale) => hunterScale * HunterStandingHeight / HumanOverDoor;

    private static float Target(string id) =>
        Targets.TryGetValue(id, out var multiple) ? multiple : throw new KeyNotFoundException($"ator '{id}' não está na tabela de escala");
}
