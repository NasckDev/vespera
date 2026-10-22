using Godot;
using Vespera.Game.Actors;

namespace Vespera.Game.World;

/// <summary>
/// Câmera que segue o jogador com suavização e olha um pouco à frente do movimento; presa aos limites da fase.
/// O alvo fica no peito do caçador (não no pé), para o enquadramento não cortar a cabeça perto das bordas.
/// </summary>
public partial class CameraRig : Camera2D
{
    private const float LookAhead = 0.45f;
    private const float MaxLead = 140f;
    private const float LeadSmoothing = 3.5f;
    private const float ChestHeight = 60f;

    private readonly Player _target;
    private readonly float _actorScale;
    private Vector2 _lead;

    public CameraRig(Player target, Rect2 bounds, float actorScale)
    {
        _target = target;
        _actorScale = actorScale;
        ProcessCallback = Camera2DProcessCallback.Physics;
        LimitLeft = (int)bounds.Position.X;
        LimitTop = (int)bounds.Position.Y;
        LimitRight = (int)bounds.End.X;
        LimitBottom = (int)bounds.End.Y;
    }

    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 6f;
        LimitSmoothed = true;
        ProcessCallback = Camera2DProcessCallback.Physics;
        Snap();
    }

    /// <summary>Encaixa a câmera no jogador sem suavizar (início da fase, teleporte).</summary>
    public void Snap()
    {
        _lead = Vector2.Zero;
        GlobalPosition = Focus();
        ResetSmoothing();
    }

    /// <summary>Ponto fixo para a câmera (capturas de revisão); null = segue o jogador.</summary>
    public Vector2? FixedFocus { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        if (FixedFocus is { } focus)
        {
            GlobalPosition = focus;
            return;
        }

        var wanted = (_target.Velocity * LookAhead).LimitLength(MaxLead * _actorScale);
        _lead = _lead.Lerp(wanted, Mathf.Min(1f, LeadSmoothing * (float)delta));
        GlobalPosition = Focus() + _lead;
    }

    private Vector2 Focus() => _target.GlobalPosition + new Vector2(0, -ChestHeight * _actorScale);
}
