using System.Text.Json.Serialization;
using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>Peça de equipamento possuída: conjunto de Guardião ou peça rara com afixo. Os atributos são gravados na criação.</summary>
public sealed class ItemInstance
{
    /// <summary>Id único dentro do save (<see cref="HunterProgress.NextItemId"/>).</summary>
    public int Id { get; set; }

    /// <summary>Id da peça do conjunto, ou "rara:{slot}" para peças raras sorteadas.</summary>
    public string DefId { get; set; } = string.Empty;

    public string Slot { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Rank Rank { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? SetId { get; set; }

    public string? AffixId { get; set; }

    public StatBlock Stats { get; set; } = StatBlock.Zero;

    [JsonIgnore]
    public bool IsRare => AffixId is not null;
}

/// <summary>Tudo o que o caçador possui na v2: XP, moedas, materiais, peças, armas, Ecos, conquistas e a Lanterna perdida. Vai dentro do <c>SaveData</c>.</summary>
public sealed class HunterProgress
{
    public int Xp { get; set; }

    /// <summary>Moeda da cidade (comida, poções, mapas).</summary>
    public int Lumes { get; set; }

    /// <summary>Moeda do Avesso (forja e Ecos).</summary>
    public int Fragmentos { get; set; }

    public Dictionary<string, int> Materials { get; set; } = [];

    /// <summary>Peças possuídas, equipadas ou não.</summary>
    public List<ItemInstance> Items { get; set; } = [];

    public int NextItemId { get; set; } = 1;

    /// <summary>Nível da arma de cada classe (1 a <c>maxLevel</c>).</summary>
    public Dictionary<string, int> WeaponLevels { get; set; } = [];

    /// <summary>slot → id da peça equipada.</summary>
    public Dictionary<string, int> Equipped { get; set; } = [];

    public List<string> Echoes { get; set; } = [];

    /// <summary>Ecos equipados por posição (null = vazio). O tamanho vem de <c>echoes.json</c>.</summary>
    public List<string?> EchoSlots { get; set; } = [];

    public List<string> GuardiansDefeated { get; set; } = [];

    public List<string> ExamsPassed { get; set; } = [];

    /// <summary>bairro → Fragmentos deixados na Brecha onde o caçador caiu.</summary>
    public Dictionary<string, int> LostFragmentos { get; set; } = [];

    /// <summary>Bairro da Brecha em andamento (null = fora de Brecha). Se o save carrega com isto preenchido, a caçada foi abandonada.</summary>
    public string? RunNeighborhood { get; set; }

    /// <summary>Fragmentos da caçada em andamento, ainda não levados para a cidade.</summary>
    public int RunFragmentos { get; set; }

    public int MaterialCount(string id) => Materials.TryGetValue(id, out var n) ? n : 0;

    /// <summary>Estado de um save v2 antigo: só XP e Lumes são aproveitados.</summary>
    public static HunterProgress FromLegacy(int xp, int lumes) => new() { Xp = Math.Max(0, xp), Lumes = Math.Max(0, lumes) };
}
