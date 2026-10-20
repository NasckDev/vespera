using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Data;

/// <summary>Dados inválidos: JSON malformado, campo obrigatório ausente ou referência cruzada quebrada.</summary>
public sealed class GameDataException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Todos os dados do jogo (DECISIONS D5), carregados dos JSON de <c>src/Data</c> sem depender do Godot.
/// <see cref="Load"/> recebe uma função que lê o conteúdo de cada arquivo pelo nome, então serve igual
/// para o jogo (<c>FileAccess</c> em <c>res://</c>), para os testes (disco) e para um servidor futuro.
/// Depois de carregar, valida todas as referências cruzadas e lança <see cref="GameDataException"/> com a lista de problemas.
/// </summary>
public sealed partial class GameDatabase
{
    public static IReadOnlyList<string> FileNames { get; } =
    [
        "palette.json", "ranks.json", "seasons.json", "themes.json", "weapons.json", "elements.json", "materials.json",
        "gear.json", "recipes.json", "shops.json", "monsters.json", "ecos.json", "ultimates.json", "classes.json",
        "npcs.json", "oficios.json", "social.json", "quests.json", "rules.json",
    ];

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private GameDatabase()
    {
    }

    public PaletteFile Palette { get; private init; } = null!;

    /// <summary>Selos, HP base e curva de nível.</summary>
    public RanksFile RankData { get; private init; } = null!;

    /// <summary>Selos do mais baixo (E) ao mais alto (ALVOR). O índice é o <c>rankIdx</c> do protótipo.</summary>
    public IReadOnlyList<RankDef> Ranks => RankData.Ranks;

    public SeasonsFile SeasonData { get; private init; } = null!;

    public IReadOnlyList<ThemeDef> ThemeList { get; private init; } = null!;

    public IReadOnlyDictionary<string, ThemeDef> Themes { get; private init; } = null!;

    public WeaponsFile WeaponData { get; private init; } = null!;

    /// <summary>Armas em ordem de hotbar.</summary>
    public IReadOnlyList<WeaponDef> WeaponOrder => WeaponData.Weapons;

    public IReadOnlyDictionary<string, WeaponDef> Weapons { get; private init; } = null!;

    public IReadOnlyDictionary<string, ElementDef> Elements { get; private init; } = null!;

    public MaterialsFile MaterialData { get; private init; } = null!;

    public IReadOnlyDictionary<string, MaterialDef> Materials { get; private init; } = null!;

    public GearFile GearData { get; private init; } = null!;

    public IReadOnlyDictionary<string, GearDef> Gear { get; private init; } = null!;

    /// <summary>Slots na ordem do protótipo (arma, cabeça, peito, lanterna, mãos, capa, botas, relíquia).</summary>
    public IReadOnlyList<GearSlotDef> GearSlots => GearData.Slots;

    public IReadOnlyDictionary<string, RarityDef> Rarities { get; private init; } = null!;

    public IReadOnlyList<RecipeDef> Recipes { get; private init; } = null!;

    public ShopsFile Shops { get; private init; } = null!;

    public IReadOnlyDictionary<string, ServiceDef> Services { get; private init; } = null!;

    public IReadOnlyDictionary<string, MonsterDef> Monsters { get; private init; } = null!;

    public EcosFile EcoData { get; private init; } = null!;

    public IReadOnlyDictionary<string, EcoDef> Ecos { get; private init; } = null!;

    public UltimatesFile UltimateData { get; private init; } = null!;

    /// <summary>Golpe Supremo por id de classe.</summary>
    public IReadOnlyDictionary<string, UltimateDef> Ultimates { get; private init; } = null!;

    public IReadOnlyList<ClassDef> ClassList { get; private init; } = null!;

    public IReadOnlyDictionary<string, ClassDef> Classes { get; private init; } = null!;

    public NpcsFile Npcs { get; private init; } = null!;

    public IReadOnlyList<OficioDef> Oficios { get; private init; } = null!;

    public SocialFile Social { get; private init; } = null!;

    /// <summary>Missões em ordem (o save guarda o índice da atual).</summary>
    public IReadOnlyList<QuestDef> Quests { get; private init; } = null!;

    public RulesFile Rules { get; private init; } = null!;

    /// <summary>Índice do Selo pelo id ("E" → 0, "ALVOR" → 9), ou -1.</summary>
    public int RankIndexOf(string rankId)
    {
        for (var i = 0; i < Ranks.Count; i++)
        {
            if (Ranks[i].Id == rankId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Carrega e valida todos os <see cref="FileNames"/>. <paramref name="readFile"/> recebe o nome do arquivo (ex.: "monsters.json").</summary>
    public static GameDatabase Load(Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(readFile);

        var themes = Parse<ThemesFile>(readFile, "themes.json").Themes;
        var weapons = Parse<WeaponsFile>(readFile, "weapons.json");
        var materials = Parse<MaterialsFile>(readFile, "materials.json");
        var gear = Parse<GearFile>(readFile, "gear.json");
        var shops = Parse<ShopsFile>(readFile, "shops.json");
        var ecos = Parse<EcosFile>(readFile, "ecos.json");
        var ultimates = Parse<UltimatesFile>(readFile, "ultimates.json");
        var classes = Parse<ClassesFile>(readFile, "classes.json").Classes;
        var errors = new List<string>();

        var db = new GameDatabase
        {
            Palette = Parse<PaletteFile>(readFile, "palette.json"),
            RankData = Parse<RanksFile>(readFile, "ranks.json"),
            SeasonData = Parse<SeasonsFile>(readFile, "seasons.json"),
            ThemeList = themes,
            Themes = Index(themes, t => t.Id, "tema", errors),
            WeaponData = weapons,
            Weapons = Index(weapons.Weapons, w => w.Id, "arma", errors),
            Elements = Index(Parse<ElementsFile>(readFile, "elements.json").Elements, e => e.Id, "elemento", errors),
            MaterialData = materials,
            Materials = Index(materials.Materials, m => m.Id, "material", errors),
            GearData = gear,
            Gear = Index(gear.Gear, g => g.Id, "equipamento", errors),
            Rarities = Index(gear.Rarities, r => r.Id, "raridade", errors),
            Recipes = Parse<RecipesFile>(readFile, "recipes.json").Recipes,
            Shops = shops,
            Services = Index(shops.Services, s => s.Id, "serviço", errors),
            Monsters = Index(Parse<MonstersFile>(readFile, "monsters.json").Monsters, m => m.Id, "monstro", errors),
            EcoData = ecos,
            Ecos = Index(ecos.Ecos, e => e.Id, "Eco", errors),
            UltimateData = ultimates,
            Ultimates = Index(ultimates.Ultimates, u => u.Class, "Supremo", errors),
            ClassList = classes,
            Classes = Index(classes, c => c.Id, "classe", errors),
            Npcs = Parse<NpcsFile>(readFile, "npcs.json"),
            Oficios = Parse<OficiosFile>(readFile, "oficios.json").Oficios,
            Social = Parse<SocialFile>(readFile, "social.json"),
            Quests = Parse<QuestsFile>(readFile, "quests.json").Quests,
            Rules = Parse<RulesFile>(readFile, "rules.json"),
        };

        db.Validate(errors);
        if (errors.Count > 0)
        {
            throw new GameDataException($"Dados do jogo inválidos ({errors.Count} problema(s)):\n- {string.Join("\n- ", errors)}");
        }

        return db;
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

    // Raízes de arquivos que só embrulham uma lista.
    private sealed record ThemesFile
    {
        public required IReadOnlyList<ThemeDef> Themes { get; init; }
    }

    private sealed record ElementsFile
    {
        public required IReadOnlyList<ElementDef> Elements { get; init; }
    }
}
