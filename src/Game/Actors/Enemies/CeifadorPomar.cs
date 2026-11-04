using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Ceifador de Pomar (Ato VII): espantalho com podão. Verbo: <b>vigia</b> — só se move quando NÃO está no cone da
/// lanterna do caçador (para onde ele mira); iluminado, congela no meio do passo. Fora da luz anda rápido e, perto,
/// ergue o podão (âmbar) com um círculo âmbar à frente (aviso) e varre baixo. Virar a lanterna para ele é a defesa.
/// </summary>
public partial class CeifadorPomar(LevelWorld world, float sizeFactor = 1f) : Enemy("ceifador_pomar", world, sizeFactor)
{
    private const float LightRange = 520f;
    private const float SweepReach = 70f;
    private const float SweepRadius = 100f;
    private const float AttackRange = 150f;
    private const float AttackSeconds = 0.4f;
    private const float RecoverSeconds = 0.8f;
    private const float StaggerSeconds = 0.25f;
    private static readonly Color Straw = new("c9a35a");

    private float _cooldown;
    private Vector2 _sweepAt;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    /// <summary>Está congelado pela lanterna agora (capturas e testes de cena).</summary>
    public bool Frozen { get; private set; }

    protected override void OnSpawned()
    {
        _cooldown = 1.0f + StartDelay;
        Play("parado");
    }

    protected override Vector2 Think(float dt)
    {
        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Sweep();
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
                }

                return Vector2.Zero;
        }

        var target = Target;
        if (target is null)
        {
            return Vector2.Zero;
        }

        var aim = target.AimDirection;
        Frozen = AvessoMoves.InLanternCone(target.GlobalPosition.X, target.GlobalPosition.Y, aim.X, aim.Y,
            GlobalPosition.X, GlobalPosition.Y, LightRange * A);
        if (Frozen)
        {
            // Congelado no meio do passo: a animação parada é a pose travada, só a cabeça treme.
            if (Sprite.Animation != "parado")
            {
                Play("parado");
            }

            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && to.Length() <= AttackRange * A * SizeFactor)
        {
            StartSweep(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > SweepReach * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartSweep(Vector2 dir)
    {
        _cooldown = (float)Period;
        _sweepAt = World.ClampToFloor(GlobalPosition + (dir * SweepReach * A * SizeFactor));
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnCircle(_sweepAt, SweepRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Sweep()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -5f);
        World.Feel.Spark(_sweepAt, Straw, 12, 220f * A, Vector2.Up, 60f);
        StrikeCircle(_sweepAt, SweepRadius * A * SizeFactor, 1, 260f);
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
