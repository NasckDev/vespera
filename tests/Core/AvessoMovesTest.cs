using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Rift;

namespace Vespera.Tests.Core;

/// <summary>Verbos dos monstros dos Atos VI e VII: Rasgo às costas, Ceifador fora da luz, Sombra com a classe do caçador.</summary>
[TestSuite]
public class AvessoMovesTest
{
    [TestCase]
    public void RasgoArrivesBehindTheHunterOnTheFarSide()
    {
        var (x, y) = AvessoMoves.BlinkBehind(0, 0, 300, 0, 70);
        AssertThat(x).IsEqualApprox(370, 0.01);
        AssertThat(y).IsEqualApprox(0, 0.01);

        var (dx, dy) = AvessoMoves.BlinkBehind(100, 100, 100, 400, 50);
        AssertThat(dx).IsEqualApprox(100, 0.01);
        AssertThat(dy).IsEqualApprox(450, 0.01);
    }

    [TestCase]
    public void RasgoOnTopOfTheHunterStillPicksASpot()
    {
        var (x, y) = AvessoMoves.BlinkBehind(10, 10, 10, 10, 70);
        AssertThat(x).IsEqualApprox(80, 0.01);
        AssertThat(y).IsEqualApprox(10, 0.01);
    }

    [TestCase]
    public void CeifadorFreezesOnlyInsideTheLanternCone()
    {
        // Caçador na origem mirando para a direita.
        AssertThat(AvessoMoves.InLanternCone(0, 0, 1, 0, 300, 0, 500)).IsTrue();
        AssertThat(AvessoMoves.InLanternCone(0, 0, 1, 0, 300, 100, 500)).IsTrue();
        AssertThat(AvessoMoves.InLanternCone(0, 0, 1, 0, -300, 0, 500)).IsFalse();
        AssertThat(AvessoMoves.InLanternCone(0, 0, 1, 0, 0, 300, 500)).IsFalse();
        AssertThat(AvessoMoves.InLanternCone(0, 0, 1, 0, 600, 0, 500)).IsFalse();
    }

    [TestCase]
    public void WithoutAimTheLanternOnlyLightsAround()
    {
        AssertThat(AvessoMoves.InLanternCone(0, 0, 0, 0, 100, 0, 600)).IsTrue();
        AssertThat(AvessoMoves.InLanternCone(0, 0, 0, 0, 300, 0, 600)).IsFalse();
    }

    [TestCase]
    public void ShadowUsesTheHuntersClass()
    {
        AssertThat(AvessoMoves.ShadowFor("sword")).IsEqual("sombra_cacador_lamina");
        AssertThat(AvessoMoves.ShadowFor("bow")).IsEqual("sombra_cacador_arco");
        AssertThat(AvessoMoves.ShadowFor("hammer")).IsEqual("sombra_cacador_martelo");
        AssertThat(AvessoMoves.ShadowFor("desconhecida")).IsEqual("sombra_cacador_lamina");
    }
}
