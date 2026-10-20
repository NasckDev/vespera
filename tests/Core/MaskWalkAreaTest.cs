namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class MaskWalkAreaTest
{
    // '#' bloqueado, '.' andável; célula de 8 u.
    private static MaskWalkArea Parse(params string[] rows)
    {
        var cells = rows.SelectMany(r => r.Select(c => c == '.')).ToArray();
        return new MaskWalkArea(new WalkGrid(rows[0].Length, rows.Length, cells));
    }

    [TestCase]
    public void CellIsFloorOfCoordinateOverEight()
    {
        var area = Parse("#.", "..");
        AssertThat(area.IsWalkable(7.9f, 7.9f)).IsFalse();
        AssertThat(area.IsWalkable(8f, 0f)).IsTrue();
        AssertThat(area.IsWalkable(0f, 8f)).IsTrue();
    }

    [TestCase]
    public void OutsideTheGridIsBlocked()
    {
        var area = Parse("..", "..");
        AssertThat(area.IsWalkable(-0.5f, 4f)).IsFalse(); // floor, não truncamento
        AssertThat(area.IsWalkable(16f, 4f)).IsFalse();
        AssertThat(area.Width).IsEqual(16f);
        AssertThat(area.Height).IsEqual(16f);
    }

    [TestCase]
    public void BlockedRectsCoverMaskAndFrameExactly()
        => ShapeWalkAreaTest.AssertCoverage(Parse("##..", "#...", "...#", "..##"), exact: true);

    [TestCase]
    public void CanStandNeedsRoomForTheFourProbes()
    {
        var area = Parse("####", "#..#", "####");
        AssertThat(area.CanStand(16, 12)).IsTrue(); // sondas em x 12 e 20, y 9,6 e 14,4
        AssertThat(area.CanStand(11, 12)).IsFalse(); // x-4 = 7 cai na célula bloqueada
    }
}
