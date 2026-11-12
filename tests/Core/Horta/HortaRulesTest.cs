using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Horta;

namespace Vespera.Tests.Core.Horta;

/// <summary>Horta do Ato II: canteiros, crescimento por volta da Brecha, colheita, receitas da feira e prato da caçada.</summary>
[TestSuite]
public class HortaRulesTest
{
    private static HortaDef Def => HortaDef.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Horta", "horta.json")));

    private static (HortaRules Rules, HortaState State) Fresh()
    {
        var def = Def;
        var state = new HortaState();
        state.Sanitize(def);
        return (new HortaRules(def), state);
    }

    [TestCase]
    public void DataHasSixPlotsAndThreeSeeds()
    {
        AssertThat(Def.Plots).IsEqual(6);
        AssertThat(Def.Seeds.Count).IsEqual(3);
        AssertThat(Fresh().State.Plots.Count).IsEqual(6);
    }

    [TestCase]
    public void PlantedSeedIsReadyOnlyAfterItsDays()
    {
        var (rules, state) = Fresh();
        AssertThat(rules.Plant(state, 0, "raiz_doce")).IsTrue();
        AssertThat(rules.StatusOf(state.Plots[0])).IsEqual(PlotStatus.Growing);
        rules.NewDay(state);
        AssertThat(rules.StatusOf(state.Plots[0])).IsEqual(PlotStatus.Growing);
        AssertThat(rules.DaysLeft(state.Plots[0])).IsEqual(1);
        rules.NewDay(state);
        AssertThat(rules.StatusOf(state.Plots[0])).IsEqual(PlotStatus.Ready);
        rules.NewDay(state);
        AssertThat(state.Plots[0].Days).IsEqual(2);
    }

    [TestCase]
    public void CannotPlantOnBusyPlotOrWithUnknownSeedOrOutOfRange()
    {
        var (rules, state) = Fresh();
        AssertThat(rules.Plant(state, 1, "abobora_roxa")).IsTrue();
        AssertThat(rules.Plant(state, 1, "erva_de_lanterna")).IsFalse();
        AssertThat(rules.Plant(state, 2, "nao_existe")).IsFalse();
        AssertThat(rules.Plant(state, 6, "abobora_roxa")).IsFalse();
        AssertThat(rules.Plant(state, -1, "abobora_roxa")).IsFalse();
    }

    [TestCase]
    public void HarvestGivesIngredientsAndEmptiesThePlot()
    {
        var (rules, state) = Fresh();
        AssertThat(rules.Harvest(state, 0)).IsNull();
        rules.Plant(state, 0, "abobora_roxa");
        AssertThat(rules.Harvest(state, 0)).IsNull();
        rules.NewDay(state);
        var result = rules.Harvest(state, 0);
        AssertThat(result is not null).IsTrue();
        AssertThat(state.Count("abobora_roxa")).IsEqual(2);
        AssertThat(rules.StatusOf(state.Plots[0])).IsEqual(PlotStatus.Empty);
    }

    [TestCase]
    public void LearnChargesOnceAndFailsWithoutMoney()
    {
        var (rules, state) = Fresh();
        AssertThat(rules.Learn(state, "sopa_de_abobora", _ => false)).IsEqual(FeiraResult.NoMoney);
        var paid = 0;
        AssertThat(rules.Learn(state, "sopa_de_abobora", p => { paid = p; return true; })).IsEqual(FeiraResult.Ok);
        AssertThat(paid).IsEqual(20);
        AssertThat(rules.Learn(state, "sopa_de_abobora", _ => true)).IsEqual(FeiraResult.AlreadyLearned);
        AssertThat(rules.Learn(state, "nada", _ => true)).IsEqual(FeiraResult.Unknown);
    }

    [TestCase]
    public void CookNeedsRecipeIngredientsAndNoMealYet()
    {
        var (rules, state) = Fresh();
        AssertThat(rules.Cook(state, "sopa_de_abobora")).IsEqual(FeiraResult.NotLearned);
        rules.Learn(state, "sopa_de_abobora", _ => true);
        AssertThat(rules.Cook(state, "sopa_de_abobora")).IsEqual(FeiraResult.MissingIngredients);
        state.Pantry["abobora_roxa"] = 3;
        AssertThat(rules.Cook(state, "sopa_de_abobora")).IsEqual(FeiraResult.Ok);
        AssertThat(state.Count("abobora_roxa")).IsEqual(1);
        AssertThat(state.Meal).IsEqual("sopa_de_abobora");
        AssertThat(rules.Cook(state, "sopa_de_abobora")).IsEqual(FeiraResult.MealAlreadyReady);
    }

    [TestCase]
    public void ConsumeMealGivesTheBonusOnceThenNothing()
    {
        var (rules, state) = Fresh();
        state.Meal = "sopa_de_abobora";
        var dish = rules.ConsumeMeal(state);
        AssertThat(dish is not null).IsTrue();
        AssertThat(dish!.Bonus.DamagePct).IsEqual(8.0);
        AssertThat(state.Meal).IsNull();
        AssertThat(rules.ConsumeMeal(state)).IsNull();
    }

    [TestCase]
    public void SanitizeFixesOldOrBrokenSaves()
    {
        var def = Def;
        var state = new HortaState
        {
            Plots = [new PlotState { Seed = "sumiu", Days = 5 }],
            Pantry = new Dictionary<string, int> { ["abobora_roxa"] = -2, ["sumiu"] = 1, ["raiz_doce"] = 2 },
            Recipes = ["sopa_de_abobora", "sopa_de_abobora", "sumiu"],
            Meal = "sumiu",
        };
        state.Sanitize(def);
        AssertThat(state.Plots.Count).IsEqual(6);
        AssertThat(state.Plots[0].Seed).IsNull();
        AssertThat(state.Pantry.Count).IsEqual(1);
        AssertThat(state.Recipes.Count).IsEqual(1);
        AssertThat(state.Meal).IsNull();
    }

    [TestCase]
    public void EveryDishUsesIngredientsASeedGives()
    {
        var def = Def;
        foreach (var dish in def.Dishes)
        {
            AssertThat(dish.Ingredients.Keys.All(k => def.Seed(k) is not null)).IsTrue();
        }
    }
}
