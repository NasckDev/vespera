using System.Globalization;

namespace Vespera.Core.Advancement;

/// <summary>O que o caçador fez na Brecha do exame (vem do relatório da caçada).</summary>
/// <param name="Seconds">Tempo de jogo dentro da Brecha.</param>
/// <param name="HitsTaken">Golpes recebidos.</param>
/// <param name="Kills">Monstros mortos, contando o Guardião.</param>
/// <param name="GuardianDefeated">O Guardião da Brecha caiu (a saída abriu).</param>
public readonly record struct ExamRunStats(double Seconds, int HitsTaken, int Kills, bool GuardianDefeated);

/// <summary>Resultado de conferir uma prova contra os desafios do exame; <see cref="Failures"/> diz o que faltou, em pt-BR.</summary>
public sealed record ExamVerdict(bool Passed, IReadOnlyList<string> Failures);

/// <summary>
/// Confere os desafios de um exame da Ordem (<c>data/progressao/exams.json</c>): objetivos (sobreviver N s, matar N, chegar à
/// saída), limite de tempo e limite de golpes. O exame é a própria Brecha do Guardião do rank; a prova só vale se o
/// Guardião caiu. "Chegar à saída" é fechar a Brecha, e "matar N" não conta o Guardião. Lógica pura, sem Godot.
/// </summary>
public static class ExamJudge
{
    public static ExamVerdict Judge(ExamDef exam, ExamRunStats run)
    {
        ArgumentNullException.ThrowIfNull(exam);
        var failures = new List<string>();
        foreach (var objective in exam.Objectives)
        {
            switch (objective.Type)
            {
                case "reach_exit":
                    if (!run.GuardianDefeated)
                    {
                        failures.Add("a Brecha não foi fechada");
                    }

                    break;
                case "survive":
                    if (run.Seconds < (objective.Seconds ?? 0))
                    {
                        failures.Add($"era preciso aguentar {objective.Seconds} s");
                    }

                    break;
                case "kill_count":
                    var kills = Math.Max(0, run.Kills - (run.GuardianDefeated ? 1 : 0));
                    if (kills < (objective.Count ?? 0))
                    {
                        failures.Add($"faltaram ecos: {kills} de {objective.Count}");
                    }

                    break;
                default:
                    failures.Add($"objetivo '{objective.Type}' sem regra no jogo");
                    break;
            }
        }

        if (run.Seconds > exam.TimeLimitSeconds)
        {
            failures.Add($"passou do tempo ({Clock(exam.TimeLimitSeconds)})");
        }

        if (exam.MaxHits is { } max && run.HitsTaken > max)
        {
            failures.Add($"golpes demais: {run.HitsTaken} (o limite é {max})");
        }

        return new ExamVerdict(failures.Count == 0, failures);
    }

    /// <summary>Regras do exame numa frase curta para o aviso de entrada na Brecha (só o que de fato limita a prova).</summary>
    public static string Rules(ExamDef exam)
    {
        ArgumentNullException.ThrowIfNull(exam);
        var parts = new List<string> { $"em até {Clock(exam.TimeLimitSeconds)}" };
        if (exam.MaxHits is { } max)
        {
            parts.Add(max == 1 ? "levando no máximo 1 golpe" : $"levando no máximo {max} golpes");
        }

        foreach (var objective in exam.Objectives)
        {
            switch (objective.Type)
            {
                case "survive":
                    parts.Add($"aguentando {objective.Seconds} s");
                    break;
                case "kill_count":
                    parts.Add($"derrotando {objective.Count} ecos");
                    break;
            }
        }

        return $"feche a Brecha {string.Join(", ", parts)}";
    }

    /// <summary>Tempo em minutos e segundos (150 s = 2:30).</summary>
    public static string Clock(int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
}
