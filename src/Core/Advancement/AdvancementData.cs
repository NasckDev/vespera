using Vespera.Core.Common;
using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>Atributos somáveis de uma peça, bônus de conjunto ou afixo. Ausente vale 0.</summary>
public sealed record StatBlock
{
    public static readonly StatBlock Zero = new();

    public int Hp { get; init; }

    public int Defense { get; init; }

    /// <summary>Chance de crítico em pontos percentuais.</summary>
    public double CritPct { get; init; }

    /// <summary>Velocidade de movimento em pontos percentuais.</summary>
    public double SpeedPct { get; init; }

    /// <summary>Fragmentos encontrados a mais, em pontos percentuais.</summary>
    public double FragmentosPct { get; init; }

    public bool IsZero => this == Zero;

    public static StatBlock operator +(StatBlock a, StatBlock b) => new()
    {
        Hp = a.Hp + b.Hp,
        Defense = a.Defense + b.Defense,
        CritPct = a.CritPct + b.CritPct,
        SpeedPct = a.SpeedPct + b.SpeedPct,
        FragmentosPct = a.FragmentosPct + b.FragmentosPct,
    };

    /// <summary>Multiplica tudo por <paramref name="factor"/>, arredondando vida/defesa e mantendo uma casa nos percentuais.</summary>
    public StatBlock Scale(double factor) => new()
    {
        Hp = (int)Math.Round(Hp * factor, MidpointRounding.AwayFromZero),
        Defense = (int)Math.Round(Defense * factor, MidpointRounding.AwayFromZero),
        CritPct = Math.Round(CritPct * factor, 1),
        SpeedPct = Math.Round(SpeedPct * factor, 1),
        FragmentosPct = Math.Round(FragmentosPct * factor, 1),
    };

    /// <summary>Linhas legíveis ("+10 Vida", "+3 Defesa"...), só dos atributos diferentes de zero.</summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>();
        if (Hp != 0)
        {
            lines.Add($"{Signed(Hp)} Vida");
        }

        if (Defense != 0)
        {
            lines.Add($"{Signed(Defense)} Defesa");
        }

        if (CritPct != 0)
        {
            lines.Add($"{Signed(CritPct)}% Crítico");
        }

        if (SpeedPct != 0)
        {
            lines.Add($"{Signed(SpeedPct)}% Velocidade");
        }

        if (FragmentosPct != 0)
        {
            lines.Add($"{Signed(FragmentosPct)}% Fragmentos");
        }

        return lines;
    }

    private static string Signed(double value) => (value >= 0 ? "+" : string.Empty) + value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record StageDef
{
    public required int Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Um degrau da escada de ranks (E até Alvor).</summary>
public sealed record RankStepDef
{
    public required Rank Id { get; init; }

    public required string Title { get; init; }

    /// <summary>XP total mínimo para entrar neste rank (além das conquistas do rank anterior).</summary>
    public required int Xp { get; init; }

    /// <summary>Estágio visual (0–4).</summary>
    public required int Stage { get; init; }

    public required string Color { get; init; }

    /// <summary>Raridade dos itens deste rank (Comum, Incomum...).</summary>
    public required string Rarity { get; init; }

    /// <summary>Guardião que precisa cair para sair deste rank; null no último.</summary>
    public string? Guardian { get; init; }

    /// <summary>Há exame da Ordem para sair deste rank (C, A, S, SSS).</summary>
    public required bool Exam { get; init; }
}

public sealed record RanksFile
{
    public required int HpPerRank { get; init; }

    public required IReadOnlyList<StageDef> Stages { get; init; }

    public required IReadOnlyList<RankStepDef> Ranks { get; init; }
}

public sealed record ExamObjectiveDef
{
    /// <summary>survive, kill_count, destroy_marked ou reach_exit.</summary>
    public required string Type { get; init; }

    public int? Count { get; init; }

    public int? Seconds { get; init; }
}

/// <summary>Exame da Ordem: Brecha especial sem loot, só objetivo (GDD 5.3). A sala em si é montada por quem faz as Brechas.</summary>
public sealed record ExamDef
{
    public required string Id { get; init; }

    public required Rank Rank { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<ExamObjectiveDef> Objectives { get; init; }

    public required int TimeLimitSeconds { get; init; }

    /// <summary>Golpes recebidos tolerados; null = sem limite.</summary>
    public int? MaxHits { get; init; }

    public required int RewardXp { get; init; }

    public string? ArenaHint { get; init; }
}

public sealed record ExamsFile
{
    public required IReadOnlyList<ExamDef> Exams { get; init; }
}

public sealed record GuardianDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Rank Rank { get; init; }

    public required string Brecha { get; init; }

    /// <summary>Material do rank que o Guardião sempre deixa.</summary>
    public required string Material { get; init; }

    public string? ArmorSet { get; init; }

    public string? Echo { get; init; }
}

public sealed record GuardiansFile
{
    public required IReadOnlyList<GuardianDef> Guardians { get; init; }
}

public enum MaterialKind
{
    Common,
    Guardian,
}

public sealed record HunterMaterialDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Rank Rank { get; init; }

    public required MaterialKind Kind { get; init; }

    public required string Source { get; init; }

    /// <summary>"gdd" (nome vindo do GDD) ou "proposta" (preenchido para fechar a escada; revisar com o design).</summary>
    public required string Origin { get; init; }

    /// <summary>Lumes na venda.</summary>
    public required int Value { get; init; }

    /// <summary>Peso no sorteio de loot do rank (0 para materiais de Guardião).</summary>
    public required double Weight { get; init; }

    public required string Desc { get; init; }
}

public sealed record MaterialsFile
{
    public required IReadOnlyList<HunterMaterialDef> Materials { get; init; }
}

public sealed record WeaponTierDef
{
    public required Rank Rank { get; init; }

    public required string Name { get; init; }

    /// <summary>Dano no primeiro nível deste degrau (os níveis seguintes crescem geometricamente até o próximo degrau).</summary>
    public required int Damage { get; init; }
}

public sealed record ClassWeaponDef
{
    /// <summary>Id da classe (sword, bow, hammer), o mesmo de <c>SaveData.ClassId</c>.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required double AttacksPerSecond { get; init; }

    public required double CritPct { get; init; }

    public required double PartBreakPct { get; init; }

    public required IReadOnlyList<WeaponTierDef> Tiers { get; init; }
}

public sealed record ForgeTuning
{
    public required int LevelsPerTier { get; init; }

    public required int FragmentosBase { get; init; }

    public required double FragmentosGrowth { get; init; }

    public required int PrimaryBase { get; init; }

    public required int PrimaryPerLevel { get; init; }

    public required int SecondaryBase { get; init; }

    public required int SecondaryEveryLevels { get; init; }

    public required int GuardianPerCrossing { get; init; }

    public required double LastLevelDamageBonus { get; init; }

    public required double CritPerLevel { get; init; }
}

public sealed record WeaponsFile
{
    public required ForgeTuning Forge { get; init; }

    public required IReadOnlyList<ClassWeaponDef> Weapons { get; init; }
}

public sealed record GearSlotDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

public sealed record ArmorPieceDef
{
    public required string Id { get; init; }

    public required string Slot { get; init; }

    public required string Name { get; init; }

    public required StatBlock Stats { get; init; }
}

public sealed record SetBonusDef
{
    /// <summary>Quantas peças do conjunto ativam (2 ou 4).</summary>
    public required int Pieces { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required StatBlock Stats { get; init; }
}

public sealed record ArmorSetDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Rank Rank { get; init; }

    public required string Guardian { get; init; }

    public required IReadOnlyList<ArmorPieceDef> Pieces { get; init; }

    public required IReadOnlyList<SetBonusDef> Bonuses { get; init; }
}

public sealed record RareTemplateDef
{
    public required string Slot { get; init; }

    public required string Name { get; init; }

    public required StatBlock Stats { get; init; }
}

public sealed record AffixDef
{
    public required string Id { get; init; }

    /// <summary>Complemento do nome da peça ("do Vigor", "da Pressa").</summary>
    public required string Name { get; init; }

    /// <summary>hp, defense, critPct, speedPct ou fragmentosPct.</summary>
    public required string Stat { get; init; }

    public required double Base { get; init; }

    public required double PerRank { get; init; }
}

public sealed record GearFile
{
    public required IReadOnlyList<GearSlotDef> Slots { get; init; }

    public required IReadOnlyList<ArmorSetDef> Sets { get; init; }

    /// <summary>Peças de rank E crescem por este fator a cada rank.</summary>
    public required double RankStatGrowth { get; init; }

    public required IReadOnlyList<RareTemplateDef> RareTemplates { get; init; }

    public required IReadOnlyList<AffixDef> Affixes { get; init; }
}

public sealed record EchoDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Guardian { get; init; }

    public required Rank Rank { get; init; }

    public required string Description { get; init; }
}

public sealed record EchoesFile
{
    public required int Slots { get; init; }

    public required IReadOnlyList<EchoDef> Echoes { get; init; }
}

public sealed record RankWeights
{
    public required double Same { get; init; }

    public required double Next { get; init; }

    public required double Rare { get; init; }
}

public sealed record LootSourceDef
{
    public required double DropChance { get; init; }

    public required int Rolls { get; init; }

    public required IntRange Fragmentos { get; init; }
}

public sealed record GuardianLootDef
{
    public required double SetPieceChance { get; init; }

    public required IntRange MaterialAmount { get; init; }
}

public sealed record LootFile
{
    public required RankWeights RankWeights { get; init; }

    /// <summary>Fragmentos por rank crescem por este fator por degrau da escada.</summary>
    public required double FragmentosRankGrowth { get; init; }

    public required IntRange MaterialAmount { get; init; }

    public required GuardianLootDef Guardian { get; init; }

    public required IReadOnlyDictionary<string, LootSourceDef> Sources { get; init; }
}
