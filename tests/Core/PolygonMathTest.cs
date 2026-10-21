namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class PolygonMathTest
{
    private static readonly MapPoint[] Triangle = [new(0, 0), new(10, 0), new(0, 10)];

    [TestCase]
    public void BoundsCoverAllPoints() => AssertThat(PolygonMath.Bounds(Triangle)).IsEqual(new MapRect(0, 0, 10, 10));

    [TestCase]
    public void ContainsInsideAndNotOutside()
    {
        AssertThat(PolygonMath.Contains(Triangle, 2, 2)).IsTrue();
        AssertThat(PolygonMath.Contains(Triangle, 8, 8)).IsFalse();
        AssertThat(PolygonMath.Contains(Triangle, -1, 2)).IsFalse();
    }

    [TestCase]
    public void ConcavePolygon()
    {
        // "U": a parte de dentro do U não é do polígono
        MapPoint[] u = [new(0, 0), new(30, 0), new(30, 30), new(20, 30), new(20, 10), new(10, 10), new(10, 30), new(0, 30)];
        AssertThat(PolygonMath.Contains(u, 15, 20)).IsFalse();
        AssertThat(PolygonMath.Contains(u, 5, 20)).IsTrue();
        AssertThat(PolygonMath.Contains(u, 25, 20)).IsTrue();
        AssertThat(PolygonMath.Area(u)).IsEqual(700f);
    }

    [TestCase]
    public void RectsOverlapOnlyWithArea()
    {
        AssertThat(PolygonMath.Overlaps(new MapRect(0, 0, 10, 10), new MapRect(5, 5, 10, 10))).IsTrue();
        AssertThat(PolygonMath.Overlaps(new MapRect(0, 0, 10, 10), new MapRect(10, 0, 10, 10))).IsFalse();
    }

    [TestCase]
    public void CircleAndRectPolygons()
    {
        var c = MapLayout.CirclePolygon(100, 100, 10, 32);
        AssertThat(c.Count).IsEqual(32);
        AssertThat(PolygonMath.Contains(c, 100, 100)).IsTrue();
        AssertThat(PolygonMath.Contains(c, 111, 100)).IsFalse();
        AssertThat(MapLayout.RectPolygon(new MapRect(1, 2, 3, 4))[2]).IsEqual(new MapPoint(4, 6));
    }
}
