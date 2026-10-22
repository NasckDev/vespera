namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

[TestSuite]
public class LayoutGridTest
{
    [TestCase]
    public void WalkThenBlockOverTheBase()
    {
        var baseArea = new ShapeWalkArea(64, 32, [WalkShape.FromRect(new MapRect(0, 0, 32, 32))], []);
        var layout = MapLayout.Parse("""
        {"cell": 4,
         "walk": [ {"rect": [32, 0, 16, 32]} ],
         "block": [ {"rect": [8, 8, 8, 8]} ]}
        """);
        var grid = LayoutGrid.Build(64, 32, baseArea, layout);
        AssertThat(grid.Width).IsEqual(16);
        AssertThat(grid.Height).IsEqual(8);
        AssertThat(grid.IsWalkable(0, 0)).IsTrue(); // base
        AssertThat(grid.IsWalkable(9, 3)).IsTrue(); // walk do layout (x 36)
        AssertThat(grid.IsWalkable(12, 3)).IsFalse(); // fora de tudo (x 52)
        AssertThat(grid.IsWalkable(2, 2)).IsFalse(); // block do layout por cima da base
        var area = new MaskWalkArea(grid, 4);
        AssertThat(area.IsWalkable(40, 10)).IsTrue();
        AssertThat(area.IsWalkable(10, 10)).IsFalse();
    }

    [TestCase]
    public void NoBaseMeansOnlyTheLayoutShapes()
    {
        var layout = MapLayout.Parse("""{"cell": 4, "base": "none", "walk": [ {"circle": [16, 16, 8]} ]}""");
        var grid = LayoutGrid.Build(32, 32, null, layout);
        AssertThat(grid.IsWalkable(4, 4)).IsTrue();
        AssertThat(grid.IsWalkable(0, 0)).IsFalse();
    }
}
