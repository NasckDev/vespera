namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Combate do jogador (spec 02 §3.2-3.3, §3.6): esquiva, energia, i-frames, combo, arco, martelo, velocidade.</summary>
[TestSuite]
public class PlayerCombatStateTest
{
    private static PlayerCombatState NewState() => new(6);

    [TestCase]
    public void DashCostsEnergyLastsAndHasCooldown()
    {
        var s = NewState();
        AssertThat(s.TryStartDash()).IsTrue();
        AssertThat(s.Energy).IsEqualApprox(78.0, 1e-9);
        AssertThat(s.IsInvulnerable).IsTrue();
        AssertThat(s.TryStartDash()).IsFalse(); // ainda esquivando

        var moved = 0.0;
        for (var i = 0; i < 20 && s.IsDashing; i++)
        {
            s.TickTimers(0.01);
            moved += s.AdvanceDash(0.01);
        }

        AssertThat(moved).IsEqualApprox(270 * 0.17, 270 * 0.011); // 0,17 s a 270 u/s
        AssertThat(s.IsDashing).IsFalse();
        AssertThat(s.TryStartDash()).IsFalse(); // dashCd 0,32 conta em paralelo: ainda faltam ~0,15 s
        for (var i = 0; i < 16; i++)
        {
            s.TickTimers(0.01);
            s.AdvanceDash(0.01);
        }

        AssertThat(s.TryStartDash()).IsTrue();
    }

    [TestCase]
    public void EnergyRegeneratesThirtyPerSecondAndBlocksDashBelowCost()
    {
        var s = NewState();
        for (var i = 0; i < 4; i++)
        {
            AssertThat(s.TryStartDash()).IsTrue();
            s.AdvanceDash(0.17); // termina a esquiva (sem regen enquanto esquiva)
            s.TickTimers(0.5); // dashCd
        }

        AssertThat(s.Energy).IsEqualApprox(12.0, 1e-9);
        AssertThat(s.TryStartDash()).IsFalse();
        s.AdvanceDash(1.0);
        AssertThat(s.Energy).IsEqualApprox(42.0, 1e-9);
        s.AdvanceDash(10);
        AssertThat(s.Energy).IsEqual(100.0);
    }

    [TestCase]
    public void HurtGivesThreeQuarterSecondOfInvulnerability()
    {
        var s = NewState();
        AssertThat(s.TryHurt()).IsTrue();
        AssertThat(s.Invulnerable).IsEqual(0.75);
        AssertThat(s.HurtTime).IsEqual(0.25);
        AssertThat(s.TryHurt()).IsFalse();
        s.TickTimers(0.74);
        AssertThat(s.TryHurt()).IsFalse();
        s.TickTimers(0.02);
        AssertThat(s.TryHurt(chillSeconds: 1.6)).IsTrue();
        AssertThat(s.Chill).IsEqual(1.6);
    }

    [TestCase]
    public void DashDodgesDamage()
    {
        var s = NewState();
        s.TryStartDash();
        AssertThat(s.TryHurt()).IsFalse();
    }

    [TestCase]
    public void SwordComboCyclesWithinSixTenthsAndThirdHitIsStrong()
    {
        var db = TestDb.Instance;
        var sword = db.Weapons["sword"];
        var s = NewState();
        var a = s.StartMelee(sword);
        AssertThat(a.ComboStep).IsEqual(0);
        AssertThat(a.Knockback).IsEqual(90.0);
        AssertThat(s.Cooldown).IsEqual(0.32);
        AssertThat(s.AttackTime).IsEqual(0.2);

        s.TickAttackTimers(0.35);
        var b = s.StartMelee(sword);
        AssertThat(b.ComboStep).IsEqual(1);

        s.TickAttackTimers(0.35);
        var c = s.StartMelee(sword);
        AssertThat(c.ComboStep).IsEqual(2);
        AssertThat(c.Big).IsTrue();
        AssertThat(c.DamageMult).IsEqual(1.6);
        AssertThat(c.Arc).IsEqualApprox(2.1 * 1.15, 1e-9);
        AssertThat(c.Range).IsEqualApprox(36.0, 1e-9);
        AssertThat(c.Knockback).IsEqual(160.0);
        AssertThat(s.Cooldown).IsEqualApprox(0.48, 1e-9);

        s.TickAttackTimers(0.61); // janela de 0,6 s expirou: volta ao 1º golpe
        AssertThat(s.StartMelee(sword).ComboStep).IsEqual(0);
    }

    [TestCase]
    public void DaggersAreQuickAndHaveNoCombo()
    {
        var dag = TestDb.Instance.Weapons["dag"];
        var s = NewState();
        var a = s.StartMelee(dag);
        AssertThat(a.Arc).IsEqual(1.4);
        AssertThat(a.Range).IsEqual(24.0);
        AssertThat(s.AttackTime).IsEqual(0.14);
        AssertThat(s.Cooldown).IsEqual(0.15);
        s.TickAttackTimers(0.2);
        AssertThat(s.StartMelee(dag).ComboStep).IsEqual(0);
    }

    [TestCase]
    public void HammerWindsUpBeforeTheSmash()
    {
        var hammer = TestDb.Instance.Weapons["hammer"];
        var s = NewState();
        s.StartSmash(hammer);
        AssertThat(s.Cooldown).IsEqual(0.95);
        AssertThat(s.SpeedMultiplier(1)).IsEqualApprox(0.2, 1e-9);
        AssertThat(s.TickWindup(0.2)).IsFalse();
        AssertThat(s.TickWindup(0.05)).IsTrue();
        AssertThat(s.AttackTime).IsEqual(0.28);
        var swing = PlayerCombatState.SmashSwing(hammer);
        AssertThat(swing.Arc).IsEqual(2.9);
        AssertThat(swing.Range).IsEqual(36.0);
        AssertThat(swing.Knockback).IsEqual(220.0);
    }

    [TestCase]
    public void BowChargesToNineTenthsAndFullArrowPierces()
    {
        var bow = TestDb.Instance.Weapons["bow"];
        var s = NewState();
        AssertThat(s.UpdateBow(bow, 0.5, held: true, attack: 10) is null).IsTrue();
        AssertThat(s.Charge).IsEqualApprox(0.5, 1e-9);
        AssertThat(s.SpeedMultiplier(1)).IsEqualApprox(0.45, 1e-9);
        s.UpdateBow(bow, 1.0, held: true, attack: 10);
        AssertThat(s.Charge).IsEqual(0.9);
        var shot = s.UpdateBow(bow, 0.016, held: false, attack: 10)!.Value;
        AssertThat(shot.Pierce).IsTrue();
        AssertThat(shot.Knockback).IsEqual(120.0);
        AssertThat(shot.Speed).IsEqualApprox(280 + (240 * 0.9), 1e-9);
        AssertThat(shot.Damage).IsEqual(22); // round(10 × (0.8 + 1.44))
        AssertThat(s.Cooldown).IsEqual(0.4);
        AssertThat(s.IsCharging).IsFalse();
    }

    [TestCase]
    public void QuickArrowDoesNotPierce()
    {
        var shot = ArrowShot.From(0.84, 10);
        AssertThat(shot.Pierce).IsFalse();
        AssertThat(shot.Knockback).IsEqual(60.0);
        AssertThat(ArrowShot.From(0, 10).Damage).IsEqual(8);
        AssertThat(ArrowShot.From(0.85, 10).Pierce).IsTrue();
    }

    [TestCase]
    public void DashCancelsChargeAndWindup()
    {
        var db = TestDb.Instance;
        var s = NewState();
        s.UpdateBow(db.Weapons["bow"], 0.3, true, 10);
        s.TryStartDash();
        AssertThat(s.IsCharging).IsFalse();
    }

    [TestCase]
    public void UltimateStartsWithSixSecondsAndEcoHasEightTenths()
    {
        var s = NewState();
        AssertThat(s.UltimateReady).IsFalse();
        s.TickTimers(6);
        AssertThat(s.UltimateReady).IsTrue();
        s.StartEcoCooldown();
        AssertThat(s.EcoCd).IsEqual(0.8);
        s.SetEssence(50);
        AssertThat(s.TrySpendEssence(55)).IsFalse();
        AssertThat(s.TrySpendEssence(45)).IsTrue();
        AssertThat(s.Essence).IsEqual(5.0);
    }

    [TestCase]
    public void RespawnGrantsInvulnerabilityAndClearsEssence()
    {
        var s = NewState();
        s.SetEssence(80);
        s.Respawn(1.5);
        AssertThat(s.Essence).IsEqual(0.0);
        AssertThat(s.Invulnerable).IsEqual(1.5);
        AssertThat(s.TryHurt()).IsFalse();
    }

    [TestCase]
    public void SectorHitsInsideArcOrPointBlank()
    {
        // alvo à direita a 20 u (raio 10): d = 10 ≤ 30
        AssertThat(CombatMath.InSector(0, 0, 20, 0, 10, 0, 2.1, 30)).IsTrue();
        // atrás (180°) a 20 u: fora do arco e d = 10 ≥ 4
        AssertThat(CombatMath.InSector(0, 0, -20, 0, 10, 0, 2.1, 30)).IsFalse();
        // colado (d < 4) acerta mesmo atrás
        AssertThat(CombatMath.InSector(0, 0, -12, 0, 10, 0, 2.1, 30)).IsTrue();
        // fora do alcance
        AssertThat(CombatMath.InSector(0, 0, 50, 0, 10, 0, 2.1, 30)).IsFalse();
        AssertThat(CombatMath.AngDiff(3.1, -3.1)).IsEqualApprox((2 * Math.PI) - 6.2, 1e-9);
    }

    [TestCase]
    public void HitstopSlowsCombatTimeToEightPercent()
    {
        var clock = new CombatClock();
        clock.AtLeast(0.035);
        AssertThat(clock.Step(0.016)).IsEqualApprox(0.016 * 0.08, 1e-12);
        clock.AtLeast(0.01); // max: continua 0,019
        AssertThat(clock.Hitstop).IsEqualApprox(0.019, 1e-12);
        clock.Step(0.016);
        clock.Step(0.016);
        AssertThat(clock.Hitstop).IsEqual(0.0);
        AssertThat(clock.Step(0.016)).IsEqual(0.016);
    }
}
