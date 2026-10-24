namespace Vespera.Tests.Core.Advancement;

using System.Linq;
using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Peças raras com nome próprio por rank, no tema do ícone pintado (issue #44).</summary>
[TestSuite]
public class GearNamesTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    [TestCase]
    public void EveryRareTemplateHasAThemedNamePerRank()
    {
        foreach (var template in Catalog.GearData.RareTemplates)
        {
            var names = Catalog.Ranks.Select(step => template.NameFor(step.Id)).ToList();
            AssertThat(names.Distinct().Count()).IsEqual(names.Count);
            AssertThat(names.Contains(template.Name)).IsFalse();
        }
    }

    [TestCase]
    public void FirstRankBootsAreThePatchedOnes()
    {
        var boots = Catalog.GearData.RareTemplates.Single(t => t.Slot == "botas");
        AssertThat(boots.NameFor(Rank.E)).IsEqual("Botas Remendadas");
        AssertThat(boots.NameFor(Rank.D)).IsEqual("Botas da Forja");
    }
}
