using System;
using Godot;
using Vespera.World;

namespace Vespera.Items;

/// <summary>
/// Interagível do combate (Eco, Lanternas, baú, portal de saída): ao apertar [E] chama <see cref="Callback"/> em vez de
/// emitir <c>InteractionRequested</c> (essas ações são da própria frente de combate, não da Interface).
/// </summary>
public partial class CallbackInteractable : Interactable
{
    public Action? Callback { get; set; }

    public override void Interact(Node2D interactor)
    {
        if (Enabled)
        {
            Callback?.Invoke();
        }
    }
}
