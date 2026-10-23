using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>Atributos da arma de uma classe num nível.</summary>
public sealed record WeaponStats(
    string ClassId,
    int Level,
    int TierIndex,
    Rank TierRank,
    string Name,
    int Damage,
    double AttacksPerSecond,
    double CritPct,
    double PartBreakPct)
{
    /// <summary>Nome do degrau com sufixo quando o nível é intermediário ("Espada das Marés +1").</summary>
    public string DisplayName { get; init; } = Name;
}

/// <summary>Custo de um upgrade: Fragmentos mais materiais por id.</summary>
public sealed record ForgeCost(int Fragmentos, IReadOnlyDictionary<string, int> Materials);

public enum ForgeBlock
{
    None,
    MaxLevel,
    RankTooLow,
    Missing,
}

/// <summary>O que falta de um recurso para pagar um custo.</summary>
public sealed record MissingResource(string Id, string Name, int Have, int Need);

/// <summary>Resultado de "dá para subir a arma?": sempre traz arma atual, próxima e custo para a tela da forja mostrar.</summary>
public sealed record ForgeCheck(
    bool CanUpgrade,
    ForgeBlock Block,
    string Reason,
    WeaponStats Current,
    WeaponStats? Next,
    ForgeCost? Cost,
    Rank? RequiredRank,
    IReadOnlyList<MissingResource> Missing);

/// <summary>
/// Forja de armas (PLANO-V2 §6): cada classe tem uma arma que sobe de nível com Fragmentos e materiais. Dois níveis por degrau da
/// escada de ranks; o teto de nível acompanha o rank do caçador. O dano cresce geometricamente entre os degraus do catálogo
/// (<c>arsenal</c>), então o nível quebra o salto entre um rank e outro em passos menores.
/// </summary>
public sealed class WeaponForge(AdvancementCatalog catalog, RankLadder ladder)
{
    private ForgeTuning Tuning => catalog.WeaponData.Forge;

    public int MaxLevel => Tuning.LevelsPerTier * catalog.Ranks.Count;

    /// <summary>Maior nível que um caçador do rank dado pode forjar.</summary>
    public int LevelCap(Rank rank) => Tuning.LevelsPerTier * (catalog.RankIndex(rank) + 1);

    /// <summary>Rank necessário para chegar ao nível (o do degrau em que ele cai).</summary>
    public Rank RankRequiredFor(int level) => catalog.Ranks[Math.Min((level - 1) / Tuning.LevelsPerTier, catalog.TopRankIndex)].Id;

    public WeaponStats Stats(string classId, int level)
    {
        var weapon = catalog.Weapons[classId];
        level = Math.Clamp(level, 1, MaxLevel);
        var perTier = Tuning.LevelsPerTier;
        var tier = (level - 1) / perTier;
        var sub = (level - 1) % perTier;
        var from = weapon.Tiers[tier].Damage;
        double damage;
        if (tier < weapon.Tiers.Count - 1)
        {
            var to = weapon.Tiers[tier + 1].Damage;
            damage = from * Math.Pow((double)to / from, (double)sub / perTier);
        }
        else
        {
            damage = from * (1 + (Tuning.LastLevelDamageBonus * sub));
        }

        return new WeaponStats(
            classId,
            level,
            tier,
            weapon.Tiers[tier].Rank,
            weapon.Tiers[tier].Name,
            (int)Math.Round(damage, MidpointRounding.AwayFromZero),
            weapon.AttacksPerSecond,
            Math.Round(weapon.CritPct + (Tuning.CritPerLevel * (level - 1)), 1),
            weapon.PartBreakPct)
        {
            DisplayName = sub == 0 ? weapon.Tiers[tier].Name : $"{weapon.Tiers[tier].Name} +{sub}",
        };
    }

    /// <summary>Custo de ir do <paramref name="level"/> para o seguinte; null no nível máximo.</summary>
    public ForgeCost? Cost(int level)
    {
        if (level >= MaxLevel)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        var perTier = Tuning.LevelsPerTier;
        var tierRank = catalog.Ranks[(level - 1) / perTier].Id;
        var commons = catalog.CommonMaterials(tierRank);
        var materials = new Dictionary<string, int>
        {
            [commons[0].Id] = Tuning.PrimaryBase + (Tuning.PrimaryPerLevel * level),
            [commons[1].Id] = Tuning.SecondaryBase + (level / Tuning.SecondaryEveryLevels),
        };

        if (level % perTier == 0)
        {
            var guardian = catalog.Guardians[catalog.Step(tierRank).Guardian!];
            materials[guardian.Material] = Tuning.GuardianPerCrossing;
        }

        var fragmentos = (int)Math.Round(Tuning.FragmentosBase * Math.Pow(Tuning.FragmentosGrowth, level - 1), MidpointRounding.AwayFromZero);
        return new ForgeCost(fragmentos, materials);
    }

    public ForgeCheck Check(HunterProgress progress, string classId)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var level = Math.Clamp(progress.WeaponLevels.GetValueOrDefault(classId, 1), 1, MaxLevel);
        var current = Stats(classId, level);
        if (level >= MaxLevel)
        {
            return new ForgeCheck(false, ForgeBlock.MaxLevel, "A arma já está no nível máximo.", current, null, null, null, []);
        }

        var next = Stats(classId, level + 1);
        var cost = Cost(level)!;
        var required = RankRequiredFor(level + 1);
        var rank = ladder.Current(progress);
        if (level + 1 > LevelCap(rank))
        {
            return new ForgeCheck(false, ForgeBlock.RankTooLow, $"Requer o rank {required} para chegar ao nível {level + 1}.", current, next, cost, required, []);
        }

        var missing = MissingFor(progress, cost);
        return missing.Count > 0
            ? new ForgeCheck(false, ForgeBlock.Missing, "Faltam recursos para forjar.", current, next, cost, required, missing)
            : new ForgeCheck(true, ForgeBlock.None, string.Empty, current, next, cost, required, []);
    }

    /// <summary>Paga e sobe o nível. Devolve o mesmo <see cref="ForgeCheck"/> da tentativa (CanUpgrade falso = nada foi gasto).</summary>
    public ForgeCheck Upgrade(HunterProgress progress, string classId)
    {
        var check = Check(progress, classId);
        if (!check.CanUpgrade)
        {
            return check;
        }

        var cost = check.Cost!;
        progress.Fragmentos -= cost.Fragmentos;
        foreach (var (id, amount) in cost.Materials)
        {
            var left = progress.MaterialCount(id) - amount;
            if (left > 0)
            {
                progress.Materials[id] = left;
            }
            else
            {
                progress.Materials.Remove(id);
            }
        }

        progress.WeaponLevels[classId] = check.Next!.Level;
        return check;
    }

    private List<MissingResource> MissingFor(HunterProgress progress, ForgeCost cost)
    {
        var missing = new List<MissingResource>();
        if (progress.Fragmentos < cost.Fragmentos)
        {
            missing.Add(new MissingResource("fragmentos", "Fragmentos", progress.Fragmentos, cost.Fragmentos));
        }

        foreach (var (id, need) in cost.Materials)
        {
            var have = progress.MaterialCount(id);
            if (have < need)
            {
                missing.Add(new MissingResource(id, catalog.Materials[id].Name, have, need));
            }
        }

        return missing;
    }
}
