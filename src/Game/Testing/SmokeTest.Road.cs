using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>O jogo novo começa na estrada (issue #40): o smoke sobe a escadaria para a Praça antes do roteiro de lá.</summary>
public partial class SmokeTest
{
    private async Task<Level> LeaveRoad(Level level)
    {
        if (level.World.Data.Id != Level.RoadLevelId)
        {
            return level;
        }

        // Os bots de combate (--ato1, --combat-bot) caem de propósito e esperam voltar à Praça, não ao quarto.
        Level.WakeInRoom = false;

        Check(level.World.Zones.Any(z => z.Target == Level.HubLevelId), "a estrada deveria ter a subida para a Praça");
        await Shot("08a_estrada", 0.4);
        return await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(0, 30 * level.World.Data.ActorScale), "Level_praca");
    }
}
