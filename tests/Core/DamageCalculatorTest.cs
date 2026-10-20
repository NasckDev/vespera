namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Core.Data;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Fórmulas de dano da v2 (spec 02 §3.4-3.5; §6.3 itens 5-10).</summary>
[TestSuite]
public class DamageCalculatorTest
{
    // R(a,b) com 0.5 dá exatamente o meio: 1.0 nas duas variações.
    private const double NoVariance = 0.5;
    private const double NoCrit = 0.99;
    private const double Crit = 0.0;

    private static DamageCalculator Calc => new(TestDb.Instance.Rules.Combat);

    private static MonsterHitContext Ctx(PartSide side = PartSide.Front, double backstab = 0, int crit = 5, string? el = null, string[]? weak = null, string[]? res = null)
        => new(side, backstab, crit, el, weak ?? [], res ?? []);

    [TestCase]
    public void DefenseReducesDamageToPlayer()
        => AssertThat(Calc.ToPlayer(12, 6, new ConstantRandom(NoVariance))).IsEqual(11);

    [TestCase]
    public void DefenseIsCappedAtSixtyPercent()
    {
        AssertThat(Calc.ToPlayer(12, 80, new ConstantRandom(NoVariance))).IsEqual(5); // 12 × 0.4 = 4.8
        AssertThat(Calc.ToPlayer(12, 60, new ConstantRandom(NoVariance))).IsEqual(5);
    }

    [TestCase]
    public void PlayerAlwaysTakesAtLeastOne()
        => AssertThat(Calc.ToPlayer(1, 60, new ConstantRandom(0))).IsEqual(1);

    [TestCase]
    public void PlayerVarianceIsNinetyToHundredTenPercent()
    {
        AssertThat(Calc.ToPlayer(100, 0, new ConstantRandom(0))).IsEqual(90);
        AssertThat(Calc.ToPlayer(100, 0, new ConstantRandom(0.9999))).IsEqual(110);
    }

    [TestCase]
    public void PlainHitKeepsBaseDamage()
    {
        var hit = Calc.ToMonster(12, Ctx(), new FixedRandom(NoCrit, NoVariance));
        AssertThat(hit.Damage).IsEqual(12);
        AssertThat(hit.Crit).IsFalse();
        AssertThat(hit.Element).IsEqual(ElementOutcome.None);
    }

    [TestCase]
    public void CritMultipliesBySixteenTenths()
    {
        var hit = Calc.ToMonster(10, Ctx(), new FixedRandom(Crit, NoVariance));
        AssertThat(hit.Crit).IsTrue();
        AssertThat(hit.Damage).IsEqual(16);
    }

    [TestCase]
    public void CritRollUsesPercentChance()
    {
        AssertThat(Calc.ToMonster(10, Ctx(crit: 5), new FixedRandom(0.049, NoVariance)).Crit).IsTrue();
        AssertThat(Calc.ToMonster(10, Ctx(crit: 5), new FixedRandom(0.05, NoVariance)).Crit).IsFalse();
    }

    [TestCase]
    public void BackstabOnlyFromBehind()
    {
        AssertThat(Calc.ToMonster(6, Ctx(PartSide.Back, backstab: 2), new FixedRandom(NoCrit, NoVariance)).Damage).IsEqual(12);
        AssertThat(Calc.ToMonster(6, Ctx(PartSide.Front, backstab: 2), new FixedRandom(NoCrit, NoVariance)).Damage).IsEqual(6);
    }

    [TestCase]
    public void WeaknessAndResistance()
    {
        var weak = Calc.ToMonster(10, Ctx(el: "solar", weak: ["solar", "igneo"]), new FixedRandom(NoCrit, NoVariance));
        AssertThat(weak.Damage).IsEqual(15);
        AssertThat(weak.Element).IsEqual(ElementOutcome.Weak);

        var res = Calc.ToMonster(10, Ctx(el: "mare", res: ["mare"]), new FixedRandom(NoCrit, NoVariance));
        AssertThat(res.Damage).IsEqual(6);
        AssertThat(res.Element).IsEqual(ElementOutcome.Resist);

        var neutral = Calc.ToMonster(10, Ctx(el: "lunar", weak: ["solar"], res: ["mare"]), new FixedRandom(NoCrit, NoVariance));
        AssertThat(neutral.Element).IsEqual(ElementOutcome.None);
    }

    [TestCase]
    public void ModifiersMultiply()
    {
        // costas ×2 × crítico ×1.6 × fraqueza ×1.5 = ×4.8
        var hit = Calc.ToMonster(10, Ctx(PartSide.Back, backstab: 2, el: "solar", weak: ["solar"]), new FixedRandom(Crit, NoVariance));
        AssertThat(hit.Mult).IsEqualApprox(4.8, 1e-9);
        AssertThat(hit.Damage).IsEqual(48);
    }

    [TestCase]
    public void MonsterAlwaysTakesAtLeastOne()
        => AssertThat(Calc.ToMonster(0.2, Ctx(el: "mare", res: ["mare"]), new FixedRandom(NoCrit, 0)).Damage).IsEqual(1);

    [TestCase]
    public void SideIsFrontWhenSourceFacesMonster()
    {
        AssertThat(DamageCalculator.HitSide(110, 100, face: 1)).IsEqual(PartSide.Front);
        AssertThat(DamageCalculator.HitSide(90, 100, face: 1)).IsEqual(PartSide.Back);
        AssertThat(DamageCalculator.HitSide(90, 100, face: -1)).IsEqual(PartSide.Front);
        AssertThat(DamageCalculator.HitSide(100, 100, face: -1)).IsEqual(PartSide.Front);
    }

    [TestCase]
    public void PartSelectionFollowsDataOrder()
    {
        var parts = TestDb.Instance.Monsters["colosso"].Parts; // furnace (front), plates (back), pipes (any)
        AssertThat(DamageCalculator.SelectPart(parts, [false, false, false], PartSide.Front)).IsEqual(0);
        AssertThat(DamageCalculator.SelectPart(parts, [true, false, false], PartSide.Front)).IsEqual(2);
        AssertThat(DamageCalculator.SelectPart(parts, [false, false, false], PartSide.Back)).IsEqual(1);
        AssertThat(DamageCalculator.SelectPart(parts, [true, true, true], PartSide.Back)).IsEqual(-1);

        var wolf = TestDb.Instance.Monsters["wolf"].Parts;
        AssertThat(DamageCalculator.SelectPart(wolf, [true, false], PartSide.Front)).IsEqual(-1);
    }

    [TestCase]
    public void PartDamageUsesSourceMultiplier()
    {
        AssertThat(DamageCalculator.PartDamage(10, 2.2)).IsEqualApprox(22.0, 1e-9);
        AssertThat(DamageCalculator.PartDamage(10, 0)).IsEqual(10.0);
    }

    [TestCase]
    public void MissingPartIsNotBroken()
    {
        var fishm = TestDb.Instance.Monsters["fishm"].Parts; // lure, fins: não tem "jaw"
        AssertThat(DamageCalculator.IsPartBroken(fishm, [true, true], "jaw")).IsFalse();
        AssertThat(DamageCalculator.IsPartBroken(fishm, [true, false], "lure")).IsTrue();
        AssertThat(DamageCalculator.IsPartBroken(fishm, [true, false], "fins")).IsFalse();
    }

    [TestCase]
    public void KnockbackScalesByMonsterKind()
    {
        AssertThat(Calc.KnockbackScale(elite: false, boss: false)).IsEqual(1.0);
        AssertThat(Calc.KnockbackScale(elite: true, boss: false)).IsEqual(0.35);
        AssertThat(Calc.KnockbackScale(elite: false, boss: true)).IsEqual(0.0);
    }

    [TestCase]
    public void EssenceGainIsCappedAndSkipsDummies()
    {
        AssertThat(Calc.EssenceAfterHit(0, 1)).IsEqualApprox(3.2, 1e-9);
        AssertThat(Calc.EssenceAfterHit(10, 1.35)).IsEqualApprox(14.32, 1e-9);
        AssertThat(Calc.EssenceAfterHit(99, 1)).IsEqual(100.0);
        AssertThat(Calc.EssenceAfterHit(10, 1, dummy: true)).IsEqual(10.0);
    }

    [TestCase]
    public void ApplyDamageNeverGoesBelowZero()
    {
        AssertThat(DamageCalculator.ApplyDamage(30, 10)).IsEqual(20);
        AssertThat(DamageCalculator.ApplyDamage(5, 10)).IsEqual(0);
    }
}
