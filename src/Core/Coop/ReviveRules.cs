namespace Vespera.Core.Coop;

/// <summary>Situação de um caçador no co-op.</summary>
public enum HunterLife
{
    Up,

    /// <summary>Caído dentro da Brecha: espera um aliado reanimar.</summary>
    Down,
}

/// <summary>Resultado da checagem do grupo depois de alguém cair.</summary>
public enum PartyStatus
{
    /// <summary>Pelo menos um de pé: a caçada continua e os caídos podem ser reanimados.</summary>
    Fighting,

    /// <summary>Todos caídos: vale a regra da Lanterna para a caçada inteira.</summary>
    Wiped,
}

/// <summary>
/// Progresso de reanimar UM caçador caído: sobe enquanto um aliado de pé segura interagir dentro do alcance e volta
/// devagar quando ele solta ou se afasta. Completa em <see cref="ReviveTuning.HoldSeconds"/>.
/// </summary>
public sealed class ReviveTracker(ReviveTuning tuning)
{
    public double Seconds { get; private set; }

    public bool Completed { get; private set; }

    /// <summary>0..1 para o anel de progresso.</summary>
    public double Fraction => Math.Clamp(Seconds / tuning.HoldSeconds, 0, 1);

    /// <summary>Avança um passo; devolve verdadeiro no passo em que a reanimação completa.</summary>
    public bool Tick(double dt, bool helperHolding)
    {
        if (Completed || dt <= 0)
        {
            return false;
        }

        Seconds = helperHolding ? Seconds + dt : Math.Max(0, Seconds - (tuning.DecayPerSecond * dt));
        if (Seconds + 1e-9 < tuning.HoldSeconds)
        {
            return false;
        }

        Completed = true;
        return true;
    }

    public void Reset()
    {
        Seconds = 0;
        Completed = false;
    }
}

/// <summary>Regras de queda do grupo (GDD §5.8 + Lanterna, PLANO-V2 §6).</summary>
public static class ReviveRules
{
    /// <summary>Todos caídos = a caçada termina pela regra da Lanterna; senão o grupo segue lutando.</summary>
    public static PartyStatus Evaluate(IReadOnlyCollection<HunterLife> party) =>
        party.Count > 0 && party.All(l => l == HunterLife.Down) ? PartyStatus.Wiped : PartyStatus.Fighting;

    /// <summary>Um aliado pode reanimar se ele está de pé, o outro caído e os pés estão dentro do alcance.</summary>
    public static bool CanRevive(HunterLife helper, HunterLife downed, double distance, double reach) =>
        helper == HunterLife.Up && downed == HunterLife.Down && distance <= reach;
}
