namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class OcclusionRulesTest
{
    // lampião da Rua da Forja: poste de 570 a 598, de 420 a 516
    private static readonly MapPoint[] Lamp = [new(570, 420), new(598, 420), new(598, 516), new(570, 516)];
    private static readonly MapRect LampBounds = PolygonMath.Bounds(Lamp);

    [TestCase]
    public void BehindMeansFootAboveTheBase()
    {
        AssertThat(OcclusionRules.IsBehind(500, 515)).IsTrue();
        AssertThat(OcclusionRules.IsBehind(520, 515)).IsFalse();
    }

    [TestCase]
    public void CoversWhenTheBodyIsInsideTheCutout()
    {
        var body = OcclusionRules.BodyRect(584, 505, 24, 56);
        AssertThat(OcclusionRules.Covers(Lamp, LampBounds, body)).IsTrue();
        var far = OcclusionRules.BodyRect(700, 505, 24, 56);
        AssertThat(OcclusionRules.Covers(Lamp, LampBounds, far)).IsFalse();
    }

    [TestCase]
    public void BoundingBoxAloneIsNotEnough()
    {
        // triângulo fino: a caixa envolve o corpo, mas nenhum ponto do corpo está dentro
        MapPoint[] sliver = [new(0, 0), new(200, 0), new(0, 4)];
        var body = OcclusionRules.BodyRect(150, 60, 24, 56);
        AssertThat(OcclusionRules.Covers(sliver, PolygonMath.Bounds(sliver), body)).IsFalse();
    }

    [TestCase]
    public void FadeApproachesWithoutOvershoot()
    {
        var a = 1f;
        for (var i = 0; i < 30; i++)
        {
            a = OcclusionRules.Approach(a, 0.4f, 10f, 1f / 60f);
            AssertThat(a).IsGreaterEqual(0.4f);
        }

        AssertThat(a).IsEqualApprox(0.4f, 0.01f);
    }
}
