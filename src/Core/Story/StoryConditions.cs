using Vespera.Core.Ranks;

namespace Vespera.Core.Story;

/// <summary>O que as condições de fala e de objetivo precisam saber do jogo.</summary>
public interface IStoryContext
{
    bool HasFlag(string flag);

    /// <summary>Id do objetivo atual (vazio = nenhum).</summary>
    string ObjectiveId { get; }

    /// <summary>Índice do selo atual (0 = E).</summary>
    int RankIndex { get; }
}

/// <summary>
/// Condições em lista de termos, todos obrigatórios (E). Termos: <c>flag</c>, <c>!flag</c>, <c>objective:id</c>,
/// <c>!objective:id</c>, <c>rank&gt;=D</c>, <c>rank&lt;D</c> (e &gt;, &lt;=, ==). Termo desconhecido é erro, não "falso".
/// </summary>
public static class StoryConditions
{
    public static bool Evaluate(IReadOnlyList<string>? terms, IStoryContext context)
    {
        if (terms is null)
        {
            return true;
        }

        foreach (var raw in terms)
        {
            if (!EvaluateTerm(raw.Trim(), context))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateTerm(string term, IStoryContext context)
    {
        if (term.Length == 0)
        {
            throw new FormatException("condição vazia");
        }

        if (term[0] == '!')
        {
            return !EvaluateTerm(term[1..].Trim(), context);
        }

        if (term.StartsWith("objective:", StringComparison.Ordinal))
        {
            return context.ObjectiveId == term["objective:".Length..];
        }

        if (term.StartsWith("rank", StringComparison.Ordinal) && term.Length > 4 && "<>=".Contains(term[4]))
        {
            return EvaluateRank(term, context.RankIndex);
        }

        return context.HasFlag(term);
    }

    private static bool EvaluateRank(string term, int current)
    {
        var rest = term[4..];
        var op = rest.StartsWith(">=", StringComparison.Ordinal) ? ">="
            : rest.StartsWith("<=", StringComparison.Ordinal) ? "<="
            : rest.StartsWith("==", StringComparison.Ordinal) ? "=="
            : rest[..1];
        var name = rest[op.Length..].Trim();
        if (!Enum.TryParse<Rank>(name, ignoreCase: false, out var wanted))
        {
            throw new FormatException($"selo desconhecido na condição '{term}'");
        }

        var w = (int)wanted;
        return op switch
        {
            ">=" => current >= w,
            "<=" => current <= w,
            "==" => current == w,
            ">" => current > w,
            "<" => current < w,
            _ => throw new FormatException($"operador inválido em '{term}'"),
        };
    }
}
