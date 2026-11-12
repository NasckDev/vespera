using Vespera.Core.Advancement;
using Vespera.Core.Ranks;

namespace Vespera.Core.Coop;

/// <summary>Saque de um jogador: o sorteio dele e de quem é (índice do jogador, 0 = J1).</summary>
public sealed record OwnedLoot(int OwnerSlot, LootGrant Grant);

/// <summary>Resultado de XP compartilhado para um jogador.</summary>
public sealed record SharedXp(int Slot, XpResult Result);

/// <summary>
/// Saque e XP do co-op (GDD §5.8): o XP é compartilhado (cada caçador recebe o valor cheio) e o saque é INDIVIDUAL — cada
/// jogador tem o próprio sorteio, no próprio <see cref="ProgressionEngine"/>, e só o dono recolhe o que caiu para ele.
/// </summary>
public static class CoopLoot
{
    /// <summary>Só o dono recolhe um saque individual.</summary>
    public static bool CanCollect(int ownerSlot, int collectorSlot) => ownerSlot == collectorSlot;

    /// <summary>Um sorteio separado por jogador; cada um entra só na progressão do dono.</summary>
    public static List<OwnedLoot> GrantEach(IReadOnlyList<ProgressionEngine> party, Rank breachRank, LootSource source)
    {
        ArgumentNullException.ThrowIfNull(party);
        var grants = new List<OwnedLoot>(party.Count);
        for (var slot = 0; slot < party.Count; slot++)
        {
            grants.Add(new OwnedLoot(slot, party[slot].GrantLoot(breachRank, source)));
        }

        return grants;
    }

    /// <summary>XP compartilhado: todos recebem <paramref name="xp"/> inteiro.</summary>
    public static List<SharedXp> ShareXp(IReadOnlyList<ProgressionEngine> party, int xp)
    {
        ArgumentNullException.ThrowIfNull(party);
        var each = CoopRules.XpPerPlayer(xp, party.Count);
        var results = new List<SharedXp>(party.Count);
        for (var slot = 0; slot < party.Count; slot++)
        {
            results.Add(new SharedXp(slot, party[slot].AddXp(each)));
        }

        return results;
    }

    /// <summary>
    /// Fim da caçada para o grupo inteiro (uma caçada só): vivos levam os Fragmentos de cada um para casa; na queda geral,
    /// os Fragmentos de cada jogador ficam na Lanterna dele, no bairro da Brecha.
    /// </summary>
    public static List<RunEnd?> EndRunEach(IReadOnlyList<ProgressionEngine> party, bool survived)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.Select(engine => engine.EndRun(survived)).ToList();
    }
}
