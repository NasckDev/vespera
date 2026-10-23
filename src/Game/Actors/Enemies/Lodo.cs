using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Lodo (GDD §4): restos de comida e louça. Verbo: <b>pula</b> em direção ao caçador a cada 1,2 s. Antes do pulo
/// encolhe com o aviso (âmbar); no ar segue um arco e só machuca ao pousar (respingo em volta). Leva golpe e
/// tropeça quando está no chão; no ar está comprometido com o pulo.
/// </summary>
public partial class Lodo(LevelWorld world, float sizeFactor = 1f) : Enemy("lodo", world, sizeFactor)
{
    private const float JumpSeconds = 0.6f;
    private const float HopHeight = 46f;
    private const float StaggerSeconds = 0.28f;
    private const float WakeDistance = 900f;
    private const float RecoverSeconds = 0.15f;

    private Vector2 _jumpVelocity;
    private float _cooldown;

    /// <summary>Atraso inicial (os Lodos de uma onda não pulam juntos).</summary>
    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = StartDelay + 0.4f;
        Enter(EnemyState.Idle);
    }

    protected override Vector2 Think(float dt)
    {
        var target = Target;
        switch (State)
        {
            case EnemyState.Idle:
                _cooldown -= dt;
                if (target is not null)
                {
                    FaceTowards(target.GlobalPosition);
                }

                if (_cooldown <= 0f && target is not null && target.GlobalPosition.DistanceTo(GlobalPosition) < WakeDistance * A)
                {
                    Enter(EnemyState.Telegraph);
                    Play("aviso");
                    Flash.Tint = 0.4f;
                }

                return Vector2.Zero;
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    StartJump(target);
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                return TickJump();
            case EnemyState.Hurt:
                if (StateTime >= StaggerSeconds)
                {
                    Enter(EnemyState.Idle);
                    Play("parado");
                }

                return Vector2.Zero;
            default:
                if (StateTime >= RecoverSeconds)
                {
                    Enter(EnemyState.Idle);
                    Play("parado");
                }

                return Vector2.Zero;
        }
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State == EnemyState.Attack)
        {
            return;
        }

        Flash.Tint = 0f;
        Enter(EnemyState.Hurt);
        Play("dano");
        _cooldown = (float)Period * 0.6f;
    }

    private void StartJump(Player? target)
    {
        var to = target?.GlobalPosition ?? GlobalPosition;
        var maxHop = (float)Speed * JumpSeconds * A * SizeFactor;
        var delta = (to - GlobalPosition).LimitLength(maxHop);
        _jumpVelocity = delta / JumpSeconds;
        FaceTowards(to);
        Enter(EnemyState.Attack);
        Play("pulo");
        CombatSfx.Play(this, CombatSound.LodoHop, Vector2.Zero, -4f);
    }

    private Vector2 TickJump()
    {
        var t = Mathf.Clamp(StateTime / JumpSeconds, 0f, 1f);
        Sprite.Position = new Vector2(0, -Mathf.Sin(Mathf.Pi * t) * HopHeight * A * SizeFactor);
        if (t < 1f)
        {
            return _jumpVelocity;
        }

        Sprite.Position = Vector2.Zero;
        Land();
        return Vector2.Zero;
    }

    private void Land()
    {
        var radius = (float)Def.ContactRadius * A * SizeFactor;
        TouchPlayer(GlobalPosition - new Vector2(0, radius * 0.4f), radius, 1, 240f);
        CombatSfx.Play(this, CombatSound.LodoLand, Vector2.Zero, SizeFactor > 1.2f ? 2f : -3f);
        World.Feel.Spark(GlobalPosition, new Color("c06a3a"), (int)(6 * SizeFactor), 140f * SizeFactor, Vector2.Up, 120f);
        if (SizeFactor > 1.2f)
        {
            World.Feel.Shake(0.15f);
        }

        _cooldown = Mathf.Max(0.1f, (float)(Period - Def.Telegraph) - JumpSeconds - RecoverSeconds);
        Enter(EnemyState.Recover);
    }
}
