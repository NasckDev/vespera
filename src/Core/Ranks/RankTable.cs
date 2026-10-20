namespace Vespera.Core.Ranks;

/// <summary>
/// XP total necessário para cada rank e bônus de HP por rank.
/// Os valores de balanceamento vêm de <c>src/Data/RankTable.tres</c>; <see cref="Default"/> espelha o CLAUDE.md.
/// </summary>
public sealed class RankTable
{
    public const int DefaultHpPerRank = 20;

    // Precisa vir antes de Default: inicializadores estáticos rodam na ordem do texto.
    private static readonly int RankCount = Enum.GetValues<Rank>().Length;

    public static IReadOnlyList<int> DefaultThresholds { get; } = [0, 100, 300, 700, 1500, 3000, 6000, 12000, 25000];

    public static RankTable Default { get; } = new(DefaultThresholds);

    private readonly int[] _thresholds;

    public RankTable(IEnumerable<int> thresholds, int hpPerRank = DefaultHpPerRank)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        _thresholds = thresholds.ToArray();

        if (_thresholds.Length != RankCount)
        {
            throw new ArgumentException($"A tabela precisa de {RankCount} limiares (um por rank), recebeu {_thresholds.Length}.", nameof(thresholds));
        }

        if (_thresholds[0] != 0)
        {
            throw new ArgumentException("O rank E começa em 0 XP.", nameof(thresholds));
        }

        for (var i = 1; i < _thresholds.Length; i++)
        {
            if (_thresholds[i] <= _thresholds[i - 1])
            {
                throw new ArgumentException("Os limiares de XP precisam ser estritamente crescentes.", nameof(thresholds));
            }
        }

        ArgumentOutOfRangeException.ThrowIfNegative(hpPerRank);
        HpPerRank = hpPerRank;
    }

    public int HpPerRank { get; }

    public IReadOnlyList<int> Thresholds => _thresholds;

    public int ThresholdFor(Rank rank) => _thresholds[(int)rank];

    public Rank RankFor(int totalXp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalXp);

        for (var i = _thresholds.Length - 1; i > 0; i--)
        {
            if (totalXp >= _thresholds[i])
            {
                return (Rank)i;
            }
        }

        return Rank.E;
    }

    /// <summary>Próximo rank, ou <c>null</c> se já está no topo (Alvor).</summary>
    public static Rank? NextRank(Rank rank) => rank == Rank.Alvor ? null : rank + 1;

    /// <summary>XP que falta para o próximo rank, ou <c>null</c> no topo.</summary>
    public int? XpToNext(int totalXp)
    {
        var next = NextRank(RankFor(totalXp));
        return next is null ? null : ThresholdFor(next.Value) - totalXp;
    }

    /// <summary>Progresso de 0 a 1 dentro do rank atual (1 no topo). Usado pela barra de XP do HUD.</summary>
    public float ProgressInRank(int totalXp)
    {
        var rank = RankFor(totalXp);
        var next = NextRank(rank);
        if (next is null)
        {
            return 1f;
        }

        var start = ThresholdFor(rank);
        var end = ThresholdFor(next.Value);
        return (float)(totalXp - start) / (end - start);
    }

    /// <summary>HP máximo: base + <see cref="HpPerRank"/> por rank acima de E (GDD, seção 5.3).</summary>
    public int MaxHpFor(Rank rank, int baseMaxHp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseMaxHp);
        return baseMaxHp + (HpPerRank * (int)rank);
    }
}
