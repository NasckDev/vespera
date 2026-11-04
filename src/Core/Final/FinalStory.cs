namespace Vespera.Core.Final;

/// <summary>
/// Regras de história do Final (ROTEIRO-V2 §13 FINAL, CAMPANHA "Final"): quando a primeira Brecha abre na Praça, qual
/// ilustração mostra cada escolha do sino e quando a Parte 1 acabou. Lógica pura; as flags vivem no save
/// (<c>StoryProgress</c>).
/// </summary>
public static class FinalStory
{
    /// <summary>Id da Brecha final (<c>src/Data/brecha_final.json</c>).</summary>
    public const string BrechaId = "primeira_brecha";

    /// <summary>Arena da Brecha final: a praça de baixo (<c>data/levels/praca_baixo.json</c>).</summary>
    public const string ArenaLevel = "praca_baixo";

    public const string HubLevel = "praca";

    /// <summary>
    /// Ato VII cumprido (exame SSS: o jogador recusou a oferta do Avesso e voltou). Quem liga é o Ato VII (mesmo padrão de
    /// <c>act1_done</c>..<c>act6_done</c>); o Final só lê. Até o Ato VII entrar, só o smoke <c>--final</c> liga esta flag.
    /// </summary>
    public const string UnlockFlag = "act7_done";

    /// <summary>O Reflexo caiu (liga em <c>final/reflexo_queda</c>): o sino de baixo pode ser tocado.</summary>
    public const string ReflexoFellFlag = "reflexo_caiu";

    /// <summary>O sino tocou e o selo Alvor queimou (liga em <c>final/sino</c>).</summary>
    public const string AlvorFlag = "alvor";

    /// <summary>A Parte 1 acabou (liga na última fala do epílogo, junto do evento <see cref="EventEndPart1"/>).</summary>
    public const string PartOneDoneFlag = "parte1_fim";

    public const string EventEndPart1 = "EndPart1";

    /// <summary>Entrar numa Brecha pela fala (<c>EnterBreach:&lt;id&gt;</c>, a escolha "Entrar." de <c>final/antes</c>).</summary>
    public const string EventEnterBreachPrefix = "EnterBreach:";

    /// <summary>Última fala do jogo, mostrada no encerramento da Parte 1.</summary>
    public const string Hook = "Abriu uma no mar.";

    /// <summary>A primeira Brecha está aberta na Praça: Ato VII cumprido e o sino ainda não tocou.</summary>
    public static bool BreachOpen(Func<string, bool> hasFlag) => hasFlag(UnlockFlag) && !hasFlag(AlvorFlag);

    /// <summary>O sino de baixo pode ser tocado (o Reflexo caiu e ninguém tocou ainda).</summary>
    public static bool BellReady(Func<string, bool> hasFlag) => hasFlag(ReflexoFellFlag) && !hasFlag(AlvorFlag);

    public static bool PartOneDone(Func<string, bool> hasFlag) => hasFlag(PartOneDoneFlag);

    /// <summary>Ilustração 16:9 de quem tocou o sino (issue #51, H-07 a H-09).</summary>
    public static string SceneFor(BellRinger ringer) => ringer switch
    {
        BellRinger.Benicio => "res://assets/v2/cenarios/final/cena_sino_benicio.png",
        BellRinger.Lis => "res://assets/v2/cenarios/final/cena_sino_lis.png",
        _ => "res://assets/v2/cenarios/final/cena_sino_cacador.png",
    };

    /// <summary>Lê <c>EnterBreach:&lt;id&gt;</c>; outro evento = nulo.</summary>
    public static string? BreachFromEvent(string name) =>
        name.StartsWith(EventEnterBreachPrefix, StringComparison.Ordinal) && name.Length > EventEnterBreachPrefix.Length
            ? name[EventEnterBreachPrefix.Length..]
            : null;
}
