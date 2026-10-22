namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ObstacleWalkAreaTest
{
    private static ShapeWalkArea Open() => new(100, 100, [WalkShape.FromRect(new MapRect(0, 0, 100, 100))], []);

    private static MapPoint[] Square(float x, float y, float half) =>
        [new(x - half, y - half), new(x + half, y - half), new(x + half, y + half), new(x - half, y + half)];

    [TestCase]
    public void ObstacleBlocksOnlyItsOwnCells()
    {
        var area = new ObstacleWalkArea(Open());
        area.Add(Square(50, 50, 6));
        AssertThat(area.IsWalkable(50, 50)).IsFalse();
        AssertThat(area.IsWalkable(45, 55)).IsFalse();
        AssertThat(area.IsWalkable(60, 50)).IsTrue();
        AssertThat(area.Inner.IsWalkable(50, 50)).IsTrue();
        AssertThat(area.BlockedRects().Count).IsEqual(Open().BlockedRects().Count);
    }

    [TestCase]
    public void BrokenObstacleFreesTheCellsAndRestoreBlocksAgain()
    {
        var area = new ObstacleWalkArea(Open());
        var a = area.Add(Square(50, 50, 6));
        var b = area.Add(Square(52, 50, 6));
        area.SetActive(a, false);
        AssertThat(area.IsWalkable(50, 50)).IsFalse(); // o segundo ainda cobre
        area.SetActive(b, false);
        AssertThat(area.IsWalkable(50, 50)).IsTrue();
        area.SetActive(b, false); // desligar duas vezes não muda nada
        area.SetActive(a, true);
        AssertThat(area.IsWalkable(50, 50)).IsFalse();
    }

    [TestCase]
    public void PathfinderGoesAroundTheObstacle()
    {
        var area = new ObstacleWalkArea(new ShapeWalkArea(100, 40, [WalkShape.FromRect(new MapRect(0, 0, 100, 40))], []));
        area.Add(Square(50, 12, 12)); // fecha a faixa de cima: o caminho tem que passar por baixo
        var path = WalkPathfinder.FindPathInto(area, new MapPoint(10, 10), new MapRect(85, 5, 10, 10));
        AssertThat(path).IsNotNull();
        foreach (var p in path!)
        {
            AssertThat(area.CanStand(p.X, p.Y)).IsTrue();
        }

        AssertThat(path.Any(p => p.X > 38 && p.X < 62 && p.Y > 24)).IsTrue();
    }
}
