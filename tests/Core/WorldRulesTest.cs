namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class WorldRulesTest
{
    [TestCase]
    public void CharacterScaleMatchesTheDoorMeasurement()
    {
        var world = TestDb.Instance.Rules.World;
        var c = world.Calibration;
        // jogador = 0,875 da porta da Estalagem (64 u) → 56 u; o c_sword_front tem 32 u
        AssertThat(world.CharacterScale).IsEqualApprox(c.DoorHeight * c.PlayerToDoor / c.SpriteHeight, 0.01);
        AssertThat(c.SpriteHeight * world.CharacterScale / c.DoorHeight).IsBetween(0.85, 0.9);
        AssertThat(world.PropScale).IsEqualApprox(c.PaintedBarrelWidth / c.BarrelSpriteWidth, 0.02);
    }

    [TestCase]
    public void MovementIsResponsive()
    {
        var m = TestDb.Instance.Rules.World.Movement;
        AssertThat(m.AccelSeconds).IsBetween(0.0, 0.1);
        AssertThat(m.DecelSeconds).IsBetween(0.0, 0.1);
    }

    [TestCase]
    public void WorldMaterialsExist()
    {
        var db = TestDb.Instance;
        AssertThat(db.Materials.ContainsKey(db.Rules.World.Fishing.Material)).IsTrue();
        AssertThat(db.Materials[db.Rules.World.Fishing.Material].Icon).IsEqual("i_fish");
        AssertThat(db.Materials.ContainsKey(db.Rules.World.Flower.Material)).IsTrue();
    }
}
