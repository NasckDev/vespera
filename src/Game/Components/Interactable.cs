using Godot;
using Vespera.Game.Actors;

namespace Vespera.Game.Components;

/// <summary>
/// Área de interação (círculo no chão). Avisa o jogador quando ele entra e sai do alcance; quem trata o
/// "apertou interagir" é a fase, pela <see cref="Kind"/>. O texto do aviso pode mudar (lampião aceso/apagado).
/// </summary>
public partial class Interactable : Area2D
{
    private readonly float _radius;

    public Interactable(string kind, string prompt, float radius)
    {
        Kind = kind;
        Prompt = prompt;
        _radius = radius;
    }

    public string Kind { get; }

    public string Prompt { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Ponto (no mundo) acima do qual o aviso aparece.</summary>
    public Vector2 PromptAnchor { get; set; }

    /// <summary>Objeto dono (lampião, mural...), se houver.</summary>
    public Vespera.Game.World.Prop? OwnerProp { get; set; }

    public override void _Ready()
    {
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = Player.PlayerLayer;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = _radius } });
        BodyEntered += body => (body as Player)?.EnterRange(this);
        BodyExited += body => (body as Player)?.ExitRange(this);
    }
}
