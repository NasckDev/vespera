using Godot;

namespace Vespera.Game.UI;

/// <summary>Logo "VÉSPERA" em Pixelify Sans, com um brilho magenta pulsante sobre o acento do É (a rachadura da Brecha).</summary>
public partial class LogoBanner : Control
{
    private const string Word = "VÉSPERA";
    private const int AccentLetterIndex = 1;
    private const float GlowRadius = 120f;
    private const float AccentLift = 0.30f;

    private Label _label = null!;
    private double _time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _label = UiKit.MakeLabel(Word, UiTheme.LogoVariation, HorizontalAlignment.Center);
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        _time += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_label is null)
        {
            return;
        }

        var font = _label.GetThemeFont("font");
        var size = _label.GetThemeFontSize("font_size");
        var total = font.GetStringSize(Word, HorizontalAlignment.Left, -1, size).X;
        var before = font.GetStringSize(Word[..AccentLetterIndex], HorizontalAlignment.Left, -1, size).X;
        var accentWidth = font.GetStringSize(Word[AccentLetterIndex].ToString(), HorizontalAlignment.Left, -1, size).X;
        var left = (Size.X - total) / 2f;
        var center = new Vector2(left + before + (accentWidth / 2f), _label.Position.Y + (Size.Y * AccentLift));

        var pulse = 0.65f + (0.35f * Mathf.Sin((float)_time * 2.2f));
        DrawTextureRect(ArtAssets.SoftCircle, new Rect2(center - new Vector2(GlowRadius, GlowRadius), new Vector2(GlowRadius * 2f, GlowRadius * 2f)), false, new Color(Palette.Magenta, 0.8f * pulse));
        DrawTextureRect(ArtAssets.SoftCircle, new Rect2(center - new Vector2(GlowRadius * 0.4f, GlowRadius * 0.4f), new Vector2(GlowRadius * 0.8f, GlowRadius * 0.8f)), false, new Color(1f, 0.8f, 0.9f, 0.6f * pulse));
    }
}
