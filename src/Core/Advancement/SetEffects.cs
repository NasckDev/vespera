namespace Vespera.Core.Advancement;

/// <summary>
/// Efeitos de combate dos conjuntos de Guardião (ROTEIRO-V2 §8.3). O JSON de equipamento só nomeia o efeito de cada bônus
/// (<see cref="SetBonusDef.Effect"/>); a regra e os números ficam aqui, sem Godot, para serem testados. O jogo consulta o
/// <see cref="ActiveSetEffects"/> do caçador (peças vestidas) nos pontos de combate.
/// </summary>
public static class SetEffects
{
    /// <summary>Lodo-Mãe (2): a lentidão que bosses e armadilhas aplicam ao caçador não pega.</summary>
    public const string SlowImmune = "slow_immune";

    /// <summary>Lodo-Mãe (4): o rastro da esquiva vira gel que prende os inimigos.</summary>
    public const string DodgeGel = "dodge_gel";

    /// <summary>Macieira Oca (2): cada inimigo derrotado cura parte da vida máxima.</summary>
    public const string KillHeal = "kill_heal";

    /// <summary>Macieira Oca (4): ao ser ferido, espinhos ferem os inimigos em volta.</summary>
    public const string HurtThorns = "hurt_thorns";

    /// <summary>Farol Afogado (2): a esquiva dura um instante a mais invencível.</summary>
    public const string DodgeIframes = "dodge_iframes";

    /// <summary>Farol Afogado (4): ao ser ferido, um clarão prende os inimigos em volta.</summary>
    public const string HurtFlash = "hurt_flash";

    /// <summary>Velória (4): uma vez por sala, o golpe que mataria deixa o caçador com 1 de vida e invencível por um instante.</summary>
    public const string LastStand = "last_stand";

    /// <summary>Sineiro Mudo (4): a cada 6 golpes que acertam, uma badalada muda prende os inimigos em volta.</summary>
    public const string HitBell = "hit_bell";

    /// <summary>Inquilino (4): todo dano recebido cai 20%.</summary>
    public const string DamageReduction = "damage_reduction";

    /// <summary>Guardião Revertido (4): cada golpe tem chance de se repetir no mesmo alvo.</summary>
    public const string HitEcho = "hit_echo";

    /// <summary>Reflexo (4): ao ser ferido, parte do dano volta para os inimigos em volta.</summary>
    public const string HurtReflect = "hurt_reflect";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>
    {
        SlowImmune, DodgeGel, KillHeal, HurtThorns, DodgeIframes, HurtFlash, LastStand, HitBell, DamageReduction, HitEcho, HurtReflect,
    };

    public const double KillHealFraction = 0.04;
    public const double DodgeIframesBonusSeconds = 0.12;
    public const double ThornsAtkMultiplier = 0.8;
    public const float ThornsRadius = 120f;
    public const float FlashRadius = 170f;
    public const float FlashRootSeconds = 1.5f;
    public const double BurstCooldownSeconds = 6;
    public const float GelRootSeconds = 1.5f;
    public const int BellEveryHits = 6;
    public const float BellRadius = 220f;
    public const float BellRootSeconds = 2f;
    public const double DamageReductionFactor = 0.8;
    public const double EchoChance = 0.2;
    public const double EchoDamageFactor = 0.6;
    public const double ReflectFraction = 0.6;
    public const float ReflectRadius = 150f;
    public const double LastStandInvulnerableSeconds = 2;

    /// <summary>Vida curada por inimigo derrotado (pelo menos 1).</summary>
    public static int KillHealAmount(int maxHp) => Math.Max(1, (int)Math.Round(maxHp * KillHealFraction, MidpointRounding.AwayFromZero));

    /// <summary>Dano que o caçador recebe depois da redução do conjunto.</summary>
    public static double ReduceIncoming(double damage, bool active) => active ? damage * DamageReductionFactor : damage;

    /// <summary>Dano dos espinhos da Macieira, a partir do ataque do caçador.</summary>
    public static double ThornsDamage(double atk) => Math.Max(1, atk * ThornsAtkMultiplier);

    /// <summary>Dano devolvido pelo Reflexo, a partir do dano sofrido.</summary>
    public static double ReflectDamage(int taken) => Math.Max(1, Math.Round(taken * ReflectFraction, MidpointRounding.AwayFromZero));

    /// <summary>A badalada dispara no golpe de número <paramref name="hitCount"/> (contagem a partir de 1).</summary>
    public static bool BellRings(int hitCount) => hitCount > 0 && hitCount % BellEveryHits == 0;

    /// <summary>O golpe se repete se o sorteio (0..1) cair abaixo da chance.</summary>
    public static bool EchoProcs(double roll) => roll < EchoChance;

    /// <summary>O id é de um efeito que o jogo sabe aplicar.</summary>
    public static bool IsKnown(string effect) => Known.Contains(effect);
}

/// <summary>Efeitos de conjunto ativos agora (de acordo com as peças vestidas).</summary>
public sealed class ActiveSetEffects
{
    public static readonly ActiveSetEffects None = new(new HashSet<string>());

    private readonly IReadOnlySet<string> _ids;

    public ActiveSetEffects(IReadOnlySet<string> ids) => _ids = ids;

    public IReadOnlySet<string> Ids => _ids;

    public bool Has(string effect) => _ids.Contains(effect);
}
