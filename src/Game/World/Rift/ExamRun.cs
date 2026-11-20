using System;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Game.Hunter;

namespace Vespera.Game.World.Rift;

/// <summary>
/// O exame da Ordem dentro da Brecha (issue #79): o exame de um rank é a Brecha do Guardião daquele rank, e os desafios de
/// <c>data/progressao/exams.json</c> (tempo, limite de golpes, objetivos) valem de verdade: <see cref="Resolve"/> confere
/// a caçada contra eles (<see cref="ExamJudge"/>) antes de gravar o selo. Avisa na entrada, quando o limite de golpes
/// estoura e quando o tempo acaba. O bot de teste (<see cref="RiftSession.Cheat"/>: invulnerável e dano x10) não presta
/// exame: para ele vale só a regra de rank, como antes.
/// </summary>
public static class ExamRun
{
    private static bool _hitsWarned;
    private static bool _timeWarned;

    /// <summary>Exame pendente que esta Brecha toma: a Brecha é a do Guardião de um rank com exame, o caçador está nesse rank e ainda não passou.</summary>
    public static ExamDef? Pending()
    {
        if (!Progression.IsActive || RiftSession.Cheat || HuntService.Plan?.HasGuardian != true)
        {
            return null;
        }

        var catalog = Progression.Catalog;
        foreach (var step in catalog.Ranks)
        {
            if (!step.Exam || step.Guardian != RiftSession.Brecha.GuardianId)
            {
                continue;
            }

            var exam = catalog.ExamFor(step.Id);
            if (exam is null || Progression.State.ExamsPassed.Contains(exam.Id) || catalog.RankIndex(Progression.Rank) < catalog.RankIndex(step.Id))
            {
                return null;
            }

            return exam;
        }

        return null;
    }

    /// <summary>Entrou na Brecha: zera os avisos e diz as regras da prova.</summary>
    public static void Begin(Action<string> toast)
    {
        _hitsWarned = false;
        _timeWarned = false;
        if (Pending() is { } exam)
        {
            toast($"{exam.Name}: {ExamJudge.Rules(exam)}.");
        }
    }

    /// <summary>O caçador levou um golpe: avisa quando passa do limite do exame (o selo se perde nesta caçada).</summary>
    public static void OnHit(Action<string> toast)
    {
        if (_hitsWarned || Pending() is not { MaxHits: { } max } exam || HuntService.Report is not { } report || report.HitsTaken <= max)
        {
            return;
        }

        _hitsWarned = true;
        toast($"{exam.Name}: golpes demais ({report.HitsTaken} de {max}). Esta prova está perdida; tente de novo em outra Brecha.");
    }

    /// <summary>Chamado a cada quadro dentro da Brecha: avisa uma vez quando o tempo do exame acaba.</summary>
    public static void OnTick(Action<string> toast)
    {
        if (_timeWarned || HuntService.Report is not { } report || Pending() is not { } exam || report.Seconds <= exam.TimeLimitSeconds)
        {
            return;
        }

        _timeWarned = true;
        toast($"{exam.Name}: o tempo acabou. Esta prova está perdida; tente de novo em outra Brecha.");
    }

    /// <summary>
    /// O Guardião caiu: confere o exame do rank dele (se houver) e grava o selo quando os desafios foram cumpridos.
    /// Devolve o resultado para o aviso na tela (null = não havia exame a julgar).
    /// </summary>
    public static ExamResult? Resolve(Rank rank, string guardianId)
    {
        if (!Progression.IsActive)
        {
            return null;
        }

        if (RiftSession.Cheat || HuntService.Report is not { } report)
        {
            return Progression.TryPassExam(rank);
        }

        var run = new ExamRunStats(report.Seconds, report.HitsTaken, report.Kills, GuardianDefeated: true);
        return Progression.TryPassExam(rank, run);
    }

    /// <summary>Texto do aviso do resultado do exame (vazio quando não há o que dizer: sem exame, já aprovado ou rank baixo).</summary>
    public static string Message(ExamResult? result) => result switch
    {
        { Status: ExamStatus.Passed, Exam: { } e } => $" {e.Name}: aprovado! +{result.XpAwarded} XP.",
        { Status: ExamStatus.Failed, Exam: { } e, Failures: { } f } => $" {e.Name}: reprovado ({string.Join("; ", f)}). Fecha-se a Brecha de novo para tentar outra vez.",
        _ => string.Empty,
    };
}
