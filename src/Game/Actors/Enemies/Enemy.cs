using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Data;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

public enum EnemyState
{
    Idle,
    Approach,
    Telegraph,
    Attack,
    Recover,
    Hurt,
    Dead,
}

/// <summary>
/// Base dos monstros (GDD §4: um verbo cada): máquina de estados ocioso → aproximar → aviso → ataque → recuperar,
/// com dano (flash branco, recuo, número, barra de vida que só aparece ferido), morte com animação e Fragmentos.
/// Pé = origem; colide com a grade da sala pelo pé, exceto quem atravessa paredes (<see cref="PassesWalls"/>).
/// </summary>
public abstract partial class Enemy : CharacterBody2D, IHittable
{
    public const uint EnemyLayer = 4;

    private const float FadeSeconds = 0.6f;
    private const float SeparationStrength = 260f;

    private readonly DamageCalculator _damage = new(RiftSession.Db.Rules.Combat);
    private float _fade = -1f;

    protected Enemy(string monsterId, LevelWorld world, float sizeFactor = 1f)
    {
        MonsterId = monsterId;
        World = world;
        SizeFactor = sizeFactor;
        Stats = RiftSession.Rules.Stats(monsterId);
        MaxHp = Stats.Hp;
        Hp = MaxHp;
        Name = monsterId;
    }

    public string MonsterId { get; }

    public LevelWorld World { get; }

    public RiftMonsterStats Stats { get; }

    public RiftMonsterDef Def => Stats.Def;

    public float SizeFactor { get; }

    public double Hp { get; protected set; }

    public double MaxHp { get; protected set; }

    public EnemyState State { get; private set; } = EnemyState.Idle;

    public bool Dead => State == EnemyState.Dead;

    public Team Team => Team.Monsters;

    /// <summary>Altura do centro do corpo acima do pé (px do mundo).</summary>
    public virtual float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.45f;

    public virtual Vector2 HitCenter => GlobalPosition - new Vector2(0, BodyLift) + (Sprite?.Position ?? Vector2.Zero);

    public float HitRadius => (float)Def.HurtRadius * A * SizeFactor;

    public virtual bool CanBeHit => !Dead && Visible;

    /// <summary>Atravessa paredes e objetos (Espectro).</summary>
    public virtual bool PassesWalls => false;

    /// <summary>Não recua ao levar golpe (Guardião).</summary>
    public virtual bool Unstoppable => false;

    /// <summary>Fragmentos que caem ao morrer (0 nos filhos da Lodo-Mãe, por exemplo).</summary>
    public bool DropsLoot { get; set; } = true;

    public event Action<Enemy>? Died;

    public event Action<Enemy>? Damaged;

    /// <summary>Escala de personagens da sala (distâncias e velocidades na escala 1 × A).</summary>
    protected float A => World.Data.ActorScale;

    protected MonsterSheet Sheet { get; private set; } = null!;

    protected AnimatedSprite2D Sprite { get; private set; } = null!;

    protected HitFlash Flash { get; private set; } = null!;

    protected EnemyHealthBar Bar { get; private set; } = null!;

    protected float StateTime { get; private set; }

    protected Vector2 Knock { get; set; }

    protected Player? Target => World.Player is { Combat.IsDead: false } p ? p : null;

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = EnemyLayer;
        CollisionMask = PassesWalls ? 0 : Player.WorldLayer;
        var feet = (float)Def.HurtRadius * A * SizeFactor * 0.7f;
        AddChild(new CollisionShape2D
        {
            Shape = new CapsuleShape2D { Radius = feet * 0.45f, Height = feet * 2f },
            Rotation = Mathf.Pi / 2f,
            Position = new Vector2(0, -feet * 0.45f),
        });

        Sheet = MonsterSprites.Load(MonsterId, A, SizeFactor);
        Sprite = new AnimatedSprite2D { Name = "Sprite", SpriteFrames = Sheet.Frames, Offset = Sheet.Offset, Scale = Vector2.One * Sheet.SpriteScale };
        AddChild(Sprite);
        Flash = new HitFlash(Sprite);
        AddChild(ContactShadow.Make(HitRadius * 2.1f, 0.4f));
        Bar = new EnemyHealthBar(Mathf.Max(34f * A * 2f, HitRadius * 1.6f), Mathf.Max(3f, 6f * A))
        {
            Position = new Vector2(0, -(Sheet.StandHeight * Sheet.SpriteScale) - (14f * A)),
        };
        AddChild(Bar);
        World.Combat.Register(this);
        Play("parado");
        OnSpawned();
    }

    public override void _ExitTree() => World.Combat.Unregister(this);

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        StateTime += dt;
        Flash.Update(dt);
        if (Dead)
        {
            TickDeath(dt);
            return;
        }

        var desired = Think(dt);
        Knock = Knock.MoveToward(Vector2.Zero, 1500f * A * dt);
        Velocity = desired + Knock + Separation();
        MoveAndSlide();
        KeepInside();
    }

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!CanBeHit)
        {
            return HitOutcome.Miss;
        }

        var face = Sprite.FlipH ? -1 : 1;
        var side = DamageCalculator.HitSide(hit.From.X, GlobalPosition.X, face);
        var ctx = new MonsterHitContext(side, 0, hit.CritChance, null, [], []);
        var result = _damage.ToMonster(hit.Damage, ctx, RiftSession.Random);
        Hp = Math.Max(0, Hp - result.Damage);
        Flash.Pulse(Colors.White, 0.05f, 0.12f);
        Bar.Set((float)(Hp / MaxHp));
        World.Feel.Number(HitCenter - new Vector2(0, HitRadius * 0.6f), result.Damage.ToString(),
            result.Crit ? new Color("f2a541") : new Color("f4e7c6"), result.Crit ? 1.35f : hit.Heavy ? 1.15f : 1f);
        if (!Unstoppable)
        {
            Knock = hit.Push;
        }

        Damaged?.Invoke(this);
        if (Hp <= 0)
        {
            Die();
            return new HitOutcome(result.Damage, true, result.Crit, true);
        }

        OnHurt(hit);
        return new HitOutcome(result.Damage, false, result.Crit, true);
    }

    /// <summary>Vida cheia nova (divisões da Lodo-Mãe herdam a vida dela).</summary>
    public void SetHealth(double hp)
    {
        MaxHp = hp;
        Hp = hp;
    }

    /// <summary>Morte: anima, solta Fragmentos, avisa a sala e some.</summary>
    public void Die()
    {
        if (Dead)
        {
            return;
        }

        Enter(EnemyState.Dead);
        World.Combat.Unregister(this);
        CollisionLayer = 0;
        CollisionMask = 0;
        Bar.Conceal();
        Flash.Tint = 0f;
        Sprite.Position = Vector2.Zero;
        Play("morte");
        CombatSfx.Play(this, CombatSound.Kill, Vector2.Zero);
        World.Feel.Spark(HitCenter, new Color("e64a8e"), 14, 320f);
        if (DropsLoot)
        {
            World.DropFragments(GlobalPosition, RiftSession.Rules.RollFragments(MonsterId, RiftSession.Random));
        }

        OnDied();
        Died?.Invoke(this);
    }

    /// <summary>Velocidade desejada neste quadro (px/s do mundo).</summary>
    protected abstract Vector2 Think(float dt);

    protected virtual void OnSpawned()
    {
    }

    protected virtual void OnHurt(in HitInfo hit)
    {
    }

    protected virtual void OnDied()
    {
    }

    protected void Enter(EnemyState state)
    {
        State = state;
        StateTime = 0f;
    }

    protected void Play(string anim)
    {
        if (Sprite.SpriteFrames.HasAnimation(anim))
        {
            Sprite.Play(anim);
            Sprite.Frame = 0;
        }
    }

    /// <summary>Vira o sprite (desenhado olhando para a direita) para o ponto.</summary>
    protected void FaceTowards(Vector2 point)
    {
        if (Mathf.Abs(point.X - GlobalPosition.X) > 2f)
        {
            Sprite.FlipH = point.X < GlobalPosition.X;
        }
    }

    /// <summary>Acerta o caçador se ele estiver a <paramref name="radius"/> do centro do corpo.</summary>
    protected bool TouchPlayer(Vector2 center, float radius, double damageMult, float push)
    {
        var target = Target;
        if (target is null || !target.Combat.CanBeHit)
        {
            return false;
        }

        var c = target.Combat;
        if (c.HitCenter.DistanceTo(center) > radius + c.HitRadius)
        {
            return false;
        }

        var dir = (c.HitCenter - center).Normalized();
        var info = new HitInfo(Stats.Damage * damageMult, center, dir * push * A, 0.06, 1, false, 0);
        return c.ReceiveHit(info).Landed;
    }

    private void TickDeath(float dt)
    {
        if (Sprite.IsPlaying())
        {
            return;
        }

        if (_fade < 0f)
        {
            _fade = FadeSeconds;
        }

        _fade -= dt;
        Modulate = Modulate with { A = Mathf.Max(0f, _fade / FadeSeconds) };
        if (_fade <= 0f)
        {
            QueueFree();
        }
    }

    private Vector2 Separation()
    {
        var push = Vector2.Zero;
        foreach (var other in World.Enemies)
        {
            if (other == this || other.Dead)
            {
                continue;
            }

            var v = GlobalPosition - other.GlobalPosition;
            var min = (HitRadius + other.HitRadius) * 0.7f;
            var d = v.Length();
            if (d < min && d > 0.01f)
            {
                push += v / d * (1f - (d / min)) * SeparationStrength * A;
            }
        }

        return push;
    }

    /// <summary>Nunca sai da sala: preso aos limites (e, para quem atravessa paredes, ao retângulo jogável).</summary>
    private void KeepInside()
    {
        var b = World.PlayArea;
        var inset = HitRadius * 0.5f;
        var p = GlobalPosition;
        var clamped = new Vector2(Mathf.Clamp(p.X, b.Position.X + inset, b.End.X - inset), Mathf.Clamp(p.Y, b.Position.Y + inset, b.End.Y - inset));
        if (clamped != p)
        {
            GlobalPosition = clamped;
        }
    }
}
