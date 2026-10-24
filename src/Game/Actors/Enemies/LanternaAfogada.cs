using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Lanterna-Afogada (Ato III, rank C): lanterna de papel da festa, cheia de água. Verbo: <b>derrama</b>: flutua até
/// ficar sobre o caçador e despeja um círculo de água. Aviso: um círculo âmbar no chão escurece e a vela de dentro
/// brilha âmbar (0,8 s). Frágil (2 golpes); depois de derramar foge para trás dos Afogados.
/// </summary>
public partial class LanternaAfogada(LevelWorld world, float sizeFactor = 1f) : Enemy("lanterna_afogada", world, sizeFactor)
{
    private const float PourRadius = 85f;
    private const float StartRange = 70f;
    private const float AttackSeconds = 0.4f;
    private const float FleeSeconds = 1.4f;
    private const float StaggerSeconds = 0.2f;
    private static readonly Color Water = new("3c7a8a");

    private float _cooldown;
    private Vector2 _pourAt;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    public override bool PassesWalls => true;

    protected override void OnSpawned()
    {
        _cooldown = 1.2f + StartDelay;
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
                    Pour();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                if (StateTime >= AttackSeconds)
                {
                    Enter(EnemyState.Recover);
                    Play("mover");
                }

                return Vector2.Zero;
            case EnemyState.Recover:
                // Foge para trás de um Afogado (ou só para longe do caçador).
                if (StateTime >= FleeSeconds || target is null)
                {
                    Enter(EnemyState.Approach);
                }

                return target is null ? Vector2.Zero : FleeDirection(target) * (float)Speed * A;
            case EnemyState.Hurt:
                if (StateTime >= StaggerSeconds)
                {
                    Enter(EnemyState.Recover);
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
        if (_cooldown <= 0f && to.Length() <= StartRange * A)
        {
            _cooldown = (float)Period;
            _pourAt = World.ClampToFloor(target.GlobalPosition);
            Enter(EnemyState.Telegraph);
            Play("aviso");
            Flash.Tint = 0.45f;
            _marker = WarnCircle(_pourAt, PourRadius * A * SizeFactor, (float)Def.Telegraph);
            CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -8f);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > StartRange * 0.5f * A ? to.Normalized() * (float)Speed * A * (_cooldown > 0f ? 0.45f : 1f) : Vector2.Zero;
    }

    private Vector2 FleeDirection(Player target)
    {
        Enemy? shield = null;
        var best = float.MaxValue;
        foreach (var e in World.Enemies)
        {
            if (e is Afogado { Dead: false } && e.GlobalPosition.DistanceSquaredTo(GlobalPosition) < best)
            {
                best = e.GlobalPosition.DistanceSquaredTo(GlobalPosition);
                shield = e;
            }
        }

        var away = (GlobalPosition - target.GlobalPosition).Normalized();
        if (shield is null)
        {
            return away;
        }

        var behind = shield.GlobalPosition + ((shield.GlobalPosition - target.GlobalPosition).Normalized() * 90f * A);
        var to = behind - GlobalPosition;
        return to.Length() < 12f ? Vector2.Zero : to.Normalized();
    }

    private void Pour()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.LodoLand, Vector2.Zero, -6f);
        World.Feel.Spark(_pourAt, Water, 16, 240f * A, Vector2.Up, 90f);
        World.Feel.Ring(_pourAt, PourRadius * A * SizeFactor, Water, 0.35f, 6f * A);
        StrikeCircle(_pourAt, PourRadius * A * SizeFactor, 1, 160f);
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State is EnemyState.Attack)
        {
            return;
        }

        _marker?.Release();
        _marker = null;
        Flash.Tint = 0f;
        Enter(EnemyState.Hurt);
    }

    protected override void OnDied()
    {
        _marker?.Release();
        _marker = null;
    }
}
