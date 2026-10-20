namespace Vespera.Core.Ranks;

/// <summary>Resultado de um ganho de XP. <see cref="RankedUp"/> dispara o sinal <c>RankChanged</c>.</summary>
public readonly record struct XpGainResult(int Amount, int TotalXp, Rank PreviousRank, Rank NewRank)
{
    public bool RankedUp => NewRank > PreviousRank;
}

/// <summary>Acumula XP do caçador e calcula o rank pela <see cref="RankTable"/>.</summary>
public sealed class XpSystem
{
    private readonly RankTable _table;

    public XpSystem(RankTable table, int totalXp = 0)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegative(totalXp);
        _table = table;
        TotalXp = totalXp;
    }

    public int TotalXp { get; private set; }

    public Rank Rank => _table.RankFor(TotalXp);

    public float ProgressInRank => _table.ProgressInRank(TotalXp);

    public XpGainResult Add(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var previous = Rank;
        TotalXp = checked(TotalXp + amount);
        return new XpGainResult(amount, TotalXp, previous, Rank);
    }
}
