using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Varal (Ato VI, rank S): um lençol esticado entre duas varas que anda. Verbo: <b>enlaça</b> quem passa embaixo.
/// Aviso (CAMPANHA, Ato VI): o lençol infla como vela e a borda brilha âmbar enquanto um círculo âmbar marca o espaço à
/// frente (0,6 s); o lençol fecha ali e puxa quem ficou dentro para baixo dele. Depois fica um instante desinflado.
/// </summary>
public partial class Varal(LevelWorld world, float sizeFactor = 1f) : Enemy("varal", world, sizeFactor)
{
    private const float SnareReach = 70f;
    private const float SnareRadius = 90f;
    private const float AttackRange = 160f;
    private const float PullForce = -360f;
    private const float AttackSeconds = 0.4f;
    private const float RecoverSeconds = 0.8f;
    private const float StaggerSeconds = 0.25f;
    private static readonly Color Linen = new("f4e7c6");

    private float _cooldown;
    private Vector2 _snareAt;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = 0.8f + StartDelay;
        Play("mover");
    }

    protected override Vector2 Think(float dt)
    {
        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Snare();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                if (StateTime >= AttackSeconds)
                {
                    Enter(EnemyState.Recover);
                    Play("parado");
                }

                return Vector2.Zero;
            case EnemyState.Recover:
            case EnemyState.Hurt:
                if (StateTime >= (State == EnemyState.Hurt ? StaggerSeconds : RecoverSeconds))
                {
                    Enter(EnemyState.Approach);
                    Play("mover");
                }

                return Vector2.Zero;
        }

        var target = Target;
        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && to.Length() <= AttackRange * A * SizeFactor)
        {
            StartSnare(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > SnareReach * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartSnare(Vector2 dir)
    {
        _cooldown = (float)Period;
        _snareAt = World.ClampToFloor(GlobalPosition + (dir * SnareReach * A * SizeFactor));
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnCircle(_snareAt, SnareRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Snare()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -6f);
        World.Feel.Spark(_snareAt, Linen, 12, 200f * A, Vector2.Down, 60f);
        // Força negativa: quem estava no círculo é arrastado para baixo do lençol.
        StrikeCircle(_snareAt, SnareRadius * A * SizeFactor, 1, PullForce);
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State is EnemyState.Attack or EnemyState.Telegraph)
        {
            return;
        }

        Enter(EnemyState.Hurt);
    }

    protected override void OnDied()
    {
        _marker?.Release();
        _marker = null;
    }
}
