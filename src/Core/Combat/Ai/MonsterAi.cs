using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Combat.Ai;

/// <summary>
/// IA dos monstros da v2 como máquinas de estado puras (<c>updMob</c> + <c>AI</c>, <c>combat.js:258-405</c>; spec 02 §3.10),
/// com os tempos exatos do protótipo. Correções (DECISIONS D6/D39): parte inexistente não conta como quebrada; as IAs
/// genéricas consultam as partes por índice (0 = a da frente, 1 = a das costas), como o <c>knight</c> já fazia; o
/// <c>caster</c> usa <c>triple</c> dos dados e só sai de <c>idle</c> com o jogador a ≤ 230 u.
/// A separação entre monstros fica com quem tem a lista (<see cref="Separate"/>).
/// </summary>
public static class MonsterAi
{
    public const double LungerAggro = 220;
    public const double CasterAggro = 230;
    public const double SpitterAggro = 220;
    public const double BruteAggro = 200;

    private const string DefaultBoltColor = "#ff7a2a";
    private const string BurstColor = "#5fe0d0";
    private const string ChainColor = "#b8a37a";
    private const string DefaultBossColor = "#E64A8E";

    /// <summary>Um frame do monstro: timers, empurrão, puxão, isca, atordoamento e a IA do tipo.</summary>
    public static void Step(MonsterBody m, double dt, in PlayerView p, IMonsterWorld world, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(random);
        m.Flash = Math.Max(0, m.Flash - dt);
        if (m.IsDummy)
        {
            return;
        }

        if (m.Dead)
        {
            m.DeadT += dt;
            return;
        }

        m.T += dt;
        m.Bob += dt;
        if (m.SpawnT > 0)
        {
            m.SpawnT -= dt;
            return;
        }

        if (m.Kx != 0 || m.Ky != 0)
        {
            world.Move(m, m.Kx * dt, m.Ky * dt);
            var decay = Math.Pow(MonsterBody.KnockbackDecay, dt);
            m.Kx *= decay;
            m.Ky *= decay;
            if (Math.Abs(m.Kx) + Math.Abs(m.Ky) < 2)
            {
                m.Kx = m.Ky = 0;
            }
        }

        if (m.PullT > 0)
        {
            var f = Math.Min(1, dt * 10);
            world.Move(m, (m.PullX - m.X) * f, (m.PullY - m.Y) * f);
            m.PullT -= dt;
        }

        if (m.Lured is { } lure)
        {
            var lx = lure.X - m.X;
            var ly = lure.Y - m.Y;
            var ll = CombatMath.Hypot(lx, ly);
            if (ll == 0)
            {
                ll = 1;
            }

            if (ll > 8)
            {
                var sp = m.IsBoss ? 15 : 55;
                world.Move(m, lx / ll * sp * dt, ly / ll * sp * dt);
            }

            m.Lured = null;
        }

        if (m.Stun > 0)
        {
            m.Stun -= dt;
            m.ForceState(MonsterState.Idle);
            return;
        }

        var dx = p.X - m.X;
        var dy = p.Y - m.Y;
        var d = CombatMath.Hypot(dx, dy);
        if (d == 0)
        {
            d = 1;
        }

        var ctx = new Ctx(m, dt, dx, dy, d, p, world, random);
        switch (m.Def.Ai)
        {
            case AiKind.Lunger:
                Lunger(in ctx);
                break;
            case AiKind.Caster:
                Caster(in ctx);
                break;
            case AiKind.Spitter:
                Spitter(in ctx);
                break;
            case AiKind.Brute:
                Brute(in ctx);
                break;
            case AiKind.Angler:
                Angler(in ctx);
                break;
            case AiKind.Reaper:
                Reaper(in ctx);
                break;
            case AiKind.Knight:
                Knight(in ctx);
                break;
        }
    }

    /// <summary>Separação (<c>combat.js:271</c>): se mais perto que <c>(r1 + r2) × 0.8</c>, empurra <c>(mind − dist) × 0.4</c>.</summary>
    public static void Separate(MonsterBody m, MonsterBody other, IMonsterWorld world)
    {
        if (ReferenceEquals(m, other) || other.Dead || other.IsDummy || m.Dead || m.IsDummy || m.SpawnT > 0)
        {
            return;
        }

        var sx = m.X - other.X;
        var sy = m.Y - other.Y;
        var sd = CombatMath.Hypot(sx, sy);
        var mind = (m.Radius + other.Radius) * 0.8;
        if (sd > 0.01 && sd < mind)
        {
            var push = (mind - sd) * 0.4;
            world.Move(m, sx / sd * push, sy / sd * push);
        }
    }

    private readonly ref struct Ctx(MonsterBody m, double dt, double dx, double dy, double d, PlayerView p, IMonsterWorld w, IRandom r)
    {
        public readonly MonsterBody M = m;
        public readonly double Dt = dt;
        public readonly double Dx = dx;
        public readonly double Dy = dy;
        public readonly double D = d;
        public readonly PlayerView P = p;
        public readonly IMonsterWorld W = w;
        public readonly IRandom R = r;

        public void FacePlayer() => M.Face = Dx >= 0 ? 1 : -1;

        public void AimAtPlayer()
        {
            M.Ldx = Dx / D;
            M.Ldy = Dy / D;
        }

        /// <summary>Anda na direção do jogador; para frente (speed &gt; 0) contorna obstáculos pelo campo de perseguição.</summary>
        public void MoveTowards(double speed)
        {
            var ux = Dx / D;
            var uy = Dy / D;
            if (speed > 0)
            {
                W.ChaseDirection(M, ref ux, ref uy);
            }

            W.Move(M, ux * speed * Dt, uy * speed * Dt);
        }

        /// <summary>Direção de aproximação (unitária), contornando obstáculos.</summary>
        public (double X, double Y) Approach()
        {
            var ux = Dx / D;
            var uy = Dy / D;
            W.ChaseDirection(M, ref ux, ref uy);
            return (ux, uy);
        }
    }

    private static MonsterState Pick(ReadOnlySpan<MonsterState> options, IRandom random) =>
        options[Math.Min(options.Length - 1, (int)Math.Floor(random.NextDouble() * options.Length))];

    // --- lunger (wolf, wolfE, fishm, coss, mesc, gbro, kesc, ksom, kcav) ---
    private static void Lunger(in Ctx c)
    {
        var m = c.M;
        var jaw = m.IsPartBroken(0);
        var back = m.IsPartBroken(1);
        var spd = m.Speed * (back ? 0.75 : 1);
        var reach = m.Radius + 24;
        switch (m.State)
        {
            case MonsterState.Idle or MonsterState.Chase:
                if (c.P.Dead || (m.State == MonsterState.Idle && c.D > LungerAggro))
                {
                    return;
                }

                m.ForceState(MonsterState.Chase);
                m.Cd -= c.Dt;
                c.FacePlayer();
                if (c.D > reach)
                {
                    c.MoveTowards(spd);
                }
                else if (m.Cd <= 0)
                {
                    m.SetState(MonsterState.Wind);
                    c.AimAtPlayer();
                    c.W.Sound(m, MonsterSound.Growl);
                }
                else
                {
                    var ox = -c.Dy / c.D;
                    var oy = c.Dx / c.D;
                    c.W.Move(m, ox * spd * 0.5 * c.Dt, oy * spd * 0.5 * c.Dt);
                }

                break;
            case MonsterState.Wind:
                if (m.T > (m.IsElite ? 0.55 : 0.45))
                {
                    m.SetState(MonsterState.Lunge);
                }

                break;
            case MonsterState.Lunge:
                var lungeSpeed = m.IsElite ? 250 : 230;
                c.W.Move(m, m.Ldx * lungeSpeed * c.Dt, m.Ldy * lungeSpeed * c.Dt);
                if (!m.Done && c.D < m.Radius + 9)
                {
                    m.Done = true;
                    c.W.HurtPlayer(m.Damage * (jaw ? 0.55 : 1), m.X, m.Y);
                }

                if (m.T > 0.24)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Rec:
                if (m.T > 0.7)
                {
                    m.SetState(MonsterState.Chase);
                    m.Cd = c.R.Range(0.4, 1.1);
                }

                break;
        }
    }

    // --- caster (cnev, ksin) ---
    private static void Caster(in Ctx c)
    {
        var m = c.M;
        var glass = m.IsPartBroken(0);
        c.FacePlayer();
        if (m.State == MonsterState.Cast)
        {
            if (m.T > 0.6)
            {
                var a = Math.Atan2(c.Dy - 6, c.Dx);
                var n = m.Def.Triple ? 3 : 1;
                for (var i = 0; i < n; i++)
                {
                    var aa = a + ((i - ((n - 1) / 2.0)) * 0.25);
                    c.W.FireProjectile(m, MonsterProjectileKind.Bolt, m.Def.Bolt ?? DefaultBoltColor, m.X, m.Y - m.HitHeight, Math.Cos(aa) * 125, Math.Sin(aa) * 125, 4, m.Damage, 2.6);
                }

                c.W.Sound(m, MonsterSound.Magic);
                m.SetState(MonsterState.Idle);
                m.Cd = c.R.Range(1.8, 2.8);
            }

            return;
        }

        if (c.P.Dead || (m.State == MonsterState.Idle && c.D > CasterAggro))
        {
            return;
        }

        var want = glass ? 170 : 95;
        double mvx = 0, mvy = 0;
        if (c.D < want - 15)
        {
            mvx = -c.Dx / c.D;
            mvy = -c.Dy / c.D;
        }
        else if (c.D > want + 25)
        {
            (mvx, mvy) = c.Approach();
        }

        var s = Math.Sin((m.T * 0.8) + m.Bob);
        mvx += -c.Dy / c.D * 0.6 * s;
        mvy += c.Dx / c.D * 0.6 * s;
        c.W.Move(m, mvx * m.Speed * c.Dt, mvy * m.Speed * c.Dt);
        m.Cd -= c.Dt;
        if (!glass && m.Cd <= 0 && c.D < 210)
        {
            m.SetState(MonsterState.Cast);
        }
    }

    // --- spitter (cvel, mbes, gsei, gflo) ---
    private static void Spitter(in Ctx c)
    {
        var m = c.M;
        var horn = m.IsPartBroken(0);
        var shell = m.IsPartBroken(1);
        c.FacePlayer();
        if (m.State == MonsterState.Spit)
        {
            if (m.T > 0.55 && !m.Done)
            {
                m.Done = true;
                var a = Math.Atan2(c.Dy - 4, c.Dx);
                var n = horn ? 1 : (m.Def.Triple ? 5 : 3);
                var color = m.Def.Bolt ?? DefaultBoltColor;
                var ox = m.X + (m.Face * 10);
                var oy = m.Y - (m.HitHeight * 0.5);
                for (var i = 0; i < n; i++)
                {
                    var aa = a + ((i - ((n - 1) / 2.0)) * 0.3);
                    c.W.FireProjectile(m, MonsterProjectileKind.Bolt, color, ox, oy, Math.Cos(aa) * 115, Math.Sin(aa) * 115, 5, m.Damage, 2.2);
                }

                c.W.Fx(m, MonsterFx.SpitFlash, m.X + (m.Face * 12), oy, a);
                c.W.Sound(m, MonsterSound.Boom);
            }

            if (m.T > 0.9)
            {
                m.SetState(MonsterState.Chase);
                m.Cd = c.R.Range(1.6, 2.4);
            }

            return;
        }

        if (c.P.Dead || (m.State == MonsterState.Idle && c.D > SpitterAggro))
        {
            return;
        }

        m.ForceState(MonsterState.Chase);
        const double want = 85;
        var mv = c.D < want - 15 ? -1 : c.D > want + 30 ? 1 : 0;
        var s2 = Math.Sin((m.T * 0.9) + m.Bob);
        var sp = m.Speed * (shell ? 1.25 : 1);
        var (ax, ay) = mv > 0 ? c.Approach() : (c.Dx / c.D, c.Dy / c.D);
        c.W.Move(m, ((ax * mv) - (c.Dy / c.D * 0.5 * s2)) * sp * c.Dt, ((ay * mv) + (c.Dx / c.D * 0.5 * s2)) * sp * c.Dt);
        m.Cd -= c.Dt;
        if (m.Cd <= 0 && c.D < 200)
        {
            m.SetState(MonsterState.Spit);
        }
    }

    // --- brute (cosr, maut, msen, gmus) ---
    private static void Brute(in Ctx c)
    {
        var m = c.M;
        var core = m.IsPartBroken(0);
        var moss = m.IsPartBroken(1);
        if (m.State is MonsterState.Idle or MonsterState.Chase)
        {
            if (c.P.Dead || (m.State == MonsterState.Idle && c.D > BruteAggro))
            {
                return;
            }

            m.ForceState(MonsterState.Chase);
            c.FacePlayer();
            m.Cd -= c.Dt;
            if (c.D > m.Radius + 20)
            {
                c.MoveTowards(m.Speed * (moss ? 1.3 : 1));
            }
            else if (m.Cd <= 0)
            {
                m.SetState(MonsterState.Slam);
            }
        }
        else if (m.State == MonsterState.Slam)
        {
            if (m.T > 0.75 && !m.Done)
            {
                m.Done = true;
                var ix = m.X + (m.Face * 8);
                c.W.Fx(m, MonsterFx.SlamImpact, ix, m.Y, 0);
                c.W.Shake(4);
                c.W.Sound(m, MonsterSound.Boom);
                if (CombatMath.Hypot(c.P.X - ix, (c.P.Y - m.Y) * 1.3) < 38)
                {
                    c.W.HurtPlayer(m.Damage * (core ? 0.6 : 1), m.X, m.Y);
                }
            }

            if (m.T > 1.6)
            {
                m.SetState(MonsterState.Chase);
                m.Cd = c.R.Range(0.8, 1.5);
            }
        }
    }

    private static void PhaseTwoSummon(in Ctx c, string fallback, double minDistance, double yRange)
    {
        var m = c.M;
        m.Summoned = true;
        c.W.Summon(m, m.Def.Summon ?? fallback, 2, minDistance, yRange, chase: false);
        if (m.Def.Phase2 is { } toast)
        {
            c.W.Toast(toast.Title, toast.Sub);
        }
        else
        {
            c.W.Toast("O chefe enfurece", string.Empty); // combat.js:378
        }
    }

    // --- angler (O Abissal do Mercado) ---
    private static void Angler(in Ctx c)
    {
        var m = c.M;
        var lure = !m.IsPartBroken(0);
        var fins = !m.IsPartBroken(1);
        var spd = m.Speed * (fins ? 1 : 0.7);
        if (m.Hp < m.MaxHp * 0.5 && !m.Summoned)
        {
            PhaseTwoSummon(in c, "fishm", 40, 100);
        }

        switch (m.State)
        {
            case MonsterState.Idle or MonsterState.Chase:
                if (c.P.Dead)
                {
                    return;
                }

                m.ForceState(MonsterState.Chase);
                c.FacePlayer();
                if (c.D > m.Radius + 22)
                {
                    c.MoveTowards(spd);
                }

                m.Cd -= c.Dt;
                if (m.Cd <= 0)
                {
                    Span<MonsterState> o = stackalloc MonsterState[6];
                    var n = 0;
                    o[n++] = MonsterState.Bite;
                    o[n++] = MonsterState.Bite;
                    if (lure)
                    {
                        o[n++] = MonsterState.Lure;
                        o[n++] = MonsterState.Lure;
                    }

                    if (fins)
                    {
                        o[n++] = MonsterState.Tide;
                    }

                    if (c.D > 120)
                    {
                        o[n++] = MonsterState.Bite;
                    }

                    m.SetState(Pick(o[..n], c.R));
                    c.AimAtPlayer();
                    m.Lx = c.P.X + c.R.Range(-18, 18);
                    m.Ly = c.P.Y + c.R.Range(-18, 18);
                }

                break;
            case MonsterState.Bite:
                if (m.T < 0.65)
                {
                    c.FacePlayer();
                    c.AimAtPlayer();
                }
                else if (m.T < 1.0)
                {
                    c.W.Move(m, m.Ldx * 280 * c.Dt, m.Ldy * 280 * c.Dt);
                    if (!m.Done && c.D < m.Radius + 12)
                    {
                        m.Done = true;
                        c.W.HurtPlayer(m.Damage, m.X, m.Y);
                    }
                }
                else
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Lure:
                if (m.T > 0.5 && !m.Done)
                {
                    m.Done = true;
                    c.W.SpawnLure(m.Lx, m.Ly, m.Damage * 1.1);
                    c.W.Sound(m, MonsterSound.Magic);
                }

                if (m.T > 0.9)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Tide:
                if (m.T > 0.9 && !m.Done)
                {
                    m.Done = true;
                    c.W.SpawnTideWave(m.X, m.Y, m.Damage * 0.9);
                    c.W.Shake(6);
                    c.W.Sound(m, MonsterSound.Boom);
                }

                if (m.T > 1.3)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Rec:
                if (m.T > 0.8)
                {
                    m.SetState(MonsterState.Chase);
                    m.Cd = c.R.Range(0.5, 1.3);
                }

                break;
        }
    }

    // --- reaper (O Ceifador, voador) ---
    private static void Reaper(in Ctx c)
    {
        var m = c.M;
        var hood = !m.IsPartBroken(0);
        var chain = !m.IsPartBroken(1);
        if (m.Hp < m.MaxHp * 0.5 && !m.Summoned)
        {
            PhaseTwoSummon(in c, "coss", 50, 90);
        }

        switch (m.State)
        {
            case MonsterState.Idle or MonsterState.Chase:
                if (c.P.Dead)
                {
                    return;
                }

                m.ForceState(MonsterState.Chase);
                c.FacePlayer();
                m.Alpha = Math.Min(1, m.Alpha + (c.Dt * 3));
                if (c.D > 65)
                {
                    c.MoveTowards(m.Speed);
                }
                else if (c.D < 38)
                {
                    c.MoveTowards(-m.Speed * 0.6);
                }

                m.Cd -= c.Dt;
                if (m.Cd <= 0)
                {
                    Span<MonsterState> o = stackalloc MonsterState[6];
                    var n = 0;
                    o[n++] = MonsterState.Burst;
                    if (c.D < 80)
                    {
                        o[n++] = MonsterState.Swipe;
                        o[n++] = MonsterState.Swipe;
                    }

                    if (chain && c.D > 55)
                    {
                        o[n++] = MonsterState.Chain;
                        o[n++] = MonsterState.Chain;
                    }

                    if (hood)
                    {
                        o[n++] = MonsterState.Tele;
                    }

                    m.SetState(Pick(o[..n], c.R));
                    c.AimAtPlayer();
                }

                break;
            case MonsterState.Swipe:
                if (m.T < 0.45)
                {
                    c.AimAtPlayer();
                    c.FacePlayer();
                }
                else if (!m.Done)
                {
                    m.Done = true;
                    var a = Math.Atan2(m.Ldy, m.Ldx);
                    c.W.Fx(m, MonsterFx.SwipeArc, m.X, m.Y - (m.HitHeight * 0.5), a);
                    if (c.D < m.Radius + 40 && CombatMath.AngDiff(Math.Atan2(c.Dy, c.Dx), a) < 1.2)
                    {
                        c.W.HurtPlayer(m.Damage, m.X, m.Y);
                    }

                    c.W.Sound(m, MonsterSound.Swing);
                }

                if (m.T > 0.85)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Chain:
                if (m.T < 0.7)
                {
                    c.AimAtPlayer();
                    c.FacePlayer();
                }
                else if (!m.Done)
                {
                    m.Done = true;
                    c.W.FireProjectile(m, MonsterProjectileKind.Chain, ChainColor, m.X, m.Y - (m.HitHeight * 0.6), m.Ldx * 400, m.Ldy * 400, 6, m.Damage * 0.8, 0.6);
                    c.W.Sound(m, MonsterSound.Swing);
                }

                if (m.T > 1.1)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Burst:
                if (m.T > 0.8 && !m.Done)
                {
                    m.Done = true;
                    for (var j = 0; j < 12; j++)
                    {
                        var aa = (j / 12.0 * Math.PI * 2) + m.T;
                        c.W.FireProjectile(m, MonsterProjectileKind.Bolt, BurstColor, m.X, m.Y - (m.HitHeight * 0.6), Math.Cos(aa) * 105, Math.Sin(aa) * 105, 4, m.Damage * 0.7, 2.4);
                    }

                    c.W.Sound(m, MonsterSound.Magic);
                }

                if (m.T > 1.15)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Tele:
                if (m.T < 0.4)
                {
                    m.Alpha = 1 - (m.T / 0.4);
                }
                else if (!m.Done)
                {
                    m.Done = true;
                    var bx = c.P.X - (Math.Cos(c.P.Aim) * 42);
                    var by = c.P.Y - (Math.Sin(c.P.Aim) * 30);
                    if (c.W.CanStand(bx, by, 6, true))
                    {
                        m.X = bx;
                        m.Y = by;
                    }

                    c.W.Fx(m, MonsterFx.TeleportPuff, m.X, m.Y - 20, 0);
                }
                else
                {
                    m.Alpha = Math.Min(1, (m.T - 0.4) / 0.3);
                }

                if (m.T > 0.75)
                {
                    m.Alpha = 1;
                    m.SetState(MonsterState.Swipe);
                    m.T = 0.15;
                }

                break;
            case MonsterState.Rec:
                if (m.T > 0.7)
                {
                    m.SetState(MonsterState.Chase);
                    m.Cd = c.R.Range(0.4, 1.1);
                }

                break;
        }
    }

    // --- knight (Colosso, Rainha, Arauto) ---
    private static void Knight(in Ctx c)
    {
        var m = c.M;
        var halo = !m.IsPartBroken(0);
        var banner = !m.IsPartBroken(1);
        var lamps = !m.IsPartBroken(2);
        var ph2 = m.Hp < m.MaxHp * 0.5;
        var spd = m.Speed * (ph2 ? 1.3 : 1) * (banner ? 1 : 0.75);
        var color = m.Def.Color ?? DefaultBossColor;
        if (ph2 && !m.Summoned)
        {
            PhaseTwoSummon(in c, "ksom", 50, 90);
        }

        switch (m.State)
        {
            case MonsterState.Idle or MonsterState.Chase:
                if (c.P.Dead)
                {
                    return;
                }

                m.ForceState(MonsterState.Chase);
                c.FacePlayer();
                if (c.D > 58)
                {
                    c.MoveTowards(spd);
                }

                m.Cd -= c.Dt;
                if (m.Cd <= 0)
                {
                    Span<MonsterState> o = stackalloc MonsterState[5];
                    var n = 0;
                    o[n++] = MonsterState.Summon;
                    if (c.D < 90)
                    {
                        o[n++] = MonsterState.Sweep;
                        o[n++] = MonsterState.Sweep;
                    }

                    if (halo)
                    {
                        o[n++] = MonsterState.Rain;
                    }

                    if (banner && c.D > 70)
                    {
                        o[n++] = MonsterState.Charge;
                    }

                    m.SetState(Pick(o[..n], c.R));
                    c.AimAtPlayer();
                }

                break;
            case MonsterState.Sweep:
                if (m.T < 0.7)
                {
                    c.AimAtPlayer();
                    c.FacePlayer();
                }
                else if (!m.Done)
                {
                    m.Done = true;
                    var a = Math.Atan2(m.Ldy, m.Ldx);
                    c.W.Fx(m, MonsterFx.SweepArc, m.X, m.Y - 20, a);
                    c.W.Shake(6);
                    c.W.Sound(m, MonsterSound.Boom);
                    if (c.D < m.Radius + 60 && CombatMath.AngDiff(Math.Atan2(c.Dy, c.Dx), a) < 1.6)
                    {
                        c.W.HurtPlayer(m.Damage, m.X, m.Y);
                    }
                }

                if (m.T > 1.15)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Summon:
                if (m.T > 0.6 && !m.Done)
                {
                    m.Done = true;
                    var n = lamps ? 3 : 1;
                    if (m.Def.Orbs)
                    {
                        for (var k = 0; k < n; k++)
                        {
                            c.W.SpawnOrbitLantern(m, k * 2.094, 1 + (k * 0.35));
                        }
                    }
                    else if (m.Def.Summon is { } summon)
                    {
                        var count = Math.Min(n, 4 - c.W.AliveNonBossCount);
                        if (count > 0)
                        {
                            c.W.Summon(m, summon, count, 40, 80, chase: true);
                        }
                    }

                    c.W.Sound(m, MonsterSound.Magic);
                }

                if (m.T > 1.1)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Rain:
                if (m.T > 0.4 && !m.Done)
                {
                    m.Done = true;
                    var shards = ph2 ? 7 : 5;
                    for (var i = 0; i < shards; i++)
                    {
                        var rx = c.P.X + c.R.Range(-50, 50);
                        var ry = c.P.Y + c.R.Range(-40, 40);
                        if (i == 0)
                        {
                            rx = c.P.X;
                            ry = c.P.Y;
                        }

                        c.W.SpawnShard(rx, ry, i * 0.12, m.Damage * 0.8, color);
                    }
                }

                if (m.T > 1.2)
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Charge:
                if (m.T < 0.6)
                {
                    c.AimAtPlayer();
                    c.FacePlayer();
                }
                else if (m.T < 1)
                {
                    c.W.Move(m, m.Ldx * 320 * c.Dt, m.Ldy * 320 * c.Dt);
                    if (!m.Done && c.D < m.Radius + 12)
                    {
                        m.Done = true;
                        c.W.HurtPlayer(m.Damage * 1.1, m.X, m.Y);
                    }

                    c.W.Fx(m, MonsterFx.ChargeTrail, m.X, m.Y - 10, 0);
                }
                else
                {
                    m.SetState(MonsterState.Rec);
                }

                break;
            case MonsterState.Rec:
                if (m.T > (ph2 ? 0.5 : 0.8))
                {
                    m.SetState(MonsterState.Chase);
                    m.Cd = c.R.Range(0.4, 1.0);
                }

                break;
        }
    }
}
