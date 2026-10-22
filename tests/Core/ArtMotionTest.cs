namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Art;
using static GdUnit4.Assertions;

[TestSuite]
public class ArtMotionTest
{
    [TestCase]
    public void WalkLoopRunsAtNineFpsAtFullSpeed()
    {
        var counter = 0f;
        var changes = 0;
        var last = 0;
        for (var i = 0; i < 62; i++)
        {
            counter = FrameClock.Advance(counter, 1f / 60f, 9f, 1f, 6);
            var frame = FrameClock.Loop(counter, 6);
            if (frame != last)
            {
                changes++;
                last = frame;
            }
        }

        AssertThat(changes).IsEqual(9); // 9 trocas de quadro em ~1 s
        AssertThat(counter).IsBetween(0f, 6f);
    }

    [TestCase]
    public void WalkLoopSlowsWithTheBodyAndStopsWhenStill()
    {
        var half = FrameClock.Advance(0f, 1f, 9f, 0.5f, 6);
        AssertThat(half).IsEqualApprox(4.5f, 1e-4f);
        AssertThat(FrameClock.Advance(2.2f, 1f, 9f, 0f, 6)).IsEqual(2.2f);
        AssertThat(FrameClock.Advance(5.5f, 0.1f, 9f, 1f, 6)).IsEqualApprox(0.4f, 1e-4f); // dá a volta
    }

    [TestCase]
    public void OnceSpreadsFramesOverTheDurationAndHoldsTheLast()
    {
        // esquiva v4: 6 quadros em 0,17 s, o mesmo DashSeconds do combate
        AssertThat(FrameClock.Once(0f, 0.17f, 6)).IsEqual(0);
        AssertThat(FrameClock.Once(0.085f, 0.17f, 6)).IsEqual(3);
        AssertThat(FrameClock.Once(0.169f, 0.17f, 6)).IsEqual(5);
        AssertThat(FrameClock.Once(0.5f, 0.17f, 6)).IsEqual(5);
        AssertThat(FrameClock.Once(-1f, 0.17f, 6)).IsEqual(0);
    }

    [TestCase]
    public void BossPoseFollowsTheAiState()
    {
        AssertThat(CreaturePose.PoseFor(Input())).IsEqual(CreaturePoseKind.Front);
        AssertThat(CreaturePose.PoseFor(Input(facingAway: true))).IsEqual(CreaturePoseKind.Back);
        AssertThat(CreaturePose.PoseFor(Input(telegraphing: true))).IsEqual(CreaturePoseKind.Front);
        AssertThat(CreaturePose.PoseFor(Input(attacking: true, facingAway: true))).IsEqual(CreaturePoseKind.Attack);
        AssertThat(CreaturePose.PoseFor(Input(special: true, attacking: true))).IsEqual(CreaturePoseKind.Special);
        AssertThat(CreaturePose.PoseFor(Input(dead: true, special: true))).IsEqual(CreaturePoseKind.Death);
    }

    [TestCase]
    public void BossBreathesAndNeverDeformsMuch()
    {
        var minY = 2f;
        var maxY = 0f;
        for (var t = 0f; t < 6f; t += 0.05f)
        {
            foreach (var input in new[] { Input(time: t), Input(time: t, moving: true), Input(time: t, telegraphing: true, stateTime: t), Input(time: t, attacking: true, stateTime: t % 0.3f) })
            {
                var s = CreaturePose.Evaluate(input);
                minY = MathF.Min(minY, s.ScaleY);
                maxY = MathF.Max(maxY, s.ScaleY);
                AssertThat(MathF.Abs(s.ScaleX - 1f)).IsLess(0.08f);
            }
        }

        AssertThat(maxY - minY).IsGreater(0.02f); // não fica estático
        AssertThat(minY).IsGreater(0.9f);
        AssertThat(maxY).IsLess(1.1f);
    }

    [TestCase]
    public void TelegraphCrouchesAndTheStrikeStretches()
    {
        var windup = CreaturePose.Evaluate(Input(time: 0f, telegraphing: true, stateTime: 0.6f));
        var strike = CreaturePose.Evaluate(Input(time: 0f, attacking: true, stateTime: 0f));
        AssertThat(windup.ScaleY).IsLess(0.97f);
        AssertThat(windup.ScaleX).IsGreater(1.02f);
        AssertThat(strike.ScaleY).IsGreater(1.04f);
        var settled = CreaturePose.Evaluate(Input(time: 0f, attacking: true, stateTime: CreaturePose.ImpactSeconds));
        AssertThat(settled.ScaleY).IsEqualApprox(1f, 1e-4f);
    }

    [TestCase]
    public void BreakableCracksAtHalfLife()
    {
        AssertThat(PropArtStates.Breakable(30, 30)).IsEqual(BreakableStage.Whole);
        AssertThat(PropArtStates.Breakable(18, 30)).IsEqual(BreakableStage.Whole);
        AssertThat(PropArtStates.Breakable(15, 30)).IsEqual(BreakableStage.Cracked);
        AssertThat(PropArtStates.Breakable(1, 30)).IsEqual(BreakableStage.Cracked);
        AssertThat(PropArtStates.Breakable(0, 30)).IsEqual(BreakableStage.Broken);
        AssertThat(PropArtStates.Breakable(5, 10)).IsEqual(BreakableStage.Cracked); // urna/saco (10): 1 golpe da Lâmina já quebra
    }

    [TestCase]
    public void ChestShowsEveryOpeningFrameInOrder()
    {
        AssertThat(PropArtStates.ChestFrame(0f)).IsEqual(0);
        AssertThat(PropArtStates.ChestFrame(0.01f)).IsEqual(1);
        AssertThat(PropArtStates.ChestFrame(0.5f)).IsEqual(2);
        AssertThat(PropArtStates.ChestFrame(0.99f)).IsEqual(3);
        AssertThat(PropArtStates.ChestFrame(1f)).IsEqual(3);
        var last = 0;
        for (var p = 0f; p <= 1f; p += 0.01f)
        {
            var f = PropArtStates.ChestFrame(p);
            AssertThat(f).IsGreaterEqual(last);
            last = f;
        }
    }

    [TestCase]
    public void FlowerRegrowsThroughSprout()
    {
        AssertThat(PropArtStates.Flower(false, 0, 90)).IsEqual(FlowerStage.Flowering);
        AssertThat(PropArtStates.Flower(true, 1, 90)).IsEqual(FlowerStage.Harvested);
        AssertThat(PropArtStates.Flower(true, 35, 90)).IsEqual(FlowerStage.Harvested);
        AssertThat(PropArtStates.Flower(true, 40, 90)).IsEqual(FlowerStage.Sprouting);
        AssertThat(PropArtStates.Flower(true, 89, 90)).IsEqual(FlowerStage.Sprouting);
    }

    [TestCase]
    public void DodgeViewFollowsTheDashDirection()
    {
        AssertThat(PropArtStates.Dodge(1, 0)).IsEqual(DodgeView.Side);
        AssertThat(PropArtStates.Dodge(-0.7f, 0.7f)).IsEqual(DodgeView.Side);
        AssertThat(PropArtStates.Dodge(0.3f, 0.95f)).IsEqual(DodgeView.Front);
        AssertThat(PropArtStates.Dodge(0, -1)).IsEqual(DodgeView.Back);
    }

    private static CreatureMotionInput Input(bool dead = false, bool special = false, bool attacking = false, bool telegraphing = false,
        bool facingAway = false, bool moving = false, float time = 0f, float stateTime = 0f) =>
        CreaturePose.Input(dead, special, attacking, telegraphing, facingAway, moving, false, time, stateTime);
}
