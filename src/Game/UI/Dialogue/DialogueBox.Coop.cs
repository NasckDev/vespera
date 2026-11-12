using Godot;

namespace Vespera.Game.UI;

/// <summary>Caixa de diálogo no co-op local: quem puxou a conversa e o "interagir" de outro jogador avançando a fala.</summary>
public partial class DialogueBox
{
    /// <summary>
    /// Co-op: quem abriu a conversa (etiqueta "J2 · conversando" na cor do jogador, no canto do painel); null esconde.
    /// Qualquer jogador pode puxar conversa; a história só registra pelo dono do save.
    /// </summary>
    public void SetInitiator(string? text, Color color)
    {
        _who.Visible = text is not null;
        if (text is null)
        {
            return;
        }

        _whoLabel.Text = text;
        _whoLabel.AddThemeColorOverride("font_color", color);
        _who.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.97f), color, 2, 18, 2));
        _who.ResetSize();
        var size = _who.GetCombinedMinimumSize();
        _who.Size = size;
        _who.Position = new Vector2(_panel.Position.X + _panel.Size.X - size.X - 40f, _panel.Position.Y - (size.Y * 0.55f));
    }

    /// <summary>Texto da etiqueta de quem conversa (vazio sem co-op), para os bots.</summary>
    public string InitiatorText => _who.Visible ? _whoLabel.Text : string.Empty;

    /// <summary>
    /// "Interagir" vindo de outro jogador do co-op (lido pela fonte de input dele, não pela InputMap): mesmo efeito do
    /// aperto do J1. Dois apertos no mesmo quadro contam como um.
    /// </summary>
    public void Press()
    {
        if (_runner is null || !IsVisibleInTree() || Engine.GetProcessFrames() == _pressFrame)
        {
            return;
        }

        _pressFrame = Engine.GetProcessFrames();
        if (Typing)
        {
            Complete();
            return;
        }

        if (HasChoices)
        {
            if (GetViewport().GuiGetFocusOwner() is Button focused && _choiceButtons.Contains(focused))
            {
                focused.EmitSignal(BaseButton.SignalName.Pressed);
            }

            return;
        }

        Advance(0);
    }

    private void BuildWho()
    {
        _who = new PanelContainer { Name = "Who", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _whoLabel = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        _who.AddChild(_whoLabel);
        AddChild(_who);
    }
}
