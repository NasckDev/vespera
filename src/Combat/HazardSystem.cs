using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;

namespace Vespera.Combat;

public enum HazardKind
{
    None,

    /// <summary>Telegraph de nascimento (<c>spawnTele</c>): depois de <c>max</c>, o monstro nasce.</summary>
    Spawn,

    /// <summary>Isca do Abissal (<c>type:'lure'</c>).</summary>
    AnglerLure,

    /// <summary>Onda da maré (<c>type:'wave'</c>).</summary>
    TideWave,

    /// <summary>Estilhaço da chuva dos cavaleiros (<c>type:'shard'</c>).</summary>
    Shard,

    /// <summary>Lanterna orbital do Arauto (<c>type:'korb'</c>).</summary>
    OrbitLantern,

    /// <summary>Isca Hipnótica do jogador (<c>type:'plure'</c>).</summary>
    PlayerLure,

    /// <summary>Flecha da Chuva de Vésperas (<c>G.uf</c>).</summary>
    RainArrow,
}

/// <summary>
/// Efeitos de área com regra (<c>updFx</c>, <c>combat.js:441-473</c>; <c>updUlt</c> do Arco): nascimento com telegraph,
/// isca e maré do Abissal, estilhaços, lanternas orbitais, Isca Hipnótica e flechas do Supremo do Arco. Pool fixo de
/// structs, um nó desenha todos (telegraphs no chão; o resto numa camada acima).
/// </summary>
public partial class HazardSystem : Node2D
{
    public const int Capacity = 128;

    private readonly Hazard[] _pool = new Hazard[Capacity];
    private CombatDirector _combat = null!;
    private Texture2D _glow = null!;
    private int _live;
    private float _time;

    /// <summary>Monstros ainda nascendo (contam como vivos para as ondas).</summary>
    public int PendingSpawns { get; private set; }

    /// <summary>Nascimentos pendentes de invasão.</summary>
    public int PendingInvasionSpawns { get; private set; }

    public void Setup(CombatDirector combat, Texture2D glow)
    {
        _combat = combat;
        _glow = glow;
    }

    public void Clear()
    {
        Array.Clear(_pool);
        _live = 0;
        PendingSpawns = 0;
        PendingInvasionSpawns = 0;
    }

    public void SpawnMonster(string monsterId, Vector2 at, float delay, bool chase, bool invasion)
    {
        ref var h = ref Allocate();
        h.Kind = HazardKind.Spawn;
        h.Position = at;
        h.Max = delay;
        h.MonsterId = monsterId;
        h.Chase = chase;
        h.Invasion = invasion;
        PendingSpawns++;
        if (invasion)
        {
            PendingInvasionSpawns++;
        }
    }

    public void AnglerLure(Vector2 at, double damage) => Add(HazardKind.AnglerLure, at, 2.6f, damage);

    public void TideWave(Vector2 at, double damage)
    {
        ref var h = ref Add(HazardKind.TideWave, at, 0, damage);
        h.Radius = 8;
    }

    public void Shard(Vector2 at, float delay, double damage, Color color)
    {
        ref var h = ref Add(HazardKind.Shard, at, 1.1f, damage);
        h.T = -delay;
        h.Color = color;
    }

    public void OrbitLantern(MonsterBody boss, float angle, float firstShot)
    {
        ref var h = ref Add(HazardKind.OrbitLantern, Vector2.Zero, 6, boss.Damage * 0.55);
        h.Boss = boss;
        h.Angle = angle;
        h.Cd = firstShot;
    }

    /// <summary>Isca Hipnótica: voa de <paramref name="from"/> até <paramref name="target"/> a 320 u/s.</summary>
    public void PlayerLure(Vector2 from, Vector2 target, double damage)
    {
        ref var h = ref Add(HazardKind.PlayerLure, from, (float)EcoNumbers.LureDuration, damage);
        h.Target = target;
        h.Landed = false;
    }

    public void RainArrow(Vector2 at, double damage)
    {
        ref var h = ref Add(HazardKind.RainArrow, at, 0.5f, damage);
        h.Color = CombatPalette.Ambar;
    }

    public void Step(float dt)
    {
        _time += dt;
        if (_live == 0)
        {
            QueueRedraw();
            return;
        }

        var p = _combat.PlayerFoot;
        var live = 0;
        for (var i = 0; i < Capacity; i++)
        {
            ref var h = ref _pool[i];
            if (h.Kind == HazardKind.None)
            {
                continue;
            }

            h.T += dt;
            if (h.T >= 0 && !StepOne(ref h, dt, p))
            {
                Remove(ref h);
                continue;
            }

            live++;
        }

        _live = live;
        QueueRedraw();
    }

    /// <summary>Avança um perigo. False = acabou.</summary>
    private bool StepOne(ref Hazard h, float dt, Vector2 p)
    {
        switch (h.Kind)
        {
            case HazardKind.Spawn:
                if (h.T < h.Max)
                {
                    return true;
                }

                PendingSpawns--;
                if (h.Invasion)
                {
                    PendingInvasionSpawns--;
                }

                _combat.SpawnMonsterNow(h.MonsterId!, h.Position, h.Chase, h.Invasion);
                _combat.Glow.Burst(h.Position - new Vector2(0, 8), 16, CombatPalette.Spawn, new BurstOptions { V1 = 90 });
                h.MonsterId = null;
                return false;

            case HazardKind.AnglerLure:
            {
                var d = p.DistanceTo(h.Position);
                if (!_combat.PlayerDead && d < 75 && !_combat.PlayerDashing)
                {
                    _combat.MovePlayer((h.Position - p).Normalized() * 38 * dt);
                    _combat.Charm();
                }

                if (h.T < h.Max)
                {
                    return true;
                }

                _combat.Fx.Ring(h.Position, 6, 46, 0.3f, CombatPalette.Brecha with { A = 0.95f });
                _combat.Glow.Burst(h.Position - new Vector2(0, 6), 24, CombatPalette.LureBoom, new BurstOptions { V1 = 130 });
                _combat.Sfx("boom", h.Position);
                _combat.Shake(5);
                if (_combat.PlayerFoot.DistanceTo(h.Position) < 44)
                {
                    _combat.HurtPlayer(h.Damage, h.Position);
                }

                return false;
            }

            case HazardKind.TideWave:
                h.Radius += 150 * dt;
                if (!h.Done && MathF.Abs(EllipseDistance(p - h.Position, 1.4f) - h.Radius) < 7 && _combat.HurtPlayer(h.Damage, h.Position))
                {
                    h.Done = true;
                }

                return h.Radius < 115;

            case HazardKind.Shard:
                if (h.T >= 0.9f && !h.Done)
                {
                    h.Done = true;
                    _combat.Fx.Burst(h.Position, 14, CombatPalette.Shard, new BurstOptions { V1 = 90, Up = 30, Gravity = 160 });
                    _combat.Shake(3);
                    if (EllipseDistance(p - h.Position, 1.3f) < 16)
                    {
                        _combat.HurtPlayer(h.Damage, h.Position);
                    }
                }

                return h.T < h.Max;

            case HazardKind.OrbitLantern:
            {
                if (h.Boss is not { Dead: false } boss)
                {
                    return false;
                }

                h.Angle += dt * 1.6f;
                h.Position = new Vector2((float)boss.X + (MathF.Cos(h.Angle) * 40), (float)boss.Y - 30 + (MathF.Sin(h.Angle) * 22));
                h.Cd -= dt;
                if (h.Cd <= 0 && !_combat.PlayerDead)
                {
                    h.Cd = 1.3f;
                    var a = MathF.Atan2(p.Y - 11 - h.Position.Y, p.X - h.Position.X);
                    _combat.Projectiles.Fire(ProjectileKind.MonsterBolt, h.Position, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 130, 4, h.Damage, 2.4f, CombatPalette.Cyan);
                }

                return h.T < h.Max;
            }

            case HazardKind.PlayerLure:
                if (!h.Landed)
                {
                    var to = h.Target - h.Position;
                    var l = to.Length();
                    if (l < 6)
                    {
                        h.Landed = true;
                        h.T = 0;
                    }
                    else
                    {
                        h.Position += to / l * MathF.Min(l, (float)EcoNumbers.LureFlightSpeed * dt);
                    }

                    return true;
                }

                _combat.LureMonsters(h.Position, (float)EcoNumbers.LureAttractRadius);
                if (h.T < h.Max)
                {
                    return true;
                }

                _combat.Fx.Ring(h.Position, 6, 50, 0.3f, CombatPalette.Ambar with { A = 0.95f });
                _combat.Glow.Burst(h.Position, 26, CombatPalette.EcoLureBoom, new BurstOptions { V1 = 140 });
                _combat.Sfx("boom", h.Position);
                _combat.Shake(6);
                _combat.LureExplosion(h.Position, h.Damage);
                return false;

            case HazardKind.RainArrow:
                if (h.T >= (float)UltimateNumbers.BowImpact && !h.Done)
                {
                    h.Done = true;
                    _combat.Fx.Burst(h.Position, 4, CombatPalette.Crit, new BurstOptions { V1 = 50, Gravity = 60 });
                    _combat.RainArrowImpact(h.Position, h.Damage);
                }

                return h.T < h.Max;
        }

        return false;
    }

    private static float EllipseDistance(Vector2 d, float yScale) => MathF.Sqrt((d.X * d.X) + (d.Y * yScale * d.Y * yScale));

    /// <summary>Parte no chão (telegraphs e a onda), abaixo das entidades.</summary>
    public override void _Draw()
    {
        var pulse = 0.55f + (0.45f * MathF.Sin(_time * 18));
        for (var i = 0; i < Capacity; i++)
        {
            ref var h = ref _pool[i];
            if (h.Kind == HazardKind.None || h.T < 0)
            {
                continue;
            }

            switch (h.Kind)
            {
                case HazardKind.Spawn:
                {
                    var k = h.T / h.Max;
                    Telegraph(this, h.Position, 6 + (12 * k), 0.8f, k, CombatPalette.TelegraphFill);
                    break;
                }

                case HazardKind.AnglerLure:
                    Telegraph(this, h.Position, 44, pulse, h.T / h.Max, CombatPalette.TelegraphFill);
                    Telegraph(this, h.Position, 75, 0.25f, 0, CombatPalette.TelegraphFill);
                    break;
                case HazardKind.TideWave:
                    DrawSetTransform(h.Position, 0, new Vector2(1, 1 / 1.4f));
                    DrawArc(Vector2.Zero, h.Radius, 0, MathF.Tau, 40, WaveColor, 3);
                    DrawArc(Vector2.Zero, h.Radius, 0, MathF.Tau, 40, CombatPalette.Brecha with { A = 0.6f }, 1);
                    DrawSetTransform(Vector2.Zero);
                    break;
                case HazardKind.Shard when h.T < 0.9f:
                    Telegraph(this, h.Position, 16, pulse, h.T / 0.9f, CombatPalette.TelegraphFill);
                    break;
                case HazardKind.PlayerLure when h.Landed:
                    Telegraph(this, h.Position, 50, 0.5f, h.T / h.Max, CombatPalette.LureFill);
                    break;
            }
        }
    }

    /// <summary>Parte no ar (lanternas, estilhaços caindo, isca, flechas), desenhada pela camada de efeitos de cima.</summary>
    public void DrawAir(FxLayer layer)
    {
        for (var i = 0; i < Capacity; i++)
        {
            ref var h = ref _pool[i];
            if (h.Kind == HazardKind.None || h.T < 0)
            {
                continue;
            }

            switch (h.Kind)
            {
                case HazardKind.AnglerLure:
                    layer.DrawTextureRect(_glow, new Rect2(h.Position + new Vector2(-14, -26), new Vector2(28, 28)), false, CombatPalette.Brecha with { A = 0.6f });
                    Diamond(layer, h.Position + new Vector2(0, -6), 3, CombatPalette.Ambar);
                    break;
                case HazardKind.Shard when h.T > 0.6f && h.T < 1f:
                {
                    var fy = Mathf.Lerp(h.Position.Y - 80, h.Position.Y, (h.T - 0.6f) / 0.3f);
                    Diamond(layer, new Vector2(h.Position.X, fy - 4), 4, ShardDark);
                    layer.DrawRect(new Rect2(h.Position.X - 0.5f, fy - 8, 1, 8), h.Color);
                    break;
                }

                case HazardKind.OrbitLantern:
                    layer.DrawTextureRect(_glow, new Rect2(h.Position - new Vector2(10, 10), new Vector2(20, 20)), false, CombatPalette.Cyan with { A = 0.7f });
                    Diamond(layer, h.Position, 3, CombatPalette.Cyan);
                    break;
                case HazardKind.PlayerLure:
                    layer.DrawTextureRect(_glow, new Rect2(h.Position - new Vector2(12, 12), new Vector2(24, 24)), false, CombatPalette.Ambar with { A = 0.7f });
                    Diamond(layer, h.Position, 3, CombatPalette.Ambar);
                    break;
                case HazardKind.RainArrow:
                {
                    var k = MathF.Min(1, h.T / (float)UltimateNumbers.BowImpact);
                    var y0 = h.Position.Y - (70 * (1 - k));
                    var a = 1 - (MathF.Max(0, h.T - (float)UltimateNumbers.BowImpact) * 3);
                    layer.DrawLine(new Vector2(h.Position.X - (6 * (1 - k)), y0 - 14), new Vector2(h.Position.X, y0), RainColor with { A = a }, 1.5f);
                    break;
                }
            }
        }
    }

    private static readonly Color ShardDark = new("#2a1a35");
    private static readonly Color WaveColor = new(127 / 255f, 208 / 255f, 232 / 255f, 0.9f);
    private static readonly Color RainColor = new(1, 226 / 255f, 160 / 255f);

    /// <summary>Círculo de aviso no chão (<c>tgCircle</c>): elipse ×0.72, preenchimento ×0.35.</summary>
    public static void Telegraph(CanvasItem c, Vector2 at, float radius, float outlineAlpha, float fill, Color color)
    {
        c.DrawSetTransform(at, 0, new Vector2(1, 0.72f));
        if (fill > 0)
        {
            c.DrawCircle(Vector2.Zero, radius, color with { A = fill * 0.35f });
        }

        c.DrawArc(Vector2.Zero, radius, 0, MathF.Tau, 32, (color == CombatPalette.TelegraphFill ? CombatPalette.TelegraphLine : color) with { A = outlineAlpha }, 1.2f);
        c.DrawSetTransform(Vector2.Zero);
    }

    public static void Diamond(CanvasItem c, Vector2 at, float r, Color color)
    {
        c.DrawSetTransform(at, Mathf.Pi / 4);
        c.DrawRect(new Rect2(-r * 0.7f, -r * 0.7f, r * 1.4f, r * 1.4f), color);
        c.DrawSetTransform(Vector2.Zero);
    }

    private ref Hazard Add(HazardKind kind, Vector2 at, float max, double damage)
    {
        ref var h = ref Allocate();
        h.Kind = kind;
        h.Position = at;
        h.Max = max;
        h.Damage = damage;
        return ref h;
    }

    private ref Hazard Allocate()
    {
        for (var i = 0; i < Capacity; i++)
        {
            if (_pool[i].Kind == HazardKind.None)
            {
                _pool[i] = default;
                _live++;
                return ref _pool[i];
            }
        }

        // Cheio: reusa o primeiro (raro; 128 perigos simultâneos).
        Remove(ref _pool[0]);
        _pool[0] = default;
        _live++;
        return ref _pool[0];
    }

    private void Remove(ref Hazard h)
    {
        if (h.Kind == HazardKind.Spawn && h.MonsterId is not null)
        {
            PendingSpawns--;
            if (h.Invasion)
            {
                PendingInvasionSpawns--;
            }
        }

        h.Kind = HazardKind.None;
        h.Boss = null;
        h.MonsterId = null;
        _live = Math.Max(0, _live - 1);
    }

    private struct Hazard
    {
        public HazardKind Kind;
        public Vector2 Position;
        public Vector2 Target;
        public float T;
        public float Max;
        public float Radius;
        public float Angle;
        public float Cd;
        public double Damage;
        public bool Done;
        public bool Landed;
        public bool Chase;
        public bool Invasion;
        public string? MonsterId;
        public MonsterBody? Boss;
        public Color Color;
    }
}
