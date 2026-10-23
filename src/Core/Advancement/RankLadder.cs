using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

public enum RequirementKind
{
    Xp,
    Guardian,
    Exam,
}

/// <summary>Uma exigência para o próximo rank, com o texto pronto para a ficha do personagem.</summary>
public sealed record RankRequirement(RequirementKind Kind, string Id, string Text, bool Done);

/// <summary>Situação do caçador na escada: rank atual, o que falta para o próximo e o progresso do XP.</summary>
public sealed record RankStatus(
    Rank Current,
    Rank? Next,
    int Xp,
    int XpIntoRank,
    int XpSpan,
    IReadOnlyList<RankRequirement> Requirements)
{
    /// <summary>Barra de XP de 0 a 1 dentro do rank (1 no topo ou quando o XP do próximo já foi atingido).</summary>
    public float Progress => Next is null || XpSpan <= 0 ? 1f : Math.Clamp((float)XpIntoRank / XpSpan, 0f, 1f);

    public bool AtTop => Next is null;

    public IReadOnlyList<RankRequirement> Missing => [.. Requirements.Where(r => !r.Done)];
}

/// <summary>
/// A escada de ranks (PLANO-V2 §6): sobe-se por XP <b>e</b> por conquista. Para sair do rank R é preciso o XP do rank seguinte,
/// ter derrubado o Guardião de R e, onde há exame (C, A, S, SSS), ter passado nele. O rank é sempre calculado do estado:
/// nunca fica salvo, então nunca desencontra do XP e das conquistas.
/// </summary>
public sealed class RankLadder(AdvancementCatalog catalog)
{
    public IReadOnlyList<RankStepDef> Steps => catalog.Ranks;

    public int CurrentIndex(HunterProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var index = 0;
        while (index < catalog.TopRankIndex && CanLeave(progress, index) && progress.Xp >= catalog.Ranks[index + 1].Xp)
        {
            index++;
        }

        return index;
    }

    public Rank Current(HunterProgress progress) => catalog.Ranks[CurrentIndex(progress)].Id;

    public int Stage(Rank rank) => catalog.Step(rank).Stage;

    public string StageName(int stage) => catalog.RankData.Stages.First(s => s.Id == stage).Name;

    /// <summary>Vida máxima extra pelo rank (sem equipamento): 20 por degrau da escada.</summary>
    public int MaxHpBonus(Rank rank) => catalog.RankData.HpPerRank * catalog.RankIndex(rank);

    /// <summary>As conquistas do rank (Guardião e exame, quando há) estão cumpridas: dá para sair dele.</summary>
    public bool CanLeave(HunterProgress progress, int rankIndex)
    {
        var step = catalog.Ranks[rankIndex];
        if (step.Guardian is null)
        {
            return false;
        }

        return progress.GuardiansDefeated.Contains(step.Guardian) && (!step.Exam || ExamPassed(progress, step.Id));
    }

    public bool ExamPassed(HunterProgress progress, Rank rank) => catalog.ExamFor(rank) is { } exam && progress.ExamsPassed.Contains(exam.Id);

    public RankStatus Status(HunterProgress progress)
    {
        var index = CurrentIndex(progress);
        var step = catalog.Ranks[index];
        if (index == catalog.TopRankIndex)
        {
            return new RankStatus(step.Id, null, progress.Xp, 0, 0, []);
        }

        var next = catalog.Ranks[index + 1];
        var span = next.Xp - step.Xp;
        var into = Math.Clamp(progress.Xp - step.Xp, 0, span);
        return new RankStatus(step.Id, next.Id, progress.Xp, into, span, Requirements(progress, index));
    }

    private List<RankRequirement> Requirements(HunterProgress progress, int index)
    {
        var step = catalog.Ranks[index];
        var next = catalog.Ranks[index + 1];
        var list = new List<RankRequirement>();

        var missingXp = Math.Max(0, next.Xp - progress.Xp);
        list.Add(new RankRequirement(
            RequirementKind.Xp,
            next.Id.ToString(),
            missingXp == 0 ? $"XP necessário para o rank {next.Id} (cumprido)" : $"Alcance {next.Xp} XP (faltam {missingXp})",
            missingXp == 0));

        var guardian = catalog.Guardians[step.Guardian!];
        list.Add(new RankRequirement(
            RequirementKind.Guardian,
            guardian.Id,
            $"Derrote o Guardião do rank {step.Id}: {guardian.Name}",
            progress.GuardiansDefeated.Contains(guardian.Id)));

        if (step.Exam)
        {
            var exam = catalog.ExamFor(step.Id)!;
            list.Add(new RankRequirement(
                RequirementKind.Exam,
                exam.Id,
                $"Passe no Exame da Ordem do rank {step.Id}",
                progress.ExamsPassed.Contains(exam.Id)));
        }

        return list;
    }
}
