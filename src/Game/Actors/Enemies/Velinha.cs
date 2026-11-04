using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Velinha (Ato IV, rank B): vela pequena com perninhas de cera. Verbo: <b>cura</b> outros monstros e foge do caçador.
/// Aviso (CAMPANHA Ato IV): a chama cresce âmbar por 0,8 s (contorno âmbar e círculo no monstro que vai ser curado);
/// um golpe nessa hora interrompe a cura. Sem ninguém ferido por perto, chega perto e solta uma labareda (círculo âmbar).
/// </summary>
public partial class Velinha(LevelWorld world, float sizeFactor = 1f) : Enemy("velinha", world, sizeFactor)
{
    private const float FleeDistance = 280f;
    private const float HealRange = 420f;
    private const double HealFraction = 0.25;
    private const double HealCap = 70;
    private const float FlareRange = 120f;
    private const float FlareRadius = 80f;
    private const float AttackSeconds = 0.4f;
    private const float RecoverSeconds = 0.7f;
    private static readonly Color Flame = new("f2a541");

    private float _cooldown;
    private Enemy? _patient;
    private bool _flare;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    /// <summary>Curas que chegaram ao fim (testes de fumaça e capturas).</summary>
    public int Heals { get; private set; }

    protected override void OnSpawned()
    {
        _cooldown = 1.4f + StartDelay;
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
                    Release();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
            case EnemyState.Recover:
            case EnemyState.Hurt:
                if (StateTime >= (State == EnemyState.Attack ? AttackSeconds : RecoverSeconds))
                {
                    Enter(EnemyState.Approach);
                    Play("mover");
                }

                return Vector2.Zero;
        }

        if (Target is not { } target)
        {
            return Vector2.Zero;
        }

        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && FindPatient() is { } patient)
        {
            StartHeal(patient);
            return Vector2.Zero;
        }

        var hasFriends = FindFriend() is not null;
        if (_cooldown <= 0f && !hasFriends && to.Length() <= FlareRange * A)
        {
            StartFlare();
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        // Foge do caçador enquanto tem alguém para curar; sozinha, vem para a labareda.
        if (hasFriends && to.Length() < FleeDistance * A)
        {
            FaceTowards(GlobalPosition - to);
            return -to.Normalized() * (float)Speed * A;
        }

        FaceTowards(target.GlobalPosition);
        return hasFriends ? Vector2.Zero : to.Normalized() * (float)Speed * A * 0.8f;
    }

    private Enemy? FindPatient()
    {
        Enemy? best = null;
        foreach (var e in World.Enemies)
        {
            if (e == this || e.Dead || e is Velinha || e.Hp >= e.MaxHp * 0.95)
            {
                continue;
            }

            if (e.GlobalPosition.DistanceTo(GlobalPosition) <= HealRange * A && (best is null || e.Hp / e.MaxHp < best.Hp / best.MaxHp))
            {
                best = e;
            }
        }

        return best;
    }

    private Enemy? FindFriend()
    {
        foreach (var e in World.Enemies)
        {
            if (e != this && !e.Dead && e is not Velinha)
            {
                return e;
            }
        }

        return null;
    }

    private void StartHeal(Enemy patient)
    {
        _cooldown = (float)Period;
        _patient = patient;
        _flare = false;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.5f;
        FaceTowards(patient.GlobalPosition);
        _marker = WarnCircle(patient.GlobalPosition, patient.HitRadius * 1.3f, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -10f);
    }

    private void StartFlare()
    {
        _cooldown = (float)Period;
        _patient = null;
        _flare = true;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.5f;
        _marker = WarnCircle(GlobalPosition, FlareRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -8f);
    }

    private void Release()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        if (_flare)
        {
            World.Feel.Spark(GlobalPosition, Flame, 14, 240f * A, Vector2.Up, 90f);
            StrikeCircle(GlobalPosition, FlareRadius * A * SizeFactor, 1, 220f);
            return;
        }

        if (_patient is { Dead: false } patient && IsInstanceValid(patient))
        {
            patient.Restore(System.Math.Min(HealCap, patient.MaxHp * HealFraction));
            World.Feel.Spark(patient.HitCenter, Flame, 12, 160f * A, Vector2.Up, 60f);
            World.Feel.Ring(patient.GlobalPosition, patient.HitRadius * 1.3f, Flame, 0.35f, 4f * A);
            Heals++;
        }

        _patient = null;
    }

    protected override void OnHurt(in HitInfo hit)
    {
        // Um golpe durante o aviso apaga a chama: a cura (ou a labareda) não sai.
        if (State == EnemyState.Telegraph)
        {
            Flash.Tint = 0f;
            _marker?.Release();
            _marker = null;
            _patient = null;
            World.Feel.Number(HitCenter - new Vector2(0, HitRadius), "apagou", new Color("f4e7c6"), 0.7f);
        }

        Enter(EnemyState.Hurt);
    }

    protected override void OnDied()
    {
        _marker?.Release();
        _marker = null;
    }
}
