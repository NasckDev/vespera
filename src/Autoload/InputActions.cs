using Godot;

namespace Vespera.Autoload;

/// <summary>Nomes das ações do Input Map (project.godot). Use estas constantes em vez de strings soltas.</summary>
public static class InputActions
{
    public static readonly StringName MoveLeft = "move_left";
    public static readonly StringName MoveRight = "move_right";
    public static readonly StringName MoveUp = "move_up";
    public static readonly StringName MoveDown = "move_down";
    public static readonly StringName Attack = "attack";
    public static readonly StringName AttackHeavy = "attack_heavy";
    public static readonly StringName Dodge = "dodge";
    public static readonly StringName Interact = "interact";
    public static readonly StringName Lantern = "lantern";
    public static readonly StringName Pause = "pause";

    /// <summary>Abre o inventário (Tab/I, Back no gamepad; <c>main.js:96</c>).</summary>
    public static readonly StringName Inventory = "inventory";

    /// <summary>Só em build de debug: concede XP para testar HUD e ranks.</summary>
    public static readonly StringName DebugGrantXp = "debug_grant_xp";
}
