using System;
using Godot;
using Vespera.Autoload;

namespace Vespera.World;

/// <summary>Caixinhas desenhadas no mundo (u): fundo escuro translúcido com borda âmbar, como os balões da v2.</summary>
public static class WorldUiStyle
{
    public static readonly Color Ink = new("#F4E7C6");
    public static readonly Color Amber = new("#F2A541");
    public static readonly Color Back = new(0.07f, 0.06f, 0.12f, 0.86f);

    public static StyleBoxFlat Box(Color border, float radius = 3f, float pad = 3f) => new()
    {
        BgColor = Back,
        BorderColor = border,
        BorderWidthBottom = 1,
        BorderWidthTop = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusBottomLeft = (int)radius,
        CornerRadiusBottomRight = (int)radius,
        CornerRadiusTopLeft = (int)radius,
        CornerRadiusTopRight = (int)radius,
        ContentMarginLeft = pad,
        ContentMarginRight = pad,
        ContentMarginTop = pad * 0.5f,
        ContentMarginBottom = pad * 0.5f,
        AntiAliasing = false,
    };

    public static Label Text(int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.03f, 0.07f, 0.9f));
        return label;
    }
}

/// <summary>
/// Prompt de interação no mundo, em cima do interagível em foco (v2 desenhava em <c>y − 44</c>, <c>render.js:228</c>):
/// ícone da tecla/botão (<c>E</c> no teclado, <c>A</c> no gamepad, conforme o último dispositivo usado) e o rótulo.
/// Só troca texto e tamanho quando o foco muda.
/// </summary>
public partial class InteractionPrompt : Node2D
{
    private PanelContainer _panel = null!;
    private PanelContainer _key = null!;
    private Label _keyLabel = null!;
    private Label _text = null!;
    private Interactable? _shown;
    private string _shownPrompt = string.Empty;
    private bool _gamepad;
    private float _bob;

    public Player.Player? Player { get; set; }

    /// <summary>Texto do prompt visível agora ("" escondido).</summary>
    public string ShownText => Visible ? _shownPrompt : string.Empty;

    /// <summary>Ícone atual da tecla (<c>E</c>, <c>A</c>…).</summary>
    public string KeyGlyph => _keyLabel.Text;

    public override void _Ready()
    {
        ZIndex = 41;
        Visible = false;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", WorldUiStyle.Box(WorldUiStyle.Amber with { A = 0.7f }, 3f, 3f));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        _key = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var keyBox = WorldUiStyle.Box(WorldUiStyle.Ink, 2f, 2f);
        keyBox.BgColor = new Color(0.95f, 0.9f, 0.78f, 0.95f);
        _key.AddThemeStyleboxOverride("panel", keyBox);
        _keyLabel = WorldUiStyle.Text(7, new Color("#1B1F3B"));
        _keyLabel.AddThemeConstantOverride("outline_size", 0);
        _keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _key.AddChild(_keyLabel);
        _text = WorldUiStyle.Text(7, WorldUiStyle.Ink);
        row.AddChild(_key);
        row.AddChild(_text);
        _panel.AddChild(row);
        AddChild(_panel);
        UpdateGlyph();
    }

    public override void _Input(InputEvent @event)
    {
        var gamepad = @event switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion m => MathF.Abs(m.AxisValue) > 0.5f ? true : _gamepad,
            InputEventKey or InputEventMouseButton => false,
            _ => _gamepad,
        };
        if (gamepad != _gamepad)
        {
            _gamepad = gamepad;
            UpdateGlyph();
            _shown = null; // remede no próximo quadro
        }
    }

    public override void _Process(double delta)
    {
        var focused = Player is { } p && IsInstanceValid(p) && !(GameManager.Instance?.IsGameplayBlocked ?? false) ? p.FocusedInteractable : null;
        if (focused is null || !IsInstanceValid(focused) || !focused.Enabled)
        {
            Visible = false;
            _shown = null;
            return;
        }

        if (focused != _shown || focused.Prompt != _shownPrompt)
        {
            _shown = focused;
            _shownPrompt = focused.Prompt;
            _text.Text = _shownPrompt;
            _panel.Size = Vector2.Zero; // encolhe para o conteúdo novo
            _panel.ResetSize();
        }

        _bob += (float)delta;
        var size = _panel.Size;
        // acima do objeto, mas nunca em cima do corpo do jogador (que está perto dele)
        var top = focused.GlobalPosition - new Vector2(0, focused.PromptHeight);
        if (Player is { } pl && MathF.Abs(pl.GlobalPosition.X - top.X) < 28f)
        {
            top.Y = MathF.Min(top.Y, pl.VisualPosition.Y - pl.VisualHeight - 4f);
        }

        Position = top;
        _panel.Position = new Vector2(MathF.Round(-size.X / 2f), MathF.Round(-size.Y + (MathF.Sin(_bob * 3f) > 0.6f ? -1f : 0f)));
        Visible = true;
    }

    private void UpdateGlyph() => _keyLabel.Text = _gamepad ? JoyGlyph() : KeyGlyphText();

    private static string KeyGlyphText()
    {
        foreach (var e in InputMap.ActionGetEvents(InputActions.Interact))
        {
            if (e is InputEventKey k)
            {
                var code = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                return OS.GetKeycodeString(code);
            }
        }

        return "E";
    }

    private static string JoyGlyph()
    {
        foreach (var e in InputMap.ActionGetEvents(InputActions.Interact))
        {
            if (e is InputEventJoypadButton b)
            {
                return b.ButtonIndex switch
                {
                    JoyButton.A => "A",
                    JoyButton.B => "B",
                    JoyButton.X => "X",
                    JoyButton.Y => "Y",
                    _ => b.ButtonIndex.ToString(),
                };
            }
        }

        return "A";
    }
}

/// <summary>Cartão de texto curto no mundo (placa, fonte, banco, "olhar"): some sozinho ou quando o jogador se afasta.</summary>
public partial class LoreCard : Node2D
{
    private const float MaxWidth = 170f;
    private PanelContainer _panel = null!;
    private Label _text = null!;
    private float _life;
    private Vector2 _anchor;
    private float _leaveRadius;

    public Player.Player? Player { get; set; }

    public string Text => Visible ? _text.Text : string.Empty;

    public override void _Ready()
    {
        ZIndex = 39; // abaixo dos textos de saque (40)
        Visible = false;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", WorldUiStyle.Box(WorldUiStyle.Ink with { A = 0.55f }, 3f, 4f));
        _text = WorldUiStyle.Text(7, WorldUiStyle.Ink);
        _panel.AddChild(_text);
        AddChild(_panel);
    }

    /// <summary>Mostra <paramref name="text"/> em cima de <paramref name="at"/> (u) por alguns segundos.</summary>
    public void ShowText(string text, Vector2 at, float height, float leaveRadius)
    {
        // quebra por palavra medindo com a fonte (uma vez por texto); sem autowrap dentro de container
        _text.Text = Wrap(text, _text.GetThemeFont("font"), _text.GetThemeFontSize("font_size"), MaxWidth);
        _panel.Size = Vector2.Zero;
        _panel.ResetSize();
        _anchor = at - new Vector2(0, height);
        if (Player is { } pl && IsInstanceValid(pl))
        {
            _anchor.Y = MathF.Min(_anchor.Y, pl.GlobalPosition.Y - pl.VisualHeight - 18f); // não cobre o jogador nem o prompt
        }

        _leaveRadius = leaveRadius;
        _life = 3.2f + (text.Length * 0.045f);
        Position = _anchor;
        Visible = true;
        CallDeferred(MethodName.Center);
    }

    public void HideCard() => Visible = false;

    private static string Wrap(string text, Font font, int size, float maxWidth)
    {
        var sb = new System.Text.StringBuilder(text.Length + 8);
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' '))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && font.GetStringSize(candidate, HorizontalAlignment.Left, -1, size).X > maxWidth)
            {
                sb.Append(line).Append('\n');
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }

        return sb.Append(line).ToString();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _life -= (float)delta;
        var far = Player is { } p && IsInstanceValid(p) && p.GlobalPosition.DistanceTo(_anchor) > _leaveRadius + 60f;
        if (_life <= 0f || far)
        {
            Visible = false;
        }
    }

    private void Center()
    {
        var size = _panel.Size;
        _panel.Position = new Vector2(MathF.Round(-size.X / 2f), MathF.Round(-size.Y));
    }
}

/// <summary>Barra da pesca em cima do jogador: faixa certa em âmbar e a agulha que vai e volta (D46).</summary>
public partial class FishingBar : Node2D
{
    private const float Width = 56f;
    private const float Height = 5f;

    private ColorRect _back = null!;
    private ColorRect _zone = null!;
    private ColorRect _needle = null!;
    private Label _label = null!;

    public override void _Ready()
    {
        ZIndex = 43;
        Visible = false;
        var frame = new ColorRect { Color = new Color("#F4E7C6"), Position = new Vector2(-Width / 2f - 1f, -1f), Size = new Vector2(Width + 2f, Height + 2f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _back = new ColorRect { Color = new Color("#1B1F3B"), Position = new Vector2(-Width / 2f, 0), Size = new Vector2(Width, Height), MouseFilter = Control.MouseFilterEnum.Ignore };
        _zone = new ColorRect { Color = new Color("#F2A541"), Size = new Vector2(10, Height), MouseFilter = Control.MouseFilterEnum.Ignore };
        _needle = new ColorRect { Color = new Color("#E64A8E"), Size = new Vector2(1.5f, Height + 4f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _label = WorldUiStyle.Text(9, WorldUiStyle.Ink);
        _label.Position = new Vector2(-Width / 2f, -14f);
        _label.Size = new Vector2(Width, 10f);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(frame);
        AddChild(_back);
        _back.AddChild(_zone);
        _back.AddChild(_needle);
        AddChild(_label);
    }

    public string Caption => _label.Text;

    public void Show(Vector2 at, double zoneStart, double zoneWidth)
    {
        Position = at;
        _zone.Position = new Vector2((float)(zoneStart * Width), 0);
        _zone.Size = new Vector2(MathF.Max(2f, (float)(zoneWidth * Width)), Height);
        _zone.Visible = false;
        _needle.Visible = false;
        _label.Text = "Esperando…";
        Visible = true;
    }

    public void SetBiting(double needle, bool inZone)
    {
        _zone.Visible = true;
        _needle.Visible = true;
        _needle.Position = new Vector2(MathF.Round((float)(needle * Width)) - 0.75f, -2f);
        _zone.Color = inZone ? new Color("#ffd27a") : new Color("#F2A541");
        _label.Text = "Puxe!";
    }

    public void SetCaption(string text) => _label.Text = text;
}
