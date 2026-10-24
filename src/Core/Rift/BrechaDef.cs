using System.Text.Json;
using System.Text.Json.Serialization;
using Vespera.Core.Common;

namespace Vespera.Core.Rift;

/// <summary>
/// Uma Brecha jogável da v2 (<c>src/Data/brecha_*.json</c>): salas em ordem, monstros do tema com o verbo de cada um,
/// o Guardião e o ritmo de combate do caçador por arma. Distâncias e velocidades na escala 1 do caçador.
/// </summary>
public sealed class BrechaDef
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Número da Brecha na <c>LostLantern</c> do save (a Lanterna volta para a mesma Brecha).</summary>
    public int LanternId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Rank { get; set; } = "E";

    public string Bairro { get; set; } = string.Empty;

    /// <summary>Id do bairro na progressão (a Lanterna perdida volta na próxima Brecha deste bairro).</summary>
    public string NeighborhoodId { get; set; } = string.Empty;

    /// <summary>Guardião desta Brecha no catálogo de progressão (<c>data/progressao/guardians.json</c>).</summary>
    public string GuardianId { get; set; } = string.Empty;

    public List<string> Rooms { get; set; } = [];

    /// <summary>Fase do bairro para onde a Brecha devolve o caçador (Ato I: praca; Ato II: pomar_alto).</summary>
    public string Hub { get; set; } = "praca";

    /// <summary>Regras de caçada desta Brecha (res://, formato de data/cacada/cacada.json).</summary>
    public string Hunt { get; set; } = "res://data/cacada/cacada.json";

    public Dictionary<string, RiftMonsterDef> Monsters { get; set; } = [];

    public BossTuning Boss { get; set; } = new();

    public HunterTuning Hunter { get; set; } = new();

    /// <summary>Por id de arma do save (<c>sword</c>, <c>bow</c>, <c>hammer</c>).</summary>
    public Dictionary<string, MoveSet> Moves { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Lê e valida; ignora chaves que começam com "_" (comentários).</summary>
    public static BrechaDef Parse(string json)
    {
        var def = JsonSerializer.Deserialize<BrechaDef>(json, Options) ?? throw new FormatException("brecha vazia");
        var problems = def.Validate();
        if (problems.Count > 0)
        {
            throw new FormatException($"brecha '{def.Id}': {string.Join("; ", problems)}");
        }

        return def;
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        if (Rooms.Count == 0)
        {
            problems.Add("sem salas");
        }

        if (NeighborhoodId.Length == 0 || GuardianId.Length == 0)
        {
            problems.Add("sem neighborhoodId/guardianId (Lanterna e Guardião da progressão)");
        }

        foreach (var (id, monster) in Monsters)
        {
            if (monster.Hp <= 0 || monster.Damage <= 0 || monster.Period <= 0 || monster.Xp < 0)
            {
                problems.Add($"monstro '{id}' com hp/dano/período inválido");
            }
        }

        foreach (var (id, moves) in Moves)
        {
            var n = moves.Light.Count;
            if (n == 0 || moves.LightRange.Count != n || moves.LightArc.Count != n || moves.LightKnockback.Count != n || moves.LightHitStop.Count != n)
            {
                problems.Add($"golpes de '{id}' com listas de tamanhos diferentes");
            }
        }

        if (Boss.Phases.Count != 2 || Boss.Phases[0] <= Boss.Phases[1])
        {
            problems.Add("boss.phases precisa de dois limiares decrescentes");
        }

        return problems;
    }
}

public sealed class RiftMonsterDef
{
    public string Name { get; set; } = string.Empty;

    public double Hp { get; set; }

    public double Damage { get; set; }

    public double Speed { get; set; }

    /// <summary>XP ao matar (só monstros que dão saque; as crias da Lodo-Mãe não contam).</summary>
    public int Xp { get; set; }

    public double HurtRadius { get; set; }

    public double ContactRadius { get; set; }

    /// <summary>Ritmo do verbo: o Lodo pula a cada <c>period</c> s (GDD §4: 1,2 s).</summary>
    public double Period { get; set; }

    /// <summary>Duração do aviso antes do golpe (chefe: 0,6 s, GDD §4).</summary>
    public double Telegraph { get; set; }

    public bool Boss { get; set; }
}

public sealed class BossTuning
{
    /// <summary>Frações de HP em que entram as fases 2 e 3.</summary>
    public List<double> Phases { get; set; } = [];

    public int Cores { get; set; } = 3;

    public double CoreHp { get; set; }

    /// <summary>Em quantos Lodos a mãe se divide na fase 3 com os núcleos intactos.</summary>
    public int SplitBase { get; set; } = 3;

    public int SpawnPerJump { get; set; } = 2;

    public int MaxMinions { get; set; } = 5;

    public double SwallowSeconds { get; set; }

    public double SwallowDamagePerSecond { get; set; }
}

public sealed class HunterTuning
{
    public double StaminaMax { get; set; } = 100;

    public double DodgeCost { get; set; } = 30;

    public double StaminaRegen { get; set; } = 50;

    public double StaminaRegenDelay { get; set; } = 0.5;

    public double DodgeSeconds { get; set; } = 0.3;

    public double DodgeInvulnerable { get; set; } = 0.25;

    public double DodgeDistance { get; set; } = 240;

    public double HurtInvulnerable { get; set; } = 0.7;

    public double HurtStun { get; set; } = 0.25;

    public double HurtKnockback { get; set; } = 280;

    public double ComboBuffer { get; set; } = 0.25;

    public double ComboWindow { get; set; } = 0.4;
}

/// <summary>Ritmo de uma arma: multiplicadores de dano por golpe do combo, alcance, empurrão e pausa no impacto.</summary>
public sealed class MoveSet
{
    public List<double> Light { get; set; } = [];

    public List<double> LightRange { get; set; } = [];

    /// <summary>Abertura do setor em rad (0 = projétil).</summary>
    public List<double> LightArc { get; set; } = [];

    public List<double> LightKnockback { get; set; } = [];

    public List<double> LightHitStop { get; set; } = [];

    public double Heavy { get; set; }

    public double HeavyRange { get; set; }

    public double HeavyKnockback { get; set; }

    public double HeavyHitStop { get; set; }

    /// <summary>Segurar este tempo antes de soltar arma o pesado.</summary>
    public double HeavyCharge { get; set; }

    /// <summary>Multiplicador de dano em partes quebráveis (Martelo 2: quebra em dobro).</summary>
    public double PartMult { get; set; } = 1;
}
