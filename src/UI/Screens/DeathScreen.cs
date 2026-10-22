using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Tela de morte (<c>uiDeath</c>, <c>ui.js:252-260</c>): escurece até 0,82, Lanterna, "Sua Lanterna se apagou" (na
/// Brecha) ou "Você caiu na rua"; depois de 1,2 s, "[Enter] Voltar à Guilda"/"[Enter] Acordar" pulsando. Confirmar
/// emite <c>RespawnRequested</c> (o Combate aplica LanternRules e leva o jogador).
/// </summary>
public partial class DeathScreen : Screen
{
    private const double PromptDelay = 1.2;

    private readonly ColorRect _overlay;
    private readonly Control _content;
    private readonly IconRect _lantern;
    private readonly Label _title;
    private readonly Label _text;
    private readonly HBoxContainer _prompt;
    private readonly Label _promptLabel;
    private readonly Control _focusAnchor;

    public DeathScreen()
    {
        _overlay = new ColorRect { Color = UiPalette.Overlay(0f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_overlay);
        var column = Ui.Column(6);
        column.Alignment = BoxContainer.AlignmentMode.Center;
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        _content = column;
        AddChild(column);
        _lantern = IconRect.Create("i_lantern", 45);
        _lantern.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _title = Ui.Title(string.Empty, UiFonts.Title, null, HorizontalAlignment.Center);
        _text = Ui.Text(string.Empty, UiFonts.Body, new Color("#d8cfe0"), wrap: true, align: HorizontalAlignment.Center);
        _text.CustomMinimumSize = new Vector2(310, 0);
        _text.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _promptLabel = Ui.Text(string.Empty, UiFonts.Body, UiPalette.Ambar);
        _prompt = Ui.Row(4, KeyCap.Create("Enter", "A"), _promptLabel);
        _prompt.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(_lantern);
        column.AddChild(_title);
        column.AddChild(_text);
        column.AddChild(Ui.Gap(0, 8));
        column.AddChild(_prompt);
        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    public bool InBrecha { get; private set; }

    public string Title => _title.Text;

    public bool PromptVisible => Age >= PromptDelay;

    public override bool CloseOnCancel => false;

    public override bool WasdNavigation => false;

    protected override Control? DefaultFocus => _focusAnchor;

    public void Setup(bool inBrecha, string? bairro)
    {
        InBrecha = inBrecha;
        _title.Text = inBrecha ? UiText.DeathBrechaTitle : UiText.DeathCityTitle;
        _text.Text = inBrecha ? UiText.DeathBrechaText.Replace("{bairro}", bairro ?? string.Empty, System.StringComparison.Ordinal) : UiText.DeathCityText;
        _promptLabel.Text = inBrecha ? UiText.DeathBrechaPrompt : UiText.DeathCityPrompt;
    }

    /// <summary>Volta (mesmo efeito de Enter depois de 1,2 s).</summary>
    public void Respawn()
    {
        if (!PromptVisible)
        {
            return;
        }

        UiSound();
        Close();
        EventBus.Instance.EmitSignal(EventBus.SignalName.RespawnRequested);
    }

    public override bool HandleInput(InputEvent @event)
    {
        if (!@event.IsEcho() && (@event.IsActionPressed("ui_accept") || @event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed(InputActions.Attack)))
        {
            Respawn();
        }

        return true; // nada passa para o jogo
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
            Respawn();
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var a = Mathf.Min(1f, (float)Age * 1.5f);
        _overlay.Color = UiPalette.Overlay(a * 0.82f);
        _content.Modulate = new Color(1, 1, 1, a);
        _lantern.Modulate = new Color(1, 1, 1, 0.8f);
        _prompt.Visible = PromptVisible;
        if (_prompt.Visible)
        {
            _prompt.Modulate = new Color(1, 1, 1, 0.6f + (0.4f * Mathf.Sin((float)Age * 4f)));
        }
    }
}
