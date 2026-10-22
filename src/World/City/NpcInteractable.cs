using Godot;

namespace Vespera.World.City;

/// <summary>Ponto de conversa do NPC: interagir chama <see cref="TownNpc.Talk"/> (não usa <c>InteractionRequested</c>).</summary>
public partial class NpcInteractable : Interactable
{
    public TownNpc Npc { get; set; } = null!;

    public override void Interact(Node2D interactor)
    {
        if (Enabled)
        {
            Npc.Talk(interactor);
        }
    }
}
