namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using static GdUnit4.Assertions;

[TestSuite]
public class DamageCalculatorTest
{
    [TestCase(10, 1f, 10)]
    [TestCase(10, 1.5f, 15)]
    [TestCase(10, 1.25f, 13)]
    [TestCase(1, 0.1f, 1)]
    [TestCase(0, 2f, 0)]
    [TestCase(10, 0f, 0)]
    public void CalculateAppliesMultiplier(int baseDamage, float multiplier, int expected)
        => AssertThat(DamageCalculator.Calculate(baseDamage, multiplier)).IsEqual(expected);

    [TestCase]
    public void ApplyDamageNeverGoesBelowZero()
    {
        AssertThat(DamageCalculator.ApplyDamage(30, 10)).IsEqual(20);
        AssertThat(DamageCalculator.ApplyDamage(5, 10)).IsEqual(0);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void RejectsNegativeBaseDamage()
        => DamageCalculator.Calculate(-1);
}
