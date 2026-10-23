using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Carrapicho (Ato II, rank D): planta que rasteja devagar atrás do caçador. Verbo: <b>chicoteia raízes em linha</b>.
/// Aviso: o botão abre e brilha âmbar enquanto uma faixa âmbar marca no chão por onde as raízes vão passar; quem sai
/// da faixa não leva o golpe. Depois do golpe fica um instante parado (janela para bater).
/// </summary>
public partial class Carrapicho(LevelWorld world, float sizeFactor = 1f) : Enemy("carrapicho", world, sizeFactor)
{
    private const float LashRange = 230f;
    private const float LashWidth = 70f;
    private const float AttackSeconds = 0.35f;
    private const float RecoverSeconds = 0.7f;
    private const float StaggerSeconds = 0.3f;

    private float _cooldown;
    private Vector2 _dir = Vector2.Right;
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
        var target = Target;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    _marker?.Release();
                    _marker = null;
                    Enter(EnemyState.Attack);
                    Play("ataque");
                    CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -6f);
                    StrikeLane(GlobalPosition, _dir, LashRange * A * SizeFactor, LashWidth * A * SizeFactor, 1, 260f);
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

        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        var distance = to.Length();
        if (_cooldown <= 0f && distance <= LashRange * 0.9f * A * SizeFactor)
        {
            _dir = to.Normalized();
            _cooldown = (float)Period;
            Enter(EnemyState.Telegraph);
            Play("aviso");
            Flash.Tint = 0.4f;
            _marker = WarnLane(GlobalPosition, _dir, LashRange * A * SizeFactor, LashWidth * A * SizeFactor, (float)Def.Telegraph);
            CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return distance > LashRange * 0.5f * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
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
