namespace Vespera.Core.Advancement;

/// <summary>Como a caçada terminou, e quanto dos Fragmentos foi para a cidade ou ficou na Brecha.</summary>
public sealed record RunEnd(string Neighborhood, bool Survived, int FragmentosBanked, int FragmentosLeft);

/// <summary>
/// Regra da Lanterna (GDD 5.5): ao morrer na Brecha, os Fragmentos daquela caçada ficam na Lanterna, no ponto onde caiu; ela reaparece
/// na próxima Brecha do <b>mesmo bairro</b>, e entrar nela recolhe tudo. O equipamento, os materiais e as peças nunca se perdem.
/// </summary>
public sealed partial class ProgressionEngine
{
    public bool InRun => Progress.RunNeighborhood is not null;

    public string? RunNeighborhood => Progress.RunNeighborhood;

    public int RunFragmentos => Progress.RunFragmentos;

    /// <summary>Fragmentos deixados na Lanterna de um bairro (0 se não há).</summary>
    public int LanternFragmentos(string neighborhoodId) => Progress.LostFragmentos.GetValueOrDefault(neighborhoodId);

    /// <summary>Total de Fragmentos esperando em Lanternas perdidas (todos os bairros).</summary>
    public int TotalLostFragmentos => Progress.LostFragmentos.Values.Sum();

    /// <summary>
    /// Entra numa Brecha do bairro. Os Fragmentos da Lanterna daquele bairro (se houver) voltam para a caçada e são devolvidos aqui.
    /// Se já havia uma caçada em andamento, ela é tratada como abandonada (os Fragmentos dela ficam na Lanterna).
    /// </summary>
    public int BeginRun(string neighborhoodId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(neighborhoodId);
        AbandonRunIfAny();
        var recovered = 0;
        if (Progress.LostFragmentos.Remove(neighborhoodId, out var lost))
        {
            recovered = lost;
        }

        Progress.RunNeighborhood = neighborhoodId;
        Progress.RunFragmentos = recovered;
        return recovered;
    }

    /// <summary>
    /// Termina a caçada. Saiu vivo: os Fragmentos vão para a carteira. Morreu: ficam na Lanterna do bairro até a próxima entrada nele.
    /// Sem caçada em andamento não faz nada.
    /// </summary>
    public RunEnd? EndRun(bool survived)
    {
        if (Progress.RunNeighborhood is not { } neighborhood)
        {
            return null;
        }

        var pot = Progress.RunFragmentos;
        Progress.RunNeighborhood = null;
        Progress.RunFragmentos = 0;
        if (survived)
        {
            Progress.Fragmentos += pot;
            return new RunEnd(neighborhood, true, pot, 0);
        }

        if (pot > 0)
        {
            Progress.LostFragmentos[neighborhood] = Progress.LostFragmentos.GetValueOrDefault(neighborhood) + pot;
        }

        return new RunEnd(neighborhood, false, 0, pot);
    }

    private void AbandonRunIfAny()
    {
        if (Progress.RunNeighborhood is null)
        {
            return;
        }

        var end = EndRun(survived: false);
        if (end is { FragmentosLeft: > 0 })
        {
            _warnings.Add($"caçada em {end.Neighborhood} abandonada: {end.FragmentosLeft} Fragmentos ficaram na Lanterna");
        }
    }
}
