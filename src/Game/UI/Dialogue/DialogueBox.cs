using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Caixa de fala em 1920x1080: moldura <c>painel_dialogo.png</c> embaixo, retrato do falante à esquerda, placa de nome
/// sobre a borda, texto que "digita" (pular = mostrar tudo, de novo = próxima caixa) e respostas numa lista acima da
/// moldura. Teclado (Interagir/Enter/setas), controle (A/direcional) e mouse (clique). O texto usa o tema, então
/// segue a opção "Tamanho do texto". Quem desenha só repete o que o <see cref="DialogueRunner"/> entrega.
/// </summary>
public partial class DialogueBox : Control
{
    private const string PanelPath = "res://assets/v2/interface/painel_dialogo.png";
    private const float PanelWidth = 1500f;
    private const float PanelBottomMargin = 36f;
    private const float PanelSourceWidth = 1536f;
    private const float CharsPerSecond = 52f;
    private const float FadeSpeed = 9f;
    private const float PortraitX = 28f;
    private const float PortraitY = 38f;
    private const float PortraitW = 257f;
    private const float PortraitH = 242f;
    private const float TextX = 330f;
    private const float TextPadRight = 44f;
    private const float TextPadTop = 36f;

    private readonly List<Button> _choiceButtons = [];
    private DialogueRunner? _runner;
    private StoryScript? _script;
    private string _keyName = "E";
    private TextureRect _panel = null!;
    private TextureRect _portrait = null!;
    private Label _portraitCaption = null!;
    private PanelContainer _plate = null!;
    private Label _plateName = null!;
    private Label _text = null!;
    private Label _hint = null!;
    private PanelContainer _choiceBox = null!;
    private VBoxContainer _choiceList = null!;
    private float _typed;
    private int _total;
    private float _alpha;

    public event Action? Closed;

    public bool IsOpen => _runner is not null;

    public bool Typing => _runner is not null && _typed < _total;

    public bool HasChoices => _choiceButtons.Count > 0;

    /// <summary>Texto completo da caixa atual (para os bots e a revisão visual).</summary>
    public string FullText => _runner?.Text ?? string.Empty;

    public string SpeakerName => _plate.Visible ? _plateName.Text : string.Empty;

    public int Expression => _runner?.Current?.Expression ?? 1;

    public string CurrentNodeId => _runner?.CurrentId ?? string.Empty;

    public IReadOnlyList<Button> ChoiceButtons => _choiceButtons;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Pausable;
        Visible = false;
        BuildPanel();
        BuildPlate();
        BuildChoices();
    }

    /// <summary>Abre a conversa. <paramref name="keyName"/> é o nome da tecla/botão de Interagir mostrado na dica.</summary>
    public void Open(DialogueRunner runner, StoryScript script, string keyName)
    {
        _runner = runner;
        _script = script;
        _keyName = keyName;
        _alpha = 0f;
        Visible = true;
        Show(runner);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        SecondsSinceClosed += dt;
        if (_runner is null)
        {
            return;
        }

        _alpha = Mathf.MoveToward(_alpha, 1f, FadeSpeed * dt);
        Modulate = new Color(1, 1, 1, _alpha);
        if (Typing)
        {
            _typed = Mathf.Min(_total, _typed + (CharsPerSecond * dt));
            _text.VisibleCharacters = (int)_typed;
            _hint.Visible = false;
        }
        else
        {
            _hint.Visible = !HasChoices;
            _hint.Modulate = new Color(1, 1, 1, 0.55f + (0.45f * Mathf.Sin(Time.GetTicksMsec() / 260f)));
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_runner is null || !IsVisibleInTree())
        {
            return;
        }

        var interact = @event.IsActionPressed(InputActions.Interact);
        var accept = @event.IsActionPressed("ui_accept");
        var click = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse && _panel.GetGlobalRect().HasPoint(mouse.GlobalPosition);
        if (!interact && !accept && !click)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (Typing)
        {
            Complete();
            return;
        }

        if (HasChoices)
        {
            if (interact && GetViewport().GuiGetFocusOwner() is Button focused && _choiceButtons.Contains(focused))
            {
                focused.EmitSignal(BaseButton.SignalName.Pressed);
            }

            return;
        }

        Advance(0);
    }

    /// <summary>Mostra o texto inteiro agora (primeiro aperto durante a digitação).</summary>
    public void Complete()
    {
        _typed = _total;
        _text.VisibleCharacters = -1;
    }

    private void Advance(int choice)
    {
        if (_runner is null)
        {
            return;
        }

        UiSfx.Play(UiSound.Move);
        if (_runner.Advance(choice))
        {
            Show(_runner);
            return;
        }

        Close();
    }

    private void Close()
    {
        _runner = null;
        ClearChoices();
        Visible = false;
        GetViewport().GuiReleaseFocus();
        SecondsSinceClosed = 0f;
        Closed?.Invoke();
    }

    private void Show(DialogueRunner runner)
    {
        var node = runner.Current!;
        ApplySpeaker(node);
        _text.Text = runner.Text;
        _total = runner.Text.Length;
        _typed = 0f;
        _text.VisibleCharacters = 0;
        ClearChoices();
        _hint.Text = $"{_keyName}  continuar";
        if (runner.Choices.Count > 0)
        {
            BuildChoiceButtons(runner.Choices);
        }
    }

    private void ApplySpeaker(DialogueNode node)
    {
        SpeakerDef? speaker = null;
        _script?.Speakers.TryGetValue(node.Speaker, out speaker);
        var name = node.Label ?? speaker?.Name ?? string.Empty;
        _plate.Visible = name.Length > 0;
        _plateName.Text = name;
        PlacePlate();

        Texture2D? texture = null;
        if (speaker?.Portrait is { Length: > 0 } pattern)
        {
            var path = pattern.Replace("{0}", Math.Clamp(node.Expression, 1, 2).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            if (ResourceLoader.Exists(path))
            {
                texture = GD.Load<Texture2D>(path);
            }
        }

        _portrait.Texture = texture;
        _portrait.Visible = texture is not null;
        _portraitCaption.Visible = texture is null;
        _portraitCaption.Text = node.Speaker.Length == 0 ? string.Empty : "voz fora de cena";
    }

    private void BuildChoiceButtons(IReadOnlyList<DialogueChoice> choices)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var button = UiKit.MakeButton(choices[i].Text, () => Advance(index), 620);
            button.Name = $"Choice{i}";
            _choiceList.AddChild(button);
            _choiceButtons.Add(button);
        }

        UiKit.WrapFocus(_choiceButtons);
        _choiceBox.Visible = true;
        _choiceBox.ResetSize();
        CallDeferred(nameof(PlaceChoices));
    }

    private void PlaceChoices()
    {
        if (_choiceButtons.Count == 0)
        {
            return;
        }

        _choiceBox.ResetSize();
        var size = _choiceBox.GetCombinedMinimumSize();
        _choiceBox.Size = size;
        _choiceBox.Position = new Vector2(_panel.Position.X + _panel.Size.X - size.X - 24f, _panel.Position.Y - size.Y - 10f);
        _choiceButtons[0].GrabFocus();
    }

    private void ClearChoices()
    {
        foreach (var button in _choiceButtons)
        {
            _choiceList.RemoveChild(button);
            button.QueueFree();
        }

        _choiceButtons.Clear();
        _choiceBox.Visible = false;
    }

    private void PlacePlate()
    {
        _plate.ResetSize();
        var size = _plate.GetCombinedMinimumSize();
        _plate.Size = size;
        var k = _panel.Size.X / PanelSourceWidth;
        _plate.Position = new Vector2(_panel.Position.X + (TextX * k), _panel.Position.Y - (size.Y * 0.55f));
    }

    private void BuildPanel()
    {
        var texture = GD.Load<Texture2D>(PanelPath);
        var k = PanelWidth / PanelSourceWidth;
        var size = new Vector2(PanelWidth, texture.GetHeight() * k);
        _panel = new TextureRect
        {
            Name = "Panel",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = size,
            Position = new Vector2((1920f - PanelWidth) / 2f, 1080f - PanelBottomMargin - size.Y),
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_panel);

        var portraitFrame = new Control
        {
            Name = "PortraitFrame",
            Position = new Vector2(PortraitX, PortraitY) * k,
            Size = new Vector2(PortraitW, PortraitH) * k,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _panel.AddChild(portraitFrame);
        _portrait = new TextureRect
        {
            Name = "Portrait",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        portraitFrame.AddChild(_portrait);
        _portraitCaption = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        _portraitCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _portraitCaption.VerticalAlignment = VerticalAlignment.Center;
        _portraitCaption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        portraitFrame.AddChild(_portraitCaption);

        _text = UiKit.MakeWrapped(string.Empty, UiTheme.ReadingVariation);
        _text.Name = "Text";
        _text.Position = new Vector2(TextX * k, TextPadTop * k);
        _text.Size = new Vector2(size.X - ((TextX + TextPadRight) * k), size.Y - (2f * TextPadTop * k));
        _text.ClipText = true;
        _text.MouseFilter = MouseFilterEnum.Ignore;
        _panel.AddChild(_text);

        _hint = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Right);
        _hint.Name = "Hint";
        _hint.Size = new Vector2(420f, 40f);
        _hint.Position = new Vector2(size.X - 420f - (46f * k), size.Y - 54f);
        _hint.MouseFilter = MouseFilterEnum.Ignore;
        _panel.AddChild(_hint);
    }

    private void BuildPlate()
    {
        _plate = new PanelContainer { Name = "NamePlate", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _plate.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.97f), Palette.Amber, 2, 22, 2));
        _plateName = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        _plate.AddChild(_plateName);
        AddChild(_plate);
    }

    private void BuildChoices()
    {
        _choiceBox = new PanelContainer { Name = "Choices", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _choiceBox.AddThemeStyleboxOverride("panel", Bubble(new Color(Palette.Indigo, 0.94f), Palette.Amber, 2, 16, 14));
        _choiceList = UiKit.MakeColumn(8);
        _choiceBox.AddChild(_choiceList);
        AddChild(_choiceBox);
    }

    /// <summary>Segundos de jogo desde o fechamento da última conversa (o Level ignora "interagir" logo depois).</summary>
    public float SecondsSinceClosed { get; private set; } = 99f;

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
}
