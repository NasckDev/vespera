using Godot;
using Vespera.Game.Boot;
using Vespera.Game.UI;

namespace Vespera.Game.Story;

/// <summary>
/// Faixa do objetivo atual no canto superior direito ("Objetivo: ... [L] Diário") e um aviso grande por alguns
/// segundos quando o objetivo muda. Fica fora do SubViewport do mundo, como a InteractionHud.
/// </summary>
public partial class StoryHud : Control
{
    private const float ToastSeconds = 4.5f;
    private const float FadeSpeed = 4f;
    private const float Margin = 28f;
    private const float MaxWidth = 640f;

    private PanelContainer _tracker = null!;
    private Label _trackerTitle = null!;
    private Label _trackerHint = null!;
    private PanelContainer _toast = null!;
    private Label _toastTitle = null!;
    private string _lastObjective = string.Empty;
    private bool _first = true;
    private float _toastTime;

    public string TrackerText => _tracker.Visible ? _trackerTitle.Text : string.Empty;

    public string ToastText => _toast.Visible ? _toastTitle.Text : string.Empty;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildTracker();
        BuildToast();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var engine = StoryService.Engine;
        var objective = engine.CurrentObjective;
        var id = objective?.Id ?? string.Empty;
        if (id != _lastObjective)
        {
            _lastObjective = id;
            RefreshTracker(objective?.Title ?? string.Empty);
            if (!_first && objective is not null)
            {
                _toastTitle.Text = objective.Locked ? $"Em breve: {objective.Title}" : $"Novo objetivo: {objective.Title}";
                _toastTime = ToastSeconds;
                PlaceToast();
            }

            _first = false;
        }

        _toastTime = Mathf.Max(0f, _toastTime - dt);
        var wanted = _toastTime > 0f ? 1f : 0f;
        var alpha = Mathf.MoveToward(_toast.Modulate.A, wanted, FadeSpeed * dt);
        _toast.Modulate = new Color(1, 1, 1, alpha);
        _toast.Visible = alpha > 0.01f;
    }

    private void RefreshTracker(string title)
    {
        _tracker.Visible = title.Length > 0;
        _trackerTitle.Text = title;
        var key = GlyphNames.Describe(InputSetup.Table.Get(InputActions.Journal, BindingSlot.Primary), GlyphNames.DetectFamily());
        _trackerHint.Text = $"{key}  Diário";
        _tracker.ResetSize();
        var size = _tracker.GetCombinedMinimumSize();
        _tracker.Size = size;
        _tracker.Position = new Vector2(1920f - Margin - size.X, Margin);
    }

    private void PlaceToast()
    {
        _toast.ResetSize();
        var size = _toast.GetCombinedMinimumSize();
        _toast.Size = size;
        _toast.Position = new Vector2((1920f - size.X) / 2f, 150f);
    }

    private void BuildTracker()
    {
        _tracker = new PanelContainer { Name = "Tracker", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _tracker.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.82f), new Color(Palette.Amber, 0.7f), 14, 8));
        var column = UiKit.MakeColumn(0);
        column.AddChild(UiKit.MakeLabel("Objetivo", UiTheme.HeadingVariation));
        _trackerTitle = UiKit.MakeWrapped(string.Empty, UiTheme.ReadingVariation);
        _trackerTitle.CustomMinimumSize = new Vector2(MaxWidth, 0);
        column.AddChild(_trackerTitle);
        _trackerHint = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Right);
        column.AddChild(_trackerHint);
        _tracker.AddChild(column);
        AddChild(_tracker);
    }

    private void BuildToast()
    {
        _toast = new PanelContainer { Name = "Toast", MouseFilter = MouseFilterEnum.Ignore, Visible = false, Modulate = new Color(1, 1, 1, 0) };
        _toast.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.94f), Palette.Amber, 28, 12));
        _toastTitle = UiKit.MakeLabel(string.Empty, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        _toast.AddChild(_toastTitle);
        AddChild(_toast);
    }

    private static StyleBoxFlat Bubble(Color fill, Color border, int padX, int padY)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(8);
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }
}
