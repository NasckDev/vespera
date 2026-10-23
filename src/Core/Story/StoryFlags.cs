namespace Vespera.Core.Story;

/// <summary>
/// Nomes das flags e dos eventos da história. Quem avisa a história de algo que aconteceu no mundo chama
/// <c>StoryEngine.SetFlag(...)</c> com uma destas constantes; o texto vive em data/dialogue/*.json.
/// </summary>
public static class StoryFlags
{
    /// <summary>
    /// A Lodo-Mãe (Guardiã do Ato I) foi derrotado. QUEM ESCUTA: Odete muda a fala e o objetivo vai para o Ato II.
    /// QUEM EMITE: o combate, ao morrer o Guardião (<c>StoryService.SetFlag(StoryFlags.GuardianDefeatedLodoMae)</c>).
    /// </summary>
    public const string GuardianDefeatedLodoMae = "GuardianDefeated:lodo_mae";

    public const string ArrivalDone = "arrival_done";
    public const string Registered = "registered";
    public const string MetRavi = "met_ravi";
    public const string MetOdete = "met_odete";
    public const string StarterBlade = "starter_blade";
    public const string MetLis = "met_lis";

    /// <summary>Evento (não flag): Odete abriu a forja. A tela de Forja escuta <c>StoryEngine.EventRaised</c>.</summary>
    public const string EventOpenForge = "OpenForge";

    /// <summary>Evento: a Lâmina inicial passou para as mãos do jogador (a economia pode reagir).</summary>
    public const string EventStarterBladeGiven = "StarterBladeGiven";
}
