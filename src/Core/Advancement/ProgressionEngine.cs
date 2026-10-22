using Vespera.Core.Common;
using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>Mudança de rank (e de estágio visual, se cruzou uma fronteira).</summary>
public sealed record RankChange(Rank From, Rank To, int FromStage, int ToStage)
{
    public bool StageChanged => FromStage != ToStage;
}

public sealed record XpResult(int Amount, int TotalXp, RankChange? RankChange);

public sealed record LootGrant(LootResult Loot, int FragmentosGained, bool FragmentosHeldInRun);

public sealed record GuardianKillResult(bool FirstKill, LootGrant Loot, EchoDef? EchoGained, RankChange? RankChange);

public enum ExamStatus
{
    Passed,
    AlreadyPassed,
    NoExamForRank,
    RankTooLow,
}

public sealed record ExamResult(ExamStatus Status, ExamDef? Exam, int XpAwarded, RankChange? RankChange)
{
    public bool Passed => Status == ExamStatus.Passed;
}

public sealed record ForgeResult(ForgeCheck Check, RankChange? RankChange = null)
{
    public bool Done => Check.CanUpgrade;
}

/// <summary>
/// Regras de progressão do caçador da v2 sobre um <see cref="HunterProgress"/> (que vive dentro do save): XP e ranks, loot,
/// Guardiões e exames, forja, equipamento, Ecos e a regra da Lanterna. Sem Godot; o serviço do jogo só traduz o que sai daqui
/// em sinais e salva. Os métodos que mexem no estado devolvem o que mudou (rank, loot, custo) para a camada de cima reagir.
/// </summary>
public sealed partial class ProgressionEngine
{
    private readonly List<string> _warnings = [];

    public ProgressionEngine(AdvancementCatalog catalog, HunterProgress progress, string classId, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(random);
        Catalog = catalog;
        Progress = progress;
        Random = random;
        Ladder = new RankLadder(catalog);
        Forge = new WeaponForge(catalog, Ladder);
        Roller = new LootRoller(catalog);
        ClassId = catalog.Weapons.ContainsKey(classId) ? classId : catalog.WeaponData.Weapons[0].Id;
        Sanitize();
    }

    public AdvancementCatalog Catalog { get; }

    public HunterProgress Progress { get; }

    public IRandom Random { get; }

    public RankLadder Ladder { get; }

    public WeaponForge Forge { get; }

    public LootRoller Roller { get; }

    /// <summary>Classe do caçador (id da arma: sword, bow, hammer).</summary>
    public string ClassId { get; }

    /// <summary>O que foi descartado ao ler o save (ids que não existem mais, peças sem dono, caçada abandonada).</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public Rank Rank => Ladder.Current(Progress);

    public int Stage => Ladder.Stage(Rank);

    public string StageName => Ladder.StageName(Stage);

    public RankStatus RankStatus => Ladder.Status(Progress);

    public RankStepDef RankStep => Catalog.Step(Rank);

    /// <summary>Soma XP e informa se o rank (e o estágio) mudou. Rank só sobe com as conquistas em dia.</summary>
    public XpResult AddXp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var before = Ladder.CurrentIndex(Progress);
        Progress.Xp = checked(Progress.Xp + amount);
        return new XpResult(amount, Progress.Xp, DetectRankChange(before));
    }

    public void AddLumes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Progress.Lumes += amount;
    }

    /// <summary>Gasta Lumes se houver o bastante.</summary>
    public bool TrySpendLumes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Progress.Lumes < amount)
        {
            return false;
        }

        Progress.Lumes -= amount;
        return true;
    }

    /// <summary>Registra o Guardião derrotado: conquista de rank, saque garantido, Eco e conjunto. Repetir o Guardião só rende saque.</summary>
    public GuardianKillResult RecordGuardianKill(string guardianId)
    {
        if (!Catalog.Guardians.TryGetValue(guardianId, out var guardian))
        {
            throw new ArgumentException($"Guardião desconhecido: '{guardianId}'.", nameof(guardianId));
        }

        var before = Ladder.CurrentIndex(Progress);
        var first = !Progress.GuardiansDefeated.Contains(guardian.Id);
        if (first)
        {
            Progress.GuardiansDefeated.Add(guardian.Id);
        }

        var loot = GrantLoot(guardian.Rank, LootSource.Guardian(guardian.Id));
        EchoDef? echo = null;
        if (guardian.Echo is not null && !Progress.Echoes.Contains(guardian.Echo))
        {
            Progress.Echoes.Add(guardian.Echo);
            echo = Catalog.Echoes[guardian.Echo];
            AutoEquipEcho(echo.Id);
        }

        return new GuardianKillResult(first, loot, echo, DetectRankChange(before));
    }

    /// <summary>Registra a aprovação no exame da Ordem do rank (a sala do exame é jogada por fora; aqui só vale a regra).</summary>
    public ExamResult TryPassExam(Rank rank)
    {
        var exam = Catalog.ExamFor(rank);
        if (exam is null)
        {
            return new ExamResult(ExamStatus.NoExamForRank, null, 0, null);
        }

        if (Progress.ExamsPassed.Contains(exam.Id))
        {
            return new ExamResult(ExamStatus.AlreadyPassed, exam, 0, null);
        }

        if (Catalog.RankIndex(Rank) < Catalog.RankIndex(rank))
        {
            return new ExamResult(ExamStatus.RankTooLow, exam, 0, null);
        }

        var before = Ladder.CurrentIndex(Progress);
        Progress.ExamsPassed.Add(exam.Id);
        Progress.Xp = checked(Progress.Xp + exam.RewardXp);
        return new ExamResult(ExamStatus.Passed, exam, exam.RewardXp, DetectRankChange(before));
    }

    /// <summary>Vida máxima extra pelo rank e pelas peças (some ao HP base do jogador).</summary>
    public int MaxHpBonus => Ladder.MaxHpBonus(Rank) + Equipment().Total.Hp;

    private RankChange? DetectRankChange(int beforeIndex)
    {
        var after = Ladder.CurrentIndex(Progress);
        if (after == beforeIndex)
        {
            return null;
        }

        var from = Catalog.Ranks[beforeIndex];
        var to = Catalog.Ranks[after];
        return new RankChange(from.Id, to.Id, from.Stage, to.Stage);
    }

    private void Sanitize()
    {
        Progress.Materials ??= [];
        Progress.Items ??= [];
        Progress.WeaponLevels ??= [];
        Progress.Equipped ??= [];
        Progress.Echoes ??= [];
        Progress.EchoSlots ??= [];
        Progress.GuardiansDefeated ??= [];
        Progress.ExamsPassed ??= [];
        Progress.LostFragmentos ??= [];
        Progress.Xp = Math.Max(0, Progress.Xp);
        Progress.Lumes = Math.Max(0, Progress.Lumes);
        Progress.Fragmentos = Math.Max(0, Progress.Fragmentos);

        Drop(Progress.Materials, (id, n) => !Catalog.Materials.ContainsKey(id) || n <= 0, "material");
        Drop(Progress.LostFragmentos, (_, n) => n <= 0, "Lanterna vazia");
        Progress.GuardiansDefeated = Dedup(Progress.GuardiansDefeated, Catalog.Guardians.ContainsKey, "Guardião");
        Progress.ExamsPassed = Dedup(Progress.ExamsPassed, Catalog.Exams.ContainsKey, "exame");
        Progress.Echoes = Dedup(Progress.Echoes, Catalog.Echoes.ContainsKey, "Eco");
        Drop(Progress.WeaponLevels, (id, _) => !Catalog.Weapons.ContainsKey(id), "arma");
        foreach (var id in Progress.WeaponLevels.Keys.ToList())
        {
            Progress.WeaponLevels[id] = Math.Clamp(Progress.WeaponLevels[id], 1, Forge.MaxLevel);
        }

        Progress.WeaponLevels.TryAdd(ClassId, 1);

        var slots = Catalog.GearData.Slots.Select(s => s.Id).ToHashSet();
        var removed = Progress.Items.RemoveAll(i => !slots.Contains(i.Slot));
        if (removed > 0)
        {
            _warnings.Add($"{removed} peça(s) com slot inexistente descartada(s)");
        }

        var ids = Progress.Items.Select(i => i.Id).ToHashSet();
        Progress.NextItemId = Math.Max(Progress.NextItemId, (ids.Count == 0 ? 0 : ids.Max()) + 1);
        Drop(Progress.Equipped, (slot, id) => !Progress.Items.Any(i => i.Id == id && i.Slot == slot), "equipado");

        var count = Catalog.EchoData.Slots;
        var echoSlots = new List<string?>(count);
        for (var i = 0; i < count; i++)
        {
            var id = i < Progress.EchoSlots.Count ? Progress.EchoSlots[i] : null;
            echoSlots.Add(id is not null && Progress.Echoes.Contains(id) && !echoSlots.Contains(id) ? id : null);
        }

        Progress.EchoSlots = echoSlots;
        AbandonRunIfAny();
    }

    private void Drop<TValue>(Dictionary<string, TValue> map, Func<string, TValue, bool> drop, string what)
    {
        foreach (var (key, value) in map.ToList())
        {
            if (drop(key, value))
            {
                map.Remove(key);
                _warnings.Add($"{what} '{key}' descartado");
            }
        }
    }

    private List<string> Dedup(List<string> list, Func<string, bool> valid, string what)
    {
        var result = new List<string>();
        foreach (var id in list)
        {
            if (!valid(id))
            {
                _warnings.Add($"{what} '{id}' descartado");
            }
            else if (!result.Contains(id))
            {
                result.Add(id);
            }
        }

        return result;
    }
}
