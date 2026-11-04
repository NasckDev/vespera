using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Casca (Ato V, rank A): eco blindado debaixo de três placas de bronze de sino. Verbo: <b>bloqueia</b> — de frente
/// quase não sente golpe (10%); pelas costas ou com o casco aberto, leva o golpe inteiro. Aviso (CAMPANHA Ato V): abre o
/// casco, brilha âmbar e uma faixa âmbar marca a investida (0,7 s). Partes: 3 placas; cada <see cref="PlateHp"/> de dano
/// pelas costas quebra uma, e sem as três ela não bloqueia mais.
/// </summary>
public partial class Casca(LevelWorld world, float sizeFactor = 1f) : Enemy("casca", world, sizeFactor)
{
    public const int Plates = 3;
    private const double PlateHp = 40;
    private const double FrontMult = 0.1;
    private const float ChargeRange = 420f;
    private const float ChargeLength = 460f;
    private const float ChargeWidth = 120f;
    private const float ChargeSpeed = 700f;
    private const float RecoverSeconds = 1.1f;
    private static readonly Color Bronze = new("c8a35a");

    private float _cooldown;
    private Vector2 _dir = Vector2.Right;
    private float _travelled;
    private bool _struck;
    private double _plateDamage;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    /// <summary>Placas quebradas (0 a 3).</summary>
    public int PlatesBroken { get; private set; }

    /// <summary>Casco aberto: aviso, investida e o respiro depois dela.</summary>
    public bool Open => State is EnemyState.Telegraph or EnemyState.Attack or EnemyState.Recover;

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
                    BeginCharge();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                return Charge(dt);
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
        if (_cooldown <= 0f && to.Length() <= ChargeRange * A)
        {
            StartCharge(to.Normalized());
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        return to.Normalized() * (float)Speed * A;
    }

    /// <summary>De frente e com o casco fechado, o golpe resvala no bronze (pelas costas, ou aberta, leva tudo).</summary>
    protected override double IncomingDamageMult(in HitInfo hit)
    {
        var facing = Sprite.FlipH ? -1f : 1f;
        var front = (hit.From.X - GlobalPosition.X) * facing > 0f;
        if (!front)
        {
            _plateDamage += hit.Damage;
            while (_plateDamage >= PlateHp && PlatesBroken < Plates)
            {
                _plateDamage -= PlateHp;
                PlatesBroken++;
                World.Feel.Spark(HitCenter, Bronze, 12, 260f * A, Vector2.Up, 80f);
                World.Feel.Toast(PlatesBroken < Plates
                    ? $"Uma placa da Casca caiu ({PlatesBroken}/{Plates})."
                    : "A Casca perdeu as três placas: agora sente golpe de frente.");
            }

            return 1;
        }

        if (Open || PlatesBroken >= Plates)
        {
            return 1;
        }

        World.Feel.Spark(HitCenter, Bronze, 6, 200f * A, -Vector2.Right * (Sprite.FlipH ? -1f : 1f), 40f);
        return FrontMult;
    }

    private void StartCharge(Vector2 dir)
    {
        _cooldown = (float)Period;
        _dir = dir;
        Enter(EnemyState.Telegraph);
        Play("aviso");
        Flash.Tint = 0.4f;
        _marker = WarnLane(GlobalPosition, dir, ChargeLength * A, ChargeWidth * A * SizeFactor, (float)Def.Telegraph);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
    }

    private void BeginCharge()
    {
        Flash.Tint = 0f;
        _marker?.Release();
        _marker = null;
        _travelled = 0f;
        _struck = false;
        Enter(EnemyState.Attack);
        Play("ataque");
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero, -6f);
    }

    private Vector2 Charge(float dt)
    {
        var step = ChargeSpeed * A * dt;
        _travelled += step;
        if (!_struck && TouchPlayer(HitCenter, HitRadius * 1.2f, 1, 460f))
        {
            _struck = true;
        }

        if (_travelled >= ChargeLength * A || (_travelled > 3f * step && GetSlideCollisionCount() > 0))
        {
            Enter(EnemyState.Recover);
            Play("parado");
            return Vector2.Zero;
        }

        return _dir * ChargeSpeed * A;
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State is EnemyState.Attack or EnemyState.Telegraph or EnemyState.Recover)
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
