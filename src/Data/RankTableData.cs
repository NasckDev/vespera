using Godot;
using Vespera.Core.Ranks;

namespace Vespera.Data;

/// <summary>Balanceamento de ranks editável no inspector. A instância do jogo é <c>RankTable.tres</c>.</summary>
[GlobalClass]
public partial class RankTableData : Resource
{
    /// <summary>XP total para E, D, C, B, A, S, SS, SSS e Alvor, nesta ordem.</summary>
    [Export]
    public int[] XpThresholds { get; set; } = [.. RankTable.DefaultThresholds];

    [Export]
    public int HpPerRank { get; set; } = RankTable.DefaultHpPerRank;

    /// <summary>HP máximo no rank E.</summary>
    [Export]
    public int BaseMaxHp { get; set; } = 100;

    public RankTable ToRankTable() => new(XpThresholds, HpPerRank);
}
