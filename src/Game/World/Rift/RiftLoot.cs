using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Core.Story;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Ponte combate → progressão: cada morte, baú, núcleo e o Guardião viram XP e saque pela <see cref="Progression"/>
/// (a fonte única de Fragmentos, materiais, peças e da Lanterna). O mundo só mostra o que a progressão entregou:
/// cristais de Fragmento que voam para o caçador e o nome do que caiu por cima do monstro.
/// </summary>
public static class RiftLoot
{
    private static readonly Color LootColor = new("f4e7c6");
    private static readonly Color RareColor = new("f2a541");
    private static readonly Color XpColor = new("9fd8ff");

    /// <summary>Monstro comum morto: XP da Brecha e um sorteio de saque de monstro.</summary>
    public static void MonsterKilled(LevelWorld world, string monsterId, Vector2 at, bool boss, float xpMult = 1f, int rolls = 1)
    {
        if (!Progression.IsActive)
        {
            return;
        }

        var xp = (int)System.MathF.Round(RiftSession.Rules.Xp(monsterId) * xpMult);
        if (xp > 0)
        {
            Progression.AddXp(xp);

            // Acima do número de dano (que nasce no corpo) e um pouco ao lado, para os dois não se misturarem.
            var lift = (boss ? 2.2f : 1.25f) * world.Data.HumanHeight;
            world.Feel.Number(at + new Vector2(0.35f * world.Data.HumanHeight, -lift), $"+{xp} XP", XpColor, 0.7f);
        }

        for (var i = 0; !boss && i < rolls; i++)
        {
            Show(world, at, Progression.GrantLoot(RiftSession.BreachRank, LootSource.Monster), spread: 60f + (i * 30f));
        }
    }

    /// <summary>Baú aberto: saque de baú (vários sorteios) espalhado em volta.</summary>
    public static void ChestOpened(LevelWorld world, Vector2 at)
    {
        if (!Progression.IsActive)
        {
            return;
        }

        var grant = Progression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        Show(world, at, grant, spread: 90f);
        world.Feel.Toast(Summary("Baú aberto", grant.Loot));
    }

    /// <summary>Parte quebrada (núcleo da Lodo-Mãe).</summary>
    public static void PartBroken(LevelWorld world, Vector2 at)
    {
        if (Progression.IsActive)
        {
            Show(world, at, Progression.GrantLoot(RiftSession.BreachRank, LootSource.Part), spread: 50f);
        }
    }

    /// <summary>Guardião vencido: conquista e saque garantido na progressão, flag na história. Pode subir o rank (E → D).</summary>
    public static void GuardianDefeated(LevelWorld world, Vector2 at)
    {
        var guardianId = RiftSession.Brecha.GuardianId;
        StoryService.SetFlag(guardianId == "lodo_mae" ? StoryFlags.GuardianDefeatedLodoMae : $"GuardianDefeated:{guardianId}");
        if (!Progression.IsActive)
        {
            return;
        }

        var result = Progression.RecordGuardianKill(RiftSession.Brecha.GuardianId);
        Show(world, at, result.Loot, spread: 120f);
        var echo = result.EchoGained is { } e ? $" Eco absorvido: {e.Name}." : string.Empty;
        var fall = guardianId == "lodo_mae" ? "A Lodo-Mãe se desfaz" : "A Macieira Oca racha e cai";
        world.Feel.Toast(Summary(fall, result.Loot.Loot) + echo);
    }

    private static void Show(LevelWorld world, Vector2 at, LootGrant grant, float spread)
    {
        world.DropFragments(at, grant.FragmentosGained, spread);
        var human = world.Data.HumanHeight;
        var i = 0;
        foreach (var entry in grant.Loot.Entries)
        {
            var text = entry.Kind == LootEntryKind.Material ? $"+{entry.Amount} {entry.Name}" : entry.Name;
            var at2 = at + new Vector2(-0.35f * human, -(0.9f * human) - (i++ * 0.32f * human));
            world.Feel.Number(at2, text, entry.Rare || entry.SetPiece ? RareColor : LootColor, 0.7f);
        }
    }

    private static string Summary(string title, LootResult loot)
    {
        var parts = loot.Entries.Select(e => e.Kind == LootEntryKind.Material ? $"{e.Amount} {e.Name}" : e.Name).ToList();
        if (loot.Fragmentos > 0)
        {
            parts.Insert(0, $"{loot.Fragmentos} Fragmentos");
        }

        return parts.Count == 0 ? $"{title}." : $"{title}: +{string.Join(", ", parts)}.";
    }
}
