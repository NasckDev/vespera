using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;

namespace Vespera.Core.Rules;

/// <summary>Nível (Nv.) dentro da curva: XP já acumulado no nível e XP que o nível pede.</summary>
public readonly record struct LevelInfo(int Level, int Current, int Need)
{
    /// <summary>Fração de 0 a 1 do nível atual (barra fina de XP do HUD).</summary>
    public float Progress => Need <= 0 ? 1f : (float)Current / Need;
}

/// <summary>Resultado de um ganho de XP: Selo e Nível antes e depois.</summary>
public readonly record struct XpGainResult(int Amount, int PreviousXp, int TotalXp, int PreviousRankIndex, int RankIndex, int PreviousLevel, int Level)
{
    public bool RankedUp => RankIndex > PreviousRankIndex;

    public bool LeveledUp => Level > PreviousLevel;

    public Rank PreviousRank => (Rank)PreviousRankIndex;

    public Rank NewRank => (Rank)RankIndex;
}

/// <summary>
/// Selo (rank) e Nível do caçador a partir do XP total (spec 02 §1.2-1.3, <c>core.js:221-226</c>).
/// Selo e Nível são curvas separadas (DECISIONS D4); os dois somam HP base.
/// </summary>
public sealed class Progression
{
    private readonly RanksFile _data;

    public Progression(RanksFile data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    public IReadOnlyList<RankDef> Ranks => _data.Ranks;

    public int TopRankIndex => _data.Ranks.Count - 1;

    /// <summary><c>rankIdx(xp)</c>: maior índice com <c>xp &gt;= Ranks[i].Xp</c>.</summary>
    public int RankIndex(int totalXp)
    {
        var index = 0;
        for (var i = 1; i < _data.Ranks.Count; i++)
        {
            if (totalXp >= _data.Ranks[i].Xp)
            {
                index = i;
            }
        }

        return index;
    }

    public RankDef RankOf(int totalXp) => _data.Ranks[RankIndex(totalXp)];

    public Rank RankEnumOf(int totalXp) => (Rank)RankIndex(totalXp);

    /// <summary>Próximo Selo, ou null no topo (ALVOR).</summary>
    public RankDef? NextRank(int totalXp)
    {
        var next = RankIndex(totalXp) + 1;
        return next < _data.Ranks.Count ? _data.Ranks[next] : null;
    }

    /// <summary>XP que falta para o próximo Selo, ou null no topo.</summary>
    public int? XpToNextRank(int totalXp) => NextRank(totalXp) is { } next ? next.Xp - totalXp : null;

    /// <summary>Progresso de 0 a 1 dentro do Selo atual (1 no topo).</summary>
    public float ProgressToNextRank(int totalXp)
    {
        var next = NextRank(totalXp);
        if (next is null)
        {
            return 1f;
        }

        var start = RankOf(totalXp).Xp;
        return (float)(totalXp - start) / (next.Xp - start);
    }

    /// <summary><c>lvlOf(xp)</c>: começa pedindo <c>BaseNeed</c>; a cada nível <c>need = round(need * Growth)</c>.</summary>
    public LevelInfo LevelOf(int totalXp)
    {
        var curve = _data.Level;
        var level = 1;
        var need = curve.BaseNeed;
        var xp = Math.Max(0, totalXp);
        while (xp >= need)
        {
            xp -= need;
            level++;
            need = JsMath.Round(need * curve.Growth);
        }

        return new LevelInfo(level, xp, need);
    }

    /// <summary>HP base sem equipamento: <c>100 + 12*selo + 6*(nível-1)</c> (<c>core.js:226</c>).</summary>
    public int BaseMaxHp(int totalXp) =>
        _data.BaseHp + (_data.HpPerRank * RankIndex(totalXp)) + (_data.Level.HpPerLevel * (LevelOf(totalXp).Level - 1));

    /// <summary>Soma XP e informa se o Selo e/ou o Nível mudaram. Não altera estado.</summary>
    public XpGainResult Gain(int currentXp, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var total = checked(currentXp + amount);
        return new XpGainResult(amount, currentXp, total, RankIndex(currentXp), RankIndex(total), LevelOf(currentXp).Level, LevelOf(total).Level);
    }
}
