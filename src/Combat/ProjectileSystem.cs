using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;

namespace Vespera.Combat;

public enum ProjectileKind
{
    /// <summary>Flecha do jogador (perfura se cheia).</summary>
    Arrow,

    /// <summary>Orbe teleguiado do Cajado (explode em área).</summary>
    Orb,

    /// <summary>Bolt do jogador (Lanternas Votivas).</summary>
    PlayerBolt,

    /// <summary>Corrente Votiva (Eco do Ceifador): atravessa paredes, puxa e atordoa.</summary>
    PlayerChain,

    /// <summary>Bolt de monstro.</summary>
    MonsterBolt,

    /// <summary>Corrente do Ceifador: atravessa paredes e puxa o jogador.</summary>
    MonsterChain,
}

/// <summary>
/// Todos os projéteis num pool fixo de structs, simulados e desenhados por um único nó (<c>updProjs</c>/<c>drawAir</c>,
/// <c>combat.js:408-440</c>, <c>render.js:191-195</c>; spec 02 §3.11). Sem nós por projétil e sem alocação por frame.
/// Camada aditiva (brilho).
/// </summary>
public partial class ProjectileSystem : Node2D
{
    public const int Capacity = 256;
    private const int MaxHits = 24;
    private const float OrbHomingRange = 90;
    private const float OrbTurnRate = 4;
    private const float OrbBlastRadius = 30;

    private static readonly Color PlayerChainColor = new("#d99cff");
    private static readonly Color MonsterChainColor = new("#b8a37a");

    private readonly Projectile[] _pool = new Projectile[Capacity];
    private readonly Random _random = new();
    private CombatDirector _combat = null!;
    private Texture2D _glow = null!;
    private int _next;
    private int _live;

    public int Live => _live;

    public void Setup(CombatDirector combat, Texture2D glow)
    {
        _combat = combat;
        _glow = glow;
        for (var i = 0; i < Capacity; i++)
        {
            _pool[i].Hits = new MonsterBody?[MaxHits];
        }
    }

    public void Clear()
    {
        for (var i = 0; i < Capacity; i++)
        {
            Deactivate(ref _pool[i]);
        }

        _live = 0;
    }

    /// <summary>Dispara (pega um slot livre; com o pool cheio, reusa o mais antigo).</summary>
    public void Fire(ProjectileKind kind, Vector2 at, Vector2 velocity, float radius, double damage, float life, Color color,
        float part = 1, float knockback = 0, bool pierce = false, bool full = false, string? element = null, MonsterBody? source = null, Vector2? origin = null)
    {
        ref var q = ref Allocate();
        q.Active = true;
        q.Kind = kind;
        q.Position = at;
        q.Velocity = velocity;
        q.Origin = origin ?? at;
        q.Radius = radius;
        q.Damage = damage;
        q.Life = life;
        q.Color = color;
        q.Part = part;
        q.Knockback = knockback;
        q.Pierce = pierce;
        q.Full = full;
        q.Element = element;
        q.Source = source;
        q.HitCount = 0;
        _live++;
    }

    public void Step(float dt)
    {
        if (_live == 0)
        {
            QueueRedraw();
            return;
        }

        var player = _combat.PlayerFoot;
        var playerDead = _combat.PlayerDead;
        var live = 0;
        for (var i = 0; i < Capacity; i++)
        {
            ref var q = ref _pool[i];
            if (!q.Active)
            {
                continue;
            }

            q.Life -= dt;
            if (q.Kind == ProjectileKind.Orb)
            {
                Home(ref q, dt);
                if (_random.NextDouble() < 0.6)
                {
                    _combat.Glow.Burst(q.Position, 1, CombatPalette.OrbTrail, new BurstOptions { V1 = 10, Gravity = 0, L1 = 0.3f });
                }
            }

            q.Position += q.Velocity * dt;
            if (q.Kind is ProjectileKind.PlayerBolt or ProjectileKind.MonsterBolt && _random.NextDouble() < 0.5)
            {
                _combat.Glow.Burst(q.Position, 1, CombatPalette.Brechas, new BurstOptions { V1 = 8, Gravity = 0, L1 = 0.25f });
            }

            var chain = q.Kind is ProjectileKind.PlayerChain or ProjectileKind.MonsterChain;
            if (!chain && !_combat.IsWalkable(q.Position.X, q.Position.Y + 10))
            {
                if (q.Kind == ProjectileKind.Orb)
                {
                    OrbBoom(ref q);
                }
                else
                {
                    _combat.Fx.Burst(q.Position, 4, CombatPalette.Wall, new BurstOptions { V1 = 40 });
                }

                Deactivate(ref q);
                continue;
            }

            if (q.Kind is ProjectileKind.MonsterBolt or ProjectileKind.MonsterChain)
            {
                if (!playerDead && CombatMath.Hypot(player.X - q.Position.X, player.Y - 11 - q.Position.Y) < q.Radius + 6)
                {
                    var src = q.Position - (q.Velocity * 0.1f);
                    if (_combat.HurtPlayer(q.Damage, src))
                    {
                        if (q.Kind == ProjectileKind.MonsterChain && q.Source is { Dead: false } boss)
                        {
                            _combat.PullPlayerTowards(boss);
                        }

                        Deactivate(ref q);
                        continue;
                    }
                }
            }
            else if (HitMonsters(ref q))
            {
                Deactivate(ref q);
                continue;
            }

            if (q.Life <= 0)
            {
                if (q.Kind == ProjectileKind.Orb)
                {
                    OrbBoom(ref q);
                }

                Deactivate(ref q);
                continue;
            }

            live++;
        }

        _live = live;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < Capacity; i++)
        {
            ref var q = ref _pool[i];
            if (!q.Active)
            {
                continue;
            }

            switch (q.Kind)
            {
                case ProjectileKind.Arrow:
                {
                    var dir = q.Velocity.Normalized();
                    var color = q.Element is not null ? q.Color : q.Full ? CombatPalette.Ambar : CombatPalette.Creme;
                    DrawLine(q.Position - (dir * 9), q.Position, color, 1.4f);
                    if (q.Full || q.Element is not null)
                    {
                        DrawTextureRect(_glow, new Rect2(q.Position - new Vector2(8, 8), new Vector2(16, 16)), false, CombatPalette.Ambar with { A = 0.6f });
                    }

                    break;
                }

                case ProjectileKind.PlayerChain or ProjectileKind.MonsterChain:
                {
                    var player = q.Kind == ProjectileKind.PlayerChain;
                    DrawDashedLine(q.Origin, q.Position, player ? PlayerChainColor : MonsterChainColor, 1.5f, 3);
                    DrawDiamond(q.Position, 3, player ? PlayerChainColor : CombatPalette.Cyan);
                    break;
                }

                default:
                {
                    var r = q.Kind == ProjectileKind.Orb ? 7f : 4.5f;
                    DrawCircle(q.Position, r * 0.55f, q.Color with { A = 0.85f });
                    DrawTextureRect(_glow, new Rect2(q.Position - new Vector2(r * 2, r * 2), new Vector2(r * 4, r * 4)), false, q.Color with { A = 0.6f });
                    DrawRect(new Rect2(q.Position - new Vector2(0.5f, 0.5f), Vector2.One), Colors.White);
                    break;
                }
            }
        }
    }

    private void DrawDiamond(Vector2 at, float r, Color color)
    {
        DrawSetTransform(at, Mathf.Pi / 4);
        DrawRect(new Rect2(-r * 0.7f, -r * 0.7f, r * 1.4f, r * 1.4f), color);
        DrawSetTransform(Vector2.Zero);
    }

    /// <summary>Orbe: procura o monstro mais próximo a 90 u e gira até 4 rad/s.</summary>
    private void Home(ref Projectile q, float dt)
    {
        MonsterBody? best = null;
        var bestD = (double)OrbHomingRange;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (m.Dead || m.SpawnT > 0)
            {
                continue;
            }

            var d = CombatMath.Hypot(m.X - q.Position.X, m.Y - (m.HitHeight * 0.5) - q.Position.Y);
            if (d < bestD)
            {
                bestD = d;
                best = m;
            }
        }

        if (best is null)
        {
            return;
        }

        var target = Math.Atan2(best.Y - (best.HitHeight * 0.5) - q.Position.Y, best.X - q.Position.X);
        var current = Math.Atan2(q.Velocity.Y, q.Velocity.X);
        var speed = q.Velocity.Length();
        var na = CombatMath.TurnTowards(current, target, OrbTurnRate * dt);
        q.Velocity = new Vector2((float)Math.Cos(na), (float)Math.Sin(na)) * speed;
    }

    /// <summary>Projétil do jogador contra monstros. True = o projétil acabou.</summary>
    private bool HitMonsters(ref Projectile q)
    {
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (!m.IsTargetable || HasHit(ref q, m))
            {
                continue;
            }

            if (CombatMath.Hypot(m.X - q.Position.X, m.Y - (m.HitHeight * 0.5) - q.Position.Y) >= q.Radius + m.Radius)
            {
                continue;
            }

            switch (q.Kind)
            {
                case ProjectileKind.Orb:
                    OrbBoom(ref q);
                    return true;
                case ProjectileKind.PlayerChain:
                    _combat.ChainHit(m, q.Damage, q.Origin);
                    return true;
                default:
                    _combat.HitMob(m, q.Damage, q.Position - (q.Velocity * 0.08f), new HitOptions(q.Part, 0, q.Full ? 120 : 60, q.Element));
                    _combat.Sfx("hit", q.Position);
                    if (!q.Pierce)
                    {
                        return true;
                    }

                    if (q.HitCount < MaxHits)
                    {
                        q.Hits[q.HitCount++] = m;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool HasHit(ref Projectile q, MonsterBody m)
    {
        for (var i = 0; i < q.HitCount; i++)
        {
            if (ReferenceEquals(q.Hits[i], m))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary><c>orbBoom</c>: anel, partículas, dano em área <c>30 + m.r</c>, quebra props a 30 u.</summary>
    private void OrbBoom(ref Projectile q)
    {
        _combat.Fx.Ring(q.Position, 4, OrbBlastRadius, 0.25f, q.Color with { A = 0.9f });
        _combat.Glow.Burst(q.Position, 14, CombatPalette.OrbBoom, new BurstOptions { V1 = 100, Gravity = 0 });
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (!m.Dead && m.SpawnT <= 0 && CombatMath.Hypot(m.X - q.Position.X, m.Y - (m.HitHeight * 0.5) - q.Position.Y) < OrbBlastRadius + m.Radius)
            {
                _combat.HitMob(m, q.Damage, q.Position, new HitOptions(q.Part, 0, 80, q.Element));
            }
        }

        _combat.BreakPropsNear(q.Position, OrbBlastRadius);
        _combat.Sfx("boom", q.Position);
    }

    private ref Projectile Allocate()
    {
        for (var n = 0; n < Capacity; n++)
        {
            var i = (_next + n) % Capacity;
            if (!_pool[i].Active)
            {
                _next = (i + 1) % Capacity;
                return ref _pool[i];
            }
        }

        // Pool cheio: reusa o próximo (o mais antigo em ordem de disparo).
        ref var q = ref _pool[_next];
        Deactivate(ref q);
        _next = (_next + 1) % Capacity;
        return ref q;
    }

    private void Deactivate(ref Projectile q)
    {
        if (q.Active)
        {
            _live = Math.Max(0, _live - 1);
        }

        q.Active = false;
        q.Source = null;
        q.Element = null;
        if (q.Hits is not null)
        {
            Array.Clear(q.Hits, 0, q.HitCount);
        }

        q.HitCount = 0;
    }

    private struct Projectile
    {
        public bool Active;
        public ProjectileKind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Origin;
        public float Radius;
        public double Damage;
        public float Life;
        public float Part;
        public float Knockback;
        public bool Pierce;
        public bool Full;
        public string? Element;
        public Color Color;
        public MonsterBody? Source;
        public MonsterBody?[] Hits;
        public int HitCount;
    }
}
