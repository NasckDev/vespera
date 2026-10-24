namespace Vespera.Core.Story;

/// <summary>Onde está a Brecha B das Ruas Baixas na contagem do Ato IV (ROTEIRO-V2 §8.1).</summary>
public enum RuasStage
{
    /// <summary>Ainda não abriu (o Ato IV não começou).</summary>
    NotOpen,

    /// <summary>Véspera em que abriu: aviso da Lis.</summary>
    Warning1,

    /// <summary>Segunda véspera sem ninguém ir: aviso da Nilza.</summary>
    Warning2,

    /// <summary>Terceira véspera: o Ravi avisa que vai sozinho.</summary>
    Warning3,

    /// <summary>Rompeu: o Ravi entrou e caiu (fica no Avesso até o caçador recuperar a Lanterna dele). Rota B.</summary>
    Ruptured,

    /// <summary>O caçador fechou a tempo. Rota A.</summary>
    ClosedInTime,
}

/// <summary>
/// Ravi em perigo (ROTEIRO-V2 §8.1, CAMPANHA Ato IV): a Brecha B das Ruas Baixas abre no começo do Ato IV e rompe se o
/// caçador demorar três vésperas. Três avisos, um por véspera (<c>ato4/aviso_ruas</c>: Lis → Nilza → Ravi). Rompida, o
/// Ravi entra e cai; ele não morre: a Lanterna dele fica na sala 3 e, recuperada, ele volta com a marca magenta e o placar
/// some. As duas rotas são válidas. Lógica pura: o jogo chama <see cref="Advance"/> quando a noite cai e
/// <see cref="Close"/> quando a Brecha B fecha.
/// </summary>
public static class RaviRescue
{
    /// <summary>Vésperas até romper, contadas a partir da véspera em que abriu.</summary>
    public const int VespersToRupture = 3;

    /// <summary>Id da Brecha B das Ruas Baixas em <c>RiftSession.BrechaFiles</c> (enquanto não existir, ela não rompe).</summary>
    public const string BrechaId = "ruas_baixas_b";

    public const string Opened = "ruas_brecha_aberta";
    public const string Warning2Flag = "ruas_aviso_2";
    public const string Warning3Flag = "ruas_aviso_3";
    public const string Ruptured = "ruas_rompeu";
    public const string ClosedInTime = "ruas_fechada_a_tempo";
    public const string LanternRecovered = "ravi_lanterna_recuperada";
    public const string RouteA = "ravi_rota_a";
    public const string RouteB = "ravi_rota_b";

    /// <summary>
    /// Estágio a partir da noite em que abriu (<paramref name="openedOn"/>, -1 = não abriu) e da noite atual. Sem
    /// <paramref name="canRupture"/> (a Brecha B ainda não é jogável) a contagem para no terceiro aviso: o jogo nunca
    /// tira o Ravi do caçador sem dar a ele como ir buscá-lo.
    /// </summary>
    public static RuasStage Stage(int openedOn, int night, bool closed, bool canRupture)
    {
        if (closed)
        {
            return RuasStage.ClosedInTime;
        }

        if (openedOn < 0)
        {
            return RuasStage.NotOpen;
        }

        var elapsed = Math.Max(0, night - openedOn);
        return elapsed switch
        {
            0 => RuasStage.Warning1,
            1 => RuasStage.Warning2,
            2 => RuasStage.Warning3,
            _ => canRupture ? RuasStage.Ruptured : RuasStage.Warning3,
        };
    }

    /// <summary>Flags de história que valem neste estágio (cumulativas: o terceiro aviso também tem o segundo).</summary>
    public static IReadOnlyList<string> FlagsFor(RuasStage stage) => stage switch
    {
        RuasStage.Warning1 => [Opened],
        RuasStage.Warning2 => [Opened, Warning2Flag],
        RuasStage.Warning3 => [Opened, Warning2Flag, Warning3Flag],
        RuasStage.Ruptured => [Opened, Warning2Flag, Warning3Flag, Ruptured],
        RuasStage.ClosedInTime => [ClosedInTime],
        _ => [],
    };

    /// <summary>Abre a Brecha B na noite atual (uma vez só) e liga o primeiro aviso.</summary>
    public static void Open(StoryEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var night = engine.Progress.Night;
        if (night.RuasOpenedOn >= 0)
        {
            return;
        }

        night.RuasOpenedOn = night.Night;
        engine.SetFlag(Opened);
    }

    /// <summary>A noite caiu: avança a contagem e liga as flags do estágio novo.</summary>
    public static RuasStage Advance(StoryEngine engine, bool canRupture)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var closed = engine.HasFlag(ClosedInTime);
        if (engine.HasFlag(Ruptured))
        {
            return closed ? RuasStage.ClosedInTime : RuasStage.Ruptured;
        }

        var night = engine.Progress.Night;
        var stage = Stage(night.RuasOpenedOn, night.Night, closed, canRupture);
        foreach (var flag in FlagsFor(stage))
        {
            engine.SetFlag(flag);
        }

        return stage;
    }

    /// <summary>A Brecha B fechou: antes de romper é a rota A; depois, só a Lanterna do Ravi decide (rota B).</summary>
    public static void Close(StoryEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.HasFlag(Ruptured))
        {
            engine.SetFlag(ClosedInTime);
        }
    }

    /// <summary>Rota já decidida: 'A' (chegou a tempo), 'B' (rompeu e a Lanterna voltou) ou null (ainda em aberto).</summary>
    public static char? Route(IStoryContext story)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (story.HasFlag(ClosedInTime))
        {
            return 'A';
        }

        return story.HasFlag(Ruptured) && story.HasFlag(LanternRecovered) ? 'B' : null;
    }
}
