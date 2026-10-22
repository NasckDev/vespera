using Godot;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.HudKit;

/// <summary>
/// Slot da hotbar (<c>slotBox</c>, <c>core.js:119</c> + hotbar de <c>render.js:268-279</c>): ícone, tecla no canto,
/// contagem/nível, cadeado (arma de outra classe) e véu de recarga/Essência. Desenhado à mão (sem nós filhos) e só
/// redesenha quando um valor muda.
/// </summary>
public partial class HotbarSlot : Control
{
    private static readonly StringName ThemePanel = "panel";
    private static readonly StringName Normal = "SlotPanel";
    private static readonly StringName Focused = "SlotFocus";
    private static readonly Color Veil = new(10 / 255f, 8 / 255f, 18 / 255f, 0.6f);

    private Texture2D? _icon;
    private string _iconKey = string.Empty;
    private string _key = string.Empty;
    private string _corner = string.Empty;
    private Color _keyColor = UiPalette.Dim;
    private Color _cornerColor = UiPalette.Ambar;
    private bool _selected;
    private bool _locked;
    private float _iconAlpha = 1f;
    private float _veil;
    private float _iconScale = 0.72f;
    private bool _drawFrame = true;
    private bool _marker;
    private Color? _stripe;
    private Texture2D? _framed;

    public HotbarSlot()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(24, 24);
    }

    public string IconKey
    {
        get => _iconKey;
        set
        {
            if (_iconKey != value)
            {
                _iconKey = value;
                _icon = UiIcons.Get(value);
                QueueRedraw();
            }
        }
    }

    /// <summary>
    /// Ícone do designer que já traz a moldura do slot (HUD v1, DECISIONS C2-D7): desenhado no slot inteiro no lugar da
    /// moldura do tema e do <see cref="IconKey"/>. Null volta ao desenho normal.
    /// </summary>
    public Texture2D? FramedIcon
    {
        get => _framed;
        set => Set(ref _framed, value);
    }

    public float IconScale
    {
        get => _iconScale;
        set => Set(ref _iconScale, value);
    }

    public void SetKey(string key, Color color)
    {
        if (_key != key || _keyColor != color)
        {
            _key = key;
            _keyColor = color;
            QueueRedraw();
        }
    }

    /// <summary>Texto no canto inferior direito ("+3", contagem de poções).</summary>
    public void SetCorner(string text, Color color)
    {
        if (_corner != text || _cornerColor != color)
        {
            _corner = text;
            _cornerColor = color;
            QueueRedraw();
        }
    }

    public bool Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    /// <summary>Desenha a moldura do slot (false quando o pai já é um painel focável, como na grade do inventário).</summary>
    public bool DrawFrame
    {
        get => _drawFrame;
        set => Set(ref _drawFrame, value);
    }

    /// <summary>Losango âmbar no canto (arma empunhada, <c>ui.js:154</c>).</summary>
    public bool CornerMarker
    {
        get => _marker;
        set => Set(ref _marker, value);
    }

    /// <summary>Faixa da cor da raridade embaixo (equipamentos, <c>ui.js:155</c>).</summary>
    public Color? Stripe
    {
        get => _stripe;
        set => Set(ref _stripe, value);
    }

    public bool Locked
    {
        get => _locked;
        set => Set(ref _locked, value);
    }

    public float IconAlpha
    {
        get => _iconAlpha;
        set => Set(ref _iconAlpha, value);
    }

    /// <summary>Fração do slot coberta por cima (recarga, Essência que falta).</summary>
    public float VeilRatio
    {
        get => _veil;
        set => Set(ref _veil, Mathf.Clamp(value, 0f, 1f));
    }

    public override void _Draw()
    {
        var size = Size;
        var framed = _framed is not null;
        if (framed)
        {
            DrawTextureRect(_framed!, new Rect2(Vector2.Zero, size), false, new Color(1, 1, 1, _iconAlpha));
            if (_selected)
            {
                DrawRect(new Rect2(Vector2.Zero, size), UiPalette.Ambar, false, 1f);
            }
        }
        else if (_drawFrame)
        {
            DrawStyleBox(GetThemeStylebox(ThemePanel, _selected ? Focused : Normal), new Rect2(Vector2.Zero, size));
        }

        if (_icon is not null && !framed)
        {
            var side = size.X * _iconScale;
            var tex = _icon.GetSize();
            var scale = side / Mathf.Max(tex.X, tex.Y);
            var drawn = tex * scale;
            DrawTextureRect(_icon, new Rect2((size - drawn) / 2f, drawn), false, new Color(1, 1, 1, _iconAlpha));
        }

        if (_veil > 0f)
        {
            DrawRect(new Rect2(1, 1, size.X - 2, (size.Y - 2) * _veil), Veil);
        }

        if (_stripe is { } stripe)
        {
            DrawRect(new Rect2(2, size.Y - 3, size.X - 4, 1), stripe);
        }

        if (_marker)
        {
            Diamond.Paint(this, new Vector2(4, 4), 2f, UiPalette.Ambar);
        }

        if (_locked)
        {
            DrawRect(new Rect2(1, 1, size.X - 2, size.Y - 2), Veil);
            HudDraw.Lock(this, _drawFrame ? size / 2f : new Vector2(size.X - 6, 6), _drawFrame ? 3.5f : 2.5f);
        }

        var font = UiFonts.TextBold;
        if (_key.Length > 0)
        {
            HudDraw.Text(this, font, new Vector2(2, 7), _key, UiFonts.Small, _keyColor); // C1-D7: era 5 px base
        }

        if (_corner.Length > 0)
        {
            HudDraw.Text(this, font, new Vector2(size.X - 2, size.Y - 2), _corner, UiFonts.Small, _cornerColor, HorizontalAlignment.Right);
        }

        if (!_drawFrame || framed)
        {
            return;
        }

        // losangos nos cantos (slotBox)
        var corner = _selected ? UiPalette.Ambar : UiPalette.GoldDark;
        Diamond.Paint(this, Vector2.Zero, 1.5f, corner);
        Diamond.Paint(this, new Vector2(size.X, 0), 1.5f, corner);
        Diamond.Paint(this, new Vector2(0, size.Y), 1.5f, corner);
        Diamond.Paint(this, size, 1.5f, corner);
    }

    private void Set<T>(ref T field, T value)
    {
        if (!Equals(field, value))
        {
            field = value;
            QueueRedraw();
        }
    }
}

/// <summary>
/// Botão circular do Golpe Supremo (<c>extra.js:142-148</c>): ícone da arma, varredura de recarga com segundos,
/// anel na cor do Supremo quando pronto (com brilho pulsante) e arco âmbar de progresso. Conta a recarga localmente
/// entre um <c>AbilityCooldownChanged</c> e outro.
/// </summary>
public partial class UltimateButton : Control
{
    private const int Segments = 32;
    private static readonly Color Fill = new("#120c1c");
    private static readonly Color Sweep = new(8 / 255f, 6 / 255f, 14 / 255f, 0.6f);

    private readonly Vector2[] _pie = new Vector2[Segments + 2];
    private Texture2D? _icon;
    private float _remaining;
    private float _total = 1f;
    private int _shownSeconds = -1;
    private string _secondsText = string.Empty;
    private double _time;
    private Color _ready = UiPalette.Ambar;

    public UltimateButton()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(29, 29);
    }

    public bool IsReady => _remaining <= 0f;

    /// <summary>O ícone é o do designer (não o da arma no atlas).</summary>
    public bool UsesArt { get; private set; }

    public float Remaining => _remaining;

    /// <summary>Ícone e cor de pronto; <paramref name="art"/> (ícone do Supremo do designer) vence o recorte do atlas.</summary>
    public void Setup(string iconKey, Color readyColor, Texture2D? art = null)
    {
        _icon = art ?? UiIcons.Get(iconKey);
        UsesArt = art is not null;
        _ready = readyColor;
        QueueRedraw();
    }

    public void SetCooldown(float remaining, float total)
    {
        _remaining = Mathf.Max(0f, remaining);
        _total = Mathf.Max(0.01f, total);
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        if (_remaining > 0f)
        {
            _remaining = Mathf.Max(0f, _remaining - (float)delta);
        }

        QueueRedraw(); // pulsa quando pronto
    }

    public override void _Draw()
    {
        var r = Size.X / 2f;
        var c = new Vector2(r, r);
        var ready = _remaining <= 0f;
        var progress = ready ? 1f : 1f - (_remaining / _total);
        if (ready)
        {
            var pulse = 0.5f + (0.3f * Mathf.Sin((float)_time * 5f));
            DrawCircle(c, r + 3f, _ready with { A = 0.18f * pulse });
            DrawCircle(c, r + 1.5f, _ready with { A = 0.25f * pulse });
        }

        DrawCircle(c, r, Fill);
        if (_icon is not null)
        {
            var side = r * 2f * (UsesArt ? 0.74f : 0.62f);
            var tex = _icon.GetSize();
            var drawn = tex * (side / Mathf.Max(tex.X, tex.Y));
            DrawTextureRect(_icon, new Rect2(c - (drawn / 2f), drawn), false, new Color(1, 1, 1, ready ? 1f : 0.45f));
        }

        if (!ready)
        {
            // varredura escura do que falta (sentido horário a partir do topo)
            if (progress < 0.01f)
            {
                DrawCircle(c, r, Sweep);
            }
            else if (progress < 0.99f)
            {
                var start = -Mathf.Pi / 2f + (Mathf.Tau * progress);
                var end = Mathf.Pi * 1.5f;
                _pie[0] = c;
                for (var i = 0; i <= Segments; i++)
                {
                    var a = Mathf.Lerp(start, end, i / (float)Segments);
                    _pie[i + 1] = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }

                DrawColoredPolygon(_pie, Sweep);
            }
            var seconds = Mathf.CeilToInt(_remaining);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _secondsText = seconds.ToString();
            }

            HudDraw.Text(this, UiFonts.TextBold, new Vector2(r, r + 3), _secondsText, UiFonts.Body, UiPalette.Creme, HorizontalAlignment.Center);
        }

        DrawArc(c, r, 0, Mathf.Tau, 40, ready ? _ready : UiPalette.Border, 1f, true);
        DrawArc(c, r + 1.5f, -Mathf.Pi / 2f, (-Mathf.Pi / 2f) + (Mathf.Tau * progress), 40, UiPalette.Ambar, 1f, true);
    }
}

/// <summary>Moldura de retrato do HUD (guia hud-kit): rosto do personagem da classe e o Nível num losango.</summary>
public partial class PortraitFrame : OrnatePanel
{
    private readonly TextureRect _face = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        MouseFilter = MouseFilterEnum.Ignore,
        ClipContents = true,
    };

    private readonly Label _level;

    public PortraitFrame()
    {
        ThemeTypeVariation = "SlotPanel";
        CornerSize = 2f;
        InnerLine = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(30, 30);
        ClipContents = false;
        AddChild(_face);

        var badge = new PanelContainer { ThemeTypeVariation = "KeyCap", MouseFilter = MouseFilterEnum.Ignore };
        _level = Ui.Bold("1", UiFonts.Small, UiPalette.Ambar, HorizontalAlignment.Center);
        _level.AddThemeConstantOverride("shadow_offset_y", 0);
        badge.AddChild(_level);
        var corner = new Control { MouseFilter = MouseFilterEnum.Ignore };
        corner.AddChild(badge);
        AddChild(corner);
        badge.Position = new Vector2(19, 21);
    }

    public void SetClass(string classId) => _face.Texture = UiIcons.Head($"c_{classId}_front", 0.48f);

    public void SetLevel(string level) => _level.Text = level;
}

/// <summary>Desenho comum do HUD: texto com sombra (<c>txt()</c>, <c>core.js:101</c>) e cadeado (<c>drawLock</c>).</summary>
public static class HudDraw
{
    private static readonly Color Shadow = new(6 / 255f, 4 / 255f, 12 / 255f, 0.9f);
    private static readonly Color LockFill = new("#0b0812");

    public static void Text(CanvasItem canvas, Font font, Vector2 baseline, string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var width = -1f;
        var pos = baseline;
        if (align != HorizontalAlignment.Left)
        {
            var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
            pos.X -= align == HorizontalAlignment.Right ? w : w / 2f;
        }

        canvas.DrawString(font, pos + new Vector2(0, 0.5f), text, HorizontalAlignment.Left, width, size, Shadow);
        canvas.DrawString(font, pos, text, HorizontalAlignment.Left, width, size, color);
    }

    /// <summary>Cadeado âmbar (<c>drawLock</c>, <c>ui.js:259</c>).</summary>
    public static void Lock(CanvasItem canvas, Vector2 center, float r)
    {
        var body = new Rect2(center.X - r, center.Y - (r * 0.2f), r * 2f, r * 1.5f);
        canvas.DrawRect(body, LockFill);
        canvas.DrawRect(body, UiPalette.Ambar, false, Mathf.Max(0.75f, r * 0.28f));
        canvas.DrawArc(new Vector2(center.X, center.Y - (r * 0.2f)), r * 0.62f, Mathf.Pi, Mathf.Tau, 10, UiPalette.Ambar, Mathf.Max(0.75f, r * 0.28f));
        canvas.DrawRect(new Rect2(center.X - (r * 0.15f), center.Y + (r * 0.3f), r * 0.3f, r * 0.5f), UiPalette.Ambar);
    }
}
