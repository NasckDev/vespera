using System;
using System.Collections.Generic;
using System.Linq;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Actors;
using Vespera.Game.Hunter;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Equipamento do bot na luta de Guardião (issue #75). Nos roteiros o save tem a arma inicial mesmo em rank alto; o Guardião foi
/// pensado para quem chega com a arma do degrau dele. Por isso, na sala do Guardião o bot empunha a arma do degrau do
/// Guardião (e a devolve depois). Com <c>--bot-duelo</c> ele ainda luta com o dano real (x1) em vez do x10 do bot, e o tempo
/// impresso ("guardião X: luta de N s") passa a ser o de um jogador de verdade.
/// </summary>
public sealed class DuelGear
{
    private readonly string _weapon;
    private readonly int _levelBefore;
    private readonly HunterCombat _combat;

    private DuelGear(HunterCombat combat, string weapon, int levelBefore)
    {
        _combat = combat;
        _weapon = weapon;
        _levelBefore = levelBefore;
    }

    /// <summary>Equipa a arma do degrau do Guardião da Brecha atual; null sem progressão (nada a equipar).</summary>
    public static DuelGear? Equip(HunterCombat combat)
    {
        if (!Progression.IsActive)
        {
            return null;
        }

        var weapon = HunterSprites.WeaponId(combat.Player.Hunter);
        var engine = Progression.Engine;
        var before = engine.Progress.WeaponLevels.GetValueOrDefault(weapon, 1);
        var rank = Progression.Catalog.Ranks.FirstOrDefault(r => r.Guardian == RiftSession.Brecha.GuardianId)?.Id ?? engine.Rank;
        engine.Progress.WeaponLevels[weapon] = Math.Max(before, engine.Forge.FirstLevelOf(rank));
        combat.RefreshStats(fullHeal: false);
        if (RiftSession.BossDuel)
        {
            RiftSession.CheatDamageMultiplier = 1;
        }

        Log.Info($"duelo: {RiftSession.Brecha.GuardianId} com {weapon} no nível {engine.Progress.WeaponLevels[weapon]} (dano {combat.Stats.Atk}), dano do bot x{RiftSession.CheatDamageMultiplier}");
        return new DuelGear(combat, weapon, before);
    }

    public void Restore()
    {
        Progression.Engine.Progress.WeaponLevels[_weapon] = _levelBefore;
        if (GodotObjectAlive())
        {
            _combat.RefreshStats(fullHeal: false);
        }

        RiftSession.CheatDamageMultiplier = 10;
    }

    private bool GodotObjectAlive() => Godot.GodotObject.IsInstanceValid(_combat);
}
