namespace Vespera.Core.Horta;

/// <summary>Um canteiro: vazio (<see cref="Seed"/> nulo) ou com uma semente e as voltas da Brecha que ela já viu.</summary>
public sealed class PlotState
{
    public string? Seed { get; set; }

    public int Days { get; set; }
}

/// <summary>Estado da Horta no save (dentro do bloco <c>hunt</c>). Saves antigos carregam com tudo vazio.</summary>
public sealed class HortaState
{
    public List<PlotState> Plots { get; set; } = [];

    /// <summary>Ingredientes colhidos (id da semente → quantidade).</summary>
    public Dictionary<string, int> Pantry { get; set; } = [];

    /// <summary>Receitas compradas da Nilza.</summary>
    public List<string> Recipes { get; set; } = [];

    /// <summary>Prato pronto para a próxima caçada (id); nulo = nenhum.</summary>
    public string? Meal { get; set; }

    /// <summary>Garante o número de canteiros, listas presentes e nada negativo; descarta ids que não existem mais.</summary>
    public void Sanitize(HortaDef def)
    {
        Plots ??= [];
        Pantry ??= [];
        Recipes ??= [];
        Plots = [.. Plots.Take(def.Plots).Select(p => p ?? new PlotState())];
        while (Plots.Count < def.Plots)
        {
            Plots.Add(new PlotState());
        }

        foreach (var plot in Plots)
        {
            if (plot.Seed is not null && def.Seed(plot.Seed) is null)
            {
                plot.Seed = null;
            }

            plot.Days = plot.Seed is null ? 0 : Math.Max(0, plot.Days);
        }

        foreach (var key in Pantry.Keys.Where(k => def.Seed(k) is null || Pantry[k] <= 0).ToList())
        {
            Pantry.Remove(key);
        }

        Recipes = [.. Recipes.Where(r => def.Dish(r) is not null).Distinct()];
        if (Meal is not null && def.Dish(Meal) is null)
        {
            Meal = null;
        }
    }

    public int Count(string ingredient) => Pantry.GetValueOrDefault(ingredient);
}
