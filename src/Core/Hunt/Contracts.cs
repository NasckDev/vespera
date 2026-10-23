using Vespera.Core.Common;

namespace Vespera.Core.Hunt;

/// <summary>Um contrato do mural: tipo (cacada, coleta, protecao, sem_dano), alvo e, na Coleta, o material pedido.</summary>
public sealed class Contract
{
    public string Kind { get; set; } = string.Empty;

    public int Target { get; set; } = 1;

    public string Material { get; set; } = string.Empty;
}

/// <summary>O que aconteceu numa caçada (a tela de resultados e os contratos leem isto).</summary>
public sealed class RunReport
{
    public bool Survived { get; set; }

    /// <summary>A caçada foi até o fim (saiu pela fenda da última sala).</summary>
    public bool Completed { get; set; }

    public int RoomsCleared { get; set; }

    public int RoomCount { get; set; }

    public int Kills { get; set; }

    public int EliteKills { get; set; }

    public int WavesSurvived { get; set; }

    public int DamageTaken { get; set; }

    public int HitsTaken { get; set; }

    /// <summary>Salas de combate terminadas sem levar golpe.</summary>
    public int FlawlessRooms { get; set; }

    public int Fragmentos { get; set; }

    public int Xp { get; set; }

    public double Seconds { get; set; }

    public bool GuardianDefeated { get; set; }

    /// <summary>Materiais trazidos (id → quantidade).</summary>
    public Dictionary<string, int> Materials { get; set; } = [];

    /// <summary>Peças de equipamento achadas (nome, rank e se é rara), para a tela de resultados.</summary>
    public List<LootLine> Items { get; set; } = [];

    public int MaterialCount(string id) => Materials.TryGetValue(id, out var n) ? n : 0;
}

/// <summary>Uma peça achada na caçada (tela de resultados).</summary>
public sealed record LootLine(string Name, string Rank, bool Rare);

/// <summary>Andamento de um contrato depois de uma caçada.</summary>
public sealed record ContractProgress(Contract Contract, string Title, string Text, int Have, int Need, bool Done, ContractKindDef Reward);

/// <summary>
/// Mural de contratos (playtest 23/10): sempre <see cref="ContractsDef.Offered"/> contratos repetíveis, sorteados de novo
/// a cada volta da Brecha. Cada um vale para a próxima caçada: Caçada (fechar uma caçada inteira), Coleta (trazer N de
/// um material), Proteção (sobreviver a N ondas) e Sem dano (terminar uma sala de combate sem levar golpe).
/// </summary>
public sealed class ContractBoard(HuntConfig config, Func<string, string> materialName)
{
    public List<Contract> Roll(IRandom random)
    {
        var kinds = config.Contracts.Kinds.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var picked = new List<Contract>();
        while (picked.Count < config.Contracts.Offered && kinds.Count > 0)
        {
            var i = random.RangeInt(0, kinds.Count - 1);
            var kind = kinds[i];
            kinds.RemoveAt(i);
            var def = config.Contracts.Kinds[kind];
            var contract = new Contract { Kind = kind, Target = random.RangeInt(def.Min, Math.Max(def.Min, def.Max)) };
            if (kind == "coleta" && config.Contracts.Materials.Count > 0)
            {
                contract.Material = config.Contracts.Materials[random.RangeInt(0, config.Contracts.Materials.Count - 1)];
            }

            picked.Add(contract);
        }

        return picked;
    }

    /// <summary>Como cada contrato ficou com o resultado da caçada (null = ainda sem caçada: mostra 0/N).</summary>
    public ContractProgress Evaluate(Contract contract, RunReport? report)
    {
        var def = config.Contracts.Kinds[contract.Kind];
        var have = report is null ? 0 : contract.Kind switch
        {
            "cacada" => report.Completed && report.Survived ? 1 : 0,
            "coleta" => report.Survived ? report.MaterialCount(contract.Material) : 0,
            "protecao" => report.WavesSurvived,
            "sem_dano" => report.FlawlessRooms,
            _ => 0,
        };
        var need = contract.Kind is "cacada" or "sem_dano" ? 1 : contract.Target;
        return new ContractProgress(contract, def.Title, Describe(contract), Math.Min(have, need), need, have >= need, def);
    }

    public string Describe(Contract contract)
    {
        var def = config.Contracts.Kinds[contract.Kind];
        var item = contract.Material.Length > 0 ? materialName(contract.Material) : string.Empty;
        return def.Text.Replace("{n}", contract.Target.ToString(), StringComparison.Ordinal).Replace("{item}", item, StringComparison.Ordinal);
    }
}
