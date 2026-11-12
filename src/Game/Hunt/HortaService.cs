using Vespera.Core.Horta;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Story;

namespace Vespera.Game.Hunter;

/// <summary>
/// Horta do Ato II no jogo: liga as regras puras (<see cref="HortaRules"/>) ao save, aos Lumes e à história. Cada ação
/// devolve (feito, frase para o jogador) e salva sozinha quando muda algo. Estático, sem nó (como <see cref="HuntService"/>).
/// </summary>
public static class HortaService
{
    /// <summary>A Horta abre quando a Dona Celeste pede ajuda no Pomar Alto (Ato II); antes disso nada de canteiros nem feira.</summary>
    public static bool Unlocked => StoryService.Engine.HasFlag(HortaRules.UnlockFlag);

    private static HortaRules Rules => HuntService.Horta;

    private static HortaState State => HuntService.HortaState;

    /// <summary>Aviso de interação do canteiro <paramref name="index"/>.</summary>
    public static string PlotPrompt(int index) => index < 0 || index >= State.Plots.Count ? string.Empty : Rules.PlotPrompt(State.Plots[index], index);

    public static PlotStatus StatusOf(int index) => index < 0 || index >= State.Plots.Count ? PlotStatus.Empty : Rules.StatusOf(State.Plots[index]);

    public static (bool Ok, string Message) Plant(int index, string seedId)
    {
        if (!Rules.Plant(State, index, seedId))
        {
            return (false, "Esse canteiro já está ocupado.");
        }

        var seed = Rules.Def.Seed(seedId)!;
        GameSession.Autosave();
        return (true, $"{seed.Name} plantada. Fica pronta em {seed.Days} {(seed.Days == 1 ? "volta" : "voltas")} da Brecha.");
    }

    public static (bool Ok, string Message) Harvest(int index)
    {
        if (Rules.Harvest(State, index) is not { } got)
        {
            return (false, "Ainda não deu.");
        }

        GameSession.Autosave();
        return (true, $"Colheita: +{got.Amount} {got.Seed.Name}. Leve à Nilza, na Praça.");
    }

    /// <summary>Compra a receita da Nilza com Lumes.</summary>
    public static (bool Ok, string Message) Learn(string dishId)
    {
        var result = Rules.Learn(State, dishId, amount => Progression.IsActive && Progression.Engine.TrySpendLumes(amount));
        return Finish(result, dishId, cooking: false);
    }

    /// <summary>A Nilza cozinha: gasta os ingredientes e deixa o prato pronto para a próxima caçada.</summary>
    public static (bool Ok, string Message) Cook(string dishId) => Finish(Rules.Cook(State, dishId), dishId, cooking: true);

    private static (bool Ok, string Message) Finish(FeiraResult result, string dishId, bool cooking)
    {
        if (result == FeiraResult.Ok)
        {
            GameSession.Autosave();
        }

        return (result == FeiraResult.Ok, HortaRules.FeiraMessage(result, Rules.Def.Dish(dishId), cooking));
    }
}
