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

    /// <summary>Fim do Ato I (a cena da lanterna, do selo D e do gancho do Pomar Alto) já tocou.</summary>
    public const string Ato1FimDone = "ato1_fim_done";

    /// <summary>Evento: Odete oferece trocar a arma (a classe). O jogo abre a confirmação e a escolha.</summary>
    public const string EventChangeWeapon = "ChangeWeapon";

    /// <summary>Prefixo das flags de dicas já mostradas ("hint_move", "hint_attack"...). Ficam no save junto das flags da história.</summary>
    public const string HintPrefix = "hint_";

    /// <summary>Evento: a Lâmina inicial passou para as mãos do jogador (a economia pode reagir).</summary>
    public const string EventStarterBladeGiven = "StarterBladeGiven";
}
