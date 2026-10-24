using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Abertura de "Nova história" (ROTEIRO-V2 §3, 0:00): duas cartelas de 3 s, texto claro sobre fundo escuro, e corte
/// seco para o jogo. Qualquer tecla, clique ou botão pula para a próxima cartela; depois da última segue para
/// <see cref="ScreenKeys.AfterPrologue"/>. O vídeo longo saiu daqui e virou o fundo do título.
/// </summary>
public partial class PrologueScreen : Control
{
    /// <summary>As duas únicas frases de narrador do Ato I.</summary>
    public static readonly string[] Cards =
    [
        "Há vinte anos, o sol não nasceu por um ano inteiro.",
        "Lumeira acende as luzes antes de escurecer. Todo dia.",
    ];

    private const double CardSeconds = 3.0;
    private const double FadeSeconds = 0.45;
    private const double InputGraceSeconds = 0.25;

    private static readonly string[] CardArt =
    [
        "res://assets/v2/ilustracoes/cartela_abertura_1.png",
        "res://assets/v2/ilustracoes/cartela_abertura_2.png",
    ];

    private const float BandHeightFraction = 0.4f;
    private const float BandMaxAlpha = 0.85f;
    private const float TextBottomMargin = 56f;
    private const float TextSideMargin = 240f;
    private const float TextHeight = 150f;

    private TextureRect _art = null!;
    private Label _text = null!;
    private int _card;
    private double _age;
    private bool _done;

    /// <summary>Cartela mostrada agora (0 ou 1); o smoke confere a ordem.</summary>
    public int CurrentCard => _card;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        var black = new ColorRect { Color = new Color(0.03f, 0.03f, 0.07f), MouseFilter = MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(black);

        _art = new TextureRect
        {
            Texture = GD.Load<Texture2D>(CardArt[0]),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_art);
        AddChild(MakeBand());

        _text = UiKit.MakeLabel(Cards[0], UiTheme.HeadingVariation, HorizontalAlignment.Center);
        _text.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.OffsetLeft = TextSideMargin;
        _text.OffsetRight = -TextSideMargin;
        _text.OffsetBottom = -TextBottomMargin;
        _text.OffsetTop = -TextBottomMargin - TextHeight;
        _text.Modulate = Colors.Transparent;
        AddChild(_text);
    }

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }

        _age += delta;
        var fadeIn = Mathf.Clamp(_age / FadeSeconds, 0.0, 1.0);
        var fadeOut = Mathf.Clamp((CardSeconds - _age) / FadeSeconds, 0.0, 1.0);
        _text.Modulate = new Color(1f, 1f, 1f, (float)Mathf.Min(fadeIn, fadeOut));
        if (_age >= CardSeconds)
        {
            Advance();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_done || _age < InputGraceSeconds || !IsSkipEvent(@event))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        Advance();
    }

    private void Advance()
    {
        _card++;
        _age = 0;
        if (_card < Cards.Length)
        {
            _text.Text = Cards[_card];
            _art.Texture = GD.Load<Texture2D>(CardArt[_card]);
            _text.Modulate = Colors.Transparent;
            return;
        }

        _done = true;
        SceneRouter.Instance.GoTo(ScreenKeys.AfterPrologue);
    }

    /// <summary>Faixa escura em gradiente no terço de baixo (transparente em cima, escura embaixo) para o texto ler bem.</summary>
    private static TextureRect MakeBand()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.03f, 0.03f, 0.07f, 0f));
        gradient.SetColor(1, new Color(0.03f, 0.03f, 0.07f, BandMaxAlpha));
        var band = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 4,
                Height = 256,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        band.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        band.AnchorTop = 1f - BandHeightFraction;
        band.OffsetTop = 0;
        band.OffsetBottom = 0;
        return band;
    }

    private static bool IsSkipEvent(InputEvent @event) => @event switch
    {
        InputEventKey key => key.Pressed && !key.Echo,
        InputEventMouseButton mouse => mouse.Pressed,
        InputEventJoypadButton pad => pad.Pressed,
        _ => false,
    };
}
