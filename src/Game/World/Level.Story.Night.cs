using System.Linq;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.UI;

namespace Vespera.Game.World;

/// <summary>
/// Eventos de fala dos Atos IV e V: esperar a noite no banco do Cemitério (o Vigia só aparece à noite) e o primeiro
/// slot de Eco que o Vigia explica e guarda (<c>UnlockEchoSlot:1</c>, ligado ao Eco Viscoso que já existe).
/// </summary>
public partial class Level
{
    /// <summary>Evento do banco do Cemitério: a noite cai e a fase recarrega com o Vigia no lugar.</summary>
    public const string EventWaitNight = "WaitNight";

    /// <summary>Prefixo do evento do Vigia que libera um slot de Eco (<c>UnlockEchoSlot:1</c>).</summary>
    public const string EventUnlockEchoSlot = "UnlockEchoSlot:";

    /// <summary>Flag gravada quando o slot <c>n</c> de Eco é liberado (<c>eco_slot_1</c>).</summary>
    public static string EchoSlotFlag(int slot) => $"eco_slot_{slot}";

    private void HandleNightEvent(string name)
    {
        if (name == EventWaitNight)
        {
            WaitForNight();
        }
        else if (name.StartsWith(EventUnlockEchoSlot, System.StringComparison.Ordinal)
            && int.TryParse(name[EventUnlockEchoSlot.Length..], out var slot))
        {
            UnlockEchoSlot(slot);
        }
    }

    private void WaitForNight()
    {
        StoryService.NightFalls();
        var at = Player.GlobalPosition;
        GameEntry.Travel(World.Data.Id, [at.X, at.Y]);
    }

    /// <summary>
    /// O Vigia "guarda" o Eco: o slot <paramref name="slot"/> (1 = o primeiro) fica registrado na história e recebe o
    /// Eco que o caçador já absorveu (o Eco Viscoso da Lodo-Mãe, na tecla Q), se estiver vazio.
    /// </summary>
    private void UnlockEchoSlot(int slot)
    {
        var isNew = StoryService.SetFlag(EchoSlotFlag(slot));
        if (!Hunter.Progression.IsActive)
        {
            return;
        }

        var engine = Hunter.Progression.Engine;
        var index = slot - 1;
        if (index < 0 || index >= engine.EchoSlotCount)
        {
            return;
        }

        var echoes = Hunter.Progression.State.Echoes;
        var pick = engine.EchoIn(index)
            ?? (echoes.Contains("eco_viscoso") ? "eco_viscoso" : echoes.FirstOrDefault(id => engine.EchoIn(1 - index) != id));
        if (pick is not null && engine.EchoIn(index) is null)
        {
            engine.EquipEcho(index, pick);
        }

        if (isNew)
        {
            var name = pick is not null && engine.Catalog.Echoes.TryGetValue(pick, out var echo) ? echo.Name : null;
            CombatHud.Toast(name is null ? $"Slot de Eco {slot} liberado" : $"Slot de Eco {slot}: {name}");
        }

        GameSession.Autosave();
    }
}
