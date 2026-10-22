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
    /// <summary>Sem tecla desde a integração v0.1 (F era do Supremo e RB da troca de arma; a v2 não tem tecla de lanterna).</summary>
    public static readonly StringName Lantern = "lantern";
    public static readonly StringName Pause = "pause";

    /// <summary>Minimapa em tela cheia: M ou L3 do gamepad (frente B4a; Tab/Back ficaram com o inventário, DECISIONS INT-D3).</summary>
    public static readonly StringName Map = "map";
    // --- Combate (frente B1; DECISIONS B1-D42) ---

    /// <summary>Próxima/anterior arma da hotbar (roda do mouse; RB só para a próxima: LB é a poção). A classe trava a arma (v2).</summary>
    public static readonly StringName WeaponNext = "weapon_next";

    public static readonly StringName WeaponPrev = "weapon_prev";

    /// <summary>Armas 1–5 da hotbar (<c>sword, bow, staff, hammer, dag</c>).</summary>
    public static readonly StringName[] WeaponSlots = ["weapon_1", "weapon_2", "weapon_3", "weapon_4", "weapon_5"];

    /// <summary>Beber poção (R; LB no gamepad: o direcional anda e o Back é do inventário, DECISIONS INT-D3).</summary>
    public static readonly StringName Potion = "potion";

    /// <summary>Usar o Eco do slot 1 (Q; LT) e do slot 2 (C; clique do analógico direito).</summary>
    public static readonly StringName Eco1 = "eco_1";

    public static readonly StringName Eco2 = "eco_2";

    /// <summary>Golpe Supremo (F ou V, como a v2; RT).</summary>
    public static readonly StringName Ultimate = "ultimate";

    /// <summary>Mira pelo analógico direito.</summary>
    public static readonly StringName AimLeft = "aim_left";

    public static readonly StringName AimRight = "aim_right";

    public static readonly StringName AimUp = "aim_up";

    public static readonly StringName AimDown = "aim_down";
    /// <summary>Abre o inventário (Tab/I, Back no gamepad; <c>main.js:96</c>).</summary>
    public static readonly StringName Inventory = "inventory";

    /// <summary>Só em build de debug: concede XP para testar HUD e ranks.</summary>
    public static readonly StringName DebugGrantXp = "debug_grant_xp";
}
