using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Sineiro (Ato V, rank A): gárgula pequena com um sino no lugar da cabeça. Verbo: <b>atordoa</b> com uma badalada em
/// área. Aviso (CAMPANHA Ato V): o sino brilha âmbar e um anel âmbar marca o alcance da badalada (0,8 s); quem está
/// dentro leva o golpe e fica preso no lugar um instante. Partes: o sino e as duas asas (só visual por ora).
/// </summary>
public partial class Sineiro(LevelWorld world, float sizeFactor = 1f) : Enemy("sineiro", world, sizeFactor)
{
    private const float TollRange = 200f;
    private const float TollRadius = 190f;
    private const float StunSeconds = 0.9f;
    private const float StunSlow = 0.2f;
    private const float KeepDistance = 90f;
    private const float AttackSeconds = 0.5f;
    private const float RecoverSeconds = 1.0f;
    private const float StaggerSeconds = 0.25f;
    private static readonly Color Bronze = new("c8a35a");

    private float _cooldown;
    private float _stun;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = 1.2f + StartDelay;
        Play("mover");
    }

    protected override Vector2 Think(float dt)
    {
        _cooldown -= dt;
        TickStun(dt);
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Toll();
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

        if (Target is not { } target)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && to.Length() <= TollRange * A * SizeFactor)
        {
            StartToll();
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > KeepDistance * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartToll()
    {
        _cooldown = (float)Period;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnCircle(GlobalPosition, TollRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Toll()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero, -8f);
        World.Feel.Ring(GlobalPosition, TollRadius * A * SizeFactor, Bronze, 0.5f, 8f * A);
        if (StrikeCircle(GlobalPosition, TollRadius * A * SizeFactor, 1, 120f) && Target is { } target)
        {
            // Atordoado: preso no lugar um instante (anda a 20%).
            _stun = StunSeconds;
            target.Wade = StunSlow;
            World.Feel.Number(target.Combat.HitCenter - new Vector2(0, 30f * A), "atordoado", Bronze, 0.7f);
        }
    }

    private void TickStun(float dt)
    {
        if (_stun <= 0f)
        {
            return;
        }

        _stun -= dt;
        if (_stun <= 0f && World.Player is { } player)
        {
            player.Wade = 1f;
        }
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
        if (_stun > 0f && World.Player is { } player)
        {
            player.Wade = 1f;
        }
    }
}
