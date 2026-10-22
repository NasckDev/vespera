using Godot;
using Vespera.Core.Ranks;

namespace Vespera.Autoload;

/// <summary>
/// Barramento global de sinais. Sistemas emitem e escutam aqui em vez de procurar nós pela árvore.
/// Acesse por <see cref="Instance"/>. Quem assina com += deve remover com -= em _ExitTree.
/// </summary>
public partial class EventBus : Node
{
    // --- Jogador e progressão ---
    [Signal]
    public delegate void PlayerDamagedEventHandler(int amount, int currentHp, int maxHp);

    [Signal]
    public delegate void PlayerHealedEventHandler(int currentHp, int maxHp);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void StaminaChangedEventHandler(float current, float max);

    [Signal]
    public delegate void XpGainedEventHandler(int amount, int totalXp, float progressInRank);

    [Signal]
    public delegate void RankChangedEventHandler(Rank previousRank, Rank newRank);

    [Signal]
    public delegate void FragmentsChangedEventHandler(int total);

    [Signal]
    public delegate void WeaponUpgradedEventHandler(int newLevel);

    // --- Estado do jogador (GameState) ---
    /// <summary>Um save foi carregado ou um jogo novo começou: telas devem reler tudo do GameManager.</summary>
    [Signal]
    public delegate void GameLoadedEventHandler();

    /// <summary>Nível (Nv.) mudou; separado do Selo.</summary>
    [Signal]
    public delegate void LevelChangedEventHandler(int previousLevel, int newLevel);

    [Signal]
    public delegate void LumesChangedEventHandler(int total);

    /// <summary>Quantidade de um material mudou (o Estilhaço também dispara <c>FragmentsChanged</c>).</summary>
    [Signal]
    public delegate void MaterialChangedEventHandler(string materialId, int count);

    /// <summary>Mochila, materiais, poções ou lanternas carregadas mudaram.</summary>
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void EquipmentChangedEventHandler(string slotId);

    [Signal]
    public delegate void PotionsChangedEventHandler(int count);

    /// <summary>Atributos derivados (ataque, defesa, luz…) foram recalculados.</summary>
    [Signal]
    public delegate void PlayerStatsChangedEventHandler();

    /// <summary>A Lanterna ficou na Brecha (true) ou voltou (false).</summary>
    [Signal]
    public delegate void LanternStateChangedEventHandler(bool lost);

    [Signal]
    public delegate void DayChangedEventHandler(int day);

    [Signal]
    public delegate void EcoChangedEventHandler(string ecoId);

    [Signal]
    public delegate void QuestCompletedEventHandler(string questId);

    // --- Combate e Brecha ---
    [Signal]
    public delegate void EnemyDiedEventHandler(string enemyId, Vector2 globalPosition);

    [Signal]
    public delegate void PortalEnteredEventHandler(string districtId);

    [Signal]
    public delegate void WaveStartedEventHandler(int wave, int totalWaves);

    [Signal]
    public delegate void BossSpawnedEventHandler(string bossName, int maxHp);

    [Signal]
    public delegate void BossHealthChangedEventHandler(int currentHp, int maxHp);

    [Signal]
    public delegate void DungeonClearedEventHandler(string districtId);

    // --- Mundo ---
    [Signal]
    public delegate void LocationChangedEventHandler(string locationName);

    /// <summary>Fase do dia como int de <c>Vespera.Core.World.DayPhase</c>.</summary>
    [Signal]
    public delegate void DayPhaseChangedEventHandler(int phase);

    /// <summary>O World trocou de mapa (no meio do fade). Sistemas do mapa anterior devem se limpar.</summary>
    [Signal]
    public delegate void MapChangedEventHandler(string mapId, string mapName);

    /// <summary>
    /// Jogador apertou interagir num ponto do mapa. <paramref name="kind"/> = ação da v2 (<c>talkClerk</c>,
    /// <c>openBoard</c>, <c>talkSmith</c>, <c>oficiosDialog</c>, <c>portalDialog</c>, <c>talkInn</c>, <c>restDialog</c>,
    /// <c>talkGaspar</c>, <c>talkVesna</c>, <c>talkIvo</c>, <c>talkAbel</c>); <paramref name="id"/> = id do interagível no maps.json.
    /// </summary>
    [Signal]
    public delegate void InteractionRequestedEventHandler(string kind, string id);

    /// <summary>Jogador entrou andando na área de fuga (<c>back</c>) de uma dungeon: abrir o diálogo "Fugir para Lumeira".</summary>
    [Signal]
    public delegate void DungeonEscapeRequestedEventHandler(string mapId);

    /// <summary>Jogador passou pela porta liberada da sala do tema (o World já leva para a Câmara do Núcleo).</summary>
    [Signal]
    public delegate void DungeonDoorEnteredEventHandler(string mapId);

    /// <summary>Pede tremor de câmera. Intensidade na escala da v2 (px de tela a 720p; 8 = ruptura/chefe).</summary>
    [Signal]
    public delegate void CameraShakeRequestedEventHandler(float amount);

    // --- Contratos da Fase B (docs/design/contratos-fase-b.md) ---
    /// <summary>Cidade → todos: um portal de Brecha abriu na véspera (spot do maps.json, tema, rank sorteado).</summary>
    [Signal]
    public delegate void PortalOpenedEventHandler(int portalSpot, string themeId, int rank);

    /// <summary>Cidade → todos: o portal fechou (Brecha limpa, ignorada até romper, ou novo dia).</summary>
    [Signal]
    public delegate void PortalClosedEventHandler(int portalSpot);

    /// <summary>UI → Combate: o jogador confirmou entrar na Brecha deste portal.</summary>
    [Signal]
    public delegate void BrechaStartRequestedEventHandler(int portalSpot);

    /// <summary>Combate → todos: a Brecha começou (mapa de dungeon carregado, ondas vão começar).</summary>
    [Signal]
    public delegate void BrechaStartedEventHandler(string themeId, int rank);

    /// <summary>Combate → todos: a Brecha terminou. <paramref name="cleared"/> = chefe derrotado; false = fuga ou morte.</summary>
    [Signal]
    public delegate void BrechaEndedEventHandler(string themeId, bool cleared);

    /// <summary>Cidade → Combate: a Brecha ignorada rompeu; monstros invadem a Rua da Forja.</summary>
    [Signal]
    public delegate void InvasionRequestedEventHandler(string themeId, int rank);

    /// <summary>Combate → todos: invasão começou/acabou (preços sobem 35% durante, PriceCalculator).</summary>
    [Signal]
    public delegate void InvasionStateChangedEventHandler(bool active);

    /// <summary>UI → Combate: o jogador escolheu voltar depois da tela de morte (aplica LanternRules).</summary>
    [Signal]
    public delegate void RespawnRequestedEventHandler();

    /// <summary>Combate → HUD: arma ativa trocou (sword, bow, staff, hammer, dag).</summary>
    [Signal]
    public delegate void WeaponChangedEventHandler(string weaponId);

    /// <summary>Combate → HUD: Essência (barra do Golpe Supremo).</summary>
    [Signal]
    public delegate void EssenceChangedEventHandler(float current, float max);

    /// <summary>Combate → HUD: recarga de uma habilidade (<c>dash</c>, <c>potion</c>, <c>eco0</c>, <c>eco1</c>, <c>ultimate</c>).</summary>
    [Signal]
    public delegate void AbilityCooldownChangedEventHandler(string abilityId, float remaining, float total);

    // --- Combate (frente B1; docs/design/api-combate.md) ---
    /// <summary>Combate → HUD: item coletado (<c>kind</c> = lume, mat, gear, potion, eco, lantern, mylantern; id do item ou "").</summary>
    [Signal]
    public delegate void LootCollectedEventHandler(string kind, string itemId, int amount);

    /// <summary>Combate → HUD: parte de monstro quebrou (barra do chefe mostra "— quebrada").</summary>
    [Signal]
    public delegate void MonsterPartBrokenEventHandler(string monsterId, string partId, string partName, bool boss);

    /// <summary>Combate e Cidade → HUD: nota curta lateral da v2 (<c>note</c>: "Sem poções…", "+1 Presa Rubra", "A Casa X fechou a Brecha"). <paramref name="icon"/> = chave do atlas.</summary>
    [Signal]
    public delegate void NoteRequestedEventHandler(string message, string icon);

    /// <summary>Combate → todos: o chefe da Brecha caiu (antes do portal de saída).</summary>
    [Signal]
    public delegate void BossDefeatedEventHandler(string bossId);

    /// <summary>Qualquer sistema → UI: pergunta com opções (ex.: "Fugir para Lumeira?"). A UI responde com <see cref="ChoiceMade"/>.</summary>
    [Signal]
    public delegate void ChoiceRequestedEventHandler(string requestId, string speaker, string text, string[] options);

    /// <summary>UI → quem perguntou: opção escolhida (índice em <c>options</c>, ou -1 se cancelou).</summary>
    [Signal]
    public delegate void ChoiceMadeEventHandler(string requestId, int optionIndex);

    /// <summary>UI → todos: jogo pausado/despausado (o relógio do dia para; combate congela).</summary>
    [Signal]
    public delegate void PauseChangedEventHandler(bool paused);

    // --- Motor do mundo (frente B4a; docs/design/api-mundo.md) ---
    /// <summary>Mundo → todos: um quebrável do layout quebrou (<paramref name="kind"/> = barrel, crate, crate2, sack, urn).</summary>
    [Signal]
    public delegate void PropBrokenEventHandler(string mapId, string kind, Vector2 position);

    /// <summary>Mundo → todos: um baú do layout abriu (o saque já foi dado).</summary>
    [Signal]
    public delegate void ChestOpenedEventHandler(string mapId, int tier, Vector2 position);

    /// <summary>
    /// Mundo → todos: o jogador usou uma decoração do layout (<paramref name="kind"/> = lamp, flower, boat, fishing, sign,
    /// bench, fountain, stall, inspect; <paramref name="index"/> = posição na lista <c>decor</c>). Missões e conquistas escutam aqui.
    /// </summary>
    [Signal]
    public delegate void WorldObjectUsedEventHandler(string kind, string mapId, int index);
    // --- Cidade viva (frente B2, docs/design/api-cidade.md) ---
    /// <summary>
    /// Cidade → Interface (log de chat, opcional): mensagem do chat simulado de Lumeira (spec 01 §6.3). A cidade já mostra
    /// o balão sobre o caçador quando ele está no mapa. <paramref name="rank"/> = id do Selo ("C") ou "—".
    /// </summary>
    [Signal]
    public delegate void ChatMessageEventHandler(string who, string rank, string text);

    // NoteRequested(text, icon) também é emitido pela Cidade ("A Casa da Maré fechou a Brecha do Mercado de Peixes"):
    // as duas frentes criaram o mesmo sinal com a mesma assinatura; fica um só, no bloco do Combate.

    /// <summary>Cidade → todos: uma Brecha mudou de estado (int de <c>Vespera.Core.World.BrechaStatus</c>). Para o Mural e a lista do HUD.</summary>
    [Signal]
    public delegate void BrechaStatusChangedEventHandler(int brechaId, int status);

    // --- UI ---
    /// <summary>Texto do prompt de interação ("" esconde).</summary>
    [Signal]
    public delegate void InteractionPromptChangedEventHandler(string prompt);

    [Signal]
    public delegate void DialogueRequestedEventHandler(string speaker, string[] lines);

    [Signal]
    public delegate void DialogueFinishedEventHandler();

    /// <summary>Pede para abrir uma tela de UI pelo id (ex.: "forge").</summary>
    [Signal]
    public delegate void ScreenRequestedEventHandler(string screenId);

    /// <summary>Mensagem curta e passageira no centro da tela (rank up, item, aviso). Com quebra de linha: "título" + linha nova + "subtítulo".</summary>
    [Signal]
    public delegate void ToastRequestedEventHandler(string message);

    // --- Interface (frente B3; docs/design/api-interface.md) ---
    /// <summary>UI → Combate: o jogador pagou a Odete pelo resgate da Lanterna; a volta acontece em <paramref name="delaySeconds"/> (LanternRules.OdeteRescueDelay).</summary>
    [Signal]
    public delegate void LanternRescueRequestedEventHandler(double delaySeconds);

    // --- Polimento e acessibilidade (frente C1; DECISIONS C1-D12) ---
    /// <summary>
    /// Qualquer sistema → HUD: legenda curta de um som importante ("[Rugido do chefe]", "[Perigo →]"). Só aparece com
    /// a opção "Legendas de sons" ligada; quem emite pode checar <c>GameSettings.Captions</c> antes para não gastar.
    /// </summary>
    [Signal]
    public delegate void CaptionRequestedEventHandler(string text);

    // --- Arte aprovada (frente C2) ---
    /// <summary>
    /// Combate → HUD: um estado do jogador com ícone ligou/desligou (<c>shield</c> = i-frames de respawn, <c>slow</c> = gelo,
    /// <c>speed</c> = equipamento acima de 100%). Só emitido na troca (DECISIONS C2-D7).
    /// </summary>
    [Signal]
    public delegate void PlayerStatusChangedEventHandler(string status, bool active);

    public static EventBus Instance { get; private set; } = null!;

    public override void _EnterTree() => Instance = this;
}
