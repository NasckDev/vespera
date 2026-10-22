namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Core.State;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// QA da frente B1 (tester de experiência): limites exatos dos números do combate da v2 (spec 02 §3.2-3.6, §3.12):
/// janela do combo, i-frames, carga do arco, windup do martelo, esquiva, crítico × costas × fraqueza, knockback,
/// poção e ímã dos drops. Cada teste prende o valor na fronteira, para uma mudança de número aparecer aqui.
/// </summary>
[TestSuite]
public class CombateQaTest
{
    private const double Frame = 1.0 / 60;

    private static PlayerCombatState NewState() => new(6);

    private static DamageCalculator Calc => new(TestDb.Instance.Rules.Combat);

    private static MonsterBody Spawn(string id)
    {
        var db = TestDb.Instance;
        var scaled = new MonsterScaling(db).Scale(db.Monsters[id], 0, null);
        var m = new MonsterBody(scaled, 0, 0, db.Monsters[id].HitHeight ?? 20, new ConstantRandom(0.5));
        m.SpawnT = 0;
        return m;
    }

    [TestCase]
    public void TestaJanelaDoComboDaLaminaNaFronteiraDe06s()
    {
        var sword = TestDb.Instance.Weapons["sword"];
        var s = NewState();
        AssertThat(s.StartMelee(sword).ComboStep).IsEqual(0);
        s.TickAttackTimers(0.59);
        AssertThat(s.StartMelee(sword).ComboStep).IsEqual(1); // ainda dentro de 0,6 s
        s.TickAttackTimers(0.61);
        AssertThat(s.StartMelee(sword).ComboStep).IsEqual(0); // passou da janela: recomeça
    }

    [TestCase]
    public void TestaTerceiroGolpeDaLaminaComNumerosDaV2()
    {
        var sword = TestDb.Instance.Weapons["sword"];
        var s = NewState();
        s.StartMelee(sword);
        s.TickAttackTimers(0.32);
        s.StartMelee(sword);
        s.TickAttackTimers(0.32);
        var third = s.StartMelee(sword);
        AssertThat(third.ComboStep).IsEqual(2);
        AssertThat(third.Big).IsTrue();
        AssertThat(third.DamageMult).IsEqual(1.6);
        AssertThat(third.Arc).IsEqualApprox(2.1 * 1.15, 1e-9);
        AssertThat(third.Range).IsEqualApprox(36.0, 1e-9);
        AssertThat(third.Knockback).IsEqual(160.0);
        AssertThat(s.Cooldown).IsEqualApprox(0.48, 1e-9); // recarga ×1,5
        s.TickAttackTimers(0.32);
        AssertThat(s.StartMelee(sword).ComboStep).IsEqual(0); // depois do 3º volta ao 1º
    }

    [TestCase]
    public void TestaIFramesDe075sNaFronteira()
    {
        var s = NewState();
        AssertThat(s.TryHurt()).IsTrue();
        s.TickTimers(0.74);
        AssertThat(s.TryHurt()).IsFalse();
        s.TickTimers(0.02);
        AssertThat(s.TryHurt()).IsTrue();
        AssertThat(s.Invulnerable).IsEqual(PlayerCombatState.HurtInvulnerability);
    }

    [TestCase]
    public void TestaCargaDoArcoPerfuraSoAPartirDe085()
    {
        var almost = ArrowShot.From(0.84, 10);
        AssertThat(almost.Pierce).IsFalse();
        AssertThat(almost.Knockback).IsEqual(60.0);
        AssertThat(almost.Damage).IsEqual(21); // round(10 × (0,8 + 1,6 × 0,84)) = round(21,44)
        var full = ArrowShot.From(0.85, 10);
        AssertThat(full.Pierce).IsTrue();
        AssertThat(full.Knockback).IsEqual(120.0);
        AssertThat(full.Damage).IsEqual(22); // round(21,6)
        AssertThat(ArrowShot.From(0.9, 10).Speed).IsEqualApprox(496.0, 1e-9);
    }

    [TestCase]
    public void TestaArcoSeguradoDoisSegundosParaEm09ETiroRapidoEFraco()
    {
        var bow = TestDb.Instance.Weapons["bow"];
        var s = NewState();
        for (var i = 0; i < 120; i++)
        {
            AssertThat(s.UpdateBow(bow, Frame, held: true, attack: 10)).IsNull();
        }

        AssertThat(s.Charge).IsEqualApprox(PlayerCombatState.MaxCharge, 1e-9);
        var shot = s.UpdateBow(bow, Frame, held: false, attack: 10)!.Value;
        AssertThat(shot.Pierce).IsTrue();
        AssertThat(s.Cooldown).IsEqual(0.4);

        // toque de 1 frame: flecha fraca que não perfura
        s.TickAttackTimers(0.4);
        s.UpdateBow(bow, Frame, held: true, attack: 10);
        var quick = s.UpdateBow(bow, Frame, held: false, attack: 10)!.Value;
        AssertThat(quick.Pierce).IsFalse();
        AssertThat(quick.Damage).IsEqual(8);
    }

    [TestCase]
    public void TestaWindupDoMartelaSaiNo15oFrameA60Fps()
    {
        var hammer = TestDb.Instance.Weapons["hammer"];
        var s = NewState();
        s.StartSmash(hammer);
        AssertThat(s.Cooldown).IsEqual(0.95);
        for (var i = 1; i <= 14; i++)
        {
            AssertThat(s.TickWindup(Frame)).IsFalse(); // 14 frames = 0,233 s < 0,24 s
        }

        AssertThat(s.TickWindup(Frame)).IsTrue();
        AssertThat(s.AttackTime).IsEqual(PlayerCombatState.SmashAttackSeconds);
        var swing = PlayerCombatState.SmashSwing(hammer);
        AssertThat(swing.Arc).IsEqual(2.9);
        AssertThat(swing.Range).IsEqual(36.0);
        AssertThat(swing.Knockback).IsEqual(220.0);
    }

    [TestCase]
    public void TestaEsquivaA60FpsAndaEntre46E50uEGasta22()
    {
        var s = NewState();
        AssertThat(s.TryStartDash()).IsTrue();
        var moved = 0.0;
        var frames = 0;
        while (s.IsDashing)
        {
            s.TickTimers(Frame);
            moved += s.AdvanceDash(Frame);
            frames++;
            AssertThat(s.Energy).IsEqual(78.0); // não regenera durante a esquiva
        }

        // v2: dashT -= dt até ≤ 0 → 11 frames de 4,5 u a 60 FPS (o nominal 0,17 × 270 = 45,9 u)
        AssertThat(frames).IsEqual(11);
        AssertThat(moved).IsBetween(45.9, 49.6);
        AssertThat(s.IsInvulnerable).IsFalse();
    }

    [TestCase]
    public void TestaEnergiaAbaixoDe22BloqueiaEsquiva()
    {
        var s = NewState();
        for (var i = 0; i < 4; i++)
        {
            s.TryStartDash();
            s.AdvanceDash(1);
            s.TickTimers(1);
        }

        // 4 esquivas: 100 − 88 = 12; regenera 30/s fora da esquiva → precisa de ~0,34 s para chegar a 22
        AssertThat(s.Energy).IsEqualApprox(12.0, 1e-9);
        s.AdvanceDash(0.33);
        AssertThat(s.Energy).IsEqualApprox(21.9, 1e-9);
        AssertThat(s.TryStartDash()).IsFalse();
        s.AdvanceDash(0.01);
        AssertThat(s.TryStartDash()).IsTrue();
    }

    [TestCase]
    public void TestaVelocidadeAtacandoCarregandoEnoWindup()
    {
        var s = NewState();
        AssertThat(s.SpeedMultiplier(1)).IsEqual(1.0);
        s.StartMelee(TestDb.Instance.Weapons["sword"]);
        AssertThat(s.SpeedMultiplier(1)).IsEqualApprox(0.55, 1e-9);
        var bowState = NewState();
        bowState.UpdateBow(TestDb.Instance.Weapons["bow"], Frame, held: true, attack: 10);
        AssertThat(bowState.SpeedMultiplier(1)).IsEqualApprox(0.45, 1e-9);
        var hammer = NewState();
        hammer.StartSmash(TestDb.Instance.Weapons["hammer"]);
        AssertThat(hammer.SpeedMultiplier(1)).IsEqualApprox(0.2, 1e-9);
        var cold = NewState();
        cold.TryHurt(chillSeconds: 1.6);
        AssertThat(cold.SpeedMultiplier(1)).IsEqualApprox(0.65, 1e-9);
    }

    [TestCase]
    public void TestaCriticoCostasEFraquezaMultiplicam()
    {
        // crítico sorteado primeiro (0 < 100), depois a variação no meio (1,0)
        var backCrit = Calc.ToMonster(6, new MonsterHitContext(Vespera.Core.Data.PartSide.Back, 2, 100, null, [], []), new FixedRandom(0, 0.5));
        AssertThat(backCrit.Crit).IsTrue();
        AssertThat(backCrit.Mult).IsEqualApprox(3.2, 1e-9);
        AssertThat(backCrit.Damage).IsEqual(19); // round(19,2)

        var frontCrit = Calc.ToMonster(6, new MonsterHitContext(Vespera.Core.Data.PartSide.Front, 2, 100, null, [], []), new FixedRandom(0, 0.5));
        AssertThat(frontCrit.Mult).IsEqualApprox(1.6, 1e-9); // de frente as adagas não dobram

        var weakCrit = Calc.ToMonster(10, new MonsterHitContext(Vespera.Core.Data.PartSide.Front, 0, 100, "igneo", ["igneo"], ["igneo"]), new FixedRandom(0, 0.5));
        AssertThat(weakCrit.Element).IsEqual(ElementOutcome.Weak); // fraqueza vence resistência
        AssertThat(weakCrit.Damage).IsEqual(24);

        var resist = Calc.ToMonster(10, new MonsterHitContext(Vespera.Core.Data.PartSide.Front, 0, 0, "mare", [], ["mare"]), new FixedRandom(0.5, 0.5));
        AssertThat(resist.Damage).IsEqual(6);

        // variação: 0,92 a 1,08
        AssertThat(Calc.ToMonster(100, new MonsterHitContext(Vespera.Core.Data.PartSide.Front, 0, 0, null, [], []), new FixedRandom(0.5, 0)).Damage).IsEqual(92);
        AssertThat(Calc.ToMonster(100, new MonsterHitContext(Vespera.Core.Data.PartSide.Front, 0, 0, null, [], []), new FixedRandom(0.5, 0.999999)).Damage).IsEqual(108);
    }

    [TestCase]
    public void TestaKnockbackComumEliteEChefeComOMartelo()
    {
        var calc = Calc;
        var hit = new HitOptions(Part: 2.2, Knockback: PlayerCombatState.SmashKnockback);
        var wolf = Spawn("wolf");
        MonsterHitResolver.Apply(wolf, 1, -10, 0, hit, 0, calc, new ConstantRandom(0.5));
        AssertThat(wolf.Kx).IsEqualApprox(220.0, 1e-9);
        var alpha = Spawn("wolfE");
        MonsterHitResolver.Apply(alpha, 1, -10, 0, hit, 0, calc, new ConstantRandom(0.5));
        AssertThat(alpha.Kx).IsEqualApprox(220 * 0.35, 1e-9);
        foreach (var boss in new[] { "angler", "reaper", "colosso", "rainha", "knight" })
        {
            var b = Spawn(boss);
            MonsterHitResolver.Apply(b, 1, -10, 0, hit, 0, calc, new ConstantRandom(0.5));
            AssertThat(b.Kx).IsEqual(0.0);
            AssertThat(b.Ky).IsEqual(0.0);
        }
    }

    [TestCase]
    public void TestaPocaoCura45PorCentoComRecargaDeUmSegundo()
    {
        var gs = new GameState(TestDb.Instance, SaveData.NewGame(TestDb.Instance, "Rui", "sword"), new SeededRandom(1));
        var potions = gs.Save.Potions;
        AssertThat(gs.TryUsePotion(0, out _)).IsEqual(PotionResult.FullHealth);
        gs.Damage(60);
        AssertThat(gs.TryUsePotion(0, out var healed)).IsEqual(PotionResult.Used);
        AssertThat(healed).IsEqual(45);
        AssertThat(gs.Hp).IsEqual(85);
        AssertThat(gs.Save.Potions).IsEqual(potions - 1);
        AssertThat(gs.TryUsePotion(0.99, out _)).IsEqual(PotionResult.OnCooldown);
        AssertThat(gs.TryUsePotion(1.0, out _)).IsEqual(PotionResult.Used);
        AssertThat(gs.Hp).IsEqual(100);
    }

    [TestCase]
    public void TestaImaDosDropsRaio48Atraso045Coleta9()
    {
        var near = PickupMotion.Still(47, 0);
        near.T = 0.46;
        AssertThat(near.Magnet(0, 0, Frame)).IsFalse();
        AssertThat(near.X).IsLess(47.0); // dentro de 48 u: puxado

        var far = PickupMotion.Still(49, 0);
        far.T = 0.46;
        far.Magnet(0, 0, Frame);
        AssertThat(far.X).IsEqual(49.0);

        var early = PickupMotion.Still(1, 0);
        early.T = 0.44;
        AssertThat(early.Magnet(0, 0, Frame)).IsFalse(); // antes de 0,45 s nem colado é coletado
        AssertThat(early.X).IsEqual(1.0);

        var close = PickupMotion.Still(8.9, 0);
        close.T = 0.46;
        AssertThat(close.Magnet(0, 0, Frame)).IsTrue();

        // do raio 47 até coletar em menos de meio segundo a 60 FPS
        var pull = PickupMotion.Still(47, 0);
        pull.T = 0.46;
        var frames = 0;
        while (!pull.Magnet(0, 0, Frame) && frames < 120)
        {
            pull.T += Frame;
            frames++;
        }

        AssertThat(frames).IsLess(30);
    }

    [TestCase]
    public void TestaEssenciaPorAcertoEBonecosNaoDao()
    {
        var calc = Calc;
        AssertThat(calc.EssenceAfterHit(0, 1)).IsEqualApprox(3.2, 1e-9);
        AssertThat(calc.EssenceAfterHit(99, 1)).IsEqual(100.0);
        AssertThat(calc.EssenceAfterHit(10, 1, dummy: true)).IsEqual(10.0);
    }
}
