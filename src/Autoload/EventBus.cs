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
