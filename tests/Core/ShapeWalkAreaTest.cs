namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ShapeWalkAreaTest
{
    // Duas salas ligadas por um corredor, com um pilar na sala de cima.
    private static ShapeWalkArea Rooms() => new(
        200,
        200,
        [WalkShape.FromRect(new MapRect(10, 10, 100, 60)), WalkShape.FromRect(new MapRect(50, 70, 20, 100))],
        [new MapRect(30, 20, 10, 10)]);

    private static ShapeWalkArea Arena() => new(
        300,
        300,
        [WalkShape.FromCircle(new MapCircle(150, 150, 60)), WalkShape.FromRect(new MapRect(140, 210, 20, 80))],
        [new MapRect(140, 140, 20, 20)]);

    [TestCase]
    public void PointInsideWalkRectIsWalkable()
    {
        var area = Rooms();
        AssertThat(area.IsWalkable(80, 40)).IsTrue();
        AssertThat(area.IsWalkable(60, 150)).IsTrue();
    }

    [TestCase]
    public void PointOutsideEveryWalkShapeIsBlocked()
    {
        var area = Rooms();
        AssertThat(area.IsWalkable(5, 5)).IsFalse();
        AssertThat(area.IsWalkable(100, 150)).IsFalse();
        AssertThat(area.IsWalkable(-1, 40)).IsFalse();
    }

    [TestCase]
    public void BlockSubtractsFromWalk()
    {
        var area = Rooms();
        AssertThat(area.IsWalkable(35, 25)).IsFalse();
        AssertThat(area.IsWalkable(45, 25)).IsTrue();
    }

    [TestCase]
    public void EdgesAreInclusiveLikeThePrototype()
    {
        var area = Rooms();
        AssertThat(area.IsWalkable(10, 10)).IsTrue(); // canto do walk
        AssertThat(area.IsWalkable(110, 70)).IsTrue(); // canto oposto
        AssertThat(area.IsWalkable(30, 25)).IsFalse(); // borda do block também bloqueia
    }

    [TestCase]
    public void CircleIsWalkableUpToItsRadius()
    {
        var area = Arena();
        AssertThat(area.IsWalkable(150, 90)).IsTrue(); // r exato
        AssertThat(area.IsWalkable(150, 89.5f)).IsFalse();
        AssertThat(area.IsWalkable(195, 195)).IsFalse(); // canto do quadrado, fora do círculo
        AssertThat(area.IsWalkable(150, 150)).IsFalse(); // pilar no centro
        AssertThat(area.IsWalkable(150, 280)).IsTrue(); // corredor
    }

    [TestCase]
    public void CanStandUsesFourProbes()
    {
        var area = Rooms();
        AssertThat(area.CanStand(60, 150)).IsTrue();
        AssertThat(area.CanStand(52, 150)).IsFalse(); // x-4 = 48 sai do corredor
        AssertThat(area.CanStand(80, 12)).IsFalse(); // y-2,4 = 9,6 sai da sala
        AssertThat(area.CanStand(80, 13)).IsTrue();
    }

    [TestCase]
    public void NearestWalkableReturnsSamePointWhenStandable()
        => AssertThat(Rooms().NearestWalkable(80, 40)!.Value).IsEqual(new MapPoint(80, 40));

    [TestCase]
    public void NearestWalkableMovesOutOfWalls()
    {
        var area = Rooms();
        var fixedPoint = area.NearestWalkable(80, 74)!.Value; // abaixo da sala, ao lado do corredor
        AssertThat(area.CanStand(fixedPoint.X, fixedPoint.Y)).IsTrue();
        AssertThat(Distance(fixedPoint, 80, 74)).IsLessEqual(8f);
    }

    [TestCase]
    public void NearestWalkableGivesUpFarFromAnyWalk()
        => AssertThat(Rooms().NearestWalkable(190, 190, maxDistance: 20)).IsNull();

    [TestCase]
    public void BlockedRectsMatchWalkabilityExactlyForRects()
    {
        var area = Rooms();
        AssertCoverage(area, exact: true);
    }

    [TestCase]
    public void BlockedRectsCoverCircleConservatively()
    {
        var area = Arena();
        AssertCoverage(area, exact: false);
    }

    [TestCase]
    public void BlockedRectsIncludeFrameOutsideBounds()
    {
        var rects = Rooms().BlockedRects();
        AssertThat(InAny(rects, -10, -10)).IsTrue();
        AssertThat(InAny(rects, 210, 100)).IsTrue();
        AssertThat(InAny(rects, 100, 214)).IsTrue();
    }

    [TestCase]
    public void BlockedRectsAreMerged()
        => AssertThat(Rooms().BlockedRects().Count).IsLess(20);

    /// <summary>Amostra a área em passos de 1 u (deslocados 0,5): não andável ⇒ coberto; andável ⇒ não coberto (exato).</summary>
    internal static void AssertCoverage(IWalkArea area, bool exact)
    {
        var rects = area.BlockedRects();
        var failures = new List<string>();
        for (var y = -10.5f; y < area.Height + 10; y += 1f)
        {
            for (var x = -10.5f; x < area.Width + 10; x += 1f)
            {
                var walkable = area.IsWalkable(x, y);
                var covered = InAnyStrict(rects, x, y);
                if (!walkable && !covered)
                {
                    failures.Add($"({x}, {y}) bloqueado sem colisor");
                }
                else if (exact && walkable && covered)
                {
                    failures.Add($"({x}, {y}) andável com colisor");
                }
            }
        }

        AssertThat(string.Join("; ", failures.Take(5))).IsEqual(string.Empty);
    }

    private static bool InAny(IReadOnlyList<MapRect> rects, float x, float y) => rects.Any(r => r.Contains(x, y));

    private static bool InAnyStrict(IReadOnlyList<MapRect> rects, float x, float y) =>
        rects.Any(r => x > r.X && x < r.Right && y > r.Y && y < r.Bottom);

    private static float Distance(MapPoint p, float x, float y) => MathF.Sqrt(((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)));
}
