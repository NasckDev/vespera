using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Rift;
using Vespera.Core.Rules;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>Ordem de movimento que o combate dá ao caçador neste quadro.</summary>
public readonly record struct MotionOrder(float SpeedScale, Vector2? Velocity, bool OwnsAnimation)
{
    public static MotionOrder Free => new(1f, null, false);
}

public enum HunterState
{
    Free,
    Dodging,
    Hurt,
    Swallowed,
    Dead,
}

/// <summary>
/// Combate do caçador (GDD §5.2): vida, fôlego, esquiva com invencibilidade, dano com recuo e piscada, morte e o
/// conjunto de golpes da arma (<see cref="WeaponMoves"/>). É o alvo dos monstros (<see cref="IHittable"/>).
/// Dirigido pelo <see cref="Player"/> uma vez por quadro de física, antes do movimento.
/// </summary>
public sealed partial class HunterCombat : Node, IHittable
{
    private const float BodyHeight = 62f;
    private const float BodyRadius = 30f;
    private const float BlinkRate = 22f;
    private const float AfterImageEvery = 0.05f;
    private const float DeathDelay = 2.4f;

    private readonly Player _player;
    private HunterTuning _tuning = new();
    private DamageCalculator _damage = null!;
    private StaminaMeter _stamina = null!;
    private float _stateTime;
    private float _invulnerable;
    private float _afterImageTimer;
    private Vector2 _dodgeVelocity;
    private Vector2 _knock;

    public HunterCombat(Player player)
    {
        _player = player;
        Name = "Combat";
    }

    public Team Team => Team.Hunters;

    public Vector2 HitCenter => _player.GlobalPosition - new Vector2(0, BodyHeight * _player.ActorScale * 0.5f);

    public float HitRadius => BodyRadius * _player.ActorScale;

    public bool CanBeHit => State is HunterState.Free or HunterState.Hurt && _invulnerable <= 0f && !Disabled;

    public HunterState State { get; private set; } = HunterState.Free;

    public WeaponMoves Moves { get; private set; } = null!;

    public int Hp { get; private set; }

    public int MaxHp { get; private set; } = 100;

    public PlayerStats Stats { get; private set; }

    public double StaminaFraction => _stamina.Fraction;

    /// <summary>Sem combate (bots de colisão, cutscene, painel aberto).</summary>
    public bool Disabled { get; set; }

    public bool IsDead => State == HunterState.Dead;

    /// <summary>Relógio do combate (segundos de jogo), usado pela fila do combo.</summary>
    public double Clock { get; private set; }

    public Player Player => _player;

    public float Scale => _player.ActorScale;

    /// <summary>Caiu: chamado depois da animação de morte (a fase decide a Lanterna e a volta).</summary>
    public event Action<HunterCombat>? Died;

    public override void _Ready()
    {
        _tuning = RiftSession.Brecha.Hunter;
        _damage = new DamageCalculator(RiftSession.Db.Rules.Combat);
        _stamina = new StaminaMeter(_tuning.StaminaMax, _tuning.StaminaRegen, _tuning.StaminaRegenDelay);
        RefreshStats(fullHeal: true);
        Moves = WeaponMoves.For(this, _player.Hunter);
        _player.Sprite.FrameChanged += () => Moves.OnFrame(_player.Sprite.Animation, _player.Sprite.Frame);
        _player.Sprite.AnimationFinished += () => Moves.OnAnimationFinished(_player.Sprite.Animation);
    }

    /// <summary>Recalcula HP máximo, ataque, defesa e crítico pela progressão (rank, arma forjada, equipamento).</summary>
    public void RefreshStats(bool fullHeal)
    {
        Stats = HunterStats.Compute(RiftSession.Db, RiftSession.Save, HunterSprites.WeaponId(_player.Hunter));
        MaxHp = Stats.Hp;
        Hp = fullHeal ? MaxHp : Math.Min(Hp, MaxHp);
    }

    /// <summary>Troca de classe (seletor): refaz o conjunto de golpes.</summary>
    public void ChangeWeapon()
    {
        Moves?.Cancel();
        Moves = WeaponMoves.For(this, _player.Hunter);
        RefreshStats(fullHeal: true);
    }

    public MotionOrder Step(float dt, Vector2 move)
    {
        Clock += dt;
        _stateTime += dt;
        _invulnerable = Mathf.Max(0f, _invulnerable - dt);
        _stamina.Tick(dt);
        _knock = _knock.MoveToward(Vector2.Zero, 1800f * Scale * dt);
        UpdateBlink();
        if (Disabled || _player.Frozen)
        {
            return MotionOrder.Free;
        }

        switch (State)
        {
            case HunterState.Dead:
            case HunterState.Swallowed:
                return new MotionOrder(0f, Vector2.Zero, true);
            case HunterState.Dodging:
                return StepDodge(dt);
            case HunterState.Hurt:
                if (_stateTime >= _tuning.HurtStun)
                {
                    State = HunterState.Free;
                }

                return new MotionOrder(0f, _knock, true);
        }

        var input = _player.Input;
        if (input.IsJustPressed(InputActions.Dodge) && TryDodge(move))
        {
            return StepDodge(dt);
        }

        var order = Moves.Tick(dt, move);
        return _knock.LengthSquared() > 1f ? order with { Velocity = (order.Velocity ?? Vector2.Zero) + _knock } : order;
    }

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!CanBeHit)
        {
            return HitOutcome.Miss;
        }

        var damage = RiftSession.Cheat ? 0 : _damage.ToPlayer(hit.Damage, Stats.Def, RiftSession.Random);
        Hp = Math.Max(0, Hp - damage);
        var feel = _player.Feel;
        feel?.Number(HitCenter, damage.ToString(), new Color("ff6b6b"), 1.1f);
        feel?.Shake(0.45f);
        feel?.HitStop(0.07);
        feel?.Spark(HitCenter, new Color("e85d5d"), 10, 260f);
        CombatSfx.Play(_player, CombatSound.Hurt, Vector2.Zero);
        _player.Flash.Pulse(new Color(1f, 0.35f, 0.35f), 0.05f, 0.16f, 0.7f);
        Moves.Cancel();
        _knock = hit.Push;
        _invulnerable = (float)_tuning.HurtInvulnerable;
        if (Hp <= 0)
        {
            Die();
            return new HitOutcome(damage, true, false, true);
        }

        Enter(HunterState.Hurt);
        _player.PlayAnimation("dano", restart: true);
        return new HitOutcome(damage, false, false, true);
    }

    /// <summary>Engolido pela Lodo-Mãe: some dentro dela até <see cref="Release"/>.</summary>
    public bool TrySwallow()
    {
        if (!CanBeHit || RiftSession.Cheat)
        {
            return false;
        }

        Moves.Cancel();
        Enter(HunterState.Swallowed);
        _player.Sprite.Visible = false;
        return true;
    }

    /// <summary>Dano direto enquanto engolido (sem recuo).</summary>
    public void Digest(int damage)
    {
        Hp = Math.Max(0, Hp - damage);
        _player.Feel?.Number(HitCenter, damage.ToString(), new Color("ff6b6b"), 0.9f);
        if (Hp <= 0)
        {
            _player.Sprite.Visible = true;
            Die();
        }
    }

    public void Release(Vector2 push)
    {
        if (State != HunterState.Swallowed)
        {
            return;
        }

        _player.Sprite.Visible = true;
        _knock = push;
        _invulnerable = (float)_tuning.HurtInvulnerable;
        Enter(HunterState.Hurt);
        _player.PlayAnimation("dano", restart: true);
    }

    /// <summary>Volta à vida cheia (respawn na Praça).</summary>
    public void Revive()
    {
        RefreshStats(fullHeal: true);
        _stamina.Refill();
        _player.Sprite.Visible = true;
        _player.Sprite.Modulate = Colors.White;
        _invulnerable = 1.2f;
        Enter(HunterState.Free);
    }

    /// <summary>Vida trazida da sala anterior.</summary>
    public void SetHp(int hp) => Hp = Math.Clamp(hp, 1, MaxHp);

    private bool TryDodge(Vector2 move)
    {
        if (!_stamina.TrySpend(_tuning.DodgeCost))
        {
            return false;
        }

        Moves.Cancel();
        var dir = move.LengthSquared() > 0.01f ? move.Normalized() : _player.AimDirection;
        _player.Face(dir);
        _dodgeVelocity = dir * (float)(_tuning.DodgeDistance * Scale / _tuning.DodgeSeconds);
        _invulnerable = Mathf.Max(_invulnerable, (float)_tuning.DodgeInvulnerable);
        Enter(HunterState.Dodging);
        _player.PlayAnimation("esquiva", restart: true, fitSeconds: (float)_tuning.DodgeSeconds);
        CombatSfx.Play(_player, CombatSound.Dodge, Vector2.Zero);
        _afterImageTimer = 0f;
        return true;
    }

    private MotionOrder StepDodge(float dt)
    {
        _afterImageTimer -= dt;
        if (_afterImageTimer <= 0f)
        {
            _afterImageTimer = AfterImageEvery;
            AfterImage.Spawn(_player);
        }

        var t = _stateTime / (float)_tuning.DodgeSeconds;
        if (t >= 1f)
        {
            Enter(HunterState.Free);
            return new MotionOrder(1f, _dodgeVelocity * 0.25f, false);
        }

        var ease = 1.25f - (0.7f * t);
        return new MotionOrder(0f, _dodgeVelocity * ease, true);
    }

    private void Die()
    {
        Moves.Cancel();
        Enter(HunterState.Dead);
        _player.Sprite.Modulate = Colors.White;
        _player.PlayAnimation("morte", restart: true);
        _player.Feel?.SlowMotion(0.35f, 0.9);
        _player.Feel?.Shake(0.7f);
        CombatSfx.Play(_player, CombatSound.Fall, Vector2.Zero);
        var timer = GetTree().CreateTimer(DeathDelay, processAlways: false);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(this) && State == HunterState.Dead)
            {
                Died?.Invoke(this);
            }
        };
    }

    private void Enter(HunterState state)
    {
        State = state;
        _stateTime = 0f;
    }

    private void UpdateBlink()
    {
        var blinking = _invulnerable > 0f && State is HunterState.Free or HunterState.Hurt && _stateTime > 0.05f;
        var alpha = blinking && Mathf.Sin(_stateTime * BlinkRate) > 0f ? 0.45f : 1f;
        _player.Sprite.Modulate = _player.Sprite.Modulate with { A = alpha };
    }
}
