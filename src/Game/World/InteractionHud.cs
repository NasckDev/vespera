using Godot;
using Vespera.Game.Boot;
using Vespera.Game.UI;

namespace Vespera.Game.World;

/// <summary>
/// Camada de interface da fase: o aviso "[E] Ler o mural" flutuando sobre o interativo em foco e o painel de
/// leitura (mural, placas, falas curtas) na parte de baixo da tela. Fica fora do SubViewport para o texto sair nítido.
/// </summary>
public partial class InteractionHud : Control
{
    private const float PromptGap = 18f;
    private const float PanelWidth = 1180f;
    private const float PanelBottom = 64f;
    private const float FadeSpeed = 8f;

    private PanelContainer _prompt = null!;
    private Label _key = null!;
    private Label _promptText = null!;
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private Label _body = null!;
    private Label _hint = null!;
    private float _promptAlpha;

    public bool PanelOpen => _panel.Visible;

    public string PromptText => _prompt.Visible ? _promptText.Text : string.Empty;

    public string PanelText => _panel.Visible ? _body.Text : string.Empty;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildPrompt();
        BuildPanel();
    }

    /// <summary>Mostra o aviso centrado acima de <paramref name="screenPoint"/>; null esconde.</summary>
    public void ShowPrompt(string? text, Vector2 screenPoint, string keyName, float delta)
    {
        var visible = text is not null && !PanelOpen;
        _promptAlpha = Mathf.MoveToward(_promptAlpha, visible ? 1f : 0f, FadeSpeed * delta);
        _prompt.Visible = _promptAlpha > 0.01f;
        _prompt.Modulate = new Color(1, 1, 1, _promptAlpha);
        if (!visible)
        {
            return;
        }

        _key.Text = keyName;
        _promptText.Text = text!;
        _prompt.ResetSize();
        var size = _prompt.GetCombinedMinimumSize();
        var pos = screenPoint - new Vector2(size.X / 2f, size.Y + PromptGap);
        pos.X = Mathf.Clamp(pos.X, 12f, 1920f - size.X - 12f);
        pos.Y = Mathf.Clamp(pos.Y, 12f, 1080f - size.Y - 12f);
        _prompt.Position = pos.Round();
    }

    public void OpenPanel(string title, string body, string closeHint)
    {
        _title.Text = title;
        _title.Visible = !string.IsNullOrEmpty(title);
        _body.Text = body;
        _hint.Text = closeHint;
        _panel.Visible = true;
        _panel.ResetSize();
        var size = _panel.GetCombinedMinimumSize();
        _panel.Size = new Vector2(PanelWidth, size.Y);
        _panel.Position = new Vector2((1920f - PanelWidth) / 2f, 1080f - PanelBottom - _panel.Size.Y);
    }

    public void ClosePanel() => _panel.Visible = false;

    private void BuildPrompt()
    {
        _prompt = new PanelContainer { Name = "Prompt", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _prompt.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.9f), Palette.Amber, 2, 14, 8));
        var row = UiKit.MakeRow(14);
        _key = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _key.AddThemeColorOverride("font_color", Palette.Indigo);
        var keyBox = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        keyBox.AddThemeStyleboxOverride("panel", Bubble(Palette.Amber, Palette.Cream, 1, 10, 0));
        keyBox.AddChild(_key);
        row.AddChild(keyBox);
        _promptText = new Label();
        _promptText.AddThemeColorOverride("font_color", Palette.Cream);
        row.AddChild(_promptText);
        _prompt.AddChild(row);
        AddChild(_prompt);
    }

    private void BuildPanel()
    {
        _panel = new PanelContainer { Name = "ReadPanel", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _panel.AddThemeStyleboxOverride("panel", UiTheme.PanelFrame());
        var column = UiKit.MakeColumn(10);
        _title = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation);
        _body = UiKit.MakeWrapped(string.Empty);
        _body.CustomMinimumSize = new Vector2(PanelWidth - 96f, 0);
        _hint = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Right);
        column.AddChild(_title);
        column.AddChild(_body);
        column.AddChild(_hint);
        _panel.AddChild(column);
        AddChild(_panel);
    }

    private static StyleBoxFlat Bubble(Color fill, Color border, int borderWidth, int padX, int padY)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(8);
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }

    /// <summary>Nome legível da tecla de interagir para a fonte de input usada por último.</summary>
    public static string InteractKeyName(int deviceId)
    {
        var slot = deviceId == LocalInputSource.KeyboardMouse ? BindingSlot.Primary : BindingSlot.Gamepad;
        return GlyphNames.Describe(InputSetup.Table.Get(InputActions.Interact, slot), GlyphNames.DetectFamily());
    }
}
