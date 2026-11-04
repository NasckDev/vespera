using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Traça-de-Arquivo (Ato V, rank A): enxame de traças de papel velho, voando. Verbo: <b>apaga</b> — perto do caçador a
/// sala escurece e a luz da lanterna dele encolhe (<see cref="LanternGloom"/>). Aviso (CAMPANHA Ato V): o enxame junta
/// numa bola âmbar e uma faixa âmbar marca o bote (0,6 s); depois estoura em papel picado.
/// </summary>
public partial class TracaDeArquivo(LevelWorld world, float sizeFactor = 1f) : Enemy("traca_de_arquivo", world, sizeFactor)
{
    private const float GloomRange = 380f;
    private const float GloomDarkness = 0.45f;
    private const float LungeRange = 360f;
    private const float LungeLength = 380f;
    private const float LungeWidth = 100f;
    private const float LungeSpeed = 760f;
    private const float Orbit = 160f;
    private const float RecoverSeconds = 0.9f;
    private static readonly Color Paper = new("e8dcb5");

    private float _cooldown;
    private Vector2 _dir = Vector2.Right;
    private float _travelled;
    private bool _struck;
    private float _wobble;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    /// <summary>Voa: passa por cima de túmulos e estantes.</summary>
    public override bool PassesWalls => true;

    protected override void OnSpawned()
    {
        _cooldown = 1.3f + StartDelay;
        _wobble = GD.Randf() * Mathf.Tau;
        Play("mover");
    }

    protected override Vector2 Think(float dt)
    {
        _cooldown -= dt;
        _wobble += dt * 2.4f;
        var hover = ScaleRegistry.HoverHeight(MonsterId, A * ScaleRules.ReferenceHuman);
        Sprite.Position = new Vector2(0, -hover - (Mathf.Sin(_wobble * 1.7f) * 6f * A));
        if (Target is { } near && near.GlobalPosition.DistanceTo(GlobalPosition) <= GloomRange * A)
        {
            var closeness = 1f - (near.GlobalPosition.DistanceTo(GlobalPosition) / (GloomRange * A));
            LanternGloom.Request(World, GloomDarkness * (0.5f + (0.5f * closeness)), 2.2f);
        }

        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    BeginLunge();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                return Lunge(dt);
            case EnemyState.Recover:
            case EnemyState.Hurt:
                if (StateTime >= (State == EnemyState.Hurt ? 0.2f : RecoverSeconds))
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
        if (_cooldown <= 0f && to.Length() <= LungeRange * A)
        {
            StartLunge(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        // Ronda o caçador em volta de um anel (o enxame não para quieto).
        var orbitPoint = target.GlobalPosition + (Vector2.FromAngle(_wobble) * Orbit * A);
        var go = orbitPoint - GlobalPosition;
        return go.Length() > 8f ? go.Normalized() * (float)Speed * A : Vector2.Zero;
    }

    private void StartLunge(Vector2 dir)
    {
        _cooldown = (float)Period;
        _dir = dir;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.5f;
        _marker = WarnLane(GlobalPosition, dir, LungeLength * A, LungeWidth * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -8f);
    }

    private void BeginLunge()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        _travelled = 0f;
        _struck = false;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.GhostLunge, Vector2.Zero, -4f);
    }

    private Vector2 Lunge(float dt)
    {
        _travelled += LungeSpeed * A * dt;
        if (!_struck && TouchPlayer(HitCenter, HitRadius * 1.1f, 1, 200f))
        {
            _struck = true;
            World.Feel.Spark(HitCenter, Paper, 14, 240f * A);
        }

        if (_travelled >= LungeLength * A)
        {
            Enter(EnemyState.Recover);
            Play("parado");
            return Vector2.Zero;
        }

        return _dir * LungeSpeed * A;
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
        World.Feel.Spark(HitCenter, Paper, 18, 200f * A, Vector2.Down, 50f);
    }
}
