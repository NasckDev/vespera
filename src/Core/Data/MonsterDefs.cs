namespace Vespera.Core.Data;

public enum PartSide
{
    Front,
    Back,
    Any,
}

public enum AiKind
{
    Lunger,
    Caster,
    Spitter,
    Brute,
    Angler,
    Reaper,
    Knight,
}

/// <summary>Brilho do monstro: chave de cor em <c>palette.json</c> (lights) e raio.</summary>
public sealed record GlowDef
{
    public required string Light { get; init; }

    public required double Radius { get; init; }
}

/// <summary>Toast de título + subtítulo.</summary>
public sealed record ToastDef
{
    public required string Title { get; init; }

    public required string Sub { get; init; }
}

/// <summary>Parte quebrável. HP escala com o rank da Brecha.</summary>
public sealed record PartDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required PartSide Side { get; init; }

    public required double Hp { get; init; }

    public required string Drop { get; init; }

    public required int Count { get; init; }

    /// <summary>Toast ao quebrar (só partes de chefe que mudam a luta; DECISIONS D18).</summary>
    public string? Toast { get; init; }
}

/// <summary>Monstro ou chefe (<c>MON</c>, <c>data.js:222-271</c>).</summary>
public sealed record MonsterDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Prefixo do sprite (<c>_f</c>, <c>_a</c>, <c>_b</c>, <c>_d</c>, <c>_s</c>).</summary>
    public required string Sprite { get; init; }

    public required double Scale { get; init; }

    public required double Hp { get; init; }

    public required double Speed { get; init; }

    public required double Damage { get; init; }

    public required int Xp { get; init; }

    public required double Radius { get; init; }

    /// <summary>Altura do centro de acerto; null = calcular pelo sprite.</summary>
    public double? HitHeight { get; init; }

    public required AiKind Ai { get; init; }

    public bool Elite { get; init; }

    public bool Boss { get; init; }

    public bool Fly { get; init; }

    public bool Triple { get; init; }

    public bool Orbs { get; init; }

    /// <summary>Eco que pode cair.</summary>
    public string? Eco { get; init; }

    /// <summary>Chance de Eco para não-chefes (chefes usam <c>rules.loot.bossEcoChance</c>).</summary>
    public double EcoChance { get; init; }

    public string? Summon { get; init; }

    /// <summary>Cor do projétil.</summary>
    public string? Bolt { get; init; }

    /// <summary>Cor de destaque dos ataques do chefe.</summary>
    public string? Color { get; init; }

    public IReadOnlyList<string> Weak { get; init; } = [];

    public IReadOnlyList<string> Res { get; init; } = [];

    public GlowDef? Glow { get; init; }

    /// <summary>Toast ao chegar a 50% de HP (fase 2 / invocação).</summary>
    public ToastDef? Phase2 { get; init; }

    public required IReadOnlyList<PartDef> Parts { get; init; }
}

public sealed record MonstersFile
{
    public required IReadOnlyList<MonsterDef> Monsters { get; init; }
}

/// <summary>Afinação de Ecos na Vesna (<c>world.js:295</c>).</summary>
public sealed record EcoTuningDef
{
    public required int MaxLevel { get; init; }

    public required int LumesBase { get; init; }

    public required int LumesPerLevel { get; init; }

    public required string Material { get; init; }

    public required int MaterialBase { get; init; }

    public required int MaterialPerLevel { get; init; }

    public required int CostReductionPerLevel { get; init; }
}

/// <summary>Eco: habilidade deixada por um monstro (<c>ECOS</c>).</summary>
public sealed record EcoDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Skill { get; init; }

    /// <summary>Custo base em Essência.</summary>
    public required int Cost { get; init; }

    public required string Desc { get; init; }

    /// <summary>Monstro que deixa o Eco.</summary>
    public required string Source { get; init; }

    /// <summary>Rótulo "Deixado por:" (<c>ui.js:202</c>).</summary>
    public required string SourceLabel { get; init; }
}

public sealed record EcosFile
{
    public required EcoTuningDef Tuning { get; init; }

    public required IReadOnlyList<EcoDef> Ecos { get; init; }
}

/// <summary>Golpe Supremo de uma classe (<c>ULT</c>, <c>extra.js:8-13</c>).</summary>
public sealed record UltimateDef
{
    public required string Class { get; init; }

    public required string Name { get; init; }

    public required double Cooldown { get; init; }

    public required string Color { get; init; }

    public required string Desc { get; init; }
}

public sealed record UltimatesFile
{
    /// <summary>Recarga ao entrar no jogo.</summary>
    public required double InitialCooldown { get; init; }

    public required IReadOnlyList<UltimateDef> Ultimates { get; init; }
}

/// <summary>Classe escolhida na criação (<c>CLASSES</c>, <c>main.js:124-128</c>). Id = id da arma.</summary>
public sealed record ClassDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Desc { get; init; }

    public required string Difficulty { get; init; }
}

public sealed record ClassesFile
{
    public required IReadOnlyList<ClassDef> Classes { get; init; }
}
