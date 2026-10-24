using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Afogado (Ato III, rank C): marinheiro de escafandro, lento e pesado. Verbo: <b>avança</b> e dá uma pancada dupla
/// à frente. Aviso (CAMPANHA, Ato III): para de pingar, ergue os braços e o capacete brilha âmbar enquanto um círculo
/// âmbar marca onde os punhos vão cair (0,7 s); quem sai do círculo não leva o golpe. Depois fica parado um instante.
/// </summary>
public partial class Afogado(LevelWorld world, float sizeFactor = 1f) : Enemy("afogado", world, sizeFactor)
{
    private const float SlamReach = 80f;
    private const float SlamRadius = 95f;
    private const float AttackRange = 170f;
    private const float AttackSeconds = 0.45f;
    private const float RecoverSeconds = 0.9f;
    private const float StaggerSeconds = 0.25f;
    private static readonly Color Water = new("3c7a8a");

    private float _cooldown;
    private Vector2 _slamAt;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = 1.0f + StartDelay;
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
                    Slam();
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
            StartSlam(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > SlamReach * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartSlam(Vector2 dir)
    {
        _cooldown = (float)Period;
        _slamAt = World.ClampToFloor(GlobalPosition + (dir * SlamReach * A * SizeFactor));
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnCircle(_slamAt, SlamRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Slam()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -4f);
        World.Feel.Spark(_slamAt, Water, 14, 260f * A, Vector2.Up, 70f);
        StrikeCircle(_slamAt, SlamRadius * A * SizeFactor, 1, 280f);
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
