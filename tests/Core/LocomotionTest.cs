namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class LocomotionTest
{
    private const float Max = 78f;
    private const float Dt = 1f / 60f;

    [TestCase]
    public void ReachesFullSpeedInTheAccelTime()
    {
        var v = new MapPoint(0, 0);
        var frames = 0;
        while (v.X < Max - 0.001f)
        {
            v = Locomotion.Step(v, new MapPoint(1, 0), Max, 0.06f, 0.05f, Dt);
            frames++;
        }

        AssertThat(frames).IsEqual(4); // 0,06 s a 60 Hz (arredonda para cima)
        AssertThat(v.X).IsEqual(Max);
    }

    [TestCase]
    public void StopsInTheDecelTimeAndNeverOvershoots()
    {
        var v = new MapPoint(Max, 0);
        for (var i = 0; i < 3; i++)
        {
            v = Locomotion.Step(v, default, Max, 0.06f, 0.05f, Dt);
            AssertThat(v.X).IsGreaterEqual(0f);
        }

        AssertThat(v).IsEqual(new MapPoint(0, 0));
    }

    [TestCase]
    public void NeverExceedsMaxSpeedOnDiagonalsOrTurns()
    {
        var v = new MapPoint(Max, 0);
        var diagonal = new MapPoint(-MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));
        for (var i = 0; i < 30; i++)
        {
            v = Locomotion.Step(v, diagonal, Max, 0.06f, 0.05f, Dt);
            AssertThat(MathF.Sqrt((v.X * v.X) + (v.Y * v.Y))).IsLessEqual(Max + 0.001f);
        }

        AssertThat(Locomotion.SpeedFraction(v, Max)).IsEqualApprox(1f, 0.001f);
    }

    [TestCase]
    public void ZeroTimesAreInstant()
        => AssertThat(Locomotion.Step(default, new MapPoint(0, -1), Max, 0f, 0f, Dt)).IsEqual(new MapPoint(0, -Max));
}
