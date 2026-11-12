using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

/// <summary>
/// Números dos Ecos de Guardião em combate (a lógica pura; os efeitos visíveis ficam em <c>Game/Combat/Echo</c>).
/// Recarga, duração, força e alcance vêm de <c>data/progressao/echoes.json</c>; aqui ficam só as contas.
/// </summary>
public static class EchoRules
{
    /// <summary>Guardiões e chefes seguram só esta fração do atordoamento do Eco do Sino.</summary>
    public const double BossStunFactor = 0.35;

    /// <summary>Segundos entre dois espinhos da muda do Eco da Semente.</summary>
    public const double SeedInterval = 0.8;

    /// <summary>Segundos entre dois golpes do clone do Eco do Avesso.</summary>
    public const double CloneInterval = 0.7;

    /// <summary>Dano de um espinho ou de um golpe do clone.</summary>
    public static double Damage(EchoDef echo, double attack) => Math.Max(0, attack * echo.Power);

    /// <summary>Quantos espinhos a muda atira em toda a sua vida.</summary>
    public static int ThornCount(EchoDef echo) => Math.Max(0, (int)Math.Floor(echo.Duration / SeedInterval));

    /// <summary>Cura por segundo do Eco da Vela: <c>Power</c> da vida máxima espalhada em <c>Duration</c>.</summary>
    public static double HealPerSecond(EchoDef echo, int maxHp) =>
        echo.Duration <= 0 ? 0 : Math.Max(0, maxHp * echo.Power / echo.Duration);

    /// <summary>Segundos de atordoamento do Eco do Sino num alvo comum ou num Guardião.</summary>
    public static double StunSeconds(EchoDef echo, bool boss) => boss ? echo.Duration * BossStunFactor : echo.Duration;

    /// <summary>Vida máxima perdida enquanto o Eco da Vela dura: o rank fica um nível abaixo (0 no rank E).</summary>
    public static int VelaHpPenalty(AdvancementCatalog catalog, Rank rank)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.RankIndex(rank) > 0 ? catalog.RankData.HpPerRank : 0;
    }
}

/// <summary>Recarga de um Eco equipado (um por slot): conta o tempo e diz a fração para o ícone da HUD.</summary>
public sealed class EchoCooldown
{
    private double _total = 1;

    public double Remaining { get; private set; }

    public bool Ready => Remaining <= 0;

    /// <summary>0 = pronto; 1 = recém-usado.</summary>
    public float Fraction => (float)Math.Clamp(Remaining / _total, 0, 1);

    public void Start(double seconds)
    {
        _total = Math.Max(0.001, seconds);
        Remaining = seconds;
    }

    public void Tick(double dt) => Remaining = Math.Max(0, Remaining - dt);

    public void Reset() => Remaining = 0;
}
