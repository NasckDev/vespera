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

    /// <summary>
    /// O Benício aprovou o exame do rank E (caçadas completas, arma forjada, contrato cumprido): a Lodo-Mãe passa a
    /// esperar no fim da caçada. QUEM EMITE: a fala do exame (data/dialogue/benicio_quadro.json).
    /// </summary>
    public const string GuardianGate = "exame_e";

    /// <summary>Voltou da Brecha pelo menos uma vez (viva ou caída). QUEM EMITE: o fim da caçada (HuntService).</summary>
    public const string HuntBack = "hunt_back";

    /// <summary>Primeira e segunda caçadas completas (saiu pela fenda da última sala). QUEM EMITE: o fim da caçada.</summary>
    public const string HuntOne = "hunt_1";

    public const string HuntTwo = "hunt_2";

    /// <summary>Caiu dentro da Brecha pelo menos uma vez.</summary>
    public const string FellOnce = "fell_once";

    /// <summary>Melhorou a arma na forja pelo menos uma vez.</summary>
    public const string Forged = "forged";

    /// <summary>Comprou uma poção da Tia Nilza.</summary>
    public const string PotionBought = "potion_bought";

    /// <summary>Cumpriu um contrato do mural.</summary>
    public const string ContractDone = "contract_done";

    /// <summary>O XP já alcança o próximo rank (falta só a conquista: Guardião/exame).</summary>
    public const string XpReady = "xp_ready";

    public const string MetNilza = "met_nilza";

    /// <summary>Evento: a Tia Nilza dá uma poção de presente.</summary>
    public const string EventGivePotion = "GivePotion";

    /// <summary>Evento: comprar uma poção na Tia Nilza.</summary>
    public const string EventBuyPotion = "BuyPotion";

    /// <summary>Evento: abrir a tela de contratos do mural.</summary>
    public const string EventOpenContracts = "OpenContracts";

    /// <summary>Prefixo das flags de dicas já mostradas ("hint_move", "hint_attack"...). Ficam no save junto das flags da história.</summary>
    public const string HintPrefix = "hint_";

    /// <summary>Evento: a Lâmina inicial passou para as mãos do jogador (a economia pode reagir).</summary>
    public const string EventStarterBladeGiven = "StarterBladeGiven";

    /// <summary>Evento: depois do exame a Odete deixa escolher a arma definitiva (de graça, uma vez).</summary>
    public const string EventChooseWeapon = "ChooseWeapon";

    /// <summary>Evento: comprar o mapa da Lis (10 Lumes).</summary>
    public const string EventBuyMap = "BuyMap";

    /// <summary>Prefixo do evento do Benício que queima o selo do ato (<c>BurnSeal:B</c>, <c>BurnSeal:A</c>, <c>BurnSeal:S</c>...).</summary>
    public const string EventBurnSealPrefix = "BurnSeal:";

    /// <summary>Flag gravada quando o selo <paramref name="rank"/> é queimado (<c>selo_queimado_b</c>).</summary>
    public static string SealBurnedFlag(string rank) => $"selo_queimado_{rank.ToLowerInvariant()}";

    /// <summary>A escolha da arma na Odete já foi feita: trocar de novo custa Fragmentos.</summary>
    public const string WeaponChosen = "arma_escolhida";

    /// <summary>A cena da chegada abriu a Brecha na praça (a lanterna do caçador acendeu sozinha).</summary>
    public const string MetBenicio = "met_benicio";
}
