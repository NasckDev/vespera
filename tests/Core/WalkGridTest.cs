namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class WalkGridTest
{
    // '#' bloqueado, '.' andável
    private static WalkGrid Parse(params string[] rows)
    {
        var cells = rows.SelectMany(r => r.Select(c => c == '.')).ToArray();
        return new WalkGrid(rows[0].Length, rows.Length, cells);
    }

    [TestCase]
    public void OutsideGridIsBlocked()
    {
        var grid = Parse("..", "..");
        AssertThat(grid.IsWalkable(0, 0)).IsTrue();
        AssertThat(grid.IsWalkable(-1, 0)).IsFalse();
        AssertThat(grid.IsWalkable(2, 1)).IsFalse();
    }

    [TestCase]
    public void FullyBlockedGridIsOneRect()
    {
        var rects = Parse("###", "###").BlockedRects();
        AssertThat(rects.Count).IsEqual(1);
        AssertThat(rects[0]).IsEqual(new CellRect(0, 0, 3, 2));
    }

    [TestCase]
    public void WalkableGridHasNoRects()
        => AssertThat(Parse("...", "...").BlockedRects().Count).IsEqual(0);

    [TestCase]
    public void RectsCoverEveryBlockedCellExactlyOnce()
    {
        var grid = Parse(
            "####....",
            "#..#..##",
            "#..#..##",
            "####....",
            "..#.#.#.");

        var covered = new int[grid.Width * grid.Height];
        foreach (var r in grid.BlockedRects())
        {
            for (var y = r.Y; y < r.Y + r.Height; y++)
            {
                for (var x = r.X; x < r.X + r.Width; x++)
                {
                    covered[(y * grid.Width) + x]++;
                }
            }
        }

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                AssertThat(covered[(y * grid.Width) + x]).IsEqual(grid.IsWalkable(x, y) ? 0 : 1);
            }
        }
    }

    [TestCase]
    public void MergingReducesColliderCount()
    {
        // 4x4 bloqueado com buraco 2x2 no meio: 16 - 4 = 12 células, bem menos retângulos.
        var rects = Parse("####", "#..#", "#..#", "####").BlockedRects();
        AssertThat(rects.Count).IsLessEqual(4);
    }

    [TestCase]
    public void NearestWalkableFindsClosestFreeCell()
    {
        var grid = Parse("###", "##.", "###");
        AssertThat(grid.NearestWalkable(1, 1, maxRadius: 1)!.Value).IsEqual((2, 1));
        AssertThat(grid.NearestWalkable(0, 0, maxRadius: 1)).IsNull();
        AssertThat(grid.NearestWalkable(2, 1)!.Value).IsEqual((2, 1));
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void RejectsWrongCellCount()
        => _ = new WalkGrid(2, 2, new bool[3]);
}
