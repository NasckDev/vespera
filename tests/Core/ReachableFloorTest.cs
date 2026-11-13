namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class ReachableFloorTest
{
    /// <summary>Grade de texto: '#' parede, '.' chão.</summary>
    private static ReachableFloor Reach(string[] rows, int col, int row) =>
        new(rows[0].Length, rows.Length, (c, r) => rows[r][c] == '#', col, row);

    [TestCase]
    public void FloodsOnlyTheConnectedFloor()
    {
        var reach = Reach(["...#..", "...#..", "...#.."], 0, 0);

        AssertThat(reach.Count).IsEqual(9);
        AssertThat(reach.IsReachable(2, 2)).IsTrue();
        AssertThat(reach.IsReachable(3, 1)).IsFalse();
        AssertThat(reach.IsReachable(4, 1)).IsFalse();
        AssertThat(reach.IsReachable(-1, 0)).IsFalse();
        AssertThat(reach.IsReachable(6, 0)).IsFalse();
    }

    [TestCase]
    public void DiagonalGapsDoNotConnect()
    {
        var reach = Reach([".#", "#."], 0, 0);

        AssertThat(reach.Count).IsEqual(1);
        AssertThat(reach.IsReachable(1, 1)).IsFalse();
    }

    [TestCase]
    public void NearestPicksTheClosestReachableCell()
    {
        var reach = Reach(["...#..", "...#..", "...#.."], 0, 1);

        AssertThat(reach.Nearest(1, 1)).IsEqual((1, 1));
        AssertThat(reach.Nearest(5, 1)).IsEqual((2, 1));
        AssertThat(reach.Nearest(4, 0)).IsEqual((2, 0));
        AssertThat(reach.Nearest(-3, 2)).IsEqual((0, 2));
    }

    [TestCase]
    public void BlockedStartReachesNothing()
    {
        var reach = Reach(["#.", ".."], 0, 0);

        AssertThat(reach.Count).IsEqual(0);
        AssertThat(reach.Nearest(1, 1)).IsNull();
    }
}
