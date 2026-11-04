using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Despejado (Ato VI, rank S): morador carregando a casa inteira nas costas. Verbo: <b>arremessa</b> móveis.
/// Aviso (CAMPANHA, Ato VI): ergue a cadeira (âmbar) e a sombra do móvel escurece no chão onde o caçador estava (0,8 s);
/// quem sai da sombra não leva o golpe. Mantém distância de arremesso e recua se o caçador chega muito perto.
/// </summary>
public partial class Despejado(LevelWorld world, float sizeFactor = 1f) : Enemy("despejado", world, sizeFactor)
{
    private const float ThrowRange = 520f;
    private const float KeepDistance = 260f;
    private const float LandRadius = 85f;
    private const float AttackSeconds = 0.45f;
    private const float RecoverSeconds = 1.0f;
    private const float StaggerSeconds = 0.3f;
    private static readonly Color Wood = new("8a5a3c");

    private float _cooldown;
    private Vector2 _landAt;
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
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Land();
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
        if (_cooldown <= 0f && distance <= ThrowRange * A)
        {
            StartThrow(target.GlobalPosition);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        var dir = to.Normalized();
        var approach = distance > KeepDistance * 1.5f * A ? dir : distance < KeepDistance * A ? -dir * 0.7f : Vector2.Zero;
        return approach * (float)Speed * A;
    }

    private void StartThrow(Vector2 at)
    {
        _cooldown = (float)Period;
        _landAt = World.ClampToFloor(at);
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnCircle(_landAt, LandRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Land()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -6f);
        World.Feel.Spark(_landAt, Wood, 16, 260f * A, Vector2.Up, 80f);
        StrikeCircle(_landAt, LandRadius * A * SizeFactor, 1, 240f);
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
