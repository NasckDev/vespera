using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

namespace Vespera.Tests.Core.Advancement;

[TestSuite]
public class EchoSlotUnlockTest
{
    [TestCase]
    public void SecondSlotStartsLockedAndOpensWithTheLatestEcho()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.Echoes.AddRange(["eco_viscoso", "eco_semente", "eco_farol"]);
        AssertThat(engine.UnlockedEchoSlots).IsEqual(1);
        AssertThat(engine.IsEchoSlotUnlocked(1)).IsFalse();
        AssertThat(engine.EquipEcho(1, "eco_semente").Ok).IsFalse();

        var (newly, equipped) = engine.UnlockEchoSlot(1);

        AssertThat(newly).IsTrue();
        AssertThat(equipped).IsEqual("eco_farol");
        AssertThat(engine.EchoIn(1)).IsEqual("eco_farol");
        AssertThat(engine.UnlockEchoSlot(1).Newly).IsFalse();
    }

    [TestCase]
    public void CooldownsOfTheTwoSlotsAreIndependent()
    {
        var cooldowns = new EcoSlotCooldowns();
        cooldowns.Start(0, 8);
        AssertThat(cooldowns.Ready(0)).IsFalse();
        AssertThat(cooldowns.Ready(1)).IsTrue();
        cooldowns.Tick(4);
        AssertThat((double)cooldowns.Fraction(0)).IsEqual(0.5);
        cooldowns.Tick(5);
        AssertThat(cooldowns.Ready(0)).IsTrue();
    }
}
