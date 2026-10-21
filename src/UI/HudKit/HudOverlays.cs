using System.Collections.Generic;
using Godot;
using Vespera.Core.Ranks;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.HudKit;

/// <summary>
/// Toasts no centro-alto (fila, um por vez, 4,2 s; <c>render.js:299-302</c>): painel com borda da cor, ícone, título em
/// Cinzel e subtítulo. Entra e sai com fade (<c>min(1, t·4, (max−t)·2,5)</c>).
/// </summary>
public partial class ToastBox : Control
{
    public const float DefaultSeconds = 4.2f;

    private readonly Queue<(string Title, string Sub, Color Color, string? Icon, float Seconds)> _queue = new();
    private readonly OrnatePanel _panel;
    private readonly IconRect _icon;
    private readonly Label _title;
    private readonly Label _sub;
    private readonly StyleBoxFlat _style;
    private float _t;
    private float _max;
    private bool _showing;

    public ToastBox()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.CenterTop);
        _panel = new OrnatePanel { MouseFilter = MouseFilterEnum.Ignore };
        _style = (StyleBoxFlat)_panel.GetThemeStylebox("panel", "OrnatePanel").Duplicate();
        _icon = IconRect.Create(string.Empty, 15);
        _title = Ui.Title(string.Empty, 9);
        _sub = Ui.Text(string.Empty, UiFonts.Small, new Color("#d8cfe0"));
        _panel.AddChild(Ui.Row(6, _icon, Ui.Column(0, _title, _sub)));
        AddChild(_panel);
        Visible = false;
        SetProcess(false);
    }

    /// <summary>Quantos toasts esperam (inclui o atual).</summary>
    public int Pending => _queue.Count + (_showing ? 1 : 0);

    /// <summary>Título do toast visível agora ("" se nenhum).</summary>
    public string CurrentTitle => _showing ? _title.Text : string.Empty;

    /// <summary>Y do topo (sobe um pouco quando há barra de chefe, <c>render.js:300</c>).</summary>
    public float Top { get; set; } = 40;

    public void Show(string title, string sub, Color color, string? icon = null, float seconds = DefaultSeconds)
    {
        _queue.Enqueue((title, sub, color, icon, seconds));
        if (!_showing)
        {
            Next();
        }
    }

    public override void _Process(double delta)
    {
        _t += Mathf.Min(0.05f, (float)delta);
        var a = Mathf.Min(1f, Mathf.Min(_t * 4f, (_max - _t) * 2.5f));
        Modulate = new Color(1, 1, 1, Mathf.Max(0f, a));
        if (_t >= _max)
        {
            Next();
        }
    }

    private void Next()
    {
        if (!_queue.TryDequeue(out var toast))
        {
            _showing = false;
            Visible = false;
            SetProcess(false);
            return;
        }

        _showing = true;
        _t = 0;
        _max = toast.Seconds;
        _title.Text = toast.Title;
        _title.AddThemeColorOverride("font_color", toast.Color == UiPalette.Ambar ? UiPalette.Creme : toast.Color);
        _sub.Text = toast.Sub;
        _sub.Visible = toast.Sub.Length > 0;
        _icon.Key = toast.Icon ?? string.Empty;
        _icon.Visible = toast.Icon is not null;
        _style.BorderColor = toast.Color;
        _panel.AddThemeStyleboxOverride("panel", _style);
        _panel.CornerColor = toast.Color;
        _panel.QueueRedraw();
        _panel.ResetSize();
        var size = _panel.GetCombinedMinimumSize();
        _panel.Position = new Vector2(-size.X / 2f, Top);
        Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        SetProcess(true);
    }
}

/// <summary>
/// Caixa de loot / notas da direita (guia hud-kit "caixa de loot"; notas da v2, <c>render.js:289-292</c>): até 6 linhas
/// com ícone, texto e "×N" quando o mesmo item se repete; cada uma vive 4 s e some no último segundo. Linhas reaproveitadas.
/// </summary>
public partial class LootFeed : VBoxContainer
{
    private const int MaxEntries = 6;
    private const float Life = 4f;

    private readonly List<Entry> _entries = [];

    public LootFeed()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
        Alignment = AlignmentMode.End;
        for (var i = 0; i < MaxEntries; i++)
        {
            var entry = new Entry();
            _entries.Add(entry);
            AddChild(entry.Panel);
        }

        SetProcess(false);
    }

    /// <summary>Linhas visíveis (texto), da mais antiga para a mais nova (smoke test).</summary>
    public IEnumerable<string> VisibleTexts
    {
        get
        {
            foreach (var e in _entries)
            {
                if (e.Panel.Visible)
                {
                    yield return e.Text.Text + (e.Count > 1 ? $" ×{e.Count}" : string.Empty);
                }
            }
        }
    }

    /// <summary>Mostra uma linha; com <paramref name="key"/> igual a uma visível, soma na mesma linha.</summary>
    public void Add(string text, string? icon, string? key = null, int amount = 1, Color? color = null)
    {
        if (key is not null)
        {
            foreach (var e in _entries)
            {
                if (e.Panel.Visible && e.Key == key)
                {
                    e.Count += amount;
                    e.Age = 0;
                    e.Refresh();
                    MoveChild(e.Panel, -1);
                    return;
                }
            }
        }

        Entry? slot = null;
        foreach (var e in _entries)
        {
            if (!e.Panel.Visible)
            {
                slot = e;
                break;
            }
        }

        slot ??= Oldest();
        slot.Key = key;
        slot.Count = amount;
        slot.Age = 0;
        slot.Text.Text = text;
        slot.Text.AddThemeColorOverride("font_color", color ?? UiPalette.Creme);
        slot.Icon.Key = icon ?? string.Empty;
        slot.Icon.Visible = icon is not null;
        slot.Refresh();
        slot.Panel.Visible = true;
        MoveChild(slot.Panel, -1);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var any = false;
        foreach (var e in _entries)
        {
            if (!e.Panel.Visible)
            {
                continue;
            }

            e.Age += (float)delta;
            var a = Mathf.Clamp(1f - (e.Age - (Life - 1f)), 0f, 1f);
            e.Panel.Modulate = new Color(1, 1, 1, a);
            if (a <= 0f)
            {
                e.Panel.Visible = false;
            }
            else
            {
                any = true;
            }
        }

        SetProcess(any);
    }

    private Entry Oldest()
    {
        var oldest = _entries[0];
        foreach (var e in _entries)
        {
            if (e.Age > oldest.Age)
            {
                oldest = e;
            }
        }

        return oldest;
    }

    private sealed class Entry
    {
        public Entry()
        {
            Icon = IconRect.Create(string.Empty, 9);
            Text = Ui.Text(string.Empty, UiFonts.Small);
            CountLabel = Ui.Bold(string.Empty, UiFonts.Small, UiPalette.Ambar);
            Panel = Ui.Panel("SoftPanel", Ui.Row(4, Icon, Text, CountLabel));
            Panel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            Panel.Visible = false;
        }

        public PanelContainer Panel { get; }

        public IconRect Icon { get; }

        public Label Text { get; }

        public Label CountLabel { get; }

        public string? Key { get; set; }

        public int Count { get; set; }

        public float Age { get; set; }

        public void Refresh()
        {
            CountLabel.Text = Count > 1 ? $"×{Count}" : string.Empty;
            CountLabel.Visible = Count > 1;
        }
    }
}

/// <summary>
/// Faixa de texto temporária no centro: nome do lugar (3 s, linha dourada), intro do chefe (3,2 s, faixa escura) e
/// banner do Supremo (1,4 s, desliza). Alpha da v2: <c>min(1, t·a, (dur−t)·b)</c>.
/// </summary>
public partial class Banner : Control
{
    private readonly Label _title;
    private readonly Label _sub;
    private readonly ColorRect _band;
    private float _t;
    private float _duration;
    private float _fadeIn = 2f;
    private float _fadeOut = 1.5f;
    private float _slide;

    public Banner(float y, int titleSize, bool band, bool rule)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorRight = 1;
        OffsetTop = y;
        _band = new ColorRect { Color = new Color(9 / 255f, 7 / 255f, 20 / 255f, 0.75f), MouseFilter = MouseFilterEnum.Ignore, Visible = band, AnchorRight = 1, OffsetTop = -10, OffsetBottom = 30 };
        AddChild(_band);
        _title = Ui.Title(string.Empty, titleSize, null, HorizontalAlignment.Center);
        _title.AnchorRight = 1;
        AddChild(_title);
        _sub = Ui.Text(string.Empty, UiFonts.Small, UiPalette.Creme, align: HorizontalAlignment.Center);
        _sub.AnchorRight = 1;
        _sub.OffsetTop = titleSize + 5;
        AddChild(_sub);
        var line = Ui.Row(0, Ui.Rule(45, 1f), Diamond.Create(5, UiPalette.Gold), Ui.Rule(45, 1f));
        line.Alignment = BoxContainer.AlignmentMode.Center;
        line.AnchorRight = 1;
        line.OffsetTop = titleSize + 4;
        line.Visible = rule;
        AddChild(line);
        Visible = false;
        SetProcess(false);
    }

    public string Text => _title.Text;

    public void Play(string title, string sub, Color color, float duration, float fadeIn = 2f, float fadeOut = 1.5f, float slide = 0f)
    {
        _title.Text = title;
        _title.AddThemeColorOverride("font_color", color);
        _sub.Text = sub;
        _sub.Visible = sub.Length > 0;
        _t = 0;
        _duration = duration;
        _fadeIn = fadeIn;
        _fadeOut = fadeOut;
        _slide = slide;
        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _t += Mathf.Min(0.05f, (float)delta);
        var a = Mathf.Clamp(Mathf.Min(1f, Mathf.Min(_t * _fadeIn, (_duration - _t) * _fadeOut)), 0f, 1f);
        Modulate = new Color(1, 1, 1, a);
        if (_slide > 0f)
        {
            _title.Position = new Vector2((1f - Mathf.Min(1f, _t * 5f)) * _slide, _title.Position.Y);
        }

        if (_t >= _duration)
        {
            Visible = false;
            SetProcess(false);
        }
    }
}

/// <summary>
/// "Novo Selo" (<c>render.js:309</c>): escurece a tela, selo grande, "ID · Título" na cor do Selo; 4 s, sem travar o jogo.
/// </summary>
public partial class RankUpOverlay : Control
{
    private const float Duration = 4f;

    private readonly RankSeal _seal;
    private readonly Label _name;
    private float _t;

    public RankUpOverlay()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new Color(9 / 255f, 7 / 255f, 20 / 255f, 0.55f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var column = Ui.Column(4);
        column.Alignment = BoxContainer.AlignmentMode.Center;
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(column);
        _seal = new RankSeal { CustomMinimumSize = new Vector2(55, 55), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, LabelFont = UiFonts.Title700, LabelFontSize = 18 };
        column.AddChild(_seal);
        column.AddChild(Ui.Title(UiText.RankUpTitle, UiFonts.Body, UiPalette.SubLight, HorizontalAlignment.Center));
        _name = Ui.Title(string.Empty, UiFonts.Title, null, HorizontalAlignment.Center);
        column.AddChild(_name);
        Visible = false;
        SetProcess(false);
    }

    public bool Showing => Visible;

    public void Play(Rank rank, string id, string title, Color color)
    {
        _seal.Rank = rank;
        _name.Text = $"{id} · {title}";
        _name.AddThemeColorOverride("font_color", color);
        _t = 0;
        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _t += Mathf.Min(0.05f, (float)delta);
        Modulate = new Color(1, 1, 1, Mathf.Clamp(Mathf.Min(1f, Mathf.Min(_t * 2f, (Duration - _t) * 1.5f)), 0f, 1f));
        if (_t >= Duration)
        {
            Visible = false;
            SetProcess(false);
        }
    }
}

/// <summary>Barra do chefe no topo (<c>render.js:293-297</c>): nome em Cinzel, barra magenta com lag.</summary>
public partial class BossBar : VBoxContainer
{
    private readonly Label _name;
    private readonly UiBar _bar;

    public BossBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
        _name = Ui.Title(string.Empty, UiFonts.Body, null, HorizontalAlignment.Center);
        _bar = UiBar.Create("boss", new Vector2(280, 7), lag: true);
        _bar.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(_name);
        AddChild(_bar);
        Visible = false;
    }

    public string BossName => _name.Text;

    public float Ratio => _bar.Ratio;

    public void Begin(string name, int maxHp)
    {
        _name.Text = name;
        _bar.SetRatio(1f, instant: true);
        Visible = maxHp > 0;
    }

    public void SetHealth(int hp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return;
        }

        _bar.SetRatio(hp / (float)maxHp);
        if (hp <= 0)
        {
            Visible = false;
        }
    }

    public void End() => Visible = false;
}
