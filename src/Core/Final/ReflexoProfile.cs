using System.Globalization;
using Vespera.Core.Hunt;

namespace Vespera.Core.Final;

public enum DodgeSide
{
    Left,
    Right,
}

/// <summary>Os três ataques do Reflexo, cada um imitando uma arma (tiras da issue #52).</summary>
public enum ReflexoAttack
{
    Lamina,
    Arco,
    Martelo,
}

/// <summary>
/// Perfil de luta do Reflexo (Guardião do Final, ROTEIRO-V2 §8 e §13): o estilo do jogador tirado das estatísticas
/// salvas (<see cref="CombatStats"/>): arma mais usada, bênçãos e família favoritas, lado e ritmo de esquiva e
/// Lanternas perdidas. Não tem fases: a cada minuto troca para a arma seguinte mais usada e fala uma frase.
/// </summary>
public sealed class ReflexoProfile
{
    public const double MinDodgeChance = 0.15;
    public const double MaxDodgeChance = 0.6;
    public const int FavoriteCount = 3;
    public const string DefaultWeapon = "sword";

    /// <summary>Família da fala quando o jogador nunca escolheu bênção.</summary>
    public const string NoFamilyName = "Chama";

    private static readonly string[] WeaponOrder = ["sword", "bow", "hammer"];

    private ReflexoProfile(string preferredWeapon, IReadOnlyList<string> weaponRanking, IReadOnlyList<string> favoriteBlessings,
        string favoriteFamily, string favoriteFamilyName, DodgeSide dodgeSide, double dodgeRate, int lanternsLost)
    {
        PreferredWeapon = preferredWeapon;
        WeaponRanking = weaponRanking;
        FavoriteBlessings = favoriteBlessings;
        FavoriteFamily = favoriteFamily;
        FavoriteFamilyName = favoriteFamilyName;
        DodgeSide = dodgeSide;
        DodgeRate = dodgeRate;
        LanternsLost = lanternsLost;
        Buff = ReflexoBuff.For(lanternsLost);
    }

    /// <summary>Id de arma do save (<c>sword</c>, <c>bow</c>, <c>hammer</c>) com mais caçadas.</summary>
    public string PreferredWeapon { get; }

    /// <summary>As três armas, da mais usada para a menos usada (empate: Lâmina, Arco, Martelo).</summary>
    public IReadOnlyList<string> WeaponRanking { get; }

    /// <summary>Até três bênçãos mais escolhidas (empate: ordem alfabética do id).</summary>
    public IReadOnlyList<string> FavoriteBlessings { get; }

    /// <summary>Família com mais escolhas somadas (vazio se nunca escolheu).</summary>
    public string FavoriteFamily { get; }

    public string FavoriteFamilyName { get; }

    public DodgeSide DodgeSide { get; }

    /// <summary>Esquivas ÷ (esquivas + golpes levados) do jogador.</summary>
    public double DodgeRate { get; }

    /// <summary>Chance de o Reflexo esquivar um golpe do jogador: o ritmo dele, preso a [mín, máx].</summary>
    public double DodgeChance => Math.Clamp(DodgeRate, MinDodgeChance, MaxDodgeChance);

    public int LanternsLost { get; }

    /// <summary>Uma lanterna acesa por Lanterna perdida no jogo.</summary>
    public ReflexoBuff Buff { get; }

    /// <param name="stats">Estatísticas salvas (<c>save.Hunt.Stats</c>).</param>
    /// <param name="blessingFamily">Família de cada bênção (id → família, de data/cacada/bencaos.json).</param>
    /// <param name="familyNames">Nome de cada família para a fala (id → nome).</param>
    public static ReflexoProfile From(CombatStats stats, IReadOnlyDictionary<string, string> blessingFamily, IReadOnlyDictionary<string, string> familyNames)
    {
        var weapons = stats.Weapons ?? [];
        var ranking = WeaponOrder
            .Select((id, order) => (id, order, count: weapons.GetValueOrDefault(id)))
            .OrderByDescending(w => w.count).ThenBy(w => w.order)
            .Select(w => w.id).ToList();
        var preferred = weapons.Values.Any(v => v > 0) ? ranking[0] : DefaultWeapon;

        var blessings = (stats.Blessings ?? []).Where(b => b.Value > 0).ToList();
        var favorites = blessings.OrderByDescending(b => b.Value).ThenBy(b => b.Key, StringComparer.Ordinal)
            .Take(FavoriteCount).Select(b => b.Key).ToList();
        var family = blessings
            .Where(b => blessingFamily.ContainsKey(b.Key))
            .GroupBy(b => blessingFamily[b.Key])
            .Select(g => (Family: g.Key, Total: g.Sum(b => b.Value)))
            .OrderByDescending(g => g.Total).ThenBy(g => g.Family, StringComparer.Ordinal)
            .Select(g => g.Family).FirstOrDefault() ?? string.Empty;
        var familyName = family.Length > 0 && familyNames.TryGetValue(family, out var name) ? name : NoFamilyName;

        var side = stats.DodgesLeft > stats.DodgesRight ? DodgeSide.Left : DodgeSide.Right;
        return new ReflexoProfile(preferred, ranking, favorites, family, familyName, side, stats.DodgeRate, Math.Max(0, stats.LanternsLost));
    }

    /// <summary>Fichas das falas do Reflexo (<c>data/dialogue/final/reflexo.json</c>).</summary>
    public IReadOnlyDictionary<string, string> Tokens() => new Dictionary<string, string>
    {
        ["lado_esquiva"] = DodgeSide == DodgeSide.Left ? "esquerda" : "direita",
        ["familia_medo"] = FavoriteFamilyName,
        ["lanternas_perdidas"] = LanternsLost.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Ataque do minuto <paramref name="minute"/> (0 = começo): a arma mais usada, depois a seguinte, em ciclo.</summary>
    public ReflexoAttack AttackForMinute(int minute) => WeaponRanking[Math.Max(0, minute) % WeaponRanking.Count] switch
    {
        "bow" => ReflexoAttack.Arco,
        "hammer" => ReflexoAttack.Martelo,
        _ => ReflexoAttack.Lamina,
    };

    /// <summary>Flag da fala do minuto (o minuto 0 usa a primeira fala, sem flag; do 4º minuto em diante, a última).</summary>
    public static string? MinuteFlag(int minute) => minute <= 0 ? null : $"reflexo_min_{Math.Min(minute + 1, 4)}";
}

/// <summary>Buff do Reflexo: uma lanterna acesa por Lanterna perdida (dano e vida a mais, com teto para a luta ser vencível).</summary>
public sealed record ReflexoBuff(int Lamps, double DamageMult, double HpMult)
{
    public const double DamagePerLamp = 0.05;
    public const double HpPerLamp = 0.04;
    public const int MaxLamps = 12;

    public static ReflexoBuff For(int lanternsLost)
    {
        var lamps = Math.Max(0, lanternsLost);
        var counted = Math.Min(lamps, MaxLamps);
        return new ReflexoBuff(lamps, 1 + (counted * DamagePerLamp), 1 + (counted * HpPerLamp));
    }
}
