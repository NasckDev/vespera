namespace Vespera.Tests.Core.Coop;

using GdUnit4;
using Vespera.Core.Coop;
using static GdUnit4.Assertions;

/// <summary>Tela de entrada: cada dispositivo entra uma vez, escolhe classe, confirma; teclado dividido; capa de outro tom.</summary>
[TestSuite]
public class CoopRosterTest
{
    private static CoopRoster NewRoster(int max = 2) => new(max, ["sword", "bow", "hammer"]);

    [TestCase]
    public void JoinOrderGivesNumbersAndDefaultClasses()
    {
        var r = NewRoster();
        var p1 = r.Join(InputDevice.Keyboard);
        var p2 = r.Join(InputDevice.Gamepad(0));
        AssertThat(p1!.ClassId).IsEqual("sword");
        AssertThat(p2!.ClassId).IsEqual("bow");
        AssertThat(r.IndexOf(InputDevice.Gamepad(0))).IsEqual(1);
        AssertThat(r.IsFull).IsTrue();
        AssertThat(r.Join(InputDevice.Gamepad(1))).IsNull();
    }

    [TestCase]
    public void SameDeviceCannotJoinTwice()
    {
        var r = NewRoster(4);
        AssertThat(r.Join(InputDevice.Gamepad(1))).IsNotNull();
        AssertThat(r.Join(InputDevice.Gamepad(1))).IsNull();
        AssertThat(r.Join(InputDevice.Gamepad(2))).IsNotNull();
        AssertThat(r.Count).IsEqual(2);
    }

    [TestCase]
    public void LeavingShiftsTheOthersUpUnlessLocked()
    {
        var r = NewRoster(3);
        r.Join(InputDevice.Keyboard, locked: true);
        r.Join(InputDevice.KeyboardRight);
        r.Join(InputDevice.Gamepad(0));
        AssertThat(r.Leave(InputDevice.Keyboard)).IsFalse();
        AssertThat(r.Leave(InputDevice.KeyboardRight)).IsTrue();
        AssertThat(r.IndexOf(InputDevice.Gamepad(0))).IsEqual(1);
        AssertThat(r.Leave(InputDevice.KeyboardRight)).IsFalse();
    }

    [TestCase]
    public void ClassCyclesBothWaysUntilReady()
    {
        var r = NewRoster();
        r.Join(InputDevice.Keyboard);
        r.CycleClass(InputDevice.Keyboard, 1);
        AssertThat(r.Slots[0].ClassId).IsEqual("bow");
        r.CycleClass(InputDevice.Keyboard, -2);
        AssertThat(r.Slots[0].ClassId).IsEqual("hammer");
        r.SetReady(InputDevice.Keyboard, true);
        AssertThat(r.CycleClass(InputDevice.Keyboard, 1)).IsFalse();
        AssertThat(r.Slots[0].ClassId).IsEqual("hammer");
    }

    [TestCase]
    public void StartNeedsTwoReadyPlayers()
    {
        var r = NewRoster();
        r.Join(InputDevice.Keyboard);
        r.SetReady(InputDevice.Keyboard, true);
        AssertThat(r.CanStart).IsFalse();
        r.Join(InputDevice.Gamepad(0));
        AssertThat(r.CanStart).IsFalse();
        r.SetReady(InputDevice.Gamepad(0), true);
        AssertThat(r.CanStart).IsTrue();
    }

    [TestCase]
    public void KeyboardSplitsOnlyWhenBothHalvesAreTaken()
    {
        var r = NewRoster();
        r.Join(InputDevice.Keyboard);
        r.Join(InputDevice.Gamepad(0));
        AssertThat(r.KeyboardSplit).IsFalse();
        r.Leave(InputDevice.Gamepad(0));
        r.Join(InputDevice.KeyboardRight);
        AssertThat(r.KeyboardSplit).IsTrue();
    }

    [TestCase]
    public void SameClassGivesTheLaterPlayerATintedCape()
    {
        var r = NewRoster();
        r.Join(InputDevice.Keyboard);
        r.Join(InputDevice.Gamepad(0));
        AssertThat(r.NeedsCapeTint(1)).IsFalse();
        r.CycleClass(InputDevice.Gamepad(0), -1);
        AssertThat(r.Slots[1].ClassId).IsEqual("sword");
        AssertThat(r.NeedsCapeTint(1)).IsTrue();
        AssertThat(r.NeedsCapeTint(0)).IsFalse();
    }
}
