using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.Combat;

/// <summary>
/// Lâmina (GDD §5.2): combo leve de 3 golpes com fila de entrada (o 3º corta mais fundo) e pesado carregado: segura
/// para concentrar (brilho âmbar crescendo), solta para o corte giratório que acerta em volta três vezes.
/// </summary>
public sealed class BladeMoves(HunterCombat combat) : WeaponMoves(combat, "sword")
{
    private const string Spin = "pesado";
    private const int WindupFrame = 1;
    private const int SpinStartFrame = 2;
    private const float ChargeMoveScale = 0f;
    private const float LungeSpeed = 260f;

    private bool _charging;
    private bool _released;
    private float _charge;

    public override MotionOrder Tick(float dt, Vector2 move)
    {
        if (Input.IsJustPressed(InputActions.Attack))
        {
            Combo.Press(C.Clock);
        }

        if (_charging)
        {
            return TickCharge(dt, move);
        }

        if (Input.IsJustPressed(InputActions.Heavy) && (!Busy || CurrentVerb != Spin))
        {
            BeginCharge(move);
            return TickCharge(dt, move);
        }

        var ready = !Busy || (CurrentVerb != Spin && LastImpact >= 0 && P.Sprite.Frame > LastImpact);
        if (Combo.TryStart(C.Clock, ready, out var step))
        {
            var range = (float)Set.LightRange[step] * S;
            StartAttack($"ataque{step + 1}", move, 1f, range);
            CombatSfx.Play(P, step == 2 ? CombatSound.SwingHeavy : CombatSound.Swing, Vector2.Zero);
        }

        return Busy ? AttackMotion(dt, 0f) : MotionOrder.Free;
    }

    public override void Cancel()
    {
        base.Cancel();
        _charging = false;
        _released = false;
    }

    protected override void OnImpact(int frame)
    {
        if (CurrentVerb == Spin)
        {
            Strike(Chest, Aim, (float)Set.HeavyRange * S, 0f, Set.Heavy, (float)Set.HeavyKnockback, Set.HeavyHitStop, heavy: true);
            P.World?.Feel.Ring(P.GlobalPosition, (float)Set.HeavyRange * S, new Color("ffb347"), 0.22f, 4f);
            return;
        }

        var step = CurrentVerb[^1] - '1';
        Lunge(LungeSpeed * (step == 2 ? 1.4f : 1f));
        Strike(Chest, Aim, (float)Set.LightRange[step] * S, (float)Set.LightArc[step], Set.Light[step],
            (float)Set.LightKnockback[step], Set.LightHitStop[step], heavy: step == 2);
    }

    private void BeginCharge(Vector2 move)
    {
        Combo.Reset();
        StartAttack(Spin, move, 0f, 0f);
        P.Sprite.Frame = WindupFrame;
        _charging = true;
        _released = false;
        _charge = 0f;
        CombatSfx.Play(P, CombatSound.BowDraw, Vector2.Zero, -6f);
    }

    private MotionOrder TickCharge(float dt, Vector2 move)
    {
        _charge += dt;
        _released |= !Input.IsPressed(InputActions.Heavy);
        var full = _charge >= Set.HeavyCharge;
        P.Flash.Tint = Mathf.Clamp(_charge / (float)Set.HeavyCharge, 0f, 1f) * 0.45f;
        if (move.LengthSquared() > 0.04f)
        {
            Aim = move.Normalized();
            P.Face(Aim);
        }

        if (full && _released)
        {
            _charging = false;
            P.Flash.Tint = 0f;
            P.Flash.Pulse(new Color(1f, 0.85f, 0.5f), 0.03f, 0.12f, 0.8f);
            P.Sprite.Frame = SpinStartFrame;
            P.Sprite.SpeedScale = 1f;
            CombatSfx.Play(P, CombatSound.Spin, Vector2.Zero);
            OnFrame(P.Sprite.Animation, SpinStartFrame);
            return AttackMotion(dt, 0f);
        }

        return new MotionOrder(ChargeMoveScale, null, false) with { OwnsAnimation = true };
    }
}
