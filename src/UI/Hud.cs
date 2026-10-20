using Godot;
using Vespera.Autoload;
using Vespera.Core.Ranks;

namespace Vespera.UI;

/// <summary>HUD da caçada: selo de rank, HP, XP, Fragmentos e local. Só escuta o EventBus.</summary>
public partial class Hud : CanvasLayer
{
    [Export]
    public RankSeal RankSeal { get; set; } = null!;

    [Export]
    public ProgressBar HpBar { get; set; } = null!;

    [Export]
    public Label HpLabel { get; set; } = null!;

    [Export]
    public ProgressBar XpBar { get; set; } = null!;

    [Export]
    public Label FragmentsLabel { get; set; } = null!;

    [Export]
    public Label LocationLabel { get; set; } = null!;

    [Export]
    public Label PromptLabel { get; set; } = null!;

    public override void _Ready()
    {
        var game = GameManager.Instance;
        RankSeal.Rank = game.Rank;
        UpdateHp(game.CurrentHp, game.MaxHp);
        XpBar.Value = game.ProgressInRank;
        UpdateFragments(game.Fragments);

        var bus = EventBus.Instance;
        bus.PlayerDamaged += OnPlayerDamaged;
        bus.PlayerHealed += UpdateHp;
        bus.XpGained += OnXpGained;
        bus.RankChanged += OnRankChanged;
        bus.FragmentsChanged += UpdateFragments;
        bus.LocationChanged += OnLocationChanged;
        bus.InteractionPromptChanged += OnInteractionPromptChanged;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.PlayerDamaged -= OnPlayerDamaged;
        bus.PlayerHealed -= UpdateHp;
        bus.XpGained -= OnXpGained;
        bus.RankChanged -= OnRankChanged;
        bus.FragmentsChanged -= UpdateFragments;
        bus.LocationChanged -= OnLocationChanged;
        bus.InteractionPromptChanged -= OnInteractionPromptChanged;
    }

    private void OnPlayerDamaged(int amount, int currentHp, int maxHp) => UpdateHp(currentHp, maxHp);

    private void OnXpGained(int amount, int totalXp, float progressInRank) => XpBar.Value = progressInRank;

    private void OnRankChanged(Rank previousRank, Rank newRank) => RankSeal.Rank = newRank;

    private void OnLocationChanged(string locationName) => LocationLabel.Text = locationName;

    private void OnInteractionPromptChanged(string prompt)
    {
        PromptLabel.Visible = prompt.Length > 0;
        PromptLabel.Text = $"[E] {prompt}";
    }

    private void UpdateHp(int currentHp, int maxHp)
    {
        HpBar.MaxValue = maxHp;
        HpBar.Value = currentHp;
        HpLabel.Text = $"{currentHp}/{maxHp}";
    }

    private void UpdateFragments(int total) => FragmentsLabel.Text = $"Fragmentos: {total}";
}
