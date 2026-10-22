using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Rules;

namespace Vespera.World;

/// <summary>
/// Entrega do saque de objetos do mundo (quebráveis, baús, flores, pesca) no estado do jogador, com texto subindo e som
/// (DECISIONS D45). Na Brecha, o combate pode redirecionar para a bolsa da caçada com <see cref="Sink"/>.
/// </summary>
public static class WorldLoot
{
    private static readonly Color LumesColor = new("#F2A541");
    private static readonly Color ItemColor = new("#F4E7C6");
    private static readonly Color RareColor = new("#E64A8E");

    /// <summary>Rota alternativa (ex.: bolsa da Brecha). Devolva true se tratou o drop; false deixa o mundo entregar.</summary>
    public static Func<LootDrop, Vector2, bool>? Sink { get; set; }

    /// <summary>Entrega um drop e mostra o texto em <paramref name="at"/>. <paramref name="delay"/> escalona vários drops.</summary>
    public static void Grant(LootDrop drop, Vector2 at, FloatingTextPool? popups, float delay = 0f)
    {
        if (Sink is { } sink && sink(drop, at))
        {
            return;
        }

        var game = GameManager.Instance;
        var db = GameManager.Db;
        string text;
        string? icon;
        var color = ItemColor;
        var sfx = "pick";
        switch (drop.Kind)
        {
            case LootKind.Lumes:
                game.State.AddLumes(drop.Amount);
                text = $"+{drop.Amount} Lumes";
                icon = "i_coin";
                color = LumesColor;
                sfx = "coin";
                break;
            case LootKind.Material when drop.Id is { } id && db.Materials.TryGetValue(id, out var mat):
                game.State.AddMaterial(id, drop.Amount);
                text = $"+{drop.Amount} {mat.Name}";
                icon = mat.Icon;
                break;
            case LootKind.Potion:
                game.State.AddPotions(drop.Amount);
                text = drop.Amount == 1 ? "+1 Poção" : $"+{drop.Amount} Poções";
                icon = "i_potion";
                break;
            case LootKind.Gear when drop.Id is { } gid && db.Gear.TryGetValue(gid, out var gear):
                game.State.AddGear(gid);
                text = gear.Name;
                icon = gear.Icon;
                color = RareColor;
                sfx = "open";
                break;
            default:
                Log.Warning($"WorldLoot: drop desconhecido {drop}.");
                return;
        }

        popups?.Show(at + new Vector2(0, -6), text, AtlasLibrary.TryGet(icon, out _) ? icon : null, color, delay);
        AudioManager.Instance?.PlaySfx(sfx, at);
    }
}
