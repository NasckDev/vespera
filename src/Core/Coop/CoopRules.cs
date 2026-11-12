using System.Text.Json;

namespace Vespera.Core.Coop;

/// <summary>Reanimar um aliado caído: segurar interagir perto dele por <see cref="HoldSeconds"/>.</summary>
public sealed class ReviveTuning
{
    public double HoldSeconds { get; set; } = 2.0;

    /// <summary>Alcance em alturas humanas do mapa (pé a pé).</summary>
    public double Radius { get; set; } = 1.1;

    /// <summary>Vida devolvida, em fração da máxima.</summary>
    public double HpFraction { get; set; } = 0.4;

    /// <summary>Quanto o progresso volta por segundo quando o aliado solta o botão ou se afasta.</summary>
    public double DecayPerSecond { get; set; } = 1.0;
}

/// <summary>Câmera compartilhada e a coleira que impede um jogador de sair da tela.</summary>
public sealed class CoopCameraTuning
{
    /// <summary>Piso do afastamento: o caçador nunca fica menor que isto na tela 1080p.</summary>
    public double MinHunterPx { get; set; } = 85;

    /// <summary>Folga entre o caçador e a borda da tela, em alturas humanas.</summary>
    public double EdgeMargin { get; set; } = 1.3;

    /// <summary>Força do puxão de volta quando um jogador ficou além do limite (fração da distância por segundo).</summary>
    public double TetherPull { get; set; } = 2.5;
}

/// <summary>Co-op online (ENet, host autoritativo): porta padrão, frequência dos snapshots e folga da interpolação.</summary>
public sealed class OnlineTuning
{
    public int Port { get; set; } = 24680;

    public int SnapshotHz { get; set; } = 20;

    /// <summary>Atraso de desenho no convidado (ms): ele interpola entre os dois snapshots em volta de agora - atraso.</summary>
    public int InterpolationMs { get; set; } = 100;

    /// <summary>Segundos sem notícia do outro lado até tratar como desconectado.</summary>
    public double TimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Regras do co-op (GDD §5.8; <c>data/coop.json</c>): vida dos monstros e tamanho das ondas por número de jogadores, dano do
/// Guardião (não linear), XP compartilhado (cada um recebe o valor cheio), saque individual, reanimação e câmera.
/// Puro, sem Godot: é o mesmo código que o host do online vai rodar.
/// </summary>
public sealed class CoopRules
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public int MaxPlayers { get; set; } = 4;

    public List<double> EnemyHpMultiplier { get; set; } = [1.0, 1.5, 2.0, 2.5];

    public int ExtraEnemiesPerWavePerPlayer { get; set; } = 1;

    public List<double> BossDamageMultiplier { get; set; } = [1.0, 1.15, 1.15, 1.15];

    public ReviveTuning Revive { get; set; } = new();

    public CoopCameraTuning Camera { get; set; } = new();

    public OnlineTuning Online { get; set; } = new();

    /// <summary>Valores padrão (iguais aos de <c>data/coop.json</c>), para testes e fallback.</summary>
    public static CoopRules Defaults => new();

    public static CoopRules Parse(string json)
    {
        var rules = JsonSerializer.Deserialize<CoopRules>(json, Options) ?? throw new FormatException("coop.json vazio");
        var problems = rules.Validate();
        if (problems.Count > 0)
        {
            throw new FormatException($"coop.json: {string.Join("; ", problems)}");
        }

        return rules;
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        if (MaxPlayers is < 2 or > 8)
        {
            problems.Add($"maxPlayers fora de 2..8 ({MaxPlayers})");
        }

        CheckTable(problems, "enemyHpMultiplier", EnemyHpMultiplier);
        CheckTable(problems, "bossDamageMultiplier", BossDamageMultiplier);
        if (ExtraEnemiesPerWavePerPlayer < 0)
        {
            problems.Add("extraEnemiesPerWavePerPlayer negativo");
        }

        if (Revive.HoldSeconds <= 0 || Revive.Radius <= 0 || Revive.HpFraction is <= 0 or > 1 || Revive.DecayPerSecond < 0)
        {
            problems.Add("revive inválido (holdSeconds/radius > 0, hpFraction em (0,1], decayPerSecond >= 0)");
        }

        if (Camera.MinHunterPx <= 0 || Camera.EdgeMargin < 0 || Camera.TetherPull < 0)
        {
            problems.Add("camera inválida");
        }

        if (Online.Port is < 1024 or > 65535 || Online.SnapshotHz is < 5 or > 60 || Online.InterpolationMs is < 0 or > 1000 || Online.TimeoutSeconds <= 0)
        {
            problems.Add("online inválido (port 1024..65535, snapshotHz 5..60, interpolationMs 0..1000, timeoutSeconds > 0)");
        }

        return problems;
    }

    /// <summary>Jogadores dentro de 1..<see cref="MaxPlayers"/>.</summary>
    public int Clamp(int players) => Math.Clamp(players, 1, MaxPlayers);

    /// <summary>Multiplicador da vida de todo monstro (Guardião incluso): 1 jogador ×1; 2 ×1,5; 3 ×2; 4 ×2,5.</summary>
    public double EnemyHp(int players) => Lookup(EnemyHpMultiplier, players);

    /// <summary>Tamanho de uma onda: +<see cref="ExtraEnemiesPerWavePerPlayer"/> monstro por jogador além do primeiro.</summary>
    public int WaveSize(int baseCount, int players) => Math.Max(0, baseCount) + ((Clamp(players) - 1) * ExtraEnemiesPerWavePerPlayer);

    /// <summary>Dano do Guardião: não cresce com cada jogador (×1,15 no co-op).</summary>
    public double BossDamage(int players) => Lookup(BossDamageMultiplier, players);

    /// <summary>XP compartilhado: cada caçador vivo ou caído na sala recebe o valor cheio (não divide).</summary>
    public static int XpPerPlayer(int xp, int players) => players <= 0 ? 0 : Math.Max(0, xp);

    /// <summary>Vida devolvida a um caçador reanimado (pelo menos 1).</summary>
    public int RevivedHp(int maxHp) => Math.Max(1, (int)Math.Round(maxHp * Revive.HpFraction));

    /// <summary>Fração mínima do zoom da regra (110 px) que ainda deixa o caçador com <see cref="CoopCameraTuning.MinHunterPx"/>.</summary>
    public double MinZoomFactor(double ruleHunterPx) => ruleHunterPx <= 0 ? 1 : Math.Min(1, Camera.MinHunterPx / ruleHunterPx);

    private double Lookup(List<double> table, int players) => table[Math.Min(Clamp(players), table.Count) - 1];

    private void CheckTable(List<string> problems, string name, List<double> table)
    {
        if (table.Count == 0 || table.Count > MaxPlayers)
        {
            problems.Add($"{name} precisa de 1..{MaxPlayers} valores");
            return;
        }

        if (Math.Abs(table[0] - 1.0) > 1e-9)
        {
            problems.Add($"{name}[0] (1 jogador) deve ser 1");
        }

        for (var i = 0; i < table.Count; i++)
        {
            if (table[i] <= 0 || (i > 0 && table[i] < table[i - 1]))
            {
                problems.Add($"{name} deve ser positivo e não decrescente");
                return;
            }
        }
    }
}
