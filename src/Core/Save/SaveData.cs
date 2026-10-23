using Vespera.Core.Advancement;
using Vespera.Core.Data;

namespace Vespera.Core.Save;

/// <summary>
/// Estado persistido do jogador (formato v2, espelha o <c>G.s</c> da v2 — spec 02 §2.3). Serializado como JSON
/// em <c>user://save.json</c> pelo SaveService. Ao mudar o formato: incremente <see cref="CurrentVersion"/> e
/// adicione a migração em <see cref="SaveSerializer"/>. Ids referem-se ao <see cref="GameDatabase"/>.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 3;

    public int SaveVersion { get; set; } = CurrentVersion;

    public string Name { get; set; } = string.Empty;

    /// <summary>Classe permanente (id da arma). <c>cls</c> na v2.</summary>
    public string ClassId { get; set; } = string.Empty;

    /// <summary>XP total (Selo e Nível derivam dele).</summary>
    public int Xp { get; set; }

    public int Lumes { get; set; }

    /// <summary>Materiais por id (o Estilhaço de Brecha também é material).</summary>
    public Dictionary<string, int> Materials { get; set; } = [];

    /// <summary>Equipamentos na mochila, não equipados (<c>gear</c>). Pode repetir.</summary>
    public List<string> Backpack { get; set; } = [];

    /// <summary>Equipados: slot → id (<c>equip</c>).</summary>
    public Dictionary<string, string> Equipment { get; set; } = [];

    /// <summary>Armas possuídas: id → nível (1–5).</summary>
    public Dictionary<string, int> Weapons { get; set; } = [];

    /// <summary>Arma empunhada (<c>cur</c>). Na v2 é sempre a da classe.</summary>
    public string CurrentWeapon { get; set; } = string.Empty;

    /// <summary>Ecos absorvidos.</summary>
    public List<string> Ecos { get; set; } = [];

    public string? EquippedEco { get; set; }

    /// <summary>Afinações por Eco (0–2).</summary>
    public Dictionary<string, int> EcoLevels { get; set; } = [];

    /// <summary>Infusões compradas por arma (<c>elems</c>).</summary>
    public Dictionary<string, List<string>> Infusions { get; set; } = [];

    /// <summary>Infusão ativa por arma (<c>elem</c>); ausente = nenhuma.</summary>
    public Dictionary<string, string> ActiveInfusion { get; set; } = [];

    public int Potions { get; set; }

    /// <summary>Lanternas de outros caçadores carregadas (nomes), para entregar à Odete.</summary>
    public List<string> CarriedLanterns { get; set; } = [];

    public int Day { get; set; } = 1;

    /// <summary>Relógio do jogo em minutos (990 = 16:30). Passa de 1440 durante a madrugada.</summary>
    public double ClockMinutes { get; set; }

    /// <summary>Lanterna que ficou na Brecha ao morrer, com a bolsa (<c>lost</c>). Null = com a Lanterna.</summary>
    public LostLantern? LostLantern { get; set; }

    /// <summary>Passo do tutorial já mostrado (0 = ainda não). A v2 não usava <c>tut</c>.</summary>
    public int TutorialStep { get; set; }

    /// <summary>Brechas fechadas (<c>closed</c>). 0 = a próxima é a "fácil".</summary>
    public int BrechasClosed { get; set; }

    /// <summary>A primeira Brecha (sempre do Mercado) já abriu. A v2 não salvava (<c>firstDone</c>); DECISIONS D6.</summary>
    public bool FirstBrechaDone { get; set; }

    public QuestProgress Quests { get; set; } = new();

    /// <summary>
    /// Progressão da v2 (XP, Fragmentos, materiais, peças, nível da arma, Ecos, conquistas, Lanterna). Veio com a versão 3 do save;
    /// os campos <c>Xp</c>, <c>Lumes</c>, <c>Materials</c>, <c>Backpack</c>, <c>Equipment</c>, <c>Weapons</c> e <c>Ecos</c> acima são da v0.1
    /// e a v2 não os usa mais (a migração 2→3 só copia XP e Lumes para cá).
    /// </summary>
    public HunterProgress Hunter { get; set; } = new();

    /// <summary>
    /// Flags, objetivo e fase da história (Prólogo + Atos). Faz parte da versão 3; saves v1/v2 sem "story" carregam com a
    /// história zerada e um save v2 que já tinha o bloco (build de testes da história) o mantém. Ver SaveSerializer.Sanitize.
    /// </summary>
    public Vespera.Core.Story.StoryProgress Story { get; set; } = new();

    /// <summary>Caçadas da Brecha (contratos, poções, flores, poço, caçadas completas). Saves sem o bloco carregam zerados.</summary>
    public Vespera.Core.Hunt.HuntState Hunt { get; set; } = new();

    public DateTimeOffset SavedAt { get; set; }

    /// <summary>Jogo novo (<c>newSave(name, cls)</c>, <c>core.js:208-212</c>): só a arma da classe, nível 1.</summary>
    public static SaveData NewGame(GameDatabase db, string? name, string classId)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!db.Classes.ContainsKey(classId))
        {
            throw new ArgumentException($"Classe desconhecida: '{classId}'.", nameof(classId));
        }

        var start = db.Rules.Start;
        return new SaveData
        {
            Name = CleanName(name, start),
            ClassId = classId,
            Lumes = start.Lumes,
            Potions = start.Potions,
            Backpack = [.. start.Backpack],
            Equipment = new Dictionary<string, string>(start.Equipment),
            Weapons = new Dictionary<string, int> { [classId] = 1 },
            CurrentWeapon = classId,
            ClockMinutes = db.SeasonData.Clock.StartMinutes,
        };
    }

    /// <summary>Nome aparado e limitado (máx. 14); vazio vira o padrão ("Rui").</summary>
    public static string CleanName(string? name, StartRules start)
    {
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length > start.NameMaxLength)
        {
            clean = clean[..start.NameMaxLength].TrimEnd();
        }

        return clean.Length == 0 ? start.Name : clean;
    }

    public int MaterialCount(string materialId) => Materials.TryGetValue(materialId, out var n) ? n : 0;

    public int WeaponLevel(string weaponId) => Weapons.TryGetValue(weaponId, out var lv) ? lv : 0;

    /// <summary>Possui o equipamento (na mochila ou equipado).</summary>
    public bool OwnsGear(string gearId) => Backpack.Contains(gearId) || Equipment.ContainsValue(gearId);
}

/// <summary>Tudo o que foi coletado dentro de uma Brecha e ainda não entrou no save (<c>run.bag</c>).</summary>
public sealed class RunBag
{
    public Dictionary<string, int> Materials { get; set; } = [];

    public List<string> Gear { get; set; } = [];

    public int Lumes { get; set; }

    /// <summary>Lanternas de outros caçadores achadas na Brecha.</summary>
    public List<string> Lanterns { get; set; } = [];

    public bool IsEmpty => Lumes == 0 && Gear.Count == 0 && Lanterns.Count == 0 && Materials.Values.All(n => n <= 0);
}

/// <summary>Lanterna deixada na Brecha ao morrer (<c>s.lost</c>, spec 02 §3.13).</summary>
public sealed class LostLantern
{
    /// <summary>Id da Brecha na sessão em que morreu (Brechas não são salvas; 0 = desconhecida).</summary>
    public int BrechaId { get; set; }

    public string Bairro { get; set; } = string.Empty;

    public string? ThemeId { get; set; }

    public RunBag Bag { get; set; } = new();

    public string MapId { get; set; } = string.Empty;

    /// <summary>Sala da Brecha: "theme" ou "core".</summary>
    public string Room { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>Progresso do Diário (<c>qi</c>/<c>qc</c>, <c>extra.js:100-128</c>).</summary>
public sealed class QuestProgress
{
    /// <summary>Índice da missão atual em <c>quests.json</c>; igual ao total = todas cumpridas.</summary>
    public int Index { get; set; }

    /// <summary>Contadores: odete, dummy, entered, ults, bosses, broke.</summary>
    public Dictionary<string, int> Counters { get; set; } = [];

    /// <summary>Temas com Brecha fechada pelo jogador (<c>qc.bt</c>).</summary>
    public List<string> ClosedThemes { get; set; } = [];

    public int Counter(string key) => Counters.TryGetValue(key, out var n) ? n : 0;
}
