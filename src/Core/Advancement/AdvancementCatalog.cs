using System.Text.Json;
using System.Text.Json.Serialization;
using Vespera.Core.Data;
using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>
/// Dados de progressão da v2 (<c>data/progressao/*.json</c>): escada de ranks, exames, Guardiões, materiais, armas por classe,
/// conjuntos de armadura, Ecos e tabelas de loot. Independente do <see cref="GameDatabase"/> da v0.1 e sem Godot:
/// <see cref="Load"/> recebe uma função que lê cada arquivo pelo nome. Lança <see cref="GameDataException"/> com a lista de problemas.
/// </summary>
public sealed partial class AdvancementCatalog
{
    public static IReadOnlyList<string> FileNames { get; } =
        ["ranks.json", "exams.json", "guardians.json", "materials.json", "weapons.json", "gear.json", "echoes.json", "loot.json"];

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private AdvancementCatalog()
    {
    }

    public RanksFile RankData { get; private init; } = null!;

    /// <summary>A escada em ordem (E ... Alvor). O índice aqui é o "índice de rank" de todo o resto.</summary>
    public IReadOnlyList<RankStepDef> Ranks => RankData.Ranks;

    public IReadOnlyList<ExamDef> ExamList { get; private init; } = null!;

    public IReadOnlyDictionary<string, ExamDef> Exams { get; private init; } = null!;

    public IReadOnlyDictionary<string, GuardianDef> Guardians { get; private init; } = null!;

    public IReadOnlyList<HunterMaterialDef> MaterialList { get; private init; } = null!;

    public IReadOnlyDictionary<string, HunterMaterialDef> Materials { get; private init; } = null!;

    public WeaponsFile WeaponData { get; private init; } = null!;

    public IReadOnlyDictionary<string, ClassWeaponDef> Weapons { get; private init; } = null!;

    public GearFile GearData { get; private init; } = null!;

    public IReadOnlyDictionary<string, ArmorSetDef> Sets { get; private init; } = null!;

    public EchoesFile EchoData { get; private init; } = null!;

    public IReadOnlyDictionary<string, EchoDef> Echoes { get; private init; } = null!;

    public LootFile Loot { get; private init; } = null!;

    public int TopRankIndex => Ranks.Count - 1;

    public int RankIndex(Rank rank)
    {
        for (var i = 0; i < Ranks.Count; i++)
        {
            if (Ranks[i].Id == rank)
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank fora da escada da v2.");
    }

    public RankStepDef Step(Rank rank) => Ranks[RankIndex(rank)];

    public ExamDef? ExamFor(Rank rank) => ExamList.FirstOrDefault(e => e.Rank == rank);

    /// <summary>Materiais comuns do rank, na ordem do arquivo (o primeiro é o "principal" da forja, o segundo o "secundário").</summary>
    public IReadOnlyList<HunterMaterialDef> CommonMaterials(Rank rank) =>
        [.. MaterialList.Where(m => m.Rank == rank && m.Kind == MaterialKind.Common)];

    public static AdvancementCatalog Load(Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(readFile);
        var exams = Parse<ExamsFile>(readFile, "exams.json").Exams;
        var guardians = Parse<GuardiansFile>(readFile, "guardians.json").Guardians;
        var materials = Parse<MaterialsFile>(readFile, "materials.json").Materials;
        var weapons = Parse<WeaponsFile>(readFile, "weapons.json");
        var gear = Parse<GearFile>(readFile, "gear.json");
        var echoes = Parse<EchoesFile>(readFile, "echoes.json");
        var errors = new List<string>();

        var catalog = new AdvancementCatalog
        {
            RankData = Parse<RanksFile>(readFile, "ranks.json"),
            ExamList = exams,
            Exams = Index(exams, e => e.Id, "exame", errors),
            Guardians = Index(guardians, g => g.Id, "Guardião", errors),
            MaterialList = materials,
            Materials = Index(materials, m => m.Id, "material", errors),
            WeaponData = weapons,
            Weapons = Index(weapons.Weapons, w => w.Id, "arma", errors),
            GearData = gear,
            Sets = Index(gear.Sets, s => s.Id, "conjunto", errors),
            EchoData = echoes,
            Echoes = Index(echoes.Echoes, e => e.Id, "Eco", errors),
            Loot = Parse<LootFile>(readFile, "loot.json"),
        };

        catalog.Validate(errors);
        if (errors.Count > 0)
        {
            throw new GameDataException($"Dados de progressão inválidos ({errors.Count} problema(s)):\n- {string.Join("\n- ", errors)}");
        }

        return catalog;
    }

    private static T Parse<T>(Func<string, string> readFile, string fileName)
        where T : class
    {
        string json;
        try
        {
            json = readFile(fileName);
        }
        catch (Exception ex) when (ex is not GameDataException)
        {
            throw new GameDataException($"Não foi possível ler {fileName}: {ex.Message}", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new GameDataException($"{fileName} está vazio.");
        }
        catch (JsonException ex)
        {
            throw new GameDataException($"{fileName} inválido: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, T> Index<T>(IReadOnlyList<T> items, Func<T, string> key, string domain, List<string> errors)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!map.TryAdd(key(item), item))
            {
                errors.Add($"{domain} '{key(item)}' duplicado");
            }
        }

        return map;
    }
}
