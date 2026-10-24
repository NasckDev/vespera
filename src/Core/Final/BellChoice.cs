namespace Vespera.Core.Final;

/// <summary>Quem pode tocar o sino de baixo no Final (ROTEIRO-V2 §13 FINAL, fala 5).</summary>
public enum BellRinger
{
    Benicio,
    Lis,
    Jogador,
}

/// <summary>O que muda na história depois que o sino toca (o sol nasce em todos; o resto depende de quem tocou).</summary>
/// <param name="Flag">Flag gravada pela escolha (<c>final_benicio</c>, <c>final_lis</c>, <c>final_jogador</c>).</param>
/// <param name="FirstBreachClosed">A primeira Brecha fecha para sempre.</param>
/// <param name="BenicioReturns">O Benício volta para o lado de cima (não volta se foi ele quem tocou).</param>
/// <param name="TownsLinked">As duas Lumeiras ficam ligadas: as Brechas continuam, menores.</param>
/// <param name="EchoesStay">Quem é eco (Celeste, Vigia) fica deste lado.</param>
/// <param name="Epilogue">Fala do epílogo (<c>data/dialogue/final/…</c>).</param>
public sealed record BellOutcome(string Flag, bool FirstBreachClosed, bool BenicioReturns, bool TownsLinked, bool EchoesStay, string Epilogue);

/// <summary>
/// Escolha de quem toca o sino. Benício só com <see cref="BenicioFlag"/> (o jogador o trouxe à decisão no Ato VII);
/// Lis só com <see cref="LisHiddenFlag"/> e <see cref="LisInsideFlag"/> (escondida da Ordem e dentro da Brecha final);
/// o jogador sempre pode. As mesmas condições estão nas escolhas de <c>data/dialogue/final/sino.json</c>.
/// </summary>
public static class BellChoice
{
    public const string BenicioFlag = "benicio_na_decisao";
    public const string LisHiddenFlag = "lis_escondida";
    public const string LisInsideFlag = "lis_entrou";
    public const string EventPrefix = "RingBell:";

    /// <summary>Quem pode tocar, na ordem em que a escolha aparece.</summary>
    public static IReadOnlyList<BellRinger> Available(Func<string, bool> hasFlag)
    {
        var ringers = new List<BellRinger>(3);
        if (hasFlag(BenicioFlag))
        {
            ringers.Add(BellRinger.Benicio);
        }

        if (hasFlag(LisHiddenFlag) && hasFlag(LisInsideFlag))
        {
            ringers.Add(BellRinger.Lis);
        }

        ringers.Add(BellRinger.Jogador);
        return ringers;
    }

    public static BellOutcome Outcome(BellRinger ringer) => ringer switch
    {
        BellRinger.Benicio => new("final_benicio", FirstBreachClosed: true, BenicioReturns: false, TownsLinked: false, EchoesStay: true, "final/epilogo_sem_benicio"),
        BellRinger.Lis => new("final_lis", FirstBreachClosed: false, BenicioReturns: true, TownsLinked: true, EchoesStay: true, "final/epilogo"),
        _ => new("final_jogador", FirstBreachClosed: true, BenicioReturns: true, TownsLinked: false, EchoesStay: true, "final/epilogo"),
    };

    /// <summary>Lê o evento <c>RingBell:&lt;quem&gt;</c> da fala do sino; outro evento = nulo.</summary>
    public static BellRinger? FromEvent(string name)
    {
        if (!name.StartsWith(EventPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return name[EventPrefix.Length..] switch
        {
            "benicio" => BellRinger.Benicio,
            "lis" => BellRinger.Lis,
            "jogador" => BellRinger.Jogador,
            _ => null,
        };
    }

    /// <summary>Epílogo depois do sino (sem escolha gravada = o do jogador).</summary>
    public static string Epilogue(Func<string, bool> hasFlag) =>
        hasFlag(Outcome(BellRinger.Benicio).Flag) ? Outcome(BellRinger.Benicio).Epilogue : Outcome(BellRinger.Jogador).Epilogue;
}
