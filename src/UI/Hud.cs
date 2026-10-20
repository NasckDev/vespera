using Godot;
using Vespera.Autoload;
using Vespera.Core.Ranks;

namespace Vespera.UI;

/// <summary>
/// HUD da caçada: Selo, Nível, HP, barra de XP do nível, Lumes, Estilhaços e local. Só escuta o EventBus
/// e relê o <see cref="GameManager"/> quando um save é carregado.
/// </summary>
public partial class Hud : CanvasLayer
{
    [Export]
    public RankSeal RankSeal { get; set; } = null!;

    [Export]
    public Label LevelLabel { get; set; } = null!;

    [Export]
    public ProgressBar HpBar { get; set; } = null!;

    [Export]
    public Label HpLabel { get; set; } = null!;

    /// <summary>Progresso dentro do Nível atual (barra fina da v2, spec 02 §4.1).</summary>
    [Export]
    public ProgressBar XpBar { get; set; } = null!;

    [Export]
    public Label LumesLabel { get; set; } = null!;

    [Export]
    public Label FragmentsLabel { get; set; } = null!;

    [Export]
    public Label LocationLabel { get; set; } = null!;

    [Export]
    public Label PromptLabel { get; set; } = null!;

    public override void _Ready()
    {
        Refresh();

        var bus = EventBus.Instance;
        bus.GameLoaded += Refresh;
        bus.PlayerDamaged += OnPlayerDamaged;
        bus.PlayerHealed += UpdateHp;
        bus.XpGained += OnXpGained;
        bus.RankChanged += OnRankChanged;
        bus.LevelChanged += OnLevelChanged;
        bus.LumesChanged += UpdateLumes;
        bus.FragmentsChanged += UpdateFragments;
        bus.LocationChanged += OnLocationChanged;
        bus.InteractionPromptChanged += OnInteractionPromptChanged;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.GameLoaded -= Refresh;
        bus.PlayerDamaged -= OnPlayerDamaged;
        bus.PlayerHealed -= UpdateHp;
        bus.XpGained -= OnXpGained;
        bus.RankChanged -= OnRankChanged;
        bus.LevelChanged -= OnLevelChanged;
        bus.LumesChanged -= UpdateLumes;
        bus.FragmentsChanged -= UpdateFragments;
        bus.LocationChanged -= OnLocationChanged;
        bus.InteractionPromptChanged -= OnInteractionPromptChanged;
    }

    /// <summary>Relê tudo do estado atual (início e depois de carregar um save).</summary>
    private void Refresh()
    {
        var game = GameManager.Instance;
        RankSeal.Rank = game.Rank;
        UpdateLevel();
        UpdateHp(game.CurrentHp, game.MaxHp);
        UpdateLumes(game.Lumes);
        UpdateFragments(game.Fragments);
    }

    private void OnPlayerDamaged(int amount, int currentHp, int maxHp) => UpdateHp(currentHp, maxHp);

    private void OnXpGained(int amount, int totalXp, float progressInRank) => UpdateLevel();

    private void OnRankChanged(Rank previousRank, Rank newRank)
    {
        RankSeal.Rank = newRank;
        UpdateLevel();
    }

    private void OnLevelChanged(int previousLevel, int newLevel) => UpdateLevel();

    private void OnLocationChanged(string locationName) => LocationLabel.Text = locationName;

    private void OnInteractionPromptChanged(string prompt)
    {
        PromptLabel.Visible = prompt.Length > 0;
        PromptLabel.Text = $"[E] {prompt}";
    }

    private void UpdateLevel()
    {
        var state = GameManager.Instance.State;
        var level = state.Level;
        LevelLabel.Text = $"Nv. {level.Level} · Selo {state.RankDef.Id}";
        XpBar.Value = level.Progress;
    }

    private void UpdateHp(int currentHp, int maxHp)
    {
        HpBar.MaxValue = maxHp;
        HpBar.Value = currentHp;
        HpLabel.Text = $"{currentHp}/{maxHp}";
    }

    private void UpdateLumes(int total) => LumesLabel.Text = $"Lumes: {total}";

    private void UpdateFragments(int total) => FragmentsLabel.Text = $"Estilhaços: {total}";
}
