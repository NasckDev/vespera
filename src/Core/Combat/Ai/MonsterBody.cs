using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;

namespace Vespera.Core.Combat.Ai;

/// <summary>Estados das IAs da v2 (<c>m.st</c>, <c>combat.js:274-405</c>).</summary>
public enum MonsterState
{
    Idle,
    Chase,
    Wind,
    Lunge,
    Rec,
    Cast,
    Spit,
    Slam,
    Bite,
    Lure,
    Tide,
    Swipe,
    Chain,
    Burst,
    Tele,
    Sweep,
    Summon,
    Rain,
    Charge,
    Dead,
}

/// <summary>
/// Estado puro de um monstro (<c>spawnMob</c>, <c>combat.js:244-253</c>): posição em u (pé), face, HP e partes escalados
/// pelo rank, timers da IA. O nó Godot só desenha e aplica os pedidos do <see cref="IMonsterWorld"/>.
/// </summary>
public sealed class MonsterBody
{
    public const double SpawnSeconds = 0.45;
    public const double BossSpawnSeconds = 1.2;
    public const double FlashSeconds = 0.12;
    public const double KnockbackDecay = 0.001;

    private readonly double[] _partHp;
    private readonly bool[] _broken;

    public MonsterBody(ScaledMonster scaled, double x, double y, double hitHeight, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(scaled);
        ArgumentNullException.ThrowIfNull(random);
        Def = scaled.Def;
        X = x;
        Y = y;
        Hp = scaled.Hp;
        MaxHp = scaled.Hp;
        Damage = scaled.Damage;
        Speed = Def.Speed;
        Radius = Def.Radius;
        HitHeight = hitHeight;
        Face = random.NextDouble() < 0.5 ? 1 : -1;
        Cd = random.Range(0.6, 1.6);
        Bob = random.Range(0, 6);
        SpawnT = SpawnSeconds;
        _partHp = [.. scaled.PartHp];
        PartMaxHp = [.. scaled.PartHp];
        _broken = new bool[_partHp.Length];
    }

    /// <summary>Boneco de treino da Guilda (<c>world.js:13</c>): r 8, hh 22, sem partes, não perde HP nem age.</summary>
    public static readonly MonsterDef DummyDef = new()
    {
        Id = "dummy",
        Name = "Boneco de treino",
        Sprite = "pr_dummy",
        Scale = 1,
        Hp = 1,
        Speed = 0,
        Damage = 0,
        Xp = 0,
        Radius = 8,
        HitHeight = 22,
        Ai = AiKind.Lunger,
        Parts = [],
    };

    /// <summary>Cria um boneco de treino (face 1, já nascido).</summary>
    public static MonsterBody CreateDummy(double x, double y) =>
        new(new ScaledMonster(DummyDef, 1, 0, []), x, y, 22, new FixedSpawnRandom()) { Face = 1, SpawnT = 0, IsDummy = true };

    public MonsterDef Def { get; }

    public bool IsDummy { get; private init; }

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>1 olhando para a direita, −1 para a esquerda.</summary>
    public int Face { get; set; }

    public MonsterState State { get; private set; } = MonsterState.Idle;

    /// <summary>Tempo no estado atual (<c>m.t</c>).</summary>
    public double T { get; set; }

    public double Cd { get; set; }

    public bool Done { get; set; }

    public double Ldx { get; set; }

    public double Ldy { get; set; }

    /// <summary>Ponto guardado para a isca do Abissal.</summary>
    public double Lx { get; set; }

    public double Ly { get; set; }

    public double Hp { get; set; }

    public double MaxHp { get; }

    public double Damage { get; }

    public double Speed { get; }

    public double Radius { get; }

    /// <summary>Altura do centro de acerto (<c>hh</c>).</summary>
    public double HitHeight { get; }

    public double Bob { get; set; }

    /// <summary>Nascendo: inativo, intocável, fade-in.</summary>
    public double SpawnT { get; set; }

    public double Flash { get; set; }

    public double Stun { get; set; }

    /// <summary>Opacidade (Ceifador); &lt; 0.3 = intangível.</summary>
    public double Alpha { get; set; } = 1;

    public bool Summoned { get; set; }

    public double Kx { get; set; }

    public double Ky { get; set; }

    /// <summary>Puxão (Corrente Votiva): alvo e tempo.</summary>
    public double PullX { get; set; }

    public double PullY { get; set; }

    public double PullT { get; set; }

    /// <summary>Isca Hipnótica: ponto que atrai neste frame (null = nenhum).</summary>
    public (double X, double Y)? Lured { get; set; }

    public bool Dead { get; private set; }

    public double DeadT { get; set; }

    /// <summary>Monstro de invasão (Estilhaço garantido).</summary>
    public bool Invasion { get; set; }

    public bool IsBoss => Def.Boss;

    public bool IsElite => Def.Elite;

    public bool Fly => Def.Fly;

    public IReadOnlyList<double> PartMaxHp { get; }

    public IReadOnlyList<double> PartHp => _partHp;

    public IReadOnlyList<bool> Broken => _broken;

    /// <summary>Pode receber golpes: vivo, já nasceu e tangível.</summary>
    public bool IsTargetable => !Dead && SpawnT <= 0 && Alpha >= 0.3;

    /// <summary>
    /// Parte de índice <paramref name="index"/> existe e está quebrada (DECISIONS D6/B1-D39: o <c>broken()</c> da v2
    /// devolvia true para partes inexistentes).
    /// </summary>
    public bool IsPartBroken(int index) => index >= 0 && index < _broken.Length && _broken[index];

    /// <summary>Muda de estado zerando <c>t</c> e <c>done</c> (<c>setSt</c>).</summary>
    public void SetState(MonsterState state)
    {
        State = state;
        T = 0;
        Done = false;
    }

    /// <summary>Muda o estado sem zerar o tempo (<c>m.st = 'chase'</c> direto, ou atordoado → idle).</summary>
    public void ForceState(MonsterState state) => State = state;

    /// <summary>Dano na parte; devolve o índice da parte que quebrou agora, ou −1.</summary>
    public int DamagePart(int index, double amount)
    {
        if (index < 0 || index >= _partHp.Length || _broken[index])
        {
            return -1;
        }

        _partHp[index] -= amount;
        if (_partHp[index] > 0)
        {
            return -1;
        }

        _partHp[index] = 0;
        _broken[index] = true;
        return index;
    }

    public void Kill()
    {
        Dead = true;
        DeadT = 0;
        State = MonsterState.Dead;
        Kx = Ky = 0;
    }

    /// <summary>Atacando (pose <c>_a</c>; <c>isAtk</c>, <c>render.js:10</c>).</summary>
    public bool IsAttacking => State switch
    {
        MonsterState.Wind => T > 0.15,
        MonsterState.Slam => T > 0.45,
        MonsterState.Sweep => T > 0.45,
        MonsterState.Lunge or MonsterState.Cast or MonsterState.Spit or MonsterState.Bite or MonsterState.Lure
            or MonsterState.Tide or MonsterState.Swipe or MonsterState.Chain or MonsterState.Burst or MonsterState.Charge => true,
        _ => false,
    };

    /// <summary>Estado especial (pose <c>_s</c> se existir e <c>t &gt; 0.1</c>).</summary>
    public bool IsSpecial => State is MonsterState.Summon or MonsterState.Rain or MonsterState.Burst or MonsterState.Chain
        or MonsterState.Tide or MonsterState.Lure;

    /// <summary>Mirando um golpe forte: brilho branco pulsante no sprite (<c>render.js:154</c>).</summary>
    public bool IsTelegraphing => State switch
    {
        MonsterState.Wind => true,
        MonsterState.Bite => T < 0.65,
        MonsterState.Sweep => T < 0.7,
        MonsterState.Charge => T < 0.6,
        _ => false,
    };

    private sealed class FixedSpawnRandom : IRandom
    {
        public double NextDouble() => 0.5;
    }
}
