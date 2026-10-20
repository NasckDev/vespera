namespace Vespera.Core.Data;

/// <summary>Um Selo (rank). <c>ranks.json</c>, origem <c>RANKS</c> em <c>data.js:100-104</c>.</summary>
public sealed record RankDef
{
    public required string Id { get; init; }

    /// <summary>XP total mínimo para ter este Selo.</summary>
    public required int Xp { get; init; }

    public required string Title { get; init; }

    /// <summary>Cor do Selo em hex (<c>#rrggbb</c>).</summary>
    public required string Color { get; init; }

    /// <summary>Chave do ícone no atlas (<c>r_E</c>…).</summary>
    public required string Icon { get; init; }

    /// <summary>Visual do personagem neste Selo (<c>ui.js:228</c>).</summary>
    public required string Look { get; init; }
}

/// <summary>Curva do Nível (Nv.), separada do Selo (<c>lvlOf</c>, <c>core.js:223</c>).</summary>
public sealed record LevelCurveDef
{
    /// <summary>XP para ir do Nv. 1 ao 2.</summary>
    public required int BaseNeed { get; init; }

    /// <summary>A cada nível: <c>need = round(need * Growth)</c>.</summary>
    public required double Growth { get; init; }

    public required int HpPerLevel { get; init; }
}

public sealed record RanksFile
{
    public required int BaseHp { get; init; }

    public required int HpPerRank { get; init; }

    public required LevelCurveDef Level { get; init; }

    public required IReadOnlyList<RankDef> Ranks { get; init; }
}

/// <summary>Relógio do jogo em minutos (<c>world.js:137-143</c>).</summary>
public sealed record ClockDef
{
    /// <summary>Relógio de um jogo novo (16:30).</summary>
    public required double StartMinutes { get; init; }

    /// <summary>Ao chegar aqui (06:00 do dia seguinte) começa um novo dia.</summary>
    public required double NewDayAtMinutes { get; init; }

    /// <summary>Relógio ao começar um novo dia (16:00).</summary>
    public required double NewDayStartMinutes { get; init; }

    public required double MinutesPerSecond { get; init; }

    public required int VesperaFromHour { get; init; }

    public required int VesperaToHour { get; init; }
}

/// <summary>Estação e seus efeitos (<c>SEASONS</c>/<c>SEASON_MOD</c>, <c>data.js:106,114-116</c>).</summary>
public sealed record SeasonDef
{
    /// <summary>Id ASCII (primavera, verao, outono, inverno).</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string ModText { get; init; }

    /// <summary>Baús extras por sala de Brecha (Primavera +1, <c>world.js:94</c>).</summary>
    public int ExtraChests { get; init; }

    /// <summary>Multiplicador de dano dos monstros em Brecha (Verão ×1.15, <c>combat.js:246</c>).</summary>
    public double MonsterDamageMult { get; init; } = 1;

    /// <summary>Lentidão ao ser ferido em Brecha (Inverno 1.6 s, <c>combat.js:20</c>).</summary>
    public double ChillOnHitSeconds { get; init; }

    /// <summary>Zonas <c>fast</c> deixam de valer (Inverno, <c>combat.js:88</c>).</summary>
    public bool IgnoresFastZones { get; init; }
}

public sealed record SeasonsFile
{
    /// <summary>Índice da estação do dia 1 (2 = Outono).</summary>
    public required int FirstSeasonIndex { get; init; }

    public required int DaysPerSeason { get; init; }

    public required ClockDef Clock { get; init; }

    public required IReadOnlyList<SeasonDef> Seasons { get; init; }
}

/// <summary>Monstro sorteável numa onda com peso.</summary>
public sealed record ThemeMobDef
{
    public required string Monster { get; init; }

    public required double Weight { get; init; }
}

/// <summary>Tema de Brecha (<c>THEMES</c>, <c>data.js:107-113</c>). O mapa pertence a <c>maps.json</c>.</summary>
public sealed record ThemeDef
{
    public required string Id { get; init; }

    public required string Bairro { get; init; }

    public required string Map { get; init; }

    public required string Boss { get; init; }

    /// <summary>Índice do Selo mínimo para o tema aparecer (com margem +1).</summary>
    public required int MinRank { get; init; }

    /// <summary>Título da Brecha por id de estação.</summary>
    public required IReadOnlyDictionary<string, string> Titles { get; init; }

    public required IReadOnlyList<ThemeMobDef> Mobs { get; init; }

    public required string Extra { get; init; }

    public required string Elite { get; init; }

    public required IReadOnlyList<string> Props { get; init; }

    /// <summary>Material que cai dos props quebráveis.</summary>
    public required string PropMaterial { get; init; }

    /// <summary>Partículas de pedra (true) ou madeira (false).</summary>
    public required bool Stone { get; init; }
}
