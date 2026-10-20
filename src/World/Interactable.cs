using Godot;

namespace Vespera.World;

/// <summary>
/// Qualquer coisa do mapa com que o jogador pode interagir (NPC, porta, placa, portal, forja…).
/// O Player detecta o Interactable mais próximo e chama <see cref="Interact"/> na ação <c>interact</c>.
/// Fica na camada de física 6 ("interactables").
/// </summary>
[GlobalClass]
public partial class Interactable : Area2D
{
    public const uint InteractablesLayer = 1u << 5;

    [Signal]
    public delegate void InteractedEventHandler(Node2D interactor);

    /// <summary>Texto curto mostrado no HUD, ex.: "Falar", "Entrar", "Ler".</summary>
    [Export]
    public string Prompt { get; set; } = "Interagir";

    [Export]
    public bool Enabled { get; set; } = true;

    public override void _Ready()
    {
        CollisionLayer = InteractablesLayer;
        CollisionMask = 0;
        Monitoring = false;
    }

    /// <summary>Sobrescreva para comportamento próprio; a base só emite <see cref="Interacted"/>.</summary>
    public virtual void Interact(Node2D interactor)
    {
        if (Enabled)
        {
            EmitSignal(SignalName.Interacted, interactor);
        }
    }
}
