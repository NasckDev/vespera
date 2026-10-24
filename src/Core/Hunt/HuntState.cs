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

    /// <summary>Tomou a sopa da Nilza: a próxima caçada começa com mais vida máxima (<see cref="RunPerks"/>).</summary>
    public bool SoupReady { get; set; }

    /// <summary>Comprou o mapa da Lis: a próxima caçada mostra o tipo de cada sala e o tesouro.</summary>
    public bool MapReady { get; set; }

    public double BestRunSeconds { get; set; }

    public int TotalKills { get; set; }

    /// <summary>Salas limpas em todas as caçadas (placar contra o Ravi).</summary>
    public int TotalRooms { get; set; }

    /// <summary>Salas do Ravi no placar: ele anda quase no mesmo passo, um pouco atrás com o tempo.</summary>
    public int RaviRooms => 3 + (int)(TotalRooms * 0.85);

    /// <summary>Semente da próxima caçada (anda a cada entrada).</summary>
    public int NextSeed { get; set; } = 20261023;

    /// <summary>Quebráveis já quebrados neste "dia" (id do mapa + índice), para a Praça não virar mina de Lumes.</summary>
    public List<string> Broken { get; set; } = [];

    /// <summary>Estatísticas de combate desde o Ato I (o Reflexo do Final copia o estilo do jogador).</summary>
    public CombatStats Stats { get; set; } = new();

    /// <summary>Normaliza depois de carregar um save (nada negativo, listas presentes).</summary>
    public void Sanitize(int maxPotions)
    {
        RunsStarted = Math.Max(0, RunsStarted);
        RunsCompleted = Math.Clamp(RunsCompleted, 0, Math.Max(RunsStarted, RunsCompleted));
        Potions = Math.Clamp(Potions, 0, maxPotions);
        Flowers = Math.Max(0, Flowers);
        Contracts ??= [];
        Stats ??= new CombatStats();
        Stats.Weapons ??= [];
        Stats.Blessings ??= [];
        Broken ??= [];
    }
}

/// <summary>
/// Registro do estilo de luta (ROTEIRO-V2 §9 "Todos"): caçadas por arma, bênçãos escolhidas, esquivas, golpes levados e
/// Lanternas perdidas. Taxa de esquiva = esquivas ÷ (esquivas + golpes levados).
/// </summary>
public sealed class CombatStats
{
    public Dictionary<string, int> Weapons { get; set; } = [];

    public Dictionary<string, int> Blessings { get; set; } = [];

    public int Dodges { get; set; }

    public int HitsTaken { get; set; }

    public int LanternsLost { get; set; }

    public double DodgeRate => Dodges + HitsTaken == 0 ? 0 : Dodges / (double)(Dodges + HitsTaken);
}
