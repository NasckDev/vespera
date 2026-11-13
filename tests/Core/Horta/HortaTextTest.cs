using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Horta;

namespace Vespera.Tests.Core.Horta;

/// <summary>Frases da Horta para o jogador: aviso do canteiro, despensa, ingredientes e resultado da feira.</summary>
[TestSuite]
public class HortaTextTest
{
    private static (HortaRules Rules, HortaState State) Fresh()
    {
        var def = HortaDef.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Horta", "horta.json")));
        var state = new HortaState();
        state.Sanitize(def);
        return (new HortaRules(def), state);
    }

    [TestCase]
    public void PlotPrompt_FollowsThePlotFromEmptyToReady()
    {
        var (rules, state) = Fresh();

        AssertThat(rules.PlotPrompt(state.Plots[2], 2)).IsEqual("Plantar no canteiro 3");
        rules.Plant(state, 2, "raiz_doce");
        AssertThat(rules.PlotPrompt(state.Plots[2], 2)).IsEqual("Raiz-doce: falta 2 voltas da Brecha");
        rules.NewDay(state);
        AssertThat(rules.PlotPrompt(state.Plots[2], 2)).IsEqual("Raiz-doce: falta 1 volta da Brecha");
        rules.NewDay(state);
        AssertThat(rules.PlotPrompt(state.Plots[2], 2)).IsEqual("Colher Raiz-doce (3)");
    }

    [TestCase]
    public void PantryAndIngredientsText_ShowWhatYouHave()
    {
        var (rules, state) = Fresh();
        var ensopado = rules.Def.Dish("ensopado_de_raiz")!;

        AssertThat(rules.PantryText(state)).IsEqual("vazia");
        state.Pantry["raiz_doce"] = 2;
        state.Pantry["abobora_roxa"] = 1;
        AssertThat(rules.PantryText(state)).IsEqual("Abóbora-roxa x1, Raiz-doce x2");
        AssertThat(rules.IngredientsText(state, ensopado)).IsEqual("Raiz-doce 2/3, Abóbora-roxa 1/1");
    }

    [TestCase]
    public void FeiraMessage_ExplainsEveryResult()
    {
        var (rules, _) = Fresh();
        var sopa = rules.Def.Dish("sopa_de_abobora");

        AssertThat(HortaRules.FeiraMessage(FeiraResult.Ok, sopa, cooking: false)).IsEqual("Receita aprendida: Sopa de Abóbora-roxa. -20 Lumes");
        AssertThat(HortaRules.FeiraMessage(FeiraResult.Ok, sopa, cooking: true)).IsEqual("Prato pronto: Sopa de Abóbora-roxa. Vale na próxima caçada.");
        AssertThat(HortaRules.FeiraMessage(FeiraResult.NoMoney, sopa, cooking: false)).IsEqual("A receita custa 20 Lumes.");
        AssertThat(HortaRules.FeiraMessage(FeiraResult.MissingIngredients, sopa, cooking: true)).IsEqual("Faltam ingredientes da Horta.");
        AssertThat(HortaRules.FeiraMessage(FeiraResult.Unknown, null, cooking: false)).IsEqual("A Nilza não conhece esse prato.");
    }
}
