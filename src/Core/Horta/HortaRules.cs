namespace Vespera.Core.Horta;

public enum PlotStatus
{
    Empty,
    Growing,
    Ready,
}

public enum FeiraResult
{
    Ok,
    Unknown,
    AlreadyLearned,
    NotLearned,
    NoMoney,
    MissingIngredients,
    MealAlreadyReady,
}

/// <summary>
/// Regras da Horta (Ato II, ROTEIRO-V2 §8): plantar, a volta da Brecha fazer crescer, colher em ingrediente, comprar a
/// receita da Nilza na feira e cozinhar o prato que vale na próxima caçada. Lógica pura, sem Godot.
/// </summary>
public sealed class HortaRules(HortaDef def)
{
    /// <summary>Flag da história que abre a Horta e a feira: a Dona Celeste pede ajuda no Pomar Alto (Ato II).</summary>
    public const string UnlockFlag = "met_celeste";

    public HortaDef Def { get; } = def;

    public PlotStatus StatusOf(PlotState plot)
    {
        if (plot.Seed is null || Def.Seed(plot.Seed) is not { } seed)
        {
            return PlotStatus.Empty;
        }

        return plot.Days >= seed.Days ? PlotStatus.Ready : PlotStatus.Growing;
    }

    /// <summary>Voltas que ainda faltam para colher (0 se pronto ou vazio).</summary>
    public int DaysLeft(PlotState plot) => plot.Seed is not null && Def.Seed(plot.Seed) is { } seed ? Math.Max(0, seed.Days - plot.Days) : 0;

    /// <summary>Planta <paramref name="seedId"/> no canteiro. Falso se o canteiro não existe, está ocupado ou a semente é desconhecida.</summary>
    public bool Plant(HortaState state, int plotIndex, string seedId)
    {
        if (plotIndex < 0 || plotIndex >= state.Plots.Count || Def.Seed(seedId) is null || state.Plots[plotIndex].Seed is not null)
        {
            return false;
        }

        state.Plots[plotIndex] = new PlotState { Seed = seedId, Days = 0 };
        return true;
    }

    /// <summary>Uma volta da Brecha: toda planta cresce um dia (até o ponto da colheita).</summary>
    public void NewDay(HortaState state)
    {
        foreach (var plot in state.Plots.Where(p => p.Seed is not null && Def.Seed(p.Seed) is { } seed && p.Days < seed.Days))
        {
            plot.Days++;
        }
    }

    /// <summary>Colhe um canteiro pronto: o ingrediente vai para a despensa e o canteiro esvazia. Nulo se não há o que colher.</summary>
    public (SeedDef Seed, int Amount)? Harvest(HortaState state, int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= state.Plots.Count || StatusOf(state.Plots[plotIndex]) != PlotStatus.Ready)
        {
            return null;
        }

        var seed = Def.Seed(state.Plots[plotIndex].Seed)!;
        state.Pantry[seed.Id] = state.Count(seed.Id) + seed.Yield;
        state.Plots[plotIndex] = new PlotState();
        return (seed, seed.Yield);
    }

    public bool HasIngredients(HortaState state, DishDef dish) => dish.Ingredients.All(kv => state.Count(kv.Key) >= kv.Value);

    /// <summary>Compra a receita na feira. <paramref name="spend"/> cobra os Lumes (devolve falso se não há).</summary>
    public FeiraResult Learn(HortaState state, string dishId, Func<int, bool> spend)
    {
        if (Def.Dish(dishId) is not { } dish)
        {
            return FeiraResult.Unknown;
        }

        if (state.Recipes.Contains(dish.Id))
        {
            return FeiraResult.AlreadyLearned;
        }

        if (!spend(dish.Price))
        {
            return FeiraResult.NoMoney;
        }

        state.Recipes.Add(dish.Id);
        return FeiraResult.Ok;
    }

    /// <summary>A Nilza cozinha: gasta os ingredientes e deixa o prato pronto para a próxima caçada (um por vez).</summary>
    public FeiraResult Cook(HortaState state, string dishId)
    {
        if (Def.Dish(dishId) is not { } dish)
        {
            return FeiraResult.Unknown;
        }

        if (!state.Recipes.Contains(dish.Id))
        {
            return FeiraResult.NotLearned;
        }

        if (state.Meal is not null)
        {
            return FeiraResult.MealAlreadyReady;
        }

        if (!HasIngredients(state, dish))
        {
            return FeiraResult.MissingIngredients;
        }

        foreach (var (ingredient, amount) in dish.Ingredients)
        {
            var left = state.Count(ingredient) - amount;
            if (left > 0)
            {
                state.Pantry[ingredient] = left;
            }
            else
            {
                state.Pantry.Remove(ingredient);
            }
        }

        state.Meal = dish.Id;
        return FeiraResult.Ok;
    }

    /// <summary>Come o prato pronto ao entrar na Brecha: devolve o prato e esvazia. Nulo se não havia.</summary>
    public DishDef? ConsumeMeal(HortaState state)
    {
        var dish = Def.Dish(state.Meal);
        state.Meal = null;
        return dish;
    }

    /// <summary>Texto do aviso de interação do canteiro <paramref name="index"/> (0 a 5): plantar, esperar ou colher.</summary>
    public string PlotPrompt(PlotState plot, int index)
    {
        if (plot.Seed is null || Def.Seed(plot.Seed) is not { } seed)
        {
            return $"Plantar no canteiro {index + 1}";
        }

        if (StatusOf(plot) == PlotStatus.Ready)
        {
            return $"Colher {seed.Name} ({seed.Yield})";
        }

        var left = DaysLeft(plot);
        return $"{seed.Name}: falta {left} {(left == 1 ? "volta" : "voltas")} da Brecha";
    }

    /// <summary>Ingredientes colhidos numa linha ("Abóbora-roxa x2, Raiz-doce x1") ou "vazia".</summary>
    public string PantryText(HortaState state)
    {
        var items = Def.Seeds.Where(s => state.Count(s.Id) > 0).Select(s => $"{s.Name} x{state.Count(s.Id)}").ToList();
        return items.Count == 0 ? "vazia" : string.Join(", ", items);
    }

    /// <summary>Ingredientes do prato com o que já há na despensa ("Abóbora-roxa 1/2").</summary>
    public string IngredientsText(HortaState state, DishDef dish) =>
        string.Join(", ", dish.Ingredients.Select(kv => $"{Def.Seed(kv.Key)?.Name ?? kv.Key} {Math.Min(state.Count(kv.Key), kv.Value)}/{kv.Value}"));

    /// <summary>Frase para o jogador depois de comprar ou cozinhar na feira.</summary>
    public static string FeiraMessage(FeiraResult result, DishDef? dish, bool cooking)
    {
        var name = dish?.Name ?? "prato";
        return result switch
        {
            FeiraResult.Ok when cooking => $"Prato pronto: {name}. Vale na próxima caçada.",
            FeiraResult.Ok => $"Receita aprendida: {name}. -{dish?.Price ?? 0} Lumes",
            FeiraResult.AlreadyLearned => "Essa receita você já tem.",
            FeiraResult.NotLearned => "Compre a receita da Nilza primeiro.",
            FeiraResult.NoMoney => $"A receita custa {dish?.Price ?? 0} Lumes.",
            FeiraResult.MissingIngredients => "Faltam ingredientes da Horta.",
            FeiraResult.MealAlreadyReady => "Já tem um prato pronto para a próxima caçada.",
            _ => "A Nilza não conhece esse prato.",
        };
    }
}
