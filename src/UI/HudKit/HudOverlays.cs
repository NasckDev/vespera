using System;
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
        _title = UiScaling.Reading(Ui.Title(string.Empty, 10));
        _sub = UiScaling.Reading(Ui.Text(string.Empty, UiFonts.Body, new Color("#d8cfe0")));
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

    /// <summary>
    /// Bloco do HUD que o toast não pode cobrir (coordenadas da raiz do HUD). Com a escala da interface em 125/150% o
    /// toast centralizado alcança o painel de vida: aí ele desce para baixo do bloco (C1-D7).
    /// </summary>
    public Rect2 Avoid { get; set; }

    /// <summary>Descarta a fila e o toast visível (smoke, troca de cena).</summary>
    public void Clear()
    {
        _queue.Clear();
        _showing = false;
        Visible = false;
        SetProcess(false);
    }

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
        Place();
        if (_t >= _max)
        {
            Next();
        }
    }

    /// <summary>Centralizado em <see cref="Top"/>, ou abaixo de <see cref="Avoid"/> se encostaria nele.</summary>
    private void Place()
    {
        var size = _panel.Size;
        var y = Top;
        var left = Position.X - (size.X / 2f);
        if (Avoid.Size.X > 0 && left < Avoid.End.X + 4f && y < Avoid.End.Y)
        {
            y = Avoid.End.Y + 4f;
        }

        _panel.Position = new Vector2(MathF.Round(-size.X / 2f), y);
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
        Place();
        Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        SetProcess(true);
    }
}

/// <summary>
/// Caixa de loot / notas da direita (guia hud-kit "caixa de loot"; notas da v2, <c>render.js:289-292</c>): até 4 linhas
/// (C1-D7: eram 6 empilhadas no meio da tela) com ícone, texto e "×N" quando o mesmo item se repete; cada uma vive 4 s e
/// some no último segundo. Linhas reaproveitadas.
/// </summary>
public partial class LootFeed : VBoxContainer
{
    private const int MaxEntries = 4;
    private const float Life = 4f;

    private readonly List<Entry> _entries = [];
    private int _limit = MaxEntries;

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

    /// <summary>Fundo opaco nas linhas (opção "Alto contraste").</summary>
    public bool HighContrast
    {
        get => _highContrast;
        set
        {
            _highContrast = value;
            foreach (var e in _entries)
            {
                e.Panel.ThemeTypeVariation = value ? "InsetPanel" : "SoftPanel";
            }
        }
    }

    private bool _highContrast;

    /// <summary>Quantas linhas cabem (smoke test).</summary>
    public static int Capacity => MaxEntries;

    /// <summary>
    /// Quantas linhas a caixa pode mostrar agora (1 a <see cref="Capacity"/>): o HUD reduz quando a lista de Brechas ocupa o
    /// espaço que o loot cresceria (POLI-8); as mais antigas saem primeiro, as novas continuam entrando.
    /// </summary>
    public int Limit
    {
        get => _limit;
        set
        {
            _limit = Math.Clamp(value, 1, MaxEntries);
            TrimToLimit(null);
        }
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
        TrimToLimit(slot);
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

    /// <summary>Esconde as linhas mais antigas até sobrarem <see cref="Limit"/> (a linha <paramref name="incoming"/> vai contar como visível).</summary>
    private void TrimToLimit(Entry? incoming)
    {
        while (true)
        {
            var visible = 0;
            Entry? oldest = null;
            foreach (var e in _entries)
            {
                if (e != incoming && e.Panel.Visible)
                {
                    visible++;
                    if (oldest is null || e.Age > oldest.Age)
                    {
                        oldest = e;
                    }
                }
            }

            if (visible + (incoming is null ? 0 : 1) <= _limit || oldest is null)
            {
                return;
            }

            oldest.Panel.Visible = false;
        }
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
            Icon = IconRect.Create(string.Empty, 10);
            Text = UiScaling.Reading(Ui.Text(string.Empty, UiFonts.Body));
            CountLabel = Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Ambar);
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
    private const int MinFontSize = 7; // mínimo legível (14 px em 720p)

    private readonly Label _title;
    private readonly Label _sub;
    private readonly Panel _band;
    private readonly int _titleBase;
    private float _t;
    private float _duration;
    private float _fadeIn = 2f;
    private float _fadeOut = 1.5f;
    private float _slide;
    private float _spanWidth = 640f;

    public Banner(float y, int titleSize, bool band, bool rule)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _titleBase = titleSize;
        // POLI-1: o letreiro ocupa só o vão entre o painel de vida (esquerda) e a coluna do canto direito (SetSpan), em vez
        // de meia tela centrada: com a interface em 125/150% a faixa de 50% da tela entrava no painel de vida.
        AnchorLeft = 0;
        AnchorRight = 0;
        OffsetLeft = 0;
        OffsetRight = _spanWidth;
        OffsetTop = y;
        // Faixa do tamanho do texto (C1-D8: a de 40 u cobria o meio da arena no bote do chefe).
        _band = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = band };
        _band.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(9 / 255f, 7 / 255f, 20 / 255f, 0.7f) });
        _band.SetAnchor(Side.Left, 0f);
        _band.SetAnchor(Side.Right, 1f);
        _band.SetOffset(Side.Left, 0);
        _band.SetOffset(Side.Right, 0);
        _band.SetOffset(Side.Top, -3);
        _band.SetOffset(Side.Bottom, Mathf.CeilToInt(titleSize * 1.45f) + 15);
        AddChild(_band);
        _title = Ui.Title(string.Empty, titleSize, null, HorizontalAlignment.Center);
        _title.AnchorRight = 1;
        _title.ClipText = true;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        AddChild(_title);
        _sub = UiScaling.Reading(Ui.Text(string.Empty, UiFonts.Body, UiPalette.Creme, align: HorizontalAlignment.Center));
        _sub.AnchorRight = 1;
        _sub.OffsetTop = Mathf.CeilToInt(titleSize * 1.45f) + 1; // logo abaixo da linha do título (a fonte Cinzel tem ~1,4× de altura de linha)
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

    /// <summary>Largura disponível agora (u da raiz do HUD).</summary>
    public float SpanWidth => _spanWidth;

    /// <summary>
    /// Limita o letreiro ao vão horizontal <paramref name="left"/>–<paramref name="right"/> (coordenadas da raiz do HUD) e
    /// encolhe o texto até caber (mínimo 7 px base); o subtítulo que não cabe numa linha some. Chamado quando o
    /// vão muda (escala, lista de Brechas) e ao tocar o letreiro, nunca por quadro.
    /// </summary>
    public void SetSpan(float left, float right)
    {
        if (Mathf.IsEqualApprox(OffsetLeft, left) && Mathf.IsEqualApprox(OffsetRight, Math.Max(left + 40f, right)))
        {
            return;
        }

        OffsetLeft = left;
        OffsetRight = Math.Max(left + 40f, right);
        _spanWidth = OffsetRight - OffsetLeft;
        FitText();
    }

    /// <summary>Some na hora (outro letreiro vai ocupar o mesmo lugar).</summary>
    public void Stop()
    {
        Visible = false;
        SetProcess(false);
    }

    public void Play(string title, string sub, Color color, float duration, float fadeIn = 2f, float fadeOut = 1.5f, float slide = 0f)
    {
        _title.Text = title;
        _title.AddThemeColorOverride("font_color", color);
        _sub.Text = sub;
        _sub.Visible = sub.Length > 0;
        FitText();
        _t = 0;
        _duration = duration;
        _fadeIn = fadeIn;
        _fadeOut = fadeOut;
        _slide = slide;
        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        SetProcess(true);
    }

    /// <summary>Título e subtítulo numa linha só dentro do vão: encolhe a fonte até o mínimo legível; o que ainda não cabe é cortado (título) ou some (subtítulo).</summary>
    private void FitText()
    {
        var room = _spanWidth - 8f;
        if (_title.Text.Length > 0)
        {
            var font = _title.GetThemeFont("font");
            var size = _titleBase;
            while (size > MinFontSize && font.GetStringSize(_title.Text, HorizontalAlignment.Left, -1, size).X > room)
            {
                size--;
            }

            _title.AddThemeFontSizeOverride("font_size", size);
        }

        if (_sub.Text.Length > 0)
        {
            var font = _sub.GetThemeFont("font");
            var size = UiScaling.ReadingSize(UiFonts.Body); // tamanho de leitura atual (opção "Tamanho do texto")
            while (size > MinFontSize && font.GetStringSize(_sub.Text, HorizontalAlignment.Left, -1, size).X > room)
            {
                size--;
            }

            _sub.AddThemeFontSizeOverride("font_size", size);
            _sub.Visible = font.GetStringSize(_sub.Text, HorizontalAlignment.Left, -1, size).X <= room;
        }

        // A faixa acompanha as linhas que de fato aparecem.
        _band.SetOffset(Side.Bottom, _sub.Visible && _sub.Text.Length > 0 ? _sub.OffsetTop + 14f : _sub.OffsetTop + 3f);
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

/// <summary>
/// Barra do chefe (<c>render.js:293-297</c>): nome em Cinzel, barra magenta com lag. POLI-1: mora na coluna do canto
/// superior direito, logo abaixo do minimapa (a lista de Brechas some dentro da Brecha), e não mais no centro do alto,
/// onde com a interface em 125/150% ela cobria o painel de vida e o relógio.
/// </summary>
public partial class BossBar : VBoxContainer
{
    /// <summary>Largura da barra: cabe na coluna da direita em qualquer escala.</summary>
    public const float BarWidth = 136f;

    private readonly Label _name;
    private readonly UiBar _bar;

    public BossBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
        _name = Ui.Title(string.Empty, 9, null, HorizontalAlignment.Center);
        _name.ClipText = true;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _name.CustomMinimumSize = new Vector2(BarWidth, 0);
        _bar = UiBar.Create("boss", new Vector2(BarWidth, 7), lag: true);
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

/// <summary>
/// Legendas de sons importantes (frente C1, DECISIONS C1-D12): até 3 linhas curtas acima da hotbar, fundo escuro,
/// 2,6 s cada; a mesma legenda de novo só renova a linha. Só aparece com "Legendas de sons" ligada. Linhas fixas
/// reaproveitadas, sem alocar por quadro.
/// </summary>
public partial class CaptionBox : VBoxContainer
{
    private const int MaxLines = 3;
    private const float Life = 2.6f;

    private readonly PanelContainer[] _panels = new PanelContainer[MaxLines];
    private readonly Label[] _labels = new Label[MaxLines];
    private readonly float[] _age = new float[MaxLines];

    public CaptionBox()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Alignment = AlignmentMode.End;
        AddThemeConstantOverride("separation", 1);
        var style = new StyleBoxFlat { BgColor = new Color(0.03f, 0.02f, 0.06f, 0.82f), ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 1, ContentMarginBottom = 1 };
        for (var i = 0; i < MaxLines; i++)
        {
            _labels[i] = UiScaling.Reading(Ui.Bold(string.Empty, UiFonts.Body, UiPalette.Creme, HorizontalAlignment.Center));
            _panels[i] = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter, Visible = false };
            _panels[i].AddThemeStyleboxOverride("panel", style);
            _panels[i].AddChild(_labels[i]);
            AddChild(_panels[i]);
        }

        SetProcess(false);
    }

    /// <summary>Legendas na tela agora (smoke test).</summary>
    public IEnumerable<string> Shown
    {
        get
        {
            for (var i = 0; i < MaxLines; i++)
            {
                if (_panels[i].Visible)
                {
                    yield return _labels[i].Text;
                }
            }
        }
    }

    public void Show(string text)
    {
        var slot = -1;
        for (var i = 0; i < MaxLines; i++)
        {
            if (_panels[i].Visible && _labels[i].Text == text)
            {
                _age[i] = 0;
                return;
            }

            if (slot < 0 && !_panels[i].Visible)
            {
                slot = i;
            }
        }

        if (slot < 0)
        {
            slot = 0;
            for (var i = 1; i < MaxLines; i++)
            {
                if (_age[i] > _age[slot])
                {
                    slot = i;
                }
            }
        }

        _labels[slot].Text = text;
        _age[slot] = 0;
        _panels[slot].Visible = true;
        _panels[slot].Modulate = Colors.White;
        MoveChild(_panels[slot], -1);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var any = false;
        for (var i = 0; i < MaxLines; i++)
        {
            if (!_panels[i].Visible)
            {
                continue;
            }

            _age[i] += (float)delta;
            var a = Mathf.Clamp((Life - _age[i]) * 3f, 0f, 1f);
            _panels[i].Modulate = new Color(1, 1, 1, a);
            _panels[i].Visible = a > 0f;
            any |= _panels[i].Visible;
        }

        SetProcess(any);
    }
}
