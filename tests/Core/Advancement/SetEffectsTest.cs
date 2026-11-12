namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Conjuntos de Guardião (ROTEIRO-V2 §8.3): um conjunto por Guardião, bônus de 2 e 4 peças e efeitos de combate.</summary>
[TestSuite]
public class SetEffectsTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    [TestCase]
    public void EveryGuardianHasAFourPieceSetWithACombatEffectAtFour()
    {
        foreach (var guardian in Catalog.Guardians.Values)
        {
            AssertThat(guardian.ArmorSet).IsNotNull();
            var set = Catalog.Sets[guardian.ArmorSet!];
            AssertThat(set.Pieces.Count).IsEqual(4);
            AssertThat(set.Rank).IsEqual(guardian.Rank);
            AssertThat(set.Bonuses.Single(b => b.Pieces == 4).Effect).IsNotNull();
        }
    }

    [TestCase]
    public void LodoMaeTwoPiecesGiveSlowImmunityAndFourGiveGel()
    {
        var set = Catalog.Sets["conjunto_lodo_mae"];
        AssertThat(set.Bonuses.Single(b => b.Pieces == 2).Effect).IsEqual(SetEffects.SlowImmune);
        AssertThat(set.Bonuses.Single(b => b.Pieces == 4).Effect).IsEqual(SetEffects.DodgeGel);
    }

    [TestCase]
    public void EffectsAppearOnlyWithEnoughWornPieces()
    {
        var engine = AdvTestData.NewEngine();
        var set = Catalog.Sets["conjunto_lodo_mae"];
        var worn = 0;
        foreach (var piece in set.Pieces)
        {
            var item = LootRoller.NewSetPiece(set, piece);
            item.Id = engine.Progress.NextItemId++;
            engine.Progress.Items.Add(item);
            engine.Equip(item.Id);
            worn++;
            var effects = engine.Equipment().Effects;
            AssertThat(effects.Has(SetEffects.SlowImmune)).IsEqual(worn >= 2);
            AssertThat(effects.Has(SetEffects.DodgeGel)).IsEqual(worn >= 4);
        }
    }

    [TestCase]
    public void EveryEffectIdInTheDataIsKnown()
    {
        foreach (var bonus in Catalog.Sets.Values.SelectMany(s => s.Bonuses).Where(b => b.Effect is not null))
        {
            AssertThat(SetEffects.IsKnown(bonus.Effect!)).IsTrue();
        }
    }

    [TestCase]
    public void RulesBehaveAsDescribed()
    {
        AssertThat(SetEffects.KillHealAmount(100)).IsEqual(4);
        AssertThat(SetEffects.KillHealAmount(5)).IsEqual(1);
        AssertThat(SetEffects.ReduceIncoming(100, true)).IsEqual(80.0);
        AssertThat(SetEffects.ReduceIncoming(100, false)).IsEqual(100.0);
        AssertThat(SetEffects.BellRings(5)).IsFalse();
        AssertThat(SetEffects.BellRings(6)).IsTrue();
        AssertThat(SetEffects.BellRings(0)).IsFalse();
        AssertThat(SetEffects.EchoProcs(0.19)).IsTrue();
        AssertThat(SetEffects.EchoProcs(0.2)).IsFalse();
        AssertThat(SetEffects.ReflectDamage(10)).IsEqual(6.0);
    }
}
