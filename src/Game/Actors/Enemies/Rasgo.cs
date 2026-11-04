using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Rasgo (Ato VI, rank S): uma fenda em pé, de bordas rasgadas. Verbo: <b>teleporta</b> para as costas do caçador.
/// Aviso (CAMPANHA, Ato VI): dobra numa linha âmbar e o ponto de chegada brilha (0,5 s), do outro lado do caçador;
/// depois aparece ali e corta com as duas bordas. Quem sai do círculo de chegada não leva o golpe.
/// </summary>
public partial class Rasgo(LevelWorld world, float sizeFactor = 1f) : Enemy("rasgo", world, sizeFactor)
{
    private const float BlinkRange = 420f;
    private const float BehindDistance = 70f;
    private const float SlashRadius = 80f;
    private const float AttackSeconds = 0.35f;
    private const float RecoverSeconds = 0.9f;
    private const float StaggerSeconds = 0.2f;
    private static readonly Color Rift = new("e64a8e");

    private float _cooldown;
    private Vector2 _arriveAt;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

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
                    Blink();
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
        if (_cooldown <= 0f && to.Length() <= BlinkRange * A)
        {
            StartBlink(target.GlobalPosition);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > BehindDistance * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartBlink(Vector2 target)
    {
        _cooldown = (float)Period;
        var (x, y) = AvessoMoves.BlinkBehind(GlobalPosition.X, GlobalPosition.Y, target.X, target.Y, BehindDistance * A);
        _arriveAt = World.ClampToFloor(new Vector2((float)x, (float)y));
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.5f;
        _marker = WarnCircle(_arriveAt, SlashRadius * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Blink()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        World.Feel.Spark(GlobalPosition, Rift, 10, 180f * A);
        GlobalPosition = _arriveAt;
        if (Target is { } target)
        {
            FaceTowards(target.GlobalPosition);
        }

        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.HitHeavy, Vector2.Zero, -4f);
        StrikeCircle(_arriveAt, SlashRadius * A * SizeFactor, 1, 220f);
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
