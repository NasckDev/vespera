namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class MinimapProjectionTest
{
    [TestCase]
    public void FitsTheMapKeepingTheAspect()
    {
        var p = new MinimapProjection(1672, 941, new MapRect(0, 0, 160, 120));
        AssertThat(p.Scale).IsEqualApprox(160f / 1672f, 1e-6f);
        var img = p.ImageRect;
        AssertThat(img.Width).IsEqualApprox(160f, 0.001f);
        AssertThat(img.Y).IsGreater(0f); // centralizado na vertical
        AssertThat(p.ToPanel(0, 0)).IsEqual(new MapPoint(img.X, img.Y));
        AssertThat(p.ToPanel(1672, 941).X).IsEqualApprox(160f, 0.001f);
        AssertThat(p.ToPanel(-50, 2000)).IsEqual(p.ToPanel(0, 941)); // fora do mapa vai para a borda
    }

    [TestCase]
    public void TallInteriorsAreCenteredHorizontally()
    {
        var p = new MinimapProjection(500, 896, new MapRect(10, 10, 160, 120));
        AssertThat(p.ImageRect.Height).IsEqualApprox(120f, 0.001f);
        AssertThat(p.ImageRect.X).IsGreater(10f);
        var view = p.ViewToPanel(new MapRect(0, 0, 250, 448));
        AssertThat(view.Height).IsEqualApprox(60f, 0.01f);
    }
}
