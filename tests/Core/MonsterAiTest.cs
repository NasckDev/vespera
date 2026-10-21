namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Rules;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>IA dos monstros (spec 02 §3.10) e golpe no monstro (§3.4), com tempos da v2 e as correções D6/D39.</summary>
[TestSuite]
public class MonsterAiTest
{
    private const double Dt = 1.0 / 60;

    private static MonsterBody Spawn(string id, double x = 0, double y = 0, int rank = 0)
    {
        var db = TestDb.Instance;
        var scaled = new MonsterScaling(db).Scale(db.Monsters[id], rank, null);
        var m = new MonsterBody(scaled, x, y, db.Monsters[id].HitHeight ?? 20, new ConstantRandom(0.5));
        m.SpawnT = 0;
        return m;
    }

    private static void Run(MonsterBody m, FakeWorld w, double seconds, PlayerView p)
    {
        for (var t = 0.0; t < seconds; t += Dt)
        {
            MonsterAi.Step(m, Dt, in p, w, new ConstantRandom(0.5));
        }
    }

    [TestCase]
    public void LungerWindsUpLungesOnceAndRecovers()
    {
        var wolf = Spawn("wolf");
        wolf.Cd = 0;
        var w = new FakeWorld();
        var p = new PlayerView(20, 0, 0, false); // dentro do alcance (r + 24 = 34)
        MonsterAi.Step(wolf, Dt, in p, w, new ConstantRandom(0.5));
        AssertThat(wolf.State).IsEqual(MonsterState.Wind);
        AssertThat(w.Sounds).IsEqual(1); // growl

        Run(wolf, w, 0.40, p);
        AssertThat(wolf.State).IsEqual(MonsterState.Wind); // windup de 0,45 s
        Run(wolf, w, 0.1, p);
        AssertThat(wolf.State).IsEqual(MonsterState.Lunge);

        Run(wolf, w, 0.25, p);
        AssertThat(w.Hurts.Count).IsEqual(1);
        AssertThat(w.Hurts[0]).IsEqual(12.0);
        AssertThat(wolf.State).IsEqual(MonsterState.Rec);
        Run(wolf, w, 0.72, p);
        AssertThat(wolf.State).IsEqual(MonsterState.Chase);
    }

    [TestCase]
    public void BrokenJawWeakensTheBiteAndMissingPartsDoNot()
    {
        var wolf = Spawn("wolf");
        wolf.DamagePart(0, 999);
        wolf.Cd = 0;
        var w = new FakeWorld();
        var p = new PlayerView(20, 0, 0, false);
        Run(wolf, w, 1.0, p);
        AssertThat(w.Hurts[0]).IsEqualApprox(12 * 0.55, 1e-9);

        // D39: o Pescador usa a parte de índice 0 (Isca) no papel da mandíbula; intacta = dano cheio.
        var fishm = Spawn("fishm");
        fishm.Cd = 0;
        var w2 = new FakeWorld();
        Run(fishm, w2, 1.0, p);
        AssertThat(w2.Hurts[0]).IsEqual(14.0);
    }

    [TestCase]
    public void IdleLungerIgnoresFarPlayerUntilHit()
    {
        var wolf = Spawn("wolf");
        var w = new FakeWorld();
        var far = new PlayerView(300, 0, 0, false);
        Run(wolf, w, 0.5, far);
        AssertThat(wolf.State).IsEqual(MonsterState.Idle);
        AssertThat(w.Moves).IsEqual(0);

        var calc = new DamageCalculator(TestDb.Instance.Rules.Combat);
        MonsterHitResolver.Apply(wolf, 5, 300, 0, new HitOptions(Knockback: 90), 0, calc, new ConstantRandom(0.5));
        AssertThat(wolf.State).IsEqual(MonsterState.Chase);
        Run(wolf, w, 0.1, far);
        AssertThat(w.Moves > 0).IsTrue();
    }

    [TestCase]
    public void CasterShootsUntilItsFrontPartBreaks()
    {
        var cnev = Spawn("cnev");
        cnev.Cd = 0;
        var w = new FakeWorld();
        var p = new PlayerView(100, 0, 0, false);
        Run(cnev, w, 0.8, p);
        AssertThat(w.Projectiles).IsEqual(1); // D6: lança (no protótipo nunca lançava)

        var broken = Spawn("cnev");
        broken.DamagePart(0, 999);
        broken.Cd = 0;
        var w2 = new FakeWorld();
        Run(broken, w2, 3, p);
        AssertThat(w2.Projectiles).IsEqual(0);
    }

    [TestCase]
    public void SpitterFiresFanOfThreeAndOneWithHornBroken()
    {
        var mbes = Spawn("mbes");
        mbes.Cd = 0;
        var w = new FakeWorld();
        var p = new PlayerView(80, 0, 0, false);
        Run(mbes, w, 0.6, p);
        AssertThat(w.Projectiles).IsEqual(3);

        var gflo = Spawn("gflo"); // triple
        gflo.Cd = 0;
        var w2 = new FakeWorld();
        Run(gflo, w2, 0.6, p);
        AssertThat(w2.Projectiles).IsEqual(5);

        var horn = Spawn("mbes");
        horn.DamagePart(0, 999);
        horn.Cd = 0;
        var w3 = new FakeWorld();
        Run(horn, w3, 0.6, p);
        AssertThat(w3.Projectiles).IsEqual(1);
    }

    [TestCase]
    public void BruteSlamHitsAfterThreeQuartersOfASecond()
    {
        var gmus = Spawn("gmus");
        gmus.Cd = 0;
        gmus.Face = 1;
        var w = new FakeWorld();
        var p = new PlayerView(20, 0, 0, false);
        Run(gmus, w, 0.74, p);
        AssertThat(gmus.State).IsEqual(MonsterState.Slam);
        AssertThat(w.Hurts.Count).IsEqual(0);
        Run(gmus, w, 0.03, p);
        AssertThat(w.Hurts.Count).IsEqual(1);
        AssertThat(w.Hurts[0]).IsEqual(20.0);
    }

    [TestCase]
    public void BossSummonsOnceBelowHalfHealthWithItsToast()
    {
        var angler = Spawn("angler");
        var w = new FakeWorld();
        var p = new PlayerView(200, 0, 0, false);
        angler.Hp = angler.MaxHp * 0.49;
        Run(angler, w, 0.5, p);
        AssertThat(w.Summons).IsEqual(2);
        AssertThat(w.Toasts.Count).IsEqual(1);
        AssertThat(w.Toasts[0]).IsEqual("O Abissal chama a maré");
        Run(angler, w, 0.5, p);
        AssertThat(w.Summons).IsEqual(2);
    }

    [TestCase]
    public void KnightRainDropsSevenShardsInPhaseTwoFirstOnThePlayer()
    {
        var knight = Spawn("colosso");
        var w = new FakeWorld();
        var p = new PlayerView(40, 10, 0, false);
        knight.Summoned = true;
        knight.Hp = knight.MaxHp * 0.4;
        knight.SetState(MonsterState.Rain);
        Run(knight, w, 0.45, p);
        AssertThat(w.Shards.Count).IsEqual(7);
        AssertThat(w.Shards[0]).IsEqual((40.0, 10.0, 0.0));
        AssertThat(w.Shards[6].Delay).IsEqualApprox(0.72, 1e-9);
    }

    [TestCase]
    public void KnightSummonRespectsTheFourAddsCap()
    {
        var knight = Spawn("rainha");
        var w = new FakeWorld { Alive = 3 };
        var p = new PlayerView(200, 0, 0, false);
        knight.Summoned = true;
        knight.SetState(MonsterState.Summon);
        Run(knight, w, 0.65, p);
        AssertThat(w.Summons).IsEqual(1); // min(3, 4 − 3)
    }

    [TestCase]
    public void HeraldSummonsOrbitLanternsInsteadOfAdds()
    {
        var herald = Spawn("knight");
        var w = new FakeWorld();
        var p = new PlayerView(200, 0, 0, false);
        herald.Summoned = true;
        herald.SetState(MonsterState.Summon);
        Run(herald, w, 0.65, p);
        AssertThat(w.Orbits).IsEqual(3);
        AssertThat(w.Summons).IsEqual(0);
    }

    [TestCase]
    public void ReaperTeleportsBehindTheAimAndSwipes()
    {
        var reaper = Spawn("reaper", 100, 0);
        var w = new FakeWorld();
        var p = new PlayerView(0, 0, 0, false); // mira para a direita: atrás = (−42, 0)
        reaper.SetState(MonsterState.Tele);
        Run(reaper, w, 0.5, p);
        AssertThat(reaper.X).IsEqualApprox(-42.0, 1e-9);
        Run(reaper, w, 0.3, p);
        AssertThat(reaper.State).IsEqual(MonsterState.Swipe);
        AssertThat(reaper.Alpha).IsEqual(1.0);
    }

    [TestCase]
    public void StunnedMonsterDoesNothing()
    {
        var wolf = Spawn("wolf");
        wolf.Stun = 1;
        var w = new FakeWorld();
        Run(wolf, w, 0.9, new PlayerView(20, 0, 0, false));
        AssertThat(w.Moves).IsEqual(0);
        AssertThat(wolf.State).IsEqual(MonsterState.Idle);
    }

    [TestCase]
    public void HitDamagesFrontPartFirstBreaksItAndAppliesKnockback()
    {
        var calc = new DamageCalculator(TestDb.Instance.Rules.Combat);
        var wolf = Spawn("wolf");
        wolf.Face = 1;
        // golpe vindo da direita (frente): Mandíbula 16 HP; dano 12 × parte 2.2 = 26.4 quebra
        var outcome = MonsterHitResolver.Apply(wolf, 12, 10, 0, new HitOptions(Part: 2.2, Knockback: 90), 0, calc, new ConstantRandom(0.5));
        AssertThat(outcome.Side).IsEqual(Vespera.Core.Data.PartSide.Front);
        AssertThat(outcome.Hit.Damage).IsEqual(12);
        AssertThat(outcome.BrokenPart).IsEqual(0);
        AssertThat(wolf.IsPartBroken(0)).IsTrue();
        AssertThat(wolf.Hp).IsEqual(28.0);
        AssertThat(wolf.Kx).IsEqualApprox(-90.0, 1e-9);
        AssertThat(wolf.Flash).IsEqual(0.12);

        // pelas costas com adagas: ×2
        var back = MonsterHitResolver.Apply(wolf, 6, -10, 0, new HitOptions(Backstab: 2), 0, calc, new ConstantRandom(0.5));
        AssertThat(back.Side).IsEqual(Vespera.Core.Data.PartSide.Back);
        AssertThat(back.Hit.Damage).IsEqual(12);
    }

    [TestCase]
    public void BossesIgnoreKnockbackAndElitesTakeAThird()
    {
        var calc = new DamageCalculator(TestDb.Instance.Rules.Combat);
        var boss = Spawn("angler");
        MonsterHitResolver.Apply(boss, 10, 10, 0, new HitOptions(Knockback: 200), 0, calc, new ConstantRandom(0.5));
        AssertThat(boss.Kx).IsEqual(0.0);
        var elite = Spawn("wolfE");
        MonsterHitResolver.Apply(elite, 10, 10, 0, new HitOptions(Knockback: 100), 0, calc, new ConstantRandom(0.5));
        AssertThat(elite.Kx).IsEqualApprox(-35.0, 1e-9);
    }

    [TestCase]
    public void KillingBlowMarksDead()
    {
        var calc = new DamageCalculator(TestDb.Instance.Rules.Combat);
        var wolf = Spawn("wolf");
        var outcome = MonsterHitResolver.Apply(wolf, 999, 10, 0, default, 0, calc, new ConstantRandom(0.5));
        AssertThat(outcome.Killed).IsTrue();
        AssertThat(wolf.Dead).IsTrue();
        AssertThat(wolf.IsTargetable).IsFalse();
    }

    [TestCase]
    public void DummyTakesHitsButNeverLosesHealth()
    {
        var calc = new DamageCalculator(TestDb.Instance.Rules.Combat);
        var dummy = MonsterBody.CreateDummy(155, 455);
        var outcome = MonsterHitResolver.Apply(dummy, 12, 100, 455, default, 0, calc, new ConstantRandom(0.5));
        AssertThat(outcome.Hit.Damage).IsEqual(12);
        AssertThat(outcome.Side).IsEqual(Vespera.Core.Data.PartSide.Back);
        AssertThat(dummy.Dead).IsFalse();
    }

    [TestCase]
    public void SeparationPushesOverlappingMonstersApart()
    {
        var a = Spawn("wolf", 0, 0);
        var b = Spawn("wolf", 5, 0);
        var w = new FakeWorld();
        MonsterAi.Separate(a, b, w);
        AssertThat(a.X).IsEqualApprox(-((16 - 5) * 0.4), 1e-9);
    }

    private sealed class FakeWorld : IMonsterWorld
    {
        public int Alive { get; set; }

        public int AliveNonBossCount => Alive;

        public int Moves { get; private set; }

        public int Projectiles { get; private set; }

        public int Summons { get; private set; }

        public int Orbits { get; private set; }

        public int Sounds { get; private set; }

        public List<double> Hurts { get; } = [];

        public List<string> Toasts { get; } = [];

        public List<(double X, double Y, double Delay)> Shards { get; } = [];

        public void Move(MonsterBody m, double dx, double dy)
        {
            Moves++;
            m.X += dx;
            m.Y += dy;
        }

        public bool CanStand(double x, double y, double radius, bool fly) => true;

        public void ChaseDirection(MonsterBody m, ref double ux, ref double uy)
        {
        }

        public bool HurtPlayer(double damage, double sourceX, double sourceY)
        {
            Hurts.Add(damage);
            return true;
        }

        public void FireProjectile(MonsterBody owner, MonsterProjectileKind kind, string color, double x, double y, double vx, double vy, double radius, double damage, double life) => Projectiles++;

        public void Summon(MonsterBody summoner, string monsterId, int count, double minDistance, double yRange, bool chase) => Summons += count;

        public void Sound(MonsterBody m, MonsterSound sound) => Sounds++;

        public void Fx(MonsterBody m, MonsterFx fx, double x, double y, double angle)
        {
        }

        public void Shake(double amount)
        {
        }

        public void Toast(string title, string subtitle) => Toasts.Add(title);

        public void SpawnLure(double x, double y, double damage)
        {
        }

        public void SpawnTideWave(double x, double y, double damage)
        {
        }

        public void SpawnShard(double x, double y, double delay, double damage, string color) => Shards.Add((x, y, delay));

        public void SpawnOrbitLantern(MonsterBody boss, double angle, double firstShot) => Orbits++;
    }
}
