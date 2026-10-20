using Vespera.Core.Common;

namespace Vespera.Core.Data;

/// <summary>Constantes de regra da v2 (<c>rules.json</c>). Cada bloco cita a origem no protótipo.</summary>
public sealed record RulesFile
{
    public required StartRules Start { get; init; }

    public required EconomyRules Economy { get; init; }

    public required StatsRules Stats { get; init; }

    public required CombatRules Combat { get; init; }

    public required PotionRules Potion { get; init; }

    public required DeathRules Death { get; init; }

    public required LanternRulesDef Lantern { get; init; }

    public required ScalingRules Scaling { get; init; }

    public required LootRules Loot { get; init; }

    public required BrechaRulesDef Brecha { get; init; }
}

/// <summary>Jogo novo (<c>newSave</c>, <c>core.js:208-212</c>).</summary>
public sealed record StartRules
{
    public required string Name { get; init; }

    public required int NameMaxLength { get; init; }

    public required int Lumes { get; init; }

    public required int Potions { get; init; }

    /// <summary>Equipamentos na mochila (não equipados).</summary>
    public required IReadOnlyList<string> Backpack { get; init; }

    /// <summary>Equipados: slot → gear.</summary>
    public required IReadOnlyDictionary<string, string> Equipment { get; init; }

    /// <summary>Classe de saves sem classe (migração do save v1).</summary>
    public required string DefaultClass { get; init; }
}

/// <summary>Preços (<c>core.js:233-237</c>, <c>world.js:229</c>, <c>ui.js:100</c>).</summary>
public sealed record EconomyRules
{
    /// <summary>Desconto da Casa da Maré: todo preço em Lumes ×0.9.</summary>
    public required double PriceMult { get; init; }

    /// <summary>Durante invasão os preços ×1.5 (antes do desconto).</summary>
    public required double InvasionMult { get; init; }

    /// <summary>Material-moeda (Estilhaço de Brecha).</summary>
    public required string ShardMaterial { get; init; }

    /// <summary>Lumes por Estilhaço vendido ao Brás (sem multiplicador).</summary>
    public required int ShardSellPrice { get; init; }

    /// <summary>Estilhaços ao desmontar um equipamento.</summary>
    public required int DismantleShards { get; init; }
}

/// <summary>Atributos base do jogador (<c>stats()</c>, <c>core.js:225-232</c>).</summary>
public sealed record StatsRules
{
    public required int BaseCrit { get; init; }

    public required double BaseSpeed { get; init; }

    public required double BaseLight { get; init; }

    public required double BaseEssence { get; init; }

    /// <summary>Multiplicador do ataque de equipamento por arma (adagas ×0.5); ausente = 1.</summary>
    public required IReadOnlyDictionary<string, double> GearAtkMult { get; init; }
}

/// <summary>Fórmulas de dano (<c>hitMob</c>/<c>hurtPlayer</c>, <c>combat.js:14-24,164-183</c>).</summary>
public sealed record CombatRules
{
    public required double CritMult { get; init; }

    public required double WeakMult { get; init; }

    public required double ResistMult { get; init; }

    /// <summary>Variação do dano no monstro.</summary>
    public required NumRange Variance { get; init; }

    /// <summary>Variação do dano no jogador.</summary>
    public required NumRange PlayerVariance { get; init; }

    /// <summary>Teto da redução por defesa (0.6 = 60%).</summary>
    public required double DefenseCap { get; init; }

    public required double EliteKnockback { get; init; }

    public required double EssencePerHit { get; init; }

    public required double EssenceMax { get; init; }
}

/// <summary>Poção (<c>usePotion</c>, <c>combat.js:220-225</c>).</summary>
public sealed record PotionRules
{
    /// <summary>Fração da vida máxima curada.</summary>
    public required double Heal { get; init; }

    public required double Cooldown { get; init; }
}

/// <summary>Morte fora da Brecha (<c>respawnAfterDeath</c>, <c>combat.js:29-41</c>).</summary>
public sealed record DeathRules
{
    public required double CityHpFraction { get; init; }

    public required int CityLumesLoss { get; init; }

    public required double RespawnInvulnerability { get; init; }
}

/// <summary>Mecânica da Lanterna (spec 02 §3.13).</summary>
public sealed record LanternRulesDef
{
    public required double HpMult { get; init; }

    public required double LightMult { get; init; }

    /// <summary>Um caçador traz a Lanterna após <c>R(a,b)</c> segundos.</summary>
    public required NumRange RescueDelay { get; init; }

    /// <summary>Resgate pago com a Odete chega em N segundos.</summary>
    public required double OdeteRescueDelay { get; init; }

    /// <summary>Ao carregar um save com Lanterna perdida, o resgate chega em N segundos.</summary>
    public required double RescueOnLoadDelay { get; init; }

    public required int DeliveryLumes { get; init; }

    public required int DeliveryXp { get; init; }

    /// <summary>Chance de uma Brecha ter a Lanterna de outro caçador.</summary>
    public required double FoundChance { get; init; }
}

/// <summary>Escala de monstros por rank da Brecha (<c>spawnMob</c>, <c>combat.js:244-253</c>).</summary>
public sealed record ScalingRules
{
    public required double HpPerRank { get; init; }

    public required double DamagePerRank { get; init; }
}

/// <summary>Saque (<c>combat.js:194-213,476-530</c>).</summary>
public sealed record LootRules
{
    public required double KillShardChance { get; init; }

    public required int BossShards { get; init; }

    public required int KillShards { get; init; }

    public required double BossEcoChance { get; init; }

    public required IntRange BossCoinPiles { get; init; }

    public required IntRange BossCoinPileSize { get; init; }

    public required IntRange CoinPiles { get; init; }

    public required IntRange CoinPileSize { get; init; }

    public required IReadOnlyList<string> GearExclude { get; init; }

    /// <summary>Peso por raridade no <c>rollGear</c> normal.</summary>
    public required IReadOnlyDictionary<string, double> GearWeights { get; init; }

    /// <summary>Peso por raridade no <c>rollGear</c> de chefe/baú roxo.</summary>
    public required IReadOnlyDictionary<string, double> GearWeightsBoss { get; init; }

    public required ChestLootRules Chest { get; init; }

    public required PropLootRules Prop { get; init; }
}

public sealed record ChestLootRules
{
    public required int BaseCount { get; init; }

    public required double ExtraChance { get; init; }

    public required double PurpleChance { get; init; }

    public required IntRange CoinPiles { get; init; }

    public required IntRange CoinPileSize { get; init; }

    public required IntRange Shards { get; init; }

    public required IReadOnlyList<string> BasicMaterials { get; init; }

    public required double GearChance { get; init; }

    public required double PurpleGearChance { get; init; }

    public required double PotionChance { get; init; }
}

/// <summary>Faixas de um único sorteio <c>r</c> em <c>breakProp</c>.</summary>
public sealed record PropLootRules
{
    public required double LumesBelow { get; init; }

    public required IntRange Lumes { get; init; }

    public required double MaterialBelow { get; init; }

    public required double ThemeMaterialBelow { get; init; }

    public required string FallbackMaterial { get; init; }

    public required double PotionBelow { get; init; }
}

/// <summary>Brechas, ondas e invasão (<c>world.js:34-127</c>, <c>extra.js:97</c>).</summary>
public sealed record BrechaRulesDef
{
    public required int MaxOpen { get; init; }

    public required string FirstTheme { get; init; }

    public required double FirstTimer { get; init; }

    public required NumRange Timer { get; init; }

    public required double RankBonusChance { get; init; }

    public required int MaxRank { get; init; }

    public required double NpcCloseChance { get; init; }

    public required NumRange NpcCloseAt { get; init; }

    /// <summary>Só entra em Brecha até este número de Selos acima do seu.</summary>
    public required int EntryMargin { get; init; }

    public required int BossBonusBase { get; init; }

    public required int BossBonusPerRank { get; init; }

    public required int BossXp { get; init; }

    public required double FirstOpenDelay { get; init; }

    public required double ContinueOpenDelay { get; init; }

    public required NumRange OpenDelay { get; init; }

    public required InvasionRules Invasion { get; init; }

    public required WaveRules Waves { get; init; }
}

public sealed record InvasionRules
{
    public required int Base { get; init; }

    public required int RankCap { get; init; }

    /// <summary>Com mais mobs que isso, vem também um <see cref="Alpha"/>.</summary>
    public required int AlphaAbove { get; init; }

    public required string Alpha { get; init; }

    public required int Lumes { get; init; }

    public required int Xp { get; init; }
}

public sealed record WaveRules
{
    /// <summary>Primeira Brecha da vida: uma onda por item, com N mobs.</summary>
    public required IReadOnlyList<int> EasyCounts { get; init; }

    public required int BaseCount { get; init; }

    /// <summary>A partir deste rank, +1 onda.</summary>
    public required int ExtraWaveFromRank { get; init; }

    public required int BaseMobs { get; init; }

    public required IntRange RandomExtra { get; init; }

    public required double EliteChance { get; init; }
}
