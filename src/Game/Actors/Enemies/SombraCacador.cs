using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Sombra de Caçador (Ato VII): eco de um caçador caído que usa o verbo da classe de quem caça agora
/// (<see cref="Vespera.Core.Rift.AvessoMoves.ShadowFor"/>). Lâmina: investe numa faixa curta e corta. Arco: fica longe e
/// atira numa faixa longa. Martelo: chega perto e bate no chão em volta de si. Todo golpe tem aviso âmbar no chão.
/// </summary>
public partial class SombraCacador(LevelWorld world, string variantId, float sizeFactor = 1f) : Enemy(variantId, world, sizeFactor)
{
    private const float AttackSeconds = 0.35f;
    private const float RecoverSeconds = 0.8f;
    private const float StaggerSeconds = 0.25f;
    private const float DashSpeed = 900f;
    private static readonly Color Shade = new("9b4ad0");

    private float _cooldown;
    private Vector2 _dir = Vector2.Right;
    private Vector2 _dashFrom;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    private Verb Kind => MonsterId switch
    {
        "sombra_cacador_arco" => Verb.Shoot,
        "sombra_cacador_martelo" => Verb.Slam,
        _ => Verb.Dash,
    };

    private float Reach => Kind switch
    {
        Verb.Shoot => 620f,
        Verb.Slam => 110f,
        _ => 260f,
    };

    private float Width => Kind == Verb.Shoot ? 44f : 70f;

    /// <summary>Distância em que ela para e ataca (o arco fica longe; o martelo chega perto).</summary>
    private float AttackRange => Kind switch
    {
        Verb.Shoot => 520f,
        Verb.Slam => 120f,
        _ => 240f,
    };

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
                    Strike();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                if (StateTime >= AttackSeconds)
                {
                    Enter(EnemyState.Recover);
                    Play("parado");
                }

                // Lâmina: a investida leva o corpo pela faixa durante o golpe.
                return Kind == Verb.Dash && GlobalPosition.DistanceTo(_dashFrom) < Reach * A ? _dir * DashSpeed * A : Vector2.Zero;
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
        var distance = to.Length();
        if (_cooldown <= 0f && distance <= AttackRange * A)
        {
            StartStrike(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        var dir = to.Normalized();
        if (Kind == Verb.Shoot && distance < AttackRange * 0.5f * A)
        {
            return -dir * (float)Speed * A * 0.7f;
        }

        return distance > AttackRange * 0.8f * A ? dir * (float)Speed * A : Vector2.Zero;
    }

    private void StartStrike(Vector2 dir)
    {
        _cooldown = (float)Period;
        _dir = dir;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = Kind == Verb.Slam
            ? WarnCircle(GlobalPosition, Reach * A * SizeFactor, (float)Def.Telegraph)
            : WarnLane(GlobalPosition, _dir, Reach * A * SizeFactor, Width * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Strike()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        _dashFrom = GlobalPosition;
        if (Kind == Verb.Slam)
        {
            CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -4f);
            StrikeCircle(GlobalPosition, Reach * A * SizeFactor, 1, 300f);
            return;
        }

        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -5f);
        World.Feel.Spark(GlobalPosition + (_dir * Reach * A * 0.5f), Shade, 10, 200f * A);
        StrikeLane(GlobalPosition, _dir, Reach * A * SizeFactor, Width * A * SizeFactor, 1, 200f);
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

    private enum Verb
    {
        Dash,
        Shoot,
        Slam,
    }
}
