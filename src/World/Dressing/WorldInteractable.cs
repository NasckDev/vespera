using System;
using Godot;

namespace Vespera.World;

/// <summary>
/// Interagível de um objeto do layout (baú, lampião, flor, balsa, pesca, placa…): em vez de pedir no EventBus, chama
/// <see cref="Handler"/> direto no objeto dono. Fica na mesma camada de física dos outros interagíveis, então o jogador
/// escolhe o mais próximo entre todos.
/// </summary>
public partial class WorldInteractable : Interactable
{
    /// <summary>O que fazer ao interagir (o objeto dono decide).</summary>
    public Action<Node2D>? Handler { get; set; }

    public override void Interact(Node2D interactor)
    {
        if (!Enabled)
        {
            return;
        }

        EmitSignal(Interactable.SignalName.Interacted, interactor);
        Handler?.Invoke(interactor);
    }
}
