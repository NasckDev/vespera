using Godot;
using Vespera.Autoload;

namespace Vespera.World;

/// <summary>
/// Qualquer coisa do mapa com que o jogador pode interagir (NPC, porta, placa, portal, forja…).
/// O Player detecta o Interactable mais próximo e chama <see cref="Interact"/> na ação <c>interact</c>.
/// Fica na camada de física 6 ("interactables").
/// Com <see cref="Kind"/> preenchido, interagir também emite <see cref="EventBus.InteractionRequested"/> (kind, id),
/// que é por onde lojas, diálogos e portais se ligam.
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

    /// <summary>Tipo do pedido emitido no EventBus (na v2, o nome da ação: <c>talkClerk</c>, <c>portalDialog</c>…).</summary>
    [Export]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Id do interagível (ex.: <c>odete</c>, <c>portal0</c>).</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>Raio de interação em u. Se maior que zero e não houver forma filha, cria um círculo.</summary>
    [Export]
    public float Radius { get; set; }

    /// <summary>Altura (u) acima do ponto em que o prompt com ícone aparece (v2: <c>y − 44</c>, <c>render.js:228</c>).</summary>
    [Export]
    public float PromptHeight { get; set; } = 44f;

    public override void _Ready()
    {
        CollisionLayer = InteractablesLayer;
        CollisionMask = 0;
        Monitoring = false;

        if (Radius > 0 && GetChildCount() == 0)
        {
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        }
    }

    /// <summary>Sobrescreva para comportamento próprio; a base emite <see cref="Interacted"/> e o pedido no EventBus.</summary>
    public virtual void Interact(Node2D interactor)
    {
        if (!Enabled)
        {
            return;
        }

        EmitSignal(SignalName.Interacted, interactor);
        if (Kind.Length > 0)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InteractionRequested, Kind, Id);
        }
    }
}
