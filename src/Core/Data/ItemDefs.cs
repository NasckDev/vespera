namespace Vespera.Core.Data;

public enum WeaponKind
{
    Melee,
    Bow,
    Orb,
    Smash,
}

/// <summary>Arma (<c>WEAP</c>/<c>UPG</c>/<c>WELEM</c>, <c>data.js:95,118-131</c>). O id da arma é também o id da classe.</summary>
public sealed record WeaponDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Short { get; init; }

    public required string Type { get; init; }

    public required string Icon { get; init; }

    /// <summary>Dano base no nível 1.</summary>
    public required int Damage { get; init; }

    /// <summary>Recarga do ataque em segundos.</summary>
    public required double Cooldown { get; init; }

    public required WeaponKind Kind { get; init; }

    /// <summary>Abertura do setor em radianos (só melee/smash).</summary>
    public double? Arc { get; init; }

    /// <summary>Alcance em px (só melee/smash).</summary>
    public double? Range { get; init; }

    /// <summary>Multiplicador de dano em partes.</summary>
    public required double PartMult { get; init; }

    /// <summary>Multiplicador pelas costas (adagas ×2).</summary>
    public double? Backstab { get; init; }

    public required string Color { get; init; }

    public required string Desc { get; init; }

    public required Cost Unlock { get; init; }

    public required string UpgradeMaterial { get; init; }

    /// <summary>Material por nível no upgrade (cajado 2, demais 1; <c>world.js:236</c>).</summary>
    public required int UpgradeMaterialPerLevel { get; init; }

    /// <summary>Elementos que o Gaspar infunde nesta arma.</summary>
    public required IReadOnlyList<string> Elements { get; init; }
}

public sealed record WeaponsFile
{
    public required int MaxLevel { get; init; }

    /// <summary>Dano = <c>round(dmg * (1 + g * (nível - 1)))</c>.</summary>
    public required double DamageGrowthPerLevel { get; init; }

    /// <summary>Upgrade custa <c>Lumes = isto * nível atual</c>.</summary>
    public required int UpgradeLumesPerLevel { get; init; }

    /// <summary>Em ordem de hotbar (teclas 1–5).</summary>
    public required IReadOnlyList<WeaponDef> Weapons { get; init; }
}

/// <summary>Elemento e custo de infusão no Gaspar (<c>ELEM</c>/<c>ELEM_COST</c>).</summary>
public sealed record ElementDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Color { get; init; }

    public required Cost Infusion { get; init; }
}

/// <summary>Material (<c>MAT</c>, <c>data.js:133-182</c>). Raridade ausente no protótipo = comum; valor ausente = 3.</summary>
public sealed record MaterialDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Icon { get; init; }

    public required string Rarity { get; init; }

    /// <summary>Lumes na venda de sobras.</summary>
    public required int Value { get; init; }

    public required string Desc { get; init; }
}

public sealed record MaterialsFile
{
    public required int DefaultValue { get; init; }

    public required IReadOnlyList<MaterialDef> Materials { get; init; }
}

/// <summary>Slot de equipamento (<c>SLOTS</c>).</summary>
public sealed record GearSlotDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Raridade (<c>RAR</c>) e Selo mínimo padrão para equipar (<c>RAR_RK</c>, <c>extra.js:77</c>).</summary>
public sealed record RarityDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Color { get; init; }

    public required string MinRank { get; init; }
}

/// <summary>Equipamento (<c>GEAR</c> + <c>GEAR_CLS</c>). Atributos ausentes valem 0.</summary>
public sealed record GearDef
{
    public required string Id { get; init; }

    public required string Slot { get; init; }

    public required string Name { get; init; }

    public required string Icon { get; init; }

    public int Hp { get; init; }

    public int Def { get; init; }

    public int Atk { get; init; }

    /// <summary>Crítico em pontos percentuais.</summary>
    public int Crit { get; init; }

    /// <summary>Fração somada à velocidade.</summary>
    public double Spd { get; init; }

    /// <summary>Multiplicador de luz; <b>substitui</b> o anterior (null = não mexe na luz).</summary>
    public double? Light { get; init; }

    /// <summary>Fração somada ao ganho de Essência.</summary>
    public double Ess { get; init; }

    /// <summary>HP por segundo.</summary>
    public double Regen { get; init; }

    public required string Rarity { get; init; }

    /// <summary>Classes que podem equipar; null = todas.</summary>
    public IReadOnlyList<string>? Classes { get; init; }

    /// <summary>Selo mínimo explícito (nenhum item da v2 usa); null = o da raridade.</summary>
    public string? MinRank { get; init; }

    public required string Desc { get; init; }
}

public sealed record GearFile
{
    public required IReadOnlyList<GearSlotDef> Slots { get; init; }

    public required IReadOnlyList<RarityDef> Rarities { get; init; }

    /// <summary>Placeholders: <c>{classes}</c>, <c>{class}</c>.</summary>
    public required string LockClassText { get; init; }

    /// <summary>Placeholders: <c>{rank}</c>, <c>{title}</c>, <c>{current}</c>.</summary>
    public required string LockRankText { get; init; }

    public required IReadOnlyList<GearDef> Gear { get; init; }
}

/// <summary>Receita da forja do Brás (<c>RECIPES</c>).</summary>
public sealed record RecipeDef
{
    public required string Gear { get; init; }

    public required Cost Cost { get; init; }
}

public sealed record RecipesFile
{
    public required IReadOnlyList<RecipeDef> Recipes { get; init; }
}

/// <summary>Item da loja de relíquias do Abel (<c>RELSHOP</c>).</summary>
public sealed record RelicOfferDef
{
    public required string Gear { get; init; }

    public required Cost Cost { get; init; }
}

/// <summary>Serviço pago de um NPC (preços de <c>world.js</c>). Só um efeito por serviço.</summary>
public sealed record ServiceDef
{
    public required string Id { get; init; }

    public required string Npc { get; init; }

    public required string Label { get; init; }

    public required Cost Cost { get; init; }

    /// <summary>Poções ganhas.</summary>
    public int Potions { get; init; }

    /// <summary>Equipamento ganho (único: não vende se já tiver).</summary>
    public string? Gear { get; init; }

    /// <summary>Dormir até a próxima véspera.</summary>
    public bool Rest { get; init; }

    /// <summary>Resgate pago da Lanterna (Odete).</summary>
    public bool LanternRescue { get; init; }
}

public sealed record ShopsFile
{
    public required IReadOnlyList<RelicOfferDef> Relics { get; init; }

    public required IReadOnlyList<ServiceDef> Services { get; init; }
}
