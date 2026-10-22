using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.Combat;

/// <summary>
/// Martelo (PLANO-V2 §7): dois golpes lentos e pesados (pausa no impacto forte) e o pesado carregado — ergue o martelo,
/// segura, e ao soltar crava no chão com uma onda de choque em volta. Quebra partes em dobro (partMult 2).
/// </summary>
public sealed class HammerMoves(HunterCombat combat) : WeaponMoves(combat, "hammer")
{
    private const string Slam = "pesado";
    private const int RaisedFrame = 4;
    private const float SwingSpeed = 0.82f;
    private const float LungeSpeed = 200f;

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

        if (Input.IsJustPressed(InputActions.Heavy) && !Busy)
        {
            Combo.Reset();
            StartAttack(Slam, move, 1.1f, (float)Set.HeavyRange * S);
            _charging = true;
            _released = false;
            _charge = 0f;
            CombatSfx.Play(P, CombatSound.HammerSwing, Vector2.Zero, -6f);
            return TickCharge(dt, move);
        }

        var ready = !Busy || (CurrentVerb != Slam && LastImpact >= 0 && P.Sprite.Frame > LastImpact);
        if (Combo.TryStart(C.Clock, ready, out var step))
        {
            StartAttack($"ataque{step + 1}", move, SwingSpeed, (float)Set.LightRange[step] * S);
            CombatSfx.Play(P, CombatSound.HammerSwing, Vector2.Zero);
        }

        return Busy ? AttackMotion(dt, 0f) : MotionOrder.Free;
    }

    public override void Cancel()
    {
        base.Cancel();
        _charging = false;
    }

    protected override void OnImpact(int frame)
    {
        var world = P.World;
        if (CurrentVerb == Slam)
        {
            var center = P.GlobalPosition + (Aim * 30f * S);
            Strike(center - new Vector2(0, 20f * S), Aim, (float)Set.HeavyRange * S, 0f, Set.Heavy, (float)Set.HeavyKnockback, Set.HeavyHitStop, heavy: true);
            world?.Feel.Ring(center, (float)Set.HeavyRange * S, new Color("ffb347"), 0.45f, 8f);
            world?.Feel.Ring(center, (float)Set.HeavyRange * 0.6f * S, new Color("fff1c4"), 0.3f, 5f);
            world?.Feel.Spark(center, new Color("c9a27a"), 22, 380f, Vector2.Up, 160f);
            world?.Feel.Shake(0.75f);
            CombatSfx.Play(P, CombatSound.Slam, Vector2.Zero);
            return;
        }

        var step = CurrentVerb[^1] - '1';
        Lunge(LungeSpeed);
        Strike(Chest, Aim, (float)Set.LightRange[step] * S, (float)Set.LightArc[step], Set.Light[step],
            (float)Set.LightKnockback[step], Set.LightHitStop[step], heavy: true);
        if (step == 0)
        {
            world?.Feel.Spark(P.GlobalPosition + (Aim * 50f * S), new Color("b89a7a"), 8, 220f, Vector2.Up, 120f);
        }
    }

    private MotionOrder TickCharge(float dt, Vector2 move)
    {
        _charge += dt;
        _released |= !Input.IsPressed(InputActions.Heavy);
        if (P.Sprite.Frame >= RaisedFrame && (!_released || _charge < Set.HeavyCharge))
        {
            P.Sprite.Frame = RaisedFrame;
            P.Sprite.SpeedScale = 0f;
        }

        P.Flash.Tint = Mathf.Clamp(_charge / (float)Set.HeavyCharge, 0f, 1f) * 0.45f;
        if (move.LengthSquared() > 0.04f)
        {
            Aim = move.Normalized();
            P.Face(Aim);
        }

        if (_released && _charge >= Set.HeavyCharge)
        {
            _charging = false;
            P.Flash.Tint = 0f;
            P.Sprite.SpeedScale = 1.1f;
            P.Sprite.Play();
            CombatSfx.Play(P, CombatSound.SwingHeavy, Vector2.Zero);
        }

        return new MotionOrder(0f, null, true);
    }
}
