using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.Boot;

namespace Vespera.Tests.Game;

[TestSuite]
public class BindingTableTest
{
    [TestCase]
    public void Player2HasDefaultKeyForPotion()
    {
        var table = BindingTable.DefaultsPlayer2();
        AssertThat(table.Get("potion", BindingSlot.Primary)).IsNotEmpty();
    }

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

    private const string GuideButton = "pad:5";


    // Regressão #69: nenhuma ação do mesmo jogador divide tecla/mouse/botão/eixo (Eco 2 e Poção do J2 dividiam o Num 4).
    [TestCase]
    public void Defaults_NoTwoActionsOfSamePlayerShareACode()
    {
        foreach (var (name, table) in new[] { ("J1", BindingTable.Defaults()), ("J2", BindingTable.DefaultsPlayer2()) })
        {
            foreach (var group in new[] { new[] { BindingSlot.Primary, BindingSlot.Alternate }, new[] { BindingSlot.Gamepad } })
            {
                var seen = new Dictionary<string, string>();
                foreach (var action in table.Actions)
                {
                    foreach (var slot in group)
                    {
                        var code = table.Get(action, slot);
                        if (code.Length == 0)
                        {
                            continue;
                        }

                        var owner = seen.TryGetValue(code, out var other) ? other : null;
                        AssertThat(owner).OverrideFailureMessage($"{name}: '{code}' usado por '{owner}' e '{action}'").IsNull();
                        seen[code] = action;
                    }
                }
            }
        }
    }

    // O J1 no teclado dividido lê a coluna Principal; ela não pode bater com nenhuma tecla do J2.
    [TestCase]
    public void Defaults_Player1PrimaryKeysDoNotCollideWithPlayer2Keys()
    {
        var p1 = BindingTable.Defaults();
        var p2 = BindingTable.DefaultsPlayer2();
        foreach (var action in p1.Actions)
        {
            var code = p1.Get(action, BindingSlot.Primary);
            AssertThat(p2.UsesKeyboardCode(code)).OverrideFailureMessage($"J1 '{action}' ({code}) colide com o J2").IsFalse();
        }
    }

    // Regressão #69: o botão Guide (pad:5) é capturado pelo sistema e falta em muitos controles.
    [TestCase]
    public void Defaults_NothingUsesTheGuideButton()
    {
        foreach (var table in new[] { BindingTable.Defaults(), BindingTable.DefaultsPlayer2() })
        {
            foreach (var action in table.Actions)
            {
                foreach (var slot in new[] { BindingSlot.Primary, BindingSlot.Alternate, BindingSlot.Gamepad })
                {
                    AssertThat(table.Get(action, slot)).OverrideFailureMessage($"'{action}' usa o botão Guide").IsNotEqual(GuideButton);
                }
            }
        }
    }
}
