using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Cão-de-Vigília (Ato IV, rank B): cão magro de pedra com coleira de nome. Verbo: <b>investe</b> em linha reta depois
/// de um uivo. Aviso (CAMPANHA Ato IV): uiva, brilha âmbar e uma faixa âmbar marca a linha da investida (0,6 s); sair
/// para o lado basta. Se a investida termina numa parede ou túmulo, fica tonto 1,5 s (a hora de bater).
/// </summary>
public partial class CaoDeVigilia(LevelWorld world, float sizeFactor = 1f) : Enemy("cao_de_vigilia", world, sizeFactor)
{
    private const float ChargeRange = 520f;
    private const float ChargeLength = 560f;
    private const float ChargeWidth = 90f;
    private const float ChargeSpeed = 900f;
    private const float StunSeconds = 1.5f;
    private const float RecoverSeconds = 0.6f;
    private const float StaggerSeconds = 0.2f;

    private float _cooldown;
    private Vector2 _dir = Vector2.Right;
    private float _travelled;
    private bool _struck;
    private bool _dizzy;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    /// <summary>Tonto depois de bater na parede (capturas e testes).</summary>
    public bool Dizzy => _dizzy && State == EnemyState.Recover;

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
                    BeginCharge();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                return Charge(dt);
            case EnemyState.Recover:
            case EnemyState.Hurt:
                var wait = State == EnemyState.Hurt ? StaggerSeconds : _dizzy ? StunSeconds : RecoverSeconds;
                if (StateTime >= wait)
                {
                    _dizzy = false;
                    Enter(EnemyState.Approach);
                    Play("mover");
                }

                return Vector2.Zero;
        }

        if (Target is not { } target)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && to.Length() <= ChargeRange * A)
        {
            StartHowl(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Normalized() * (float)Speed * A;
    }

    private void StartHowl(Vector2 dir)
    {
        _cooldown = (float)Period;
        _dir = dir;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnLane(GlobalPosition, dir, ChargeLength * A, ChargeWidth * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Ghost, Vector2.Zero, -4f);
    }

    private void BeginCharge()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        _travelled = 0f;
        _struck = false;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.GhostLunge, Vector2.Zero, -2f);
    }

    private Vector2 Charge(float dt)
    {
        var step = ChargeSpeed * A * dt;
        _travelled += step;
        if (!_struck && TouchPlayer(HitCenter, HitRadius * 1.2f, 1, 420f))
        {
            _struck = true;
        }

        // Bateu em parede ou túmulo: a investida para e o cão fica tonto.
        if (_travelled > 3f * step && GetSlideCollisionCount() > 0)
        {
            Stun();
            return Vector2.Zero;
        }

        if (_travelled >= ChargeLength * A)
        {
            Enter(EnemyState.Recover);
            Play("parado");
            return Vector2.Zero;
        }

        return _dir * ChargeSpeed * A;
    }

    private void Stun()
    {
        _dizzy = true;
        Enter(EnemyState.Recover);
        Play("tonto");
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -6f);
        World.Feel.Spark(HitCenter, new Color("f4e7c6"), 10, 200f * A, Vector2.Up, 80f);
        World.Feel.Shake(0.12f);
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State is EnemyState.Attack or EnemyState.Telegraph || Dizzy)
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
