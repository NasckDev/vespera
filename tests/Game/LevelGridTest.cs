using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

[TestSuite]
public class LevelGridTest
{
    private static LevelGrid Sample() => LevelGrid.Parse(10, 4, 3, ["####", "#..#", "####"]);

    [TestCase]
    public void Parse_RejectsWrongWidthAndUnknownChars()
    {
        AssertThrown(() => LevelGrid.Parse(10, 4, 1, ["###"])).IsInstanceOf<FormatException>();
        AssertThrown(() => LevelGrid.Parse(10, 2, 1, ["#x"])).IsInstanceOf<FormatException>();
        AssertThrown(() => LevelGrid.Parse(10, 2, 2, ["##"])).IsInstanceOf<FormatException>();
    }

    [TestCase]
    public void IsBlocked_TreatsOutsideAsWall()
    {
        var grid = Sample();

        AssertThat(grid.IsBlocked(1, 1)).IsFalse();
        AssertThat(grid.IsBlocked(-1, 1)).IsTrue();
        AssertThat(grid.IsBlocked(4, 1)).IsTrue();
        AssertThat(grid.IsBlockedAt(25f, 15f)).IsFalse();
        AssertThat(grid.IsBlockedAt(35f, 15f)).IsTrue();
    }

    [TestCase]
    public void BoundaryFaces_PointFromWallToFloor()
    {
        var faces = Sample().BoundaryFaces().ToList();

        // 2 células andáveis, cada uma com 3 paredes vizinhas = 6 faces.
        AssertThat(faces.Count).IsEqual(6);
        AssertThat(faces).Contains(new GridFace(0, 1, 1, 0));
        AssertThat(faces).Contains(new GridFace(3, 1, -1, 0));
        AssertThat(faces).Contains(new GridFace(1, 0, 0, 1));
    }

    [TestCase]
    public void OverlapsBlocked_DetectsRectTouchingWallCell()
    {
        var grid = Sample();

        AssertThat(grid.OverlapsBlocked(new RectF(12, 12, 16, 6))).IsFalse();
        AssertThat(grid.OverlapsBlocked(new RectF(12, 12, 20, 6))).IsTrue();
    }

    [TestCase]
    public void CapsuleHitsRect_AllowsRestingContactButNotPenetration()
    {
        var wall = new RectF(100, 0, 50, 50);

        // Cápsula de raio 10 com a ponta direita em x = 100 + margem: encostada, sem penetrar.
        AssertThat(CollisionMath.CapsuleHitsRect(60, 89.5f, 25, 10, wall, 1.5f)).IsFalse();
        AssertThat(CollisionMath.CapsuleHitsRect(60, 95f, 25, 10, wall, 1.5f)).IsTrue();
        AssertThat(CollisionMath.CapsuleHitsRect(60, 120f, 25, 10, wall, 1.5f)).IsTrue();
    }

    [TestCase]
    public void CapsuleHitsGrid_FindsWallNextToFeet()
    {
        var grid = Sample();
        var free = CollisionMath.FeetCapsule(20f, 18f, 6f, 3f);
        var intoWall = CollisionMath.FeetCapsule(31f, 18f, 6f, 3f);

        AssertThat(CollisionMath.CapsuleHitsGrid(grid, free, 0.5f)).IsFalse();
        AssertThat(CollisionMath.CapsuleHitsGrid(grid, intoWall, 0.5f)).IsTrue();
    }
}
