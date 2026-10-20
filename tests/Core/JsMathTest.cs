namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class JsMathTest
{
    [TestCase(40.5, 41)]
    [TestCase(2.5, 3)]
    [TestCase(3.5, 4)]
    [TestCase(1.4999, 1)]
    [TestCase(-0.5, 0)]
    [TestCase(-1.5, -1)]
    [TestCase(0.0, 0)]
    public void RoundMatchesJavaScriptMathRound(double value, int expected)
        => AssertThat(JsMath.Round(value)).IsEqual(expected);

    [TestCase]
    public void RoundDiffersFromBankersRounding()
    {
        // O caso que motivou a D7: 30 Lumes em invasão.
        AssertThat(JsMath.Round(30 * 1.35)).IsEqual(41);
        AssertThat((int)Math.Round(40.5)).IsEqual(40);
    }

    [TestCase]
    public void RangeIntIsInclusiveOnBothEnds()
    {
        AssertThat(new ConstantRandom(0).RangeInt(3, 5)).IsEqual(3);
        AssertThat(new ConstantRandom(0.9999).RangeInt(3, 5)).IsEqual(5);
        AssertThat(new ConstantRandom(0.5).RangeInt(3, 5)).IsEqual(4);
    }

    [TestCase]
    public void RangeAndChanceFollowPrototype()
    {
        AssertThat(new ConstantRandom(0.5).Range(0.92, 1.08)).IsEqualApprox(1.0, 1e-9);
        AssertThat(new ConstantRandom(0.69).Chance(0.7)).IsTrue();
        AssertThat(new ConstantRandom(0.7).Chance(0.7)).IsFalse();
    }

    [TestCase]
    public void PickUsesUniformIndex()
    {
        string[] items = ["a", "b", "c"];
        AssertThat(new ConstantRandom(0).Pick(items)).IsEqual("a");
        AssertThat(new ConstantRandom(0.5).Pick(items)).IsEqual("b");
        AssertThat(new ConstantRandom(0.99).Pick(items)).IsEqual("c");
    }

    [TestCase]
    public void SeededRandomIsReproducible()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        for (var i = 0; i < 5; i++)
        {
            AssertThat(a.NextDouble()).IsEqual(b.NextDouble());
        }
    }
}
