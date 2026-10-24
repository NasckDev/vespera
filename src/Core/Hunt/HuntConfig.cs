using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Hunt;

/// <summary>
/// Regras das caçadas da Brecha (data/cacada/cacada.json): como montar as salas de cada entrada, a variante de elite,
/// os contratos do mural, as poções da barraca, as flores, o poço e os objetos quebráveis. Só dados: quem sorteia é o
/// <see cref="RunPlanner"/> e o <see cref="ContractBoard"/>.
/// </summary>
public sealed class HuntConfig
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public int RoomsMin { get; set; } = 5;

    /// <summary>Chão da última sala (Covil do Guardião). Ato I: esgoto_arena; Ato II: pomar_arena.</summary>
    public string FinalGround { get; set; } = "esgoto_arena";

    /// <summary>Monstro comum da Brecha (a primeira sala só tem dele).</summary>
    public string CommonMonster { get; set; } = "lodo";

    /// <summary>Segundo monstro, sorteado com a fatia <see cref="EspectroShareStart"/>... (Ato I: Espectro; Ato II: Rato-Lanterna).</summary>
    public string SecondMonster { get; set; } = "espectro";

    /// <summary>Quantos nascem juntos quando sai o segundo monstro (enxame: Rato-Lanterna 3; Espectro 1).</summary>
    public int SecondSwarm { get; set; } = 1;

    public int RoomsMax { get; set; } = 7;

    /// <summary>A Brecha leva direto à arena do Guardião (Ato VI enquanto as salas das Ruas Invertidas não têm arte, issue #47).</summary>
    public bool GuardianOnly { get; set; }

    /// <summary>Caçadas completas antes de o Benício aceitar o exame (a Lodo-Mãe só aparece com o exame aprovado).</summary>
    public int GuardianAfterRuns { get; set; } = 2;

    /// <summary>Salas da primeira caçada (curta, para aprender).</summary>
    public int FirstRunRooms { get; set; } = 5;

    /// <summary>Salas da caçada com a Lodo-Mãe (contando o descanso e o covil): mais curta, o chefe é a prova.</summary>
    public int GuardianRunRooms { get; set; } = 5;

    /// <summary>Monstros a mais por onda a cada caçada completa (a Brecha "aprende" com o caçador).</summary>
    public double RunGrowth { get; set; } = 0.5;

    /// <summary>Caçadas completas que ainda aumentam as ondas.</summary>
    public int RunGrowthCap { get; set; } = 3;

    public double TreasureChance { get; set; } = 0.6;

    public double RestHealPct { get; set; } = 0.3;

    public int FirstWaveSize { get; set; } = 3;

    public double WaveSizePerRoom { get; set; } = 0.5;

    public int MaxWaveSize { get; set; } = 6;

    /// <summary>A partir desta sala (índice), as salas de combate têm uma onda a mais.</summary>
    public int ExtraWaveFromRoom { get; set; } = 2;

    public double EspectroShareStart { get; set; } = 0.15;

    public double EspectroSharePerRoom { get; set; } = 0.07;

    public double EspectroShareMax { get; set; } = 0.45;

    public int EliteChanceFromRoom { get; set; } = 2;

    /// <summary>Chance de um monstro de elite numa onda das salas comuns (a partir de <see cref="EliteChanceFromRoom"/>).</summary>
    public double EliteChance { get; set; } = 0.3;

    public EliteDef Elite { get; set; } = new();

    public Dictionary<string, GroundDef> Grounds { get; set; } = [];

    public ContractsDef Contracts { get; set; } = new();

    public PotionDef Potions { get; set; } = new();

    public FlowerDef Flowers { get; set; } = new();

    public WellDef Well { get; set; } = new();

    public BreakableDef Breakables { get; set; } = new();

    public static HuntConfig Parse(string json)
    {
        var config = JsonSerializer.Deserialize<HuntConfig>(json, Options) ?? throw new FormatException("cacada.json vazio");
        var problems = config.Validate();
        if (problems.Count > 0)
        {
            throw new FormatException($"cacada.json: {string.Join("; ", problems)}");
        }

        return config;
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        if (RoomsMin < 3 || RoomsMax < RoomsMin)
        {
            problems.Add($"roomsMin/roomsMax inválidos ({RoomsMin}/{RoomsMax})");
        }

        if (GuardianOnly)
        {
            if (!Grounds.TryGetValue(FinalGround, out var arena) || !arena.Kinds.Contains(RoomKind.Guardian))
            {
                problems.Add($"guardianOnly sem o chão final '{FinalGround}' aceitando o Guardião");
            }

            return problems;
        }

        foreach (var kind in new[] { RoomKind.Combat, RoomKind.Elite, RoomKind.Treasure, RoomKind.Rest, RoomKind.Guardian })
        {
            if (!Grounds.Values.Any(g => g.Kinds.Contains(kind)))
            {
                problems.Add($"nenhum chão aceita salas do tipo {kind}");
            }
        }

        foreach (var (id, ground) in Grounds)
        {
            if (ground.Spawns.Count < MaxWaveSize)
            {
                problems.Add($"{id}: precisa de pelo menos {MaxWaveSize} pontos de nascimento");
            }

            if (ground.Kinds.Contains(RoomKind.Rest) && ground.Rest.Length != 2)
            {
                problems.Add($"{id}: sala de descanso sem posição da lanterna");
            }
        }

        if (Contracts.Kinds.Count == 0 || Contracts.Offered < 1)
        {
            problems.Add("contratos vazios");
        }

        if (Potions.Max < 1 || Potions.Price < 1)
        {
            problems.Add("poções inválidas");
        }

        return problems;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<RoomKind>))]
public enum RoomKind
{
    Combat,
    Elite,
    Treasure,
    Rest,
    Guardian,
}

public sealed class GroundDef
{
    public List<RoomKind> Kinds { get; set; } = [];

    /// <summary>Pontos de nascimento (pé) [x, y].</summary>
    public List<float[]> Spawns { get; set; } = [];

    /// <summary>Pé da lanterna de descanso [x, y].</summary>
    public float[] Rest { get; set; } = [];

    /// <summary>Barris e caixas que quebram [x, y].</summary>
    public List<float[]> Breakables { get; set; } = [];
}

/// <summary>Variante de elite: maior, tingida, mais vida, mais dano, mais rápida e mais agressiva (intervalo menor).</summary>
public sealed class EliteDef
{
    public double Scale { get; set; } = 1.15;

    public double Hp { get; set; } = 2.4;

    public double Damage { get; set; } = 1.25;

    public double Speed { get; set; } = 1.2;

    /// <summary>Multiplica o intervalo entre ataques (menor = mais agressivo).</summary>
    public double Period { get; set; } = 0.7;

    public double Xp { get; set; } = 3;

    public string Tint { get; set; } = "#ff9d6e";
}

public sealed class ContractsDef
{
    public int Offered { get; set; } = 2;

    public Dictionary<string, ContractKindDef> Kinds { get; set; } = [];

    /// <summary>Materiais que um contrato de Coleta pode pedir.</summary>
    public List<string> Materials { get; set; } = [];
}

public sealed class ContractKindDef
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Texto com {n} (quantidade) e {item} (nome do material).</summary>
    public string Text { get; set; } = string.Empty;

    public int Min { get; set; } = 1;

    public int Max { get; set; } = 1;

    public int Xp { get; set; }

    public int Fragmentos { get; set; }

    public int Lumes { get; set; }

    /// <summary>Alvo fixo (segundos do Relâmpago).</summary>
    public int Target { get; set; }

    /// <summary>Só aparece quando a caçada termina no Guardião.</summary>
    public bool Guardian { get; set; }
}

public sealed class PotionDef
{
    public int Price { get; set; } = 14;

    /// <summary>Preço com uma flor colhida na Praça (a flor é o ingrediente).</summary>
    public int FlowerPrice { get; set; } = 5;

    public int Max { get; set; } = 3;

    public double HealPct { get; set; } = 0.4;

    public double DrinkSeconds { get; set; } = 0.6;
}

public sealed class FlowerDef
{
    /// <summary>Flores que dá para colher na Praça a cada volta da Brecha.</summary>
    public int PerReturn { get; set; } = 3;
}

public sealed class WellDef
{
    public int Price { get; set; } = 1;

    /// <summary>Fragmentos extras (%) na próxima caçada depois de jogar a moeda.</summary>
    public int FragmentBonusPct { get; set; } = 25;
}

public sealed class BreakableDef
{
    public int[] Lumes { get; set; } = [1, 3];

    public double FragmentChance { get; set; } = 0.15;
}
