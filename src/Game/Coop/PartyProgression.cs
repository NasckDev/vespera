using System;
using System.Collections.Generic;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Coop;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;

namespace Vespera.Game.Coop;

/// <summary>
/// Progressão do grupo no co-op local. O J1 continua na <see cref="Hunter.Progression"/> (dono do save: eventos, tela de
/// rank, forja); o J2 tem um <see cref="ProgressionEngine"/> próprio sobre o <see cref="GuestHunter"/> do mesmo save.
/// XP é compartilhado (cada um recebe o valor cheio) e o saque é individual (um sorteio por jogador), regras do
/// <see cref="CoopLoot"/>. Sem co-op, só o J1 existe e tudo passa direto para a progressão de sempre.
/// </summary>
public static class PartyProgression
{
    private static ProgressionEngine? _guest;
    private static GuestHunter? _boundGuest;
    private static string _boundClass = string.Empty;

    /// <summary>O J2 subiu de rank (aviso curto na HUD; a celebração grande é do dono do save).</summary>
    public static event Action<int, RankChange>? GuestRankChanged;

    /// <summary>Há convidado com progressão nesta sessão.</summary>
    public static bool GuestActive => CoopSession.Active && Hunter.Progression.IsActive && GameSession.Data?.Guest is not null;

    /// <summary>Motor do convidado (refeito ao trocar de save ou de classe).</summary>
    public static ProgressionEngine Guest
    {
        get
        {
            var guest = GameSession.Data?.Guest ?? throw new InvalidOperationException("co-op sem convidado no save");
            if (_guest is null || !ReferenceEquals(_boundGuest, guest) || _boundClass != guest.ClassId)
            {
                _boundGuest = guest;
                _boundClass = guest.ClassId;
                _guest = new ProgressionEngine(Hunter.Progression.Catalog, guest.Progress, guest.ClassId, new SeededRandom(Environment.TickCount ^ 0x5eed));
            }

            return _guest;
        }
    }

    /// <summary>Jogadores com progressão: [0] ou [0, 1].</summary>
    public static IReadOnlyList<int> Slots => GuestActive ? [0, 1] : [0];

    /// <summary>Motor de progressão do jogador (null sem sessão).</summary>
    public static ProgressionEngine? EngineFor(int slot)
    {
        if (!Hunter.Progression.IsActive)
        {
            return null;
        }

        return slot <= 0 ? Hunter.Progression.Engine : GuestActive ? Guest : null;
    }

    /// <summary>XP compartilhado: cada caçador do grupo recebe o valor cheio.</summary>
    public static void AddXp(int xp)
    {
        Hunter.Progression.AddXp(CoopRules.XpPerPlayer(xp, Slots.Count));
        if (GuestActive && Guest.AddXp(CoopRules.XpPerPlayer(xp, Slots.Count)).RankChange is { } change)
        {
            GuestRankChanged?.Invoke(1, change);
        }
    }

    /// <summary>Um sorteio por jogador; cada saque entra só na progressão do dono.</summary>
    public static List<OwnedLoot> GrantLoot(Rank breachRank, LootSource source)
    {
        var grants = new List<OwnedLoot> { new(0, Hunter.Progression.GrantLoot(breachRank, source)) };
        if (GuestActive)
        {
            grants.Add(new OwnedLoot(1, Guest.GrantLoot(breachRank, source)));
        }

        return grants;
    }

    /// <summary>Guardião vencido também conta para o convidado (conquista de rank, saque garantido e Eco dele).</summary>
    public static GuardianKillResult? RecordGuestGuardian(string guardianId)
    {
        if (!GuestActive)
        {
            return null;
        }

        var result = Guest.RecordGuardianKill(guardianId);
        if (result.RankChange is { } change)
        {
            GuestRankChanged?.Invoke(1, change);
        }

        return result;
    }

    /// <summary>Entrada na Brecha do convidado; devolve os Fragmentos que ele recuperou da Lanterna dele.</summary>
    public static int BeginGuestRun(string neighborhoodId) => GuestActive ? Guest.BeginRun(neighborhoodId) : 0;

    /// <summary>Fim da caçada do convidado (vivo leva os Fragmentos dele; na queda geral eles ficam na Lanterna dele).</summary>
    public static RunEnd? EndGuestRun(bool survived) => GuestActive && Guest.InRun ? Guest.EndRun(survived) : null;

    /// <summary>Fragmentos do jogador: da caçada dentro da Brecha ou da carteira na cidade.</summary>
    public static int Fragments(int slot, bool inRift)
    {
        var engine = EngineFor(slot);
        return engine is null ? 0 : inRift ? engine.RunFragmentos : engine.Progress.Fragmentos;
    }

    public static Rank RankOf(int slot) => EngineFor(slot)?.Rank ?? Rank.E;

    public static void Reset()
    {
        _guest = null;
        _boundGuest = null;
        _boundClass = string.Empty;
    }
}
