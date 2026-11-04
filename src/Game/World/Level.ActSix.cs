using Godot;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Components;
using Vespera.Game.Story;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Ato VI (ROTEIRO-V2 §13, "A Sopa da Véspera") e a Brecha B das Ruas Baixas do Ato IV (§8.1, Ravi em perigo):
/// a Sopa da Véspera da Nilza (<c>UnlockSopaDaVespera</c>: segunda vida por caçada, ver <see cref="RunPerks.CanRise"/>),
/// a arma de Duas Vésperas da Odete (<c>GiveLegendaryWeapon</c>) e a Lanterna do Ravi na sala 3 da Brecha B rompida.
/// Ganchos: <c>OnWorldMounted</c> e o fim de cada conversa.
/// </summary>
public partial class Level
{
    public const string EventUnlockSopa = "UnlockSopaDaVespera";
    public const string EventGiveLegendaryWeapon = "GiveLegendaryWeapon";

    public const string RaviLanternDialogue = "ato4/lanterna_ravi";
    private static readonly Vector2 RaviLanternAt = new(600, 330);

    private void ShowActSix()
    {
        if (!RiftSession.InRift || !RaviRescue.LanternWaiting(RiftSession.Brecha.Id, World.Data.Id, StoryService.Engine))
        {
            return;
        }

        PlaceFloorLantern("LanternaRavi", "zone_lanterna_ravi", RaviLanternAt, RaviLanternDialogue, "Pegar a lanterna");
        Log.Info("ato4: Lanterna do Ravi na Cozinha Velha da Brecha B");
    }

    /// <summary>Eventos dos Atos VI e VII; falso se o evento é de outro ato.</summary>
    private bool HandleActSixSevenEvent(string name)
    {
        HideTakenRaviLantern();
        if (name == EventUnlockSopa)
        {
            StoryService.SetFlag(RunPerks.SecondLifeFlag);
            CombatHud.Toast("Sopa da Véspera: se cair dentro da Brecha, você levanta uma vez por caçada.");
            return true;
        }

        if (name == EventGiveLegendaryWeapon)
        {
            GiveLegendaryWeapon();
            return true;
        }

        return HandleActSevenEvent(name);
    }

    /// <summary>A Odete forja a arma de Duas Vésperas da classe: a arma sobe para o degrau SS, de graça.</summary>
    private void GiveLegendaryWeapon()
    {
        if (!Hunter.Progression.IsActive)
        {
            return;
        }

        var engine = Hunter.Progression.Engine;
        var weapon = engine.Forge.Grant(engine.Progress, engine.ClassId, Rank.SS);
        Hunter.Progression.Save();
        Hunter.Progression.Notify();
        World.Player.Combat.RefreshStats(fullHeal: false);
        CombatHud.Toast($"{weapon.Name}: {weapon.Damage} de dano.");
    }

    private void HideTakenRaviLantern()
    {
        if (!StoryService.Engine.HasFlag(RaviRescue.LanternRecovered))
        {
            return;
        }

        World.Entities.GetNodeOrNull("LanternaRavi")?.QueueFree();
        if (World.Entities.GetNodeOrNull<Interactable>("zone_lanterna_ravi") is { } zone)
        {
            zone.Enabled = false;
            zone.QueueFree();
        }
    }
}
