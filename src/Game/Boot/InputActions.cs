using System.Collections.Generic;

namespace Vespera.Game.Boot;

/// <summary>Uma ação remapeável: id da <c>InputMap</c>, rótulo em pt-BR e se o controle é fixo (analógico/direcional).</summary>
public sealed record ActionInfo(string Id, string Label, bool GamepadFixed = false);

/// <summary>Ações nomeadas do jogo (PLANO-V2 §3). As <c>ui_*</c> são as da engine e não são remapeáveis.</summary>
public static class InputActions
{
    public const string MoveUp = "move_up";
    public const string MoveDown = "move_down";
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string Attack = "attack";
    public const string Heavy = "heavy";
    public const string Dodge = "dodge";
    public const string Skill = "skill";
    public const string Interact = "interact";
    public const string Lantern = "lantern";
    public const string Inventory = "inventory";
    public const string Pause = "pause";

    public static readonly IReadOnlyList<ActionInfo> Remappable =
    [
        new(MoveUp, "Mover cima", GamepadFixed: true),
        new(MoveDown, "Mover baixo", GamepadFixed: true),
        new(MoveLeft, "Mover esquerda", GamepadFixed: true),
        new(MoveRight, "Mover direita", GamepadFixed: true),
        new(Attack, "Ataque leve"),
        new(Heavy, "Ataque pesado"),
        new(Dodge, "Esquiva"),
        new(Skill, "Habilidade"),
        new(Interact, "Interagir"),
        new(Lantern, "Lanterna"),
        new(Inventory, "Inventário"),
        new(Pause, "Pausa"),
    ];

    public static string LabelOf(string actionId)
    {
        foreach (var info in Remappable)
        {
            if (info.Id == actionId)
            {
                return info.Label;
            }
        }

        return actionId;
    }
}
