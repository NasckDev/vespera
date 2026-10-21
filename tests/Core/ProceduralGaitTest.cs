namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class ProceduralGaitTest
{
    [TestCase]
    public void IdleBreathesWithoutSwaying()
    {
        var pose = ProceduralGait.Sample(GaitView.Front, walkPhase: 0.3f, time: 1.2f, speed: 0f, leanX: 0f, scale: 1.75f);
        AssertThat(pose.OffsetX).IsEqual(0f);
        AssertThat(MathF.Abs(pose.OffsetY)).IsLessEqual(ProceduralGait.IdleBobHeight * 1.75f + 0.001f);
        AssertThat(pose.Rotation).IsEqual(0f);
    }

    [TestCase]
    public void WalkingBobsUpAndSquashesAtFootContact()
    {
        // contato do pé: |sin| = 0 → sem quique, squash
        var contact = ProceduralGait.Sample(GaitView.Front, walkPhase: 0f, time: 0f, speed: 1f, leanX: 0f, scale: 1f);
        AssertThat(contact.OffsetY).IsEqual(0f);
        AssertThat(contact.ScaleY).IsLess(1f);
        AssertThat(contact.ScaleX).IsGreater(1f);

        // alto do passo: sobe 1,6 u × escala e estica um pouco
        var peak = ProceduralGait.Sample(GaitView.Front, walkPhase: MathF.PI / 2f / ProceduralGait.StepFrequency, time: 0f, speed: 1f, leanX: 0f, scale: 2f);
        AssertThat(peak.OffsetY).IsEqualApprox(-ProceduralGait.BobHeight * 2f, 0.001f);
        AssertThat(peak.ScaleY).IsGreater(1f);
    }

    [TestCase]
    public void FrontViewSwaysSideViewDoesNot()
    {
        var phase = MathF.PI / ProceduralGait.StepFrequency; // meio balanço
        AssertThat(MathF.Abs(ProceduralGait.Sample(GaitView.Front, phase, 0f, 1f, 0f, 1f).OffsetX)).IsGreater(0.5f);
        AssertThat(ProceduralGait.Sample(GaitView.Side, phase, 0f, 1f, 0f, 1f).OffsetX).IsEqual(0f);
    }

    [TestCase]
    public void LeansTowardTheMovement()
    {
        AssertThat(ProceduralGait.Sample(GaitView.Side, 0f, 0f, 1f, 1f, 1f).Rotation).IsGreater(0f);
        AssertThat(ProceduralGait.Sample(GaitView.Side, 0f, 0f, 1f, -1f, 1f).Rotation).IsLess(0f);
    }

    [TestCase]
    public void StepsAndCycleFramesAdvanceWithThePhase()
    {
        var oneStep = MathF.PI / ProceduralGait.StepFrequency;
        AssertThat(ProceduralGait.StepIndex(0f)).IsEqual(0);
        AssertThat(ProceduralGait.StepIndex(oneStep * 1.01f)).IsEqual(1);
        AssertThat(ProceduralGait.StepIndex(oneStep * 3.5f)).IsEqual(3);
        // 4 quadros = 2 passos
        AssertThat(ProceduralGait.CycleFrame(0f, 4)).IsEqual(0);
        AssertThat(ProceduralGait.CycleFrame(oneStep * 0.51f, 4)).IsEqual(1);
        AssertThat(ProceduralGait.CycleFrame(oneStep * 1.51f, 4)).IsEqual(3);
        AssertThat(ProceduralGait.CycleFrame(oneStep * 2.01f, 4)).IsEqual(0);
    }
}
