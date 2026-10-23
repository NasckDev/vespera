using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Story;

namespace Vespera.Game.World;

/// <summary>NPCs da fase: entram conforme as condições da história e ficam no y-sort com os demais atores.</summary>
public partial class LevelWorld
{
    /// <summary>Liga/desliga os NPCs como obstáculos e patrulheiros (o bot de colisão varre a fase sem eles).</summary>
    public void SetNpcsActive(bool active)
    {
        foreach (var npc in _npcs)
        {
            npc.SetActive(active);
        }
    }

    private void SpawnNpcs()
    {
        foreach (var placement in _data.Npcs)
        {
            if (!NpcSprites.Exists(placement.Id) || !StoryConditions.Evaluate(placement.Conditions, StoryService.Engine))
            {
                continue;
            }

            var npc = new Npc(placement.Id, placement, _data.HumanHeight)
            {
                Name = $"Npc_{placement.Id}_{_npcs.Count}",
                Position = new Vector2(placement.At[0], placement.At[1]),
            };
            Entities.AddChild(npc);
            _npcs.Add(npc);
        }
    }
}
