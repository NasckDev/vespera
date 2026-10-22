using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>Tamanho de uma invasão (ruptura de Brecha na Rua da Forja).</summary>
public readonly record struct InvasionSize(int Mobs, bool WithAlpha, string Alpha);

/// <summary>
/// Regras de Brecha (spec 01 §1.9; spec 02 §3.14; <c>world.js:34-127</c>, <c>extra.js:97</c>): tema, rank, timer,
/// permissão de entrada, ondas, invasão e bônus de chefe. Cada método consome o <see cref="IRandom"/> na ordem do protótipo.
/// </summary>
public sealed class BrechaRules(GameDatabase db)
{
    private readonly BrechaRulesDef _rules = db.Rules.Brecha;

    public BrechaRulesDef Def => _rules;

    /// <summary>Temas que podem abrir para o jogador: <c>minRank &lt;= selo + 1</c>.</summary>
    public IReadOnlyList<ThemeDef> AvailableThemes(int playerRankIndex) =>
        [.. db.ThemeList.Where(t => t.MinRank <= playerRankIndex + _rules.EntryMargin)];

    /// <summary>
    /// Tema da próxima Brecha: a primeira é sempre do Mercado; depois, um tema disponível ainda não aberto
    /// (ou qualquer disponível, se todos já estiverem abertos).
    /// </summary>
    public ThemeDef ChooseTheme(int playerRankIndex, IReadOnlyCollection<string> openThemeIds, bool first, IRandom random)
    {
        if (first)
        {
            return db.Themes[_rules.FirstTheme];
        }

        var available = AvailableThemes(playerRankIndex);
        var fresh = available.Where(t => !openThemeIds.Contains(t.Id)).ToList();
        return random.Pick(fresh.Count > 0 ? fresh : available);
    }

    /// <summary>Rank: <c>clamp(max(minRank, selo) + (25% ? 1 : 0), 0, 9)</c>; a primeira não ganha o bônus (nem sorteia).</summary>
    public int RollRank(ThemeDef theme, int playerRankIndex, bool first, IRandom random)
    {
        var bonus = first ? 0 : (random.Chance(_rules.RankBonusChance) ? 1 : 0);
        return Math.Clamp(Math.Max(theme.MinRank, playerRankIndex) + bonus, 0, _rules.MaxRank);
    }

    /// <summary>
    /// Rank com a proteção do novato (DECISIONS C1-D2): enquanto o jogador fechou menos de <c>noviceBrechas</c>
    /// Brechas, o rank sorteado não passa do que o Selo deixa entrar (<see cref="CanEnter"/>). O sorteio consome o
    /// <see cref="IRandom"/> igual ao <see cref="RollRank(ThemeDef, int, bool, IRandom)"/>.
    /// </summary>
    public int RollRank(ThemeDef theme, int playerRankIndex, bool first, bool novice, IRandom random)
    {
        var rank = RollRank(theme, playerRankIndex, first, random);
        return novice ? Math.Min(rank, Math.Max(0, playerRankIndex) + _rules.EntryMargin) : rank;
    }

    /// <summary>Ainda é novato (as primeiras Brechas respeitam o Selo): fechou menos de <c>noviceBrechas</c>.</summary>
    public bool IsNovice(int brechasClosed) => brechasClosed < _rules.NoviceBrechas;

    /// <summary>Tempo até romper: 300 s na primeira, senão <c>R(210, 270)</c>.</summary>
    public double RollTimer(bool first, IRandom random) => first ? _rules.FirstTimer : random.Range(_rules.Timer);

    /// <summary>
    /// 55% das Brechas (nunca a primeira) são fechadas por uma Casa quando o timer chega a <c>R(25, 120)</c>.
    /// Null = ninguém fecha.
    /// </summary>
    public double? RollNpcCloseAt(bool first, IRandom random)
    {
        if (first || !random.Chance(_rules.NpcCloseChance))
        {
            return null;
        }

        return random.Range(_rules.NpcCloseAt);
    }

    /// <summary>Espera até a primeira Brecha abrir: 16 s em jogo novo, 6 s ao continuar.</summary>
    public double InitialOpenDelay(bool newGame) => newGame ? _rules.FirstOpenDelay : _rules.ContinueOpenDelay;

    /// <summary>Espera entre aberturas: <c>R(80, 120)</c>.</summary>
    public double RollOpenDelay(IRandom random) => random.Range(_rules.OpenDelay);

    /// <summary>Código da Véspera: só entra em Brecha até um Selo acima do seu.</summary>
    public bool CanEnter(int brechaRank, int playerRankIndex) => brechaRank <= playerRankIndex + _rules.EntryMargin;

    /// <summary>Título da Brecha pelo tema e estação (ex.: "Maré Congelada").</summary>
    public static string Title(ThemeDef theme, string seasonId) => theme.Titles[seasonId];

    /// <summary>Lumes na bolsa ao derrotar o chefe: <c>40 + 25*rk</c>.</summary>
    public int BossBonusLumes(int brechaRank) => _rules.BossBonusBase + (_rules.BossBonusPerRank * brechaRank);

    public int BossXp => _rules.BossXp;

    /// <summary>Ruptura: <c>5 + min(4, rk)</c> mobs do tema e, com mais de 4, também um Alfa.</summary>
    public InvasionSize Invasion(int brechaRank)
    {
        var inv = _rules.Invasion;
        var mobs = inv.Base + Math.Min(inv.RankCap, brechaRank);
        return new InvasionSize(mobs, mobs > inv.AlphaAbove, inv.Alpha);
    }

    /// <summary>Recompensa por limpar a invasão.</summary>
    public (int Lumes, int Xp) InvasionReward => (_rules.Invasion.Lumes, _rules.Invasion.Xp);

    /// <summary>
    /// Ondas da sala do tema (<c>setupDungeon</c>, <c>world.js:85-96</c>). Primeira Brecha da vida (<paramref name="easy"/>):
    /// 2 ondas de 2 e 3 mobs. Demais: <c>2 + (rk &gt;= 1)</c> ondas, onda i com <c>3 + i + RI(0,1)</c> mobs por peso;
    /// ondas i &gt; 0 ganham o <c>extra</c> do tema; a última tem 50% de ganhar a <c>elite</c>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> GenerateWaves(ThemeDef theme, int brechaRank, bool easy, IRandom random)
    {
        var w = _rules.Waves;
        var count = easy ? w.EasyCounts.Count : w.BaseCount + (brechaRank >= w.ExtraWaveFromRank ? 1 : 0);
        var waves = new List<IReadOnlyList<string>>(count);
        for (var i = 0; i < count; i++)
        {
            var size = easy ? w.EasyCounts[i] : w.BaseMobs + i + random.RangeInt(w.RandomExtra);
            var wave = new List<string>(size + 2);
            for (var j = 0; j < size; j++)
            {
                wave.Add(PickMob(theme, random));
            }

            if (!easy && i > 0)
            {
                wave.Add(theme.Extra);
            }

            if (!easy && i == count - 1 && random.Chance(w.EliteChance))
            {
                wave.Add(theme.Elite);
            }

            waves.Add(wave);
        }

        return waves;
    }

    /// <summary>
    /// Mob por peso: primeiro com <c>r &lt;= peso acumulado</c>. Se o acumulado ficar abaixo de r por erro de ponto
    /// flutuante, usa o último mob do tema (o protótipo caía em 'wolf' em qualquer tema; DECISIONS D6).
    /// </summary>
    public static string PickMob(ThemeDef theme, IRandom random)
    {
        var r = random.NextDouble();
        var acc = 0.0;
        foreach (var mob in theme.Mobs)
        {
            acc += mob.Weight;
            if (r <= acc)
            {
                return mob.Monster;
            }
        }

        return theme.Mobs[^1].Monster;
    }
}
