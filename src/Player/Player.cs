using Godot;
using Vespera.Autoload;
using Vespera.World;

namespace Vespera.Player;

/// <summary>
/// Caçador controlado pelo jogador: movimento em 8 direções e interação com o Interactable mais próximo.
/// A state machine (Idle, Move, Attack, Dodge, Hurt, Dead) entra com o combate.
/// </summary>
public partial class Player : CharacterBody2D
{
    [Export]
    public float MoveSpeed { get; set; } = 90f;

    [Export]
    public Camera2D Camera { get; set; } = null!;

    /// <summary>Área que detecta Interactables (máscara na camada 6).</summary>
    [Export]
    public Area2D InteractionArea { get; set; } = null!;

    /// <summary>Última direção de movimento; usada para animação e direção do ataque.</summary>
    public Vector2 Facing { get; private set; } = Vector2.Down;

    private Interactable? _focused;

    public override void _Ready()
    {
        InteractionArea.CollisionLayer = 0;
        InteractionArea.CollisionMask = Interactable.InteractablesLayer;
    }

    public override void _ExitTree() => SetFocused(null);

    public override void _PhysicsProcess(double delta)
    {
        if (GameManager.Instance.IsGameplayBlocked)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var direction = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        if (direction != Vector2.Zero)
        {
            Facing = direction;
        }

        Velocity = direction * MoveSpeed;
        MoveAndSlide();
        SetFocused(FindClosestInteractable());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_focused is not null && !GameManager.Instance.IsGameplayBlocked && @event.IsActionPressed(InputActions.Interact))
        {
            GetViewport().SetInputAsHandled();
            _focused.Interact(this);
        }
    }

    private Interactable? FindClosestInteractable()
    {
        Interactable? closest = null;
        var bestDistance = float.MaxValue;
        foreach (var area in InteractionArea.GetOverlappingAreas())
        {
            if (area is not Interactable { Enabled: true } candidate)
            {
                continue;
            }

            var distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void SetFocused(Interactable? interactable)
    {
        if (interactable == _focused)
        {
            return;
        }

        _focused = interactable;
        EventBus.Instance?.EmitSignal(EventBus.SignalName.InteractionPromptChanged, interactable?.Prompt ?? string.Empty);
    }
}
