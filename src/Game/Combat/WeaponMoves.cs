using System.Collections.Generic;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Rift;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>
/// Golpes de uma arma (PLANO-V2 §7): o que cada botão faz, quando o golpe acerta (só nos quadros de impacto da folha)
/// e com que peso. A base cuida da animação de ataque, da mira (com leve ajuda para o alvo à frente), do avanço no
/// golpe e de resolver o acerto com pausa, tremor, faíscas e som.
/// </summary>
public abstract class WeaponMoves
{
    private const float AssistAngle = 0.9f;
    private const float AssistReach = 1.4f;
    private const float ChestHeight = 34f;

    /// <summary>Golpes com alcance acima disto (px na escala 1) são tiros, sem rastro de corte.</summary>
    private const float MeleeTrailMaxRange = 400f;

    private readonly HashSet<int> _framesDone = [];
    private float _lunge;

    protected WeaponMoves(HunterCombat combat, string weaponId)
    {
        C = combat;
        Set = RiftSession.Brecha.Moves[weaponId];
        var tuning = RiftSession.Brecha.Hunter;
        Combo = new ComboChain(Set.Light.Count, tuning.ComboBuffer, tuning.ComboWindow);
    }

    protected HunterCombat C { get; }

    protected MoveSet Set { get; }

    protected ComboChain Combo { get; }

    protected Player P => C.Player;

    protected IInputSource Input => P.Input;

    protected float S => C.Scale;

    /// <summary>Animação de ataque em curso (null = livre).</summary>
    protected string? CurrentAnim { get; private set; }

    protected string CurrentVerb { get; private set; } = string.Empty;

    protected Vector2 Aim { get; set; } = Vector2.Down;

    /// <summary>Último quadro de impacto já resolvido do golpe atual (-1 = nenhum).</summary>
    protected int LastImpact { get; private set; } = -1;

    public bool Busy => CurrentAnim is not null;

    public static WeaponMoves For(HunterCombat combat, HunterClass hunter) => hunter switch
    {
        HunterClass.Arco => new BowMoves(combat),
        HunterClass.Martelo => new HammerMoves(combat),
        _ => new BladeMoves(combat),
    };

    public abstract MotionOrder Tick(float dt, Vector2 move);

    public void OnFrame(string anim, int frame)
    {
        if (CurrentAnim is null || anim != CurrentAnim)
        {
            return;
        }

        foreach (var hit in HunterSprites.HitFrames(P.Hunter, CurrentVerb, P.Facing))
        {
            if (hit == frame && _framesDone.Add(frame))
            {
                LastImpact = frame;
                OnImpact(frame);
            }
        }

        // A recuperação começa logo depois do impacto: daí já dá para encadear o próximo golpe do combo.
        if (LastImpact >= 0 && frame == LastImpact + 1)
        {
            Combo.Finish(C.Clock);
        }

        OnAnimFrame(frame);
    }

    public void OnAnimationFinished(string anim)
    {
        if (CurrentAnim is not null && anim == CurrentAnim)
        {
            EndAttack();
        }
    }

    public virtual void Cancel()
    {
        CurrentAnim = null;
        Combo.Reset();
        _lunge = 0f;
        P.Flash.Tint = 0f;
        P.Sprite.SpeedScale = 1f;
    }

    protected abstract void OnImpact(int frame);

    protected virtual void OnAnimFrame(int frame)
    {
    }

    protected virtual void EndAttack()
    {
        CurrentAnim = null;
        Combo.Finish(C.Clock);
        P.Sprite.SpeedScale = 1f;
    }

    /// <summary>Começa a animação do golpe mirando pelo input (ou para onde olha), com ajuda de mira.</summary>
    protected void StartAttack(string verb, Vector2 move, float speedScale, float range)
    {
        var steering = move.LengthSquared() > 0.04f;
        var aim = steering ? move.Normalized() : P.AimDirection;
        if (MouseAim() is { } mouse)
        {
            // Mouse em uso: o golpe vai para o cursor (ajuda de mira só num cone estreito em volta dele).
            Aim = Assist(mouse, range, AssistAngle * 0.6f);
        }
        else
        {
            Aim = Assist(aim, range, steering ? AssistAngle : Mathf.Pi);
        }

        P.Face(Aim);
        CurrentVerb = verb;
        _framesDone.Clear();
        LastImpact = -1;
        CurrentAnim = P.PlayAnimation(verb, restart: true);
        P.Sprite.SpeedScale = speedScale;
    }

    /// <summary>Direção do peito do caçador até o cursor, se o jogador humano está usando o mouse; senão null.</summary>
    protected Vector2? MouseAim()
    {
        if (P.Input is not MergedInput || !InputDevices.MouseRecently || Player.MouseWorld?.Invoke() is not { } cursor)
        {
            return null;
        }

        var to = cursor - (P.GlobalPosition - new Vector2(0, ChestHeight * S));
        return to.LengthSquared() < 1f ? null : to.Normalized();
    }

    /// <summary>A animação do golpe mudou de direção no meio (mira girando na carga do Arco).</summary>
    protected void RetargetAnimation() => CurrentAnim = P.Sprite.Animation;

    /// <summary>Pequeno avanço no impacto (px/s na escala 1), que decai.</summary>
    protected void Lunge(float speed) => _lunge = speed;

    /// <summary>Movimento padrão durante um golpe: quase parado mais o avanço.</summary>
    protected MotionOrder AttackMotion(float dt, float moveScale)
    {
        _lunge = Mathf.MoveToward(_lunge, 0f, 2400f * dt);
        return new MotionOrder(moveScale, _lunge > 1f ? Aim * _lunge * S : null, true);
    }

    protected Vector2 Chest => P.GlobalPosition - new Vector2(0, ChestHeight * S);

    protected double BaseDamage(double mult) => C.Stats.Atk * mult * (RiftSession.Cheat ? 10 : 1);

    /// <summary>Resolve um golpe em setor (arc &gt; 0) ou círculo (arc 0) e aplica o peso. Devolve quantos acertou.</summary>
    protected int Strike(Vector2 origin, Vector2 dir, float range, float arc, double mult, float knockback, double hitStop, bool heavy)
    {
        var world = P.World;
        if (world is null)
        {
            return 0;
        }

        var landed = 0;
        var killed = false;
        var look = WeaponLook.Current;
        if (arc > 0f && range < MeleeTrailMaxRange * S)
        {
            // Rastro do corte na cor do nível da arma (aço → âmbar → âmbar vivo).
            world.AddFx(new SlashTrail(origin, dir, range * 0.92f, arc, range * (heavy ? 0.34f : 0.26f), look));
        }

        foreach (var target in world.Combat.InSector(origin, dir, range, arc, Team.Hunters))
        {
            var push = (target.HitCenter - origin).Normalized() * knockback * S;
            var info = new HitInfo(BaseDamage(mult), origin, push, hitStop, Set.PartMult, heavy, C.Stats.Crit);
            var outcome = target.ReceiveHit(info);
            if (!outcome.Landed)
            {
                continue;
            }

            landed++;
            killed |= outcome.Killed;
            var sparkDir = (target.HitCenter - origin).Normalized();
            world.Feel.Spark(target.HitCenter - (sparkDir * target.HitRadius * 0.5f), heavy ? look.Spark.Lightened(0.1f) : look.Spark,
                (heavy ? 16 : 9) + look.SparkBonus, (heavy ? 420f : 300f) * look.TrailScale, sparkDir, 70f + (look.SparkBonus * 4f));
        }

        if (landed > 0)
        {
            world.Feel.HitStop(RiftRules.HitStop(hitStop, killed));
            world.Feel.Shake(heavy ? 0.42f : 0.2f);
            CombatSfx.Play(P, heavy ? CombatSound.HitHeavy : CombatSound.Hit, Vector2.Zero);
        }

        return landed;
    }

    /// <summary>
    /// Puxa a mira para o alvo vivo mais próximo dentro de um cone à frente (parado, sem direção no controle, vale
    /// qualquer lado: o golpe vira para o monstro mais perto).
    /// </summary>
    private Vector2 Assist(Vector2 aim, float range, float cone)
    {
        var world = P.World;
        if (world is null || range <= 0f)
        {
            return aim;
        }

        Vector2? best = null;
        var bestScore = float.MaxValue;
        foreach (var target in world.Combat.Targets)
        {
            if (target.Team == Team.Hunters || !target.CanBeHit || target is World.Breakable)
            {
                continue;
            }

            var v = target.HitCenter - Chest;
            var d = v.Length();
            var angle = Mathf.Abs(v.AngleTo(aim));
            if (d > range * AssistReach || angle > cone)
            {
                continue;
            }

            var score = d * (1f + angle);
            if (score < bestScore)
            {
                bestScore = score;
                best = v.Normalized();
            }
        }

        return best ?? aim;
    }
}
