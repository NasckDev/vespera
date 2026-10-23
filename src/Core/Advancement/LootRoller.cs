using Vespera.Core.Common;
using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

public enum LootSourceKind
{
    Monster,
    Part,
    Chest,
    Guardian,
}

/// <summary>De onde veio o loot. Para <see cref="LootSourceKind.Guardian"/> informe o id do Guardião.</summary>
public sealed record LootSource(LootSourceKind Kind, string? GuardianId = null)
{
    public static LootSource Monster { get; } = new(LootSourceKind.Monster);

    public static LootSource Part { get; } = new(LootSourceKind.Part);

    public static LootSource Chest { get; } = new(LootSourceKind.Chest);

    public static LootSource Guardian(string guardianId) => new(LootSourceKind.Guardian, guardianId);

    internal string Key => Kind.ToString().ToLowerInvariant();
}

public enum LootEntryKind
{
    Material,
    Gear,
}

/// <summary>Um item do saque. Material: <see cref="Id"/> + <see cref="Amount"/>. Peça: <see cref="Item"/> preenchido.</summary>
public sealed record LootEntry(LootEntryKind Kind, string Id, string Name, int Amount, Rank Rank, bool Rare, bool SetPiece, ItemInstance? Item = null);

public sealed record LootResult(Rank BreachRank, LootSource Source, IReadOnlyList<LootEntry> Entries, int Fragmentos)
{
    public static LootResult Empty(Rank rank, LootSource source) => new(rank, source, [], 0);

    public bool IsEmpty => Entries.Count == 0 && Fragmentos == 0;
}

/// <summary>
/// Sorteio de loot (PLANO-V2 §6). Cada sorteio de uma Brecha de rank R rende: 80% material do rank R, 15% material do rank R+1,
/// 5% peça rara do rank R com afixo. O Guardião, além disso, sempre deixa seu material e tem 30% de soltar uma peça do conjunto.
/// Puro: usa o <see cref="IRandom"/> recebido e não mexe em nenhum estado (quem aplica é o <see cref="ProgressionEngine"/>).
/// </summary>
public sealed class LootRoller(AdvancementCatalog catalog)
{
    private LootFile Config => catalog.Loot;

    /// <summary>Fragmentos de um rank crescem por degrau da escada (a forja cresce na mesma proporção).</summary>
    public double FragmentosMultiplier(Rank rank) => Math.Pow(Config.FragmentosRankGrowth, catalog.RankIndex(rank));

    /// <param name="breachRank">Rank da Brecha (para um Guardião, o rank dele).</param>
    /// <param name="source">Origem do saque.</param>
    /// <param name="random">Fonte de aleatoriedade.</param>
    /// <param name="ownedPieceIds">Ids de peças de conjunto que o caçador já tem (o Guardião prefere as que faltam).</param>
    public LootResult Roll(Rank breachRank, LootSource source, IRandom random, IReadOnlySet<string>? ownedPieceIds = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(random);
        var def = Config.Sources[source.Key];
        var entries = new List<LootEntry>();
        var fragmentos = 0;

        if (random.Chance(def.DropChance))
        {
            fragmentos = (int)Math.Round(random.RangeInt(def.Fragmentos) * FragmentosMultiplier(breachRank), MidpointRounding.AwayFromZero);
            for (var i = 0; i < def.Rolls; i++)
            {
                entries.Add(RollOne(breachRank, random));
            }
        }

        if (source.Kind == LootSourceKind.Guardian)
        {
            AddGuardianLoot(source, random, ownedPieceIds ?? new HashSet<string>(), entries);
        }

        return new LootResult(breachRank, source, Merge(entries), fragmentos);
    }

    /// <summary>Um sorteio de tier: 80/15/5.</summary>
    public LootEntry RollOne(Rank breachRank, IRandom random)
    {
        var weights = Config.RankWeights;
        var x = random.NextDouble();
        if (x < weights.Same)
        {
            return RollMaterial(breachRank, random);
        }

        if (x < weights.Same + weights.Next)
        {
            return RollMaterial(NextRank(breachRank), random);
        }

        return RollRareGear(breachRank, random);
    }

    public Rank NextRank(Rank rank)
    {
        var index = catalog.RankIndex(rank);
        return catalog.Ranks[Math.Min(index + 1, catalog.TopRankIndex)].Id;
    }

    public LootEntry RollMaterial(Rank rank, IRandom random)
    {
        var pool = catalog.CommonMaterials(rank);
        var x = random.NextDouble() * pool.Sum(m => m.Weight);
        var chosen = pool[^1];
        foreach (var material in pool)
        {
            x -= material.Weight;
            if (x < 0)
            {
                chosen = material;
                break;
            }
        }

        var amount = random.RangeInt(Config.MaterialAmount);
        return new LootEntry(LootEntryKind.Material, chosen.Id, chosen.Name, amount, rank, Rare: false, SetPiece: false);
    }

    /// <summary>Peça rara do rank com um afixo sorteado: atributos-base do molde (que crescem por rank) mais o afixo.</summary>
    public LootEntry RollRareGear(Rank rank, IRandom random)
    {
        var gear = catalog.GearData;
        var template = random.Pick(gear.RareTemplates);
        var affix = random.Pick(gear.Affixes);
        var index = catalog.RankIndex(rank);
        var stats = template.Stats.Scale(Math.Pow(gear.RankStatGrowth, index)) + AffixStats(affix, index);
        var item = new ItemInstance
        {
            DefId = $"rara:{template.Slot}",
            Slot = template.Slot,
            Rank = rank,
            Name = $"{template.Name} {affix.Name}",
            AffixId = affix.Id,
            Stats = stats,
        };
        return new LootEntry(LootEntryKind.Gear, item.DefId, item.Name, 1, rank, Rare: true, SetPiece: false, item);
    }

    public static StatBlock AffixStats(AffixDef affix, int rankIndex)
    {
        var value = affix.Base + (affix.PerRank * rankIndex);
        return affix.Stat switch
        {
            "hp" => new StatBlock { Hp = (int)Math.Round(value, MidpointRounding.AwayFromZero) },
            "defense" => new StatBlock { Defense = (int)Math.Round(value, MidpointRounding.AwayFromZero) },
            "critPct" => new StatBlock { CritPct = Math.Round(value, 1) },
            "speedPct" => new StatBlock { SpeedPct = Math.Round(value, 1) },
            "fragmentosPct" => new StatBlock { FragmentosPct = Math.Round(value, 1) },
            _ => throw new ArgumentOutOfRangeException(nameof(affix), affix.Stat, "Atributo de afixo desconhecido."),
        };
    }

    public static ItemInstance NewSetPiece(ArmorSetDef set, ArmorPieceDef piece) => new()
    {
        DefId = piece.Id,
        Slot = piece.Slot,
        Rank = set.Rank,
        Name = piece.Name,
        SetId = set.Id,
        Stats = piece.Stats,
    };

    private void AddGuardianLoot(LootSource source, IRandom random, IReadOnlySet<string> owned, List<LootEntry> entries)
    {
        var guardian = catalog.Guardians[source.GuardianId ?? throw new ArgumentException("Loot de Guardião exige o id do Guardião.", nameof(source))];
        var material = catalog.Materials[guardian.Material];
        entries.Add(new LootEntry(
            LootEntryKind.Material,
            material.Id,
            material.Name,
            random.RangeInt(Config.Guardian.MaterialAmount),
            guardian.Rank,
            Rare: false,
            SetPiece: false));

        var dropsPiece = random.Chance(Config.Guardian.SetPieceChance);
        if (!dropsPiece || guardian.ArmorSet is null)
        {
            return;
        }

        var set = catalog.Sets[guardian.ArmorSet];
        var missing = set.Pieces.Where(p => !owned.Contains(p.Id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var piece = random.Pick(missing);
        var item = NewSetPiece(set, piece);
        entries.Add(new LootEntry(LootEntryKind.Gear, piece.Id, piece.Name, 1, set.Rank, Rare: false, SetPiece: true, item));
    }

    /// <summary>Junta materiais repetidos numa só linha (peças ficam separadas).</summary>
    private static List<LootEntry> Merge(List<LootEntry> entries)
    {
        var merged = new List<LootEntry>();
        foreach (var entry in entries)
        {
            var index = entry.Kind == LootEntryKind.Material ? merged.FindIndex(m => m.Kind == LootEntryKind.Material && m.Id == entry.Id) : -1;
            if (index >= 0)
            {
                merged[index] = merged[index] with { Amount = merged[index].Amount + entry.Amount };
            }
            else
            {
                merged.Add(entry);
            }
        }

        return merged;
    }
}
