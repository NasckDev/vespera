using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Ancoreiro (Ato III, rank C): caranguejo com uma âncora no lugar da garra. Verbo: <b>ancora</b>: arremessa a âncora
/// em linha reta e quem é pego é puxado para perto dele. Aviso: a corrente chacoalha e uma linha âmbar marca o chão
/// (0,6 s); saia para o lado. Depois de puxar fica um instante parado recolhendo a corrente.
/// </summary>
public partial class Ancoreiro(LevelWorld world, float sizeFactor = 1f) : Enemy("ancoreiro", world, sizeFactor)
{
    private const float ThrowRange = 440f;
    private const float ThrowWidth = 60f;
    private const float PullForce = -520f;
    private const float AttackSeconds = 0.4f;
    private const float RecoverSeconds = 0.8f;
    private const float StaggerSeconds = 0.25f;
    private const float KeepDistance = 220f;
    private static readonly Color Iron = new("6b6f8a");

    private float _cooldown;
    private float _side = 1f;
    private Vector2 _dir = Vector2.Right;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = 1.0f + StartDelay;
        _side = GD.Randf() > 0.5f ? 1f : -1f;
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
                    Throw();
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
        var distance = to.Length();
        if (_cooldown <= 0f && distance <= ThrowRange * 0.85f * A * SizeFactor)
        {
            _dir = to.Normalized();
            _cooldown = (float)Period;
            Enter(EnemyState.Telegraph);
            Play("aviso");
            Flash.Tint = 0.4f;
            _marker = WarnLane(GlobalPosition, _dir, ThrowRange * A * SizeFactor, ThrowWidth * A * SizeFactor, (float)Def.Telegraph);
            CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        // Anda de lado (caranguejo): mantém distância de arremesso e circula o caçador.
        var dir = to.Normalized();
        var sideways = dir.Orthogonal() * _side;
        var approach = distance > KeepDistance * 1.6f * A ? dir : distance < KeepDistance * A ? -dir * 0.6f : Vector2.Zero;
        return (approach + (sideways * 0.7f)).Normalized() * (float)Speed * A;
    }

    private void Throw()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -6f);
        var length = ThrowRange * A * SizeFactor;
        for (var i = 1; i <= 6; i++)
        {
            World.Feel.Spark(GlobalPosition + (_dir * length * i / 6f), Iron, 3, 120f * A);
        }

        // Força negativa: o empurrão vira puxão (o caçador é arrastado até a pinça).
        StrikeLane(GlobalPosition, _dir, length, ThrowWidth * A * SizeFactor, 0.8, PullForce);
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
