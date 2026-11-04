using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Carpideira (Ato IV, rank B): figura de luto, véu até o chão. Verbo: <b>lamenta</b> — um grito em cone que empurra.
/// Aviso (CAMPANHA Ato IV): o véu sobe, o rosto vazio brilha âmbar e o cone aparece no chão (0,7 s); quem sai da
/// faixa não leva o grito. Depois fica parada um instante, de costas para o caçador que a contornou.
/// </summary>
public partial class Carpideira(LevelWorld world, float sizeFactor = 1f) : Enemy("carpideira", world, sizeFactor)
{
    private const float WailRange = 300f;
    private const float WailLength = 340f;
    private const float WailWidth = 190f;
    private const float WailPush = 620f;
    private const float KeepDistance = 150f;
    private const float AttackSeconds = 0.5f;
    private const float RecoverSeconds = 1.0f;
    private const float StaggerSeconds = 0.25f;
    private static readonly Color Grief = new("e64a8e");

    private float _cooldown;
    private Vector2 _wailDir = Vector2.Right;
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
                    Wail();
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
        var reach = WailRange * A * SizeFactor;
        if (_cooldown <= 0f && to.Length() <= reach)
        {
            StartWail(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Length() > KeepDistance * A ? to.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartWail(Vector2 dir)
    {
        _cooldown = (float)Period;
        _wailDir = dir;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnLane(GlobalPosition, dir, WailLength * A * SizeFactor, WailWidth * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void Wail()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.Ghost, Vector2.Zero, -2f);
        var steps = 4;
        for (var i = 1; i <= steps; i++)
        {
            World.Feel.Ring(GlobalPosition + (_wailDir * WailLength * A * SizeFactor * i / (steps + 1)), 40f * A * i, Grief, 0.35f, 4f * A);
        }

        StrikeLane(GlobalPosition, _wailDir, WailLength * A * SizeFactor, WailWidth * A * SizeFactor, 1, WailPush);
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
