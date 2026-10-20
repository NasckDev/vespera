using Vespera.Core.Common;

namespace Vespera.Core.Data;

/// <summary>NPC com rotina (<c>NPCS</c>, <c>data.js:83-92</c>). Aparece se <c>HourFrom &lt;= clock/60 &lt; HourTo</c>.</summary>
public sealed record WalkerDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Map { get; init; }

    public required int HourFrom { get; init; }

    /// <summary>Pode passar de 24 (Lauro vai até 27 = 03:00).</summary>
    public required int HourTo { get; init; }

    public required double Speed { get; init; }

    public required IReadOnlyList<Point2> Path { get; init; }

    /// <summary>Falas em ciclo, verbatim.</summary>
    public required IReadOnlyList<string> Lines { get; init; }
}

/// <summary>
/// NPCs: retratos, andantes e falas fixas por NPC (<c>world.js</c>, <c>extra.js</c>). Falas usam placeholders
/// <c>{name}</c>, <c>{class}</c>… preenchidos por quem exibe.
/// </summary>
public sealed record NpcsFile
{
    /// <summary>Nome do NPC → chave do retrato.</summary>
    public required IReadOnlyDictionary<string, string> Portraits { get; init; }

    public required IReadOnlyList<WalkerDef> Walkers { get; init; }

    /// <summary>npc → chave → falas (lista de 1 quando é fala única).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> Dialogue { get; init; }
}

/// <summary>Loja da Rua dos Ofícios (<c>OFICIOS</c>).</summary>
public sealed record OficioDef
{
    public required string Map { get; init; }

    public required string Name { get; init; }

    public required string Owner { get; init; }

    public required string Desc { get; init; }
}

public sealed record OficiosFile
{
    public required IReadOnlyList<OficioDef> Oficios { get; init; }
}

/// <summary>Caçador simulado (<c>HUNTERS</c>).</summary>
public sealed record HunterDef
{
    public required string Name { get; init; }

    public required string Rank { get; init; }

    public required string Weapon { get; init; }
}

/// <summary>Caçadores, Casas, chat e boatos (<c>data.js:280-292</c>).</summary>
public sealed record SocialFile
{
    public required IReadOnlyList<HunterDef> Hunters { get; init; }

    public required IReadOnlyList<string> Casas { get; init; }

    /// <summary>Placeholders <c>{b}</c> (um de <see cref="ChatBairros"/>) e <c>{c}</c> (uma Casa).</summary>
    public required IReadOnlyList<string> Chat { get; init; }

    public required IReadOnlyList<string> ChatBairros { get; init; }

    public required IReadOnlyList<string> Rumors { get; init; }

    /// <summary>Balões ao parar de andar (<c>world.js:174</c>).</summary>
    public required IReadOnlyList<string> IdleLines { get; init; }

    /// <summary>Balões aleatórios (<c>main.js:109</c>).</summary>
    public required IReadOnlyList<string> RandomLines { get; init; }
}

public enum QuestConditionKind
{
    /// <summary>Contador da missão (<c>Key</c>) ≥ <c>Target</c>.</summary>
    Counter,

    /// <summary>Nível da arma da classe ≥ <c>Target</c>.</summary>
    WeaponLevel,

    /// <summary>Selo ≥ <c>Rank</c>.</summary>
    Rank,

    /// <summary>Fechou uma Brecha do tema <c>Theme</c>.</summary>
    ThemeClosed,
}

public sealed record QuestConditionDef
{
    public required QuestConditionKind Kind { get; init; }

    public string? Key { get; init; }

    public int Target { get; init; }

    public string? Rank { get; init; }

    public string? Theme { get; init; }
}

public sealed record QuestRewardDef
{
    public int Lumes { get; init; }

    public int Potions { get; init; }

    public IReadOnlyDictionary<string, int> Mats { get; init; } = Cost.NoMaterials;
}

public sealed record QuestEndDef
{
    public required string Who { get; init; }

    public required string Text { get; init; }
}

/// <summary>Missão do Diário (<c>QUESTS</c>, <c>extra.js:100-113</c>), em ordem.</summary>
public sealed record QuestDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Desc { get; init; }

    public required QuestConditionDef Condition { get; init; }

    public required QuestRewardDef Reward { get; init; }

    /// <summary>Mostra "n/alvo" no HUD.</summary>
    public bool ShowProgress { get; init; }

    /// <summary>Diálogo ao concluir.</summary>
    public QuestEndDef? End { get; init; }
}

public sealed record QuestsFile
{
    public required IReadOnlyList<QuestDef> Quests { get; init; }
}
