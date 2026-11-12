using System.Text.Json;
using System.Text.Json.Serialization;
using Vespera.Core.Hunt;

namespace Vespera.Core.Horta;

/// <summary>Uma semente da Horta: quantas voltas da Brecha leva para nascer e quantos ingredientes dá.</summary>
public sealed class SeedDef
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Voltas da Brecha até a colheita (a planta de antes da caçada fica pronta na volta).</summary>
    public int Days { get; set; } = 1;

    public int Yield { get; set; } = 1;

    public string Desc { get; set; } = string.Empty;
}

/// <summary>Um prato da feira: a Nilza vende a receita; cozinhar gasta ingredientes e vale na próxima caçada.</summary>
public sealed class DishDef
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lumes para comprar a receita da Nilza.</summary>
    public int Price { get; set; }

    public Dictionary<string, int> Ingredients { get; set; } = [];

    /// <summary>Linha do HUD durante a caçada.</summary>
    public string Label { get; set; } = string.Empty;

    public string Desc { get; set; } = string.Empty;

    /// <summary>Efeito na caçada: os mesmos campos percentuais das bênçãos.</summary>
    public BlessingDef Bonus { get; set; } = new();
}

/// <summary>Dados da Horta (data/horta/horta.json).</summary>
public sealed class HortaDef
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public int Plots { get; set; } = 6;

    public List<SeedDef> Seeds { get; set; } = [];

    public List<DishDef> Dishes { get; set; } = [];

    [JsonIgnore]
    public IEnumerable<string> IngredientIds => Seeds.Select(s => s.Id);

    public SeedDef? Seed(string? id) => Seeds.FirstOrDefault(s => s.Id == id);

    public DishDef? Dish(string? id) => Dishes.FirstOrDefault(d => d.Id == id);

    /// <summary>Lê e valida o JSON: ids únicos, pratos só com ingredientes que alguma semente dá.</summary>
    public static HortaDef Parse(string json)
    {
        var def = JsonSerializer.Deserialize<HortaDef>(json, Options) ?? throw new FormatException("horta.json vazio");
        if (def.Plots < 1 || def.Seeds.Count == 0)
        {
            throw new FormatException("horta.json precisa de canteiros e sementes");
        }

        if (def.Seeds.Select(s => s.Id).Distinct().Count() != def.Seeds.Count || def.Dishes.Select(d => d.Id).Distinct().Count() != def.Dishes.Count)
        {
            throw new FormatException("horta.json com ids repetidos");
        }

        foreach (var seed in def.Seeds.Where(s => s.Days < 1 || s.Yield < 1))
        {
            throw new FormatException($"semente '{seed.Id}' com dias ou rendimento inválido");
        }

        foreach (var dish in def.Dishes)
        {
            var unknown = dish.Ingredients.Keys.FirstOrDefault(k => def.Seed(k) is null);
            if (unknown is not null)
            {
                throw new FormatException($"prato '{dish.Id}' usa ingrediente que nenhuma semente dá: '{unknown}'");
            }
        }

        return def;
    }
}
