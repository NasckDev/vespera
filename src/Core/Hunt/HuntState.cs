namespace Vespera.Core.Hunt;

/// <summary>
/// Estado das caçadas no save (bloco <c>hunt</c>): caçadas começadas e completas (a Lodo-Mãe aparece depois de N
/// completas), contratos em oferta no mural, poções na bolsa, flores colhidas, a moeda do poço e o melhor tempo.
/// Campos novos com padrão: saves antigos carregam com tudo zerado.
/// </summary>
public sealed class HuntState
{
    public int RunsStarted { get; set; }

    public int RunsCompleted { get; set; }

    public int Deaths { get; set; }

    /// <summary>Voltas da Brecha (cada uma é um "dia": contratos e flores renovam).</summary>
    public int Returns { get; set; }

    public List<Contract> Contracts { get; set; } = [];

    public int ContractsDone { get; set; }

    public int Potions { get; set; }

    public int Flowers { get; set; }

    /// <summary>Flores ainda colhíveis na Praça neste "dia".</summary>
    public int FlowersLeft { get; set; } = -1;

    /// <summary>Moeda jogada no poço: a próxima caçada rende mais Fragmentos.</summary>
    public bool WellLuck { get; set; }

    public double BestRunSeconds { get; set; }

    public int TotalKills { get; set; }

    /// <summary>Semente da próxima caçada (anda a cada entrada).</summary>
    public int NextSeed { get; set; } = 20261023;

    /// <summary>Quebráveis já quebrados neste "dia" (id do mapa + índice), para a Praça não virar mina de Lumes.</summary>
    public List<string> Broken { get; set; } = [];

    /// <summary>Normaliza depois de carregar um save (nada negativo, listas presentes).</summary>
    public void Sanitize(int maxPotions)
    {
        RunsStarted = Math.Max(0, RunsStarted);
        RunsCompleted = Math.Clamp(RunsCompleted, 0, Math.Max(RunsStarted, RunsCompleted));
        Potions = Math.Clamp(Potions, 0, maxPotions);
        Flowers = Math.Max(0, Flowers);
        Contracts ??= [];
        Broken ??= [];
    }
}
