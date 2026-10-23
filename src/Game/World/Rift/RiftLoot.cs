using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Coop;
using Vespera.Game.Coop;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Core.Story;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Ponte combate → progressão: cada morte, baú, núcleo e o Guardião viram XP e saque pela <see cref="Progression"/>
/// (a fonte única de Fragmentos, materiais, peças e da Lanterna). O mundo só mostra o que a progressão entregou:
/// cristais de Fragmento que voam para o caçador e o nome do que caiu por cima do monstro. No co-op o XP é de todos (valor
/// cheio para cada um) e o saque é individual: um sorteio por jogador, cristais que só voam para o dono e brilham na cor dele.
/// </summary>
public static class RiftLoot
{
    private static readonly Color LootColor = new("f4e7c6");
    private static readonly Color RareColor = new("f2a541");
    private static readonly Color XpColor = new("9fd8ff");

    /// <summary>Monstro comum morto: XP da Brecha e um sorteio de saque de monstro.</summary>
    public static void MonsterKilled(LevelWorld world, string monsterId, Vector2 at, bool boss)
    {
        if (!Progression.IsActive)
        {
            return;
        }

        var xp = RiftSession.Rules.Xp(monsterId);
        if (xp > 0)
        {
            PartyProgression.AddXp(xp);

            // Acima do número de dano (que nasce no corpo) e um pouco ao lado, para os dois não se misturarem.
            var lift = (boss ? 2.2f : 1.25f) * world.Data.HumanHeight;
            world.Feel.Number(at + new Vector2(0.35f * world.Data.HumanHeight, -lift), $"+{xp} XP", XpColor, 0.7f);
        }

        if (!boss)
        {
            ShowAll(world, at, PartyProgression.GrantLoot(RiftSession.BreachRank, LootSource.Monster), spread: 60f);
        }
    }

    /// <summary>Baú aberto: saque de baú (vários sorteios) espalhado em volta.</summary>
    public static void ChestOpened(LevelWorld world, Vector2 at)
    {
        if (!Progression.IsActive)
        {
            return;
        }

        var grants = PartyProgression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        ShowAll(world, at, grants, spread: 90f);
        world.Feel.Toast(Summary("Baú aberto", grants[0].Grant.Loot) + GuestSummary(grants));
    }

    /// <summary>Parte quebrada (núcleo da Lodo-Mãe).</summary>
    public static void PartBroken(LevelWorld world, Vector2 at)
    {
        if (Progression.IsActive)
        {
            ShowAll(world, at, PartyProgression.GrantLoot(RiftSession.BreachRank, LootSource.Part), spread: 50f);
        }
    }

    /// <summary>Guardião vencido: conquista e saque garantido na progressão, flag na história. Pode subir o rank (E → D).</summary>
    public static void GuardianDefeated(LevelWorld world, Vector2 at)
    {
        StoryService.SetFlag(StoryFlags.GuardianDefeatedLodoMae);
        if (!Progression.IsActive)
        {
            return;
        }

        var result = Progression.RecordGuardianKill(RiftSession.Brecha.GuardianId);
        var grants = new System.Collections.Generic.List<OwnedLoot> { new(0, result.Loot) };
        if (PartyProgression.RecordGuestGuardian(RiftSession.Brecha.GuardianId) is { } guest)
        {
            grants.Add(new OwnedLoot(1, guest.Loot));
        }

        ShowAll(world, at, grants, spread: 120f);
        var echo = result.EchoGained is { } e ? $" Eco absorvido: {e.Name}." : string.Empty;
        world.Feel.Toast(Summary("A Lodo-Mãe se desfaz", result.Loot.Loot) + echo + GuestSummary(grants));
    }

    private static void ShowAll(LevelWorld world, Vector2 at, System.Collections.Generic.IReadOnlyList<OwnedLoot> grants, float spread)
    {
        var coop = grants.Count > 1;
        var line = 0;
        foreach (var owned in grants)
        {
            // No co-op os cristais de cada um saem do lado dele do monstro; os nomes do saque ficam numa coluna só
            // (J1 primeiro, J2 embaixo), cada linha com a etiqueta e a cor do dono, sem um texto pisar no outro.
            var side = coop ? (owned.OwnerSlot == 0 ? -1f : 1f) * 0.35f * world.Data.HumanHeight : 0f;
            world.DropFragments(at + new Vector2(side, 0f), owned.Grant.FragmentosGained, spread, owned.OwnerSlot);
            line = ShowNames(world, at, owned.Grant, coop ? owned.OwnerSlot : -1, line);
        }
    }

    /// <summary>Nomes do saque empilhados acima do monstro a partir da linha <paramref name="line"/>; devolve a próxima linha livre.</summary>
    private static int ShowNames(LevelWorld world, Vector2 at, LootGrant grant, int owner, int line)
    {
        var human = world.Data.HumanHeight;
        var i = line;
        var tag = owner >= 0 ? $"{CoopSession.TagOf(owner)} " : string.Empty;
        const float x = -0.35f;
        foreach (var entry in grant.Loot.Entries)
        {
            var text = entry.Kind == LootEntryKind.Material ? $"{tag}+{entry.Amount} {entry.Name}" : $"{tag}{entry.Name}";
            var at2 = at + new Vector2(x * human, -(0.9f * human) - (i++ * 0.32f * human));
            var color = entry.Rare || entry.SetPiece ? RareColor : owner >= 0 ? CoopSession.ColorOf(owner).Lightened(0.35f) : LootColor;
            world.Feel.Number(at2, text, color, 0.7f);
        }

        return i;
    }

    /// <summary>Resumo do saque do J2 no aviso (o do J1 já vem no começo).</summary>
    private static string GuestSummary(System.Collections.Generic.IReadOnlyList<OwnedLoot> grants)
    {
        if (grants.Count < 2)
        {
            return string.Empty;
        }

        var g = grants[1].Grant.Loot;
        var n = g.Entries.Count + (g.Fragmentos > 0 ? 1 : 0);
        return n == 0 ? string.Empty : $" {CoopSession.TagOf(1)}: +{g.Fragmentos} Fragmentos e {g.Entries.Count} item(ns).";
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
