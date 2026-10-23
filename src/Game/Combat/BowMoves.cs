using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.Combat;

/// <summary>
/// Arco (PLANO-V2 §7): tocar o ataque solta uma flecha; segurar (ou o pesado) puxa a corda e carrega — cheia, a flecha
/// de luz atravessa todos no caminho. A flecha sai no quadro em que a corda solta. Enquanto puxa, o caçador fica
/// parado e só gira a mira; a flecha comum deixa andar devagar (recuar atirando).
/// </summary>
public sealed class BowMoves(HunterCombat combat) : WeaponMoves(combat, "bow")
{
    private const string Shot = "tiro";
    private const string Charged = "tiro_carregado";
    private const float HoldToCharge = 0.18f;
    private const float ArrowSpeed = 1500f;
    private const float ChargedSpeed = 2100f;
    private const float KiteMoveScale = 0.55f;

    private float _held = -1f;
    private bool _charging;
    private bool _full;
    private float _charge;

    public override MotionOrder Tick(float dt, Vector2 move)
    {
        if (_charging)
        {
            return TickCharge(dt, move);
        }

        if (!Busy && (Input.IsJustPressed(InputActions.Heavy) || (_held >= 0f && _held + dt >= HoldToCharge && Input.IsPressed(InputActions.Attack))))
        {
            _held = -1f;
            BeginCharge(move);
            return TickCharge(dt, move);
        }

        if (Input.IsJustPressed(InputActions.Attack))
        {
            _held = 0f;
        }
        else if (_held >= 0f)
        {
            _held += dt;
            if (!Input.IsPressed(InputActions.Attack))
            {
                _held = -1f;
                Combo.Press(C.Clock);
            }
        }

        var ready = !Busy || (LastImpact >= 0 && P.Sprite.Frame > LastImpact);
        if (Combo.TryStart(C.Clock, ready, out _))
        {
            StartAttack(Shot, move, 1.15f, (float)Set.LightRange[0] * S);
            CombatSfx.Play(P, CombatSound.BowDraw, Vector2.Zero, -8f);
        }

        // Identidade do Arco (PLANO §7: "longo, kiting"): dá para andar devagar enquanto atira a flecha comum.
        return Busy ? AttackMotion(dt, KiteMoveScale) : MotionOrder.Free;
    }

    public override void Cancel()
    {
        base.Cancel();
        _charging = false;
        _held = -1f;
    }

    protected override void OnImpact(int frame)
    {
        var charged = CurrentVerb == Charged;
        var pierce = charged && _full;
        var mult = pierce ? Set.Heavy : Set.Light[0] * (charged ? 1.3 : 1);
        var range = (float)(pierce ? Set.HeavyRange : Set.LightRange[0]) * S;
        var speed = (pierce ? ChargedSpeed : ArrowSpeed) * S;
        var info = new HitInfo(BaseDamage(mult), Chest, Aim * (float)(pierce ? Set.HeavyKnockback : Set.LightKnockback[0]) * S,
            pierce ? Set.HeavyHitStop : Set.LightHitStop[0], PartMult, pierce, Crit);
        var start = Chest + (Aim * 26f * S);
        P.World?.AddProjectile(new Arrow(info, Aim, speed, range, pierce, S) { Position = start });
        CombatSfx.Play(P, pierce ? CombatSound.BowCharged : CombatSound.BowShot, Vector2.Zero);
        _full = false;
    }

    private void BeginCharge(Vector2 move)
    {
        Combo.Reset();
        StartAttack(Charged, move, 1f, (float)Set.HeavyRange * S);
        _charging = true;
        _full = false;
        _charge = 0f;
        CombatSfx.Play(P, CombatSound.BowDraw, Vector2.Zero);
    }

    private MotionOrder TickCharge(float dt, Vector2 move)
    {
        _charge += dt;
        if (P.Sprite.Frame >= HunterSprites.BowHoldFrame)
        {
            P.Sprite.Frame = HunterSprites.BowHoldFrame;
            P.Sprite.SpeedScale = 0f;
        }

        var mouse = MouseAim();
        if (mouse is not null || move.LengthSquared() > 0.04f)
        {
            Aim = mouse ?? move.Normalized();
            P.Face(Aim);
            var anim = HunterSprites.Resolve(P.Sprite.SpriteFrames, Charged, P.Facing);
            if (anim != P.Sprite.Animation)
            {
                var frame = P.Sprite.Frame;
                P.PlayAnimation(Charged, restart: true);
                P.Sprite.Frame = frame;
                P.Sprite.SpeedScale = 0f;
                RetargetAnimation();
            }
        }

        if (!_full && _charge >= Set.HeavyCharge)
        {
            _full = true;
            P.Flash.Pulse(new Color(1f, 0.9f, 0.55f), 0.04f, 0.15f, 0.7f);
            P.World?.Feel.Spark(Chest + (Aim * 20f * S), new Color("ffd27a"), 10, 180f);
            CombatSfx.Play(P, CombatSound.Telegraph, Vector2.Zero, -4f);
        }

        P.Flash.Tint = Mathf.Clamp(_charge / (float)Set.HeavyCharge, 0f, 1f) * 0.35f;
        var holding = Input.IsPressed(InputActions.Attack) || Input.IsPressed(InputActions.Heavy);
        if (!holding)
        {
            _charging = false;
            P.Flash.Tint = 0f;
            P.Sprite.SpeedScale = 1f;
            P.Sprite.Play();
        }

        return new MotionOrder(0f, null, true);
    }
}
