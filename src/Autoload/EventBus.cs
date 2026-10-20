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

    /// <summary>Mensagem curta e passageira no centro da tela (rank up, item, aviso).</summary>
    [Signal]
    public delegate void ToastRequestedEventHandler(string message);

    public static EventBus Instance { get; private set; } = null!;

    public override void _EnterTree() => Instance = this;
}
