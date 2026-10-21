namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class PixelCameraTest
{
    [TestCase]
    public void IntegerScaleForCommonMonitors()
    {
        AssertThat(CameraFraming.IntegerScale(1280, 720)).IsEqual(2);
        AssertThat(CameraFraming.IntegerScale(1920, 1080)).IsEqual(3);
        AssertThat(CameraFraming.IntegerScale(2560, 1440)).IsEqual(4);
        AssertThat(CameraFraming.IntegerScale(3840, 2160)).IsEqual(6);
        AssertThat(CameraFraming.IntegerScale(1366, 768)).IsEqual(2);
        AssertThat(CameraFraming.IntegerScale(2560, 1080)).IsEqual(3); // ultrawide: limitado pela altura
        AssertThat(CameraFraming.IntegerScale(320, 200)).IsEqual(1);
    }

    [TestCase]
    public void SnapsToScreenPixels()
    {
        AssertThat(CameraFraming.SnapToPixel(10.2f, 3)).IsEqualApprox(10f + (1f / 3f), 0.0001f);
        AssertThat(CameraFraming.SnapToPixel(10.1f, 3)).IsEqual(10f);
        AssertThat(CameraFraming.SnapToPixel(-0.5f, 2)).IsEqual(-0.5f);
        AssertThat(CameraFraming.SnapToPixel(7.3f, 0)).IsEqual(7.3f);
    }

    [TestCase]
    public void SmoothingIsFrameRateIndependent()
    {
        // 1 s em 60 passos ou em 144 passos chega ao mesmo ponto
        float Run(int steps)
        {
            var x = 0f;
            for (var i = 0; i < steps; i++)
            {
                x += (100f - x) * CameraFraming.SmoothFactor(1f / steps);
            }

            return x;
        }

        AssertThat(Run(60)).IsEqualApprox(Run(144), 0.01f);
        AssertThat(CameraFraming.SmoothFactor(1f / 60f)).IsEqualApprox(0.110f, 0.001f);
        AssertThat(CameraFraming.SmoothFactor(0f)).IsEqual(0f);
    }

    [TestCase]
    public void FocusOffsetFollowsTheCharacterSize()
        => AssertThat(CameraFraming.Focus(new MapPoint(100, 200), null, -24.5f)).IsEqual(new MapPoint(100, 175.5f));
}
