namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class CameraFramingTest
{
    [TestCase]
    public void ZoomShowsFourHundredUnitsOnHdMaps()
        => AssertThat(CameraFraming.Zoom(640, 360, 400)).IsEqual(0.9f);

    [TestCase]
    public void ZoomShowsThreeHundredUnitsOnOtherMaps()
        => AssertThat(CameraFraming.Zoom(640, 360, 300)).IsEqual(1.2f);

    [TestCase]
    public void NarrowViewportKeepsMinimumWidth()
    {
        var zoom = CameraFraming.Zoom(300, 360, 300); // 300 / 1.2 = 250 u < 380
        AssertThat(zoom).IsEqual(300f / 380f);
        AssertThat(300f / zoom).IsEqual(380f);
    }

    [TestCase]
    public void FocusIsFourteenUnitsAboveTheFeet()
        => AssertThat(CameraFraming.Focus(new MapPoint(100, 200), null)).IsEqual(new MapPoint(100, 186));

    [TestCase]
    public void BossPullsTheFocus()
    {
        var f = CameraFraming.Focus(new MapPoint(100, 200), new MapPoint(200, 100));
        AssertThat(f.X).IsEqual(130f); // lerp(100, 200, 0,3)
        AssertThat(MathF.Round(f.Y, 3)).IsEqual(145.4f); // lerp(186, 70, 0,35)
    }

    [TestCase]
    public void SmallerMapIsCentered()
    {
        // Guilda 500 u de largura numa vista de 533,3 u: offset negativo.
        AssertThat(CameraFraming.AxisTarget(500, 540, 250)).IsEqual(-20f);
        AssertThat(CameraFraming.AxisTarget(500, 500, 10)).IsEqual(0f); // igual também centraliza
    }

    [TestCase]
    public void LargerMapClampsToItsEdges()
    {
        AssertThat(CameraFraming.AxisTarget(1672, 400, 100)).IsEqual(0f);
        AssertThat(CameraFraming.AxisTarget(1672, 400, 1000)).IsEqual(800f);
        AssertThat(CameraFraming.AxisTarget(1672, 400, 1600)).IsEqual(1272f);
    }

    [TestCase]
    public void BottomMarginLetsTheCameraPastTheMapEdge()
    {
        // Mapa de 1000 u de altura numa vista de 360 u: sem folga a câmera para em 640; com 60 u de hotbar vai a 700.
        AssertThat(CameraFraming.AxisTarget(1000, 360, 990)).IsEqual(640f);
        AssertThat(CameraFraming.AxisTarget(1000, 360, 990, 60)).IsEqual(700f);
        // Longe da borda a folga não muda nada.
        AssertThat(CameraFraming.AxisTarget(1000, 360, 400, 60)).IsEqual(220f);
        // Mapa que cabe inteiro acima do HUD continua centralizado; o que só cabe sem a folga passa a rolar.
        AssertThat(CameraFraming.AxisTarget(280, 360, 10, 60)).IsEqual(-40f);
        AssertThat(CameraFraming.AxisTarget(350, 360, 340, 60)).IsEqual(50f);
        AssertThat(CameraFraming.AxisTarget(350, 360, 10, 60)).IsEqual(0f);
        AssertThat(CameraFraming.AxisTarget(1000, 360, 990, -20)).IsEqual(640f); // folga negativa é ignorada
        AssertThat(CameraFraming.Target(500, 1000, 640, 360, new MapPoint(250, 990), 60).Y).IsEqual(700f);
    }

    [TestCase]
    public void FollowFactorIsCappedAtOne()
    {
        AssertThat(CameraFraming.FollowFactor(0.1f)).IsEqual(0.7f);
        AssertThat(CameraFraming.FollowFactor(0.5f)).IsEqual(1f);
    }

    [TestCase]
    public void ShakeDecaysAndStops()
    {
        AssertThat(MathF.Round(CameraFraming.DecayShake(8, 0.1f), 3)).IsEqual(MathF.Round(8 * MathF.Pow(0.002f, 0.1f), 3));
        AssertThat(CameraFraming.DecayShake(0.25f, 0.05f)).IsEqual(0f);
        AssertThat(CameraFraming.ShakeAmplitude(0.3f, 400)).IsEqual(0f);
        AssertThat(CameraFraming.ShakeAmplitude(9f, 400)).IsEqual(5f);
    }
}
