using Vespera.Core.Story;
using Vespera.Game.Hunter;

namespace Vespera.Game.Story;

/// <summary>
/// Liga as flags da história que vêm do estado do jogo (não de uma fala): arma forjada, XP do próximo rank e o Eco.
/// Escuta <see cref="Progression.Changed"/>; as flags das caçadas (hunt_back, hunt_1, hunt_2, fell_once,
/// contract_done) são ligadas pelo <see cref="HuntService"/> no fim de cada caçada.
/// </summary>
public static class StoryWatch
{
    private const int StartingWeaponLevel = 1;
    private static bool _subscribed;

    public static void Ensure()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        Progression.Changed += Refresh;
    }

    /// <summary>Confere o estado e liga as flags que já valem (idempotente).</summary>
    public static void Refresh()
    {
        if (!Progression.IsActive || Boot.GameSession.Data is null)
        {
            return;
        }

        var engine = StoryService.Engine;
        if (Progression.Engine.WeaponStats.Level > StartingWeaponLevel && !engine.HasFlag(StoryFlags.Forged))
        {
            StoryService.SetFlag(StoryFlags.Forged);
        }

        var status = Progression.Status;
        if (status.Next is not null && status.XpSpan > 0 && status.XpIntoRank >= status.XpSpan && !engine.HasFlag(StoryFlags.XpReady))
        {
            StoryService.SetFlag(StoryFlags.XpReady);
        }

        if (HuntService.HasEcoViscoso && !engine.HasFlag("eco_viscoso"))
        {
            StoryService.SetFlag("eco_viscoso");
        }
    }
}
