namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class AmbientPlannerTest
{
    private static readonly LayoutAmbient[] Emitters =
    [
        new("water", 0, 0, 100),
        new("fire", 50, 0, 100),
        new("birds", 1000, 1000, 100),
        new("crowd", 20, 0, 400),
    ];

    [TestCase]
    public void PicksTheAudibleOnesStrongestFirst()
    {
        var result = new int[8];
        var n = AmbientPlanner.SelectAudible(Emitters, 40, 0, result, margin: 0);
        AssertThat(n).IsEqual(3);
        AssertThat(result[0]).IsEqual(3); // crowd a 20/400 = 0,05 do alcance
        AssertThat(result[1]).IsEqual(1); // fire a 10/100 = 0,1
        AssertThat(result[2]).IsEqual(0); // water a 40/100 = 0,4
    }

    [TestCase]
    public void KeepsOnlyAsManyAsThePool()
    {
        var result = new int[2];
        AssertThat(AmbientPlanner.SelectAudible(Emitters, 40, 0, result, margin: 0)).IsEqual(2);
        AssertThat(result[0]).IsEqual(3);
        AssertThat(result[1]).IsEqual(1);
    }

    [TestCase]
    public void GainFallsSmoothlyToZero()
    {
        AssertThat(AmbientPlanner.Gain(0, 100)).IsEqual(1f);
        AssertThat(AmbientPlanner.Gain(50, 100)).IsEqual(0.5f);
        AssertThat(AmbientPlanner.Gain(100, 100)).IsEqual(0f);
        AssertThat(AmbientPlanner.Gain(10, 0)).IsEqual(0f);
    }
}
