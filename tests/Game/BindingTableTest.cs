using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.Boot;

namespace Vespera.Tests.Game;

[TestSuite]
public class BindingTableTest
{
    [TestCase]
    public void Assign_SwapsWithConflictingAction()
    {
        var table = BindingTable.Defaults();
        var oldAttack = table.Get("attack", BindingSlot.Primary);

        var conflict = table.Assign("heavy", BindingSlot.Primary, oldAttack);

        AssertThat(conflict).IsEqual("attack");
        AssertThat(table.Get("heavy", BindingSlot.Primary)).IsEqual(oldAttack);
        AssertThat(table.Get("attack", BindingSlot.Primary)).IsEqual("key:75");
    }

    [TestCase]
    public void Assign_GamepadAndKeyboardDoNotConflict()
    {
        var table = BindingTable.Defaults();

        var conflict = table.Assign("attack", BindingSlot.Gamepad, "pad:99");

        AssertThat(conflict).IsNull();
        AssertThat(table.Get("attack", BindingSlot.Primary)).IsEqual("key:74");
    }

    [TestCase]
    public void FromDictionary_IgnoresUnknownActionsAndBadShapes()
    {
        var saved = new Dictionary<string, string[]>
        {
            ["attack"] = ["key:72", "", "pad:2"],
            ["nao_existe"] = ["key:1", "", ""],
            ["heavy"] = ["key:1"],
        };

        var table = BindingTable.FromDictionary(saved);

        AssertThat(table.Get("attack", BindingSlot.Primary)).IsEqual("key:72");
        AssertThat(table.Get("heavy", BindingSlot.Primary)).IsEqual("key:75");
        AssertThat(table.Get("nao_existe", BindingSlot.Primary)).IsEqual(string.Empty);
    }

    [TestCase]
    public void ToDictionary_RoundTripsAndIsACopy()
    {
        var table = BindingTable.Defaults();
        var copy = table.ToDictionary();
        copy["attack"][0] = "key:1";

        AssertThat(table.Get("attack", BindingSlot.Primary)).IsEqual("key:74");
        AssertThat(BindingTable.FromDictionary(table.ToDictionary()).Get("dodge", BindingSlot.Primary)).IsEqual("key:32");
    }
}
