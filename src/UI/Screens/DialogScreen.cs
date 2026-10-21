using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>Uma opção do diálogo (<c>{l, sub, dis, icon, fn}</c> da v2) e, opcionalmente, o item para o tooltip.</summary>
public sealed record DialogChoice(string Label, string? Sub = null, Action? Run = null, bool Disabled = false, string? Icon = null)
{
    /// <summary>Mostra o tooltip deste item ao focar a opção.</summary>
    public Func<ItemInfo>? Tooltip { get; init; }
}

/// <summary>
/// Caixa de diálogo da v2 (<c>uiDialog</c>, <c>ui.js:17-52</c>; guia hud-kit "janela de diálogo"): retrato e nome do
/// NPC, máquina de escrever a 75 car/s, "▸" pulsante, e opções num painel acima à direita (com ícone, sub-rótulo,
/// desabilitadas tocam <c>hurt</c>). Confirmar (Enter/E/J/A/clique) completa o texto e depois avança; Esc fecha.
/// Uma opção que abre outro diálogo encadeia (lojas); as outras fecham. Lojas e forja usam só este componente.
/// </summary>
public partial class DialogScreen : Screen
{
    public const float CharsPerSecond = 75f; // ui.js:30
    private const double ReadyDelay = 0.18; // ui.js:31
    private const float BoxHeight = 96f; // cabe o retrato 58×70
    private const float BoxHeightNoPortrait = 66f; // sem retrato (placas, portal, oficinas) a caixa não sobra vazia

    private static readonly Color RowText = new("#ddd2e4");
    private static readonly Color SubDisabled = new("#5d5366");

    private readonly OrnatePanel _box;
    private readonly PanelContainer _portraitFrame;
    private readonly IconRect _portrait;
    private readonly PanelContainer _nameTab;
    private readonly Label _name;
    private readonly Label _text;
    private readonly Label _more;
    private readonly OrnatePanel _choicesPanel;
    private readonly ScrollContainer _scroll;
    private readonly VBoxContainer _choices;
    private readonly ItemTooltip _tooltip;
    private readonly List<SelectableRow> _rows = [];
    private IReadOnlyList<DialogChoice>? _options;
    private Action? _next;
    private Action? _cancel;
    private int _generation;
    private double _typing;
    private int _total;
    private bool _choicesShown;

    public DialogScreen()
    {
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.35f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });

        _box = Ui.Ornate();
        _box.AnchorLeft = 0.5f;
        _box.AnchorRight = 0.5f;
        _box.AnchorTop = 1;
        _box.AnchorBottom = 1;
        _box.OffsetLeft = -230;
        _box.OffsetRight = 230;
        _box.OffsetTop = -8 - BoxHeight;
        _box.OffsetBottom = -8;
        _box.GrowVertical = GrowDirection.Begin; // texto longo cresce para cima, nunca para fora da tela
        AddChild(_box);

        _portrait = IconRect.Create(string.Empty, 0);
        _portrait.CustomMinimumSize = new Vector2(58, 70);
        _portraitFrame = Ui.Panel("SlotPanel", _portrait);
        _portraitFrame.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _name = Ui.Title(string.Empty, 9, UiPalette.Ambar);
        _nameTab = Ui.Panel("TabFocus", _name);
        _nameTab.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _text = Ui.Text(string.Empty, UiFonts.Body, UiPalette.Creme, wrap: true);
        _text.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        _text.SizeFlagsVertical = SizeFlags.ExpandFill;
        _text.CustomMinimumSize = new Vector2(330, 0);
        _more = Ui.Text("▾", UiFonts.Body, UiPalette.Ambar, align: HorizontalAlignment.Right);
        var textColumn = Ui.Column(4, _nameTab, _text, _more);
        textColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _box.AddChild(Ui.Row(10, _portraitFrame, textColumn));

        _choices = Ui.Column(1);
        _choices.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _scroll.AddChild(_choices);
        _choicesPanel = Ui.Ornate(_scroll);
        _choicesPanel.AnchorLeft = 0.5f;
        _choicesPanel.AnchorRight = 0.5f;
        _choicesPanel.AnchorTop = 1;
        _choicesPanel.AnchorBottom = 1;
        _choicesPanel.OffsetRight = 230;
        _choicesPanel.OffsetLeft = 230 - 262;
        _choicesPanel.OffsetBottom = -110;
        _choicesPanel.GrowVertical = GrowDirection.Begin;
        _choicesPanel.GrowHorizontal = GrowDirection.Begin;
        _choicesPanel.Visible = false;
        AddChild(_choicesPanel);

        _tooltip = new ItemTooltip { Visible = false };
        AddChild(_tooltip);
    }

    /// <summary>Quem fala agora (smoke test).</summary>
    public string Speaker => _name.Text;

    /// <summary>Texto inteiro da fala atual.</summary>
    public string Text => _text.Text;

    public bool IsTyping => _text.VisibleCharacters >= 0;

    /// <summary>Quantos caracteres faltam aparecer (0 se completo).</summary>
    public int TypingRemaining => IsTyping ? _total - _text.VisibleCharacters : 0;

    public bool ChoicesVisible => _choicesShown;

    public IReadOnlyList<DialogChoice> Options => _options ?? [];

    public IReadOnlyList<SelectableRow> Rows => _rows;

    public ItemTooltip Tooltip => _tooltip;

    /// <summary>Retângulo do painel de opções na tela base 640×360 (smoke test).</summary>
    public Rect2 ChoicesRect => _choicesPanel.GetGlobalRect();

    public override bool WasdNavigation => _choicesShown;

    protected override Control? DefaultFocus => _choicesShown && _rows.Count > 0 ? _rows[0] : null;

    /// <summary>
    /// Mostra uma fala. Sem opções: confirmar fecha e chama <paramref name="next"/>. Com opções: aparecem quando o
    /// texto termina. <paramref name="cancel"/> é chamado se o jogador fechar com Esc.
    /// </summary>
    public void Show(string speaker, string text, IReadOnlyList<DialogChoice>? choices = null, Action? next = null, Action? cancel = null)
    {
        _generation++;
        _options = choices is { Count: > 0 } ? choices : null;
        _next = next;
        _cancel = cancel;
        _name.Text = speaker;
        var portrait = Database.Db.Npcs.Portraits.TryGetValue(speaker, out var key) ? key : null;
        _portrait.Key = portrait ?? string.Empty;
        _portraitFrame.Visible = portrait is not null;
        _box.OffsetTop = _box.OffsetBottom - (portrait is null ? BoxHeightNoPortrait : BoxHeight);
        _text.Text = text;
        _total = _text.GetTotalCharacterCount(); // caracteres depois do shaping (o que VisibleCharacters conta)
        _typing = 0;
        _text.VisibleCharacters = _total > 0 ? 0 : -1;
        _more.Visible = false;
        HideChoices();
        RestartAge(); // cada fala espera 0,18 s antes de aceitar confirmação (ui.js:31)
        UiSound(); // dialog() toca "ui" (world.js:204)
    }

    /// <summary>Confirma: completa o texto ou avança/fecha (o mesmo que Enter).</summary>
    public void Advance()
    {
        if (!InputReadyForText)
        {
            return;
        }

        if (IsTyping)
        {
            CompleteText();
            return;
        }

        if (_options is null)
        {
            var next = _next;
            var generation = _generation;
            UiSound();
            next?.Invoke();
            if (generation == _generation)
            {
                Close();
            }
        }
    }

    /// <summary>Escolhe a opção pelo índice (mouse/teclado/gamepad e smoke test).</summary>
    public void Choose(int index)
    {
        if (_options is null || index < 0 || index >= _options.Count || !_choicesShown)
        {
            return;
        }

        var option = _options[index];
        if (option.Disabled)
        {
            Common.UiSound.Denied();
            return;
        }

        var generation = _generation;
        option.Run?.Invoke();
        if (generation == _generation && IsOpen)
        {
            Close(); // a opção não abriu outra fala: fecha (ui.js:51)
        }
    }

    public override bool HandleInput(InputEvent @event)
    {
        if (@event.IsEcho())
        {
            return false;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (!InputReady)
            {
                return true;
            }

            if (_options is null && !IsTyping)
            {
                Advance(); // Esc também avança fala sem opções (ui.js:33)
                return true;
            }

            var cancel = _cancel;
            UiSound();
            Close();
            cancel?.Invoke();
            return true;
        }

        if (IsConfirm(@event))
        {
            if (IsTyping || _options is null)
            {
                Advance();
            }

            return true;
        }

        return false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
            if (IsTyping || _options is null)
            {
                Advance();
            }
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (IsTyping)
        {
            _typing += delta;
            var shown = (int)(_typing * CharsPerSecond);
            if (shown >= _total)
            {
                CompleteText();
            }
            else
            {
                _text.VisibleCharacters = shown;
            }
        }
        else if (_more.Visible)
        {
            _more.Modulate = new Color(1, 1, 1, 0.55f + (0.45f * Mathf.Sin((float)Age * 6f)));
        }
    }

    protected override void OnClosed()
    {
        HideChoices();
        _tooltip.Visible = false;
    }

    private bool InputReadyForText => Age >= ReadyDelay;

    private static bool IsConfirm(InputEvent e) =>
        e.IsActionPressed("ui_accept") || e.IsActionPressed(InputActions.Interact) || e.IsActionPressed(InputActions.Attack);

    private void CompleteText()
    {
        _text.VisibleCharacters = -1;
        if (_options is not null)
        {
            ShowChoices();
        }
        else
        {
            _more.Visible = true;
        }
    }

    private void ShowChoices()
    {
        if (_choicesShown || _options is null)
        {
            return;
        }

        _choicesShown = true;
        Ui.Clear(_choices);
        _rows.Clear();
        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            var index = i;
            var label = Ui.Text(option.Label, UiFonts.Body, option.Disabled ? UiPalette.Disabled : RowText);
            var column = Ui.Column(0, label);
            if (option.Sub is { } sub)
            {
                column.AddChild(Ui.Text(sub, UiFonts.Tiny, option.Disabled ? SubDisabled : UiPalette.Sub));
            }

            var content = Ui.Row(4);
            if (option.Icon is { } icon)
            {
                content.AddChild(IconRect.Create(icon, 12, option.Disabled ? 0.4f : 1f));
            }

            content.AddChild(column);
            var row = new SelectableRow
            {
                Disabled = option.Disabled,
                ClickSound = !option.Disabled,
                CustomMinimumSize = new Vector2(0, option.Sub is null ? 15 : 20),
                OnActivate = () => Choose(index),
                OnFocused = () => Focused(index),
            };
            row.AddChild(content);
            _choices.AddChild(row);
            _rows.Add(row);
        }

        // Altura: até caber acima da caixa (a lista rola com o foco).
        var wanted = 0f;
        foreach (var row in _rows)
        {
            wanted += row.CustomMinimumSize.Y + 1;
        }

        _scroll.CustomMinimumSize = new Vector2(246, Mathf.Min(wanted + 2, 196));
        _choicesPanel.Visible = true;
        // Encolhe até o mínimo a partir da base: ResetSize() manteria o topo da lista anterior (mais longa) e a
        // lista nova subiria para fora da tela.
        _choicesPanel.OffsetBottom = _box.Position.Y - Size.Y - 6;
        _choicesPanel.OffsetTop = _choicesPanel.OffsetBottom - _choicesPanel.GetCombinedMinimumSize().Y;
        _rows[0].GrabFocus();
    }

    private void Focused(int index)
    {
        foreach (var row in _rows)
        {
            if (row.GetChild(0) is HBoxContainer content && content.GetChild(content.GetChildCount() - 1) is VBoxContainer col && col.GetChild(0) is Label l)
            {
                l.AddThemeColorOverride("font_color", row.Disabled ? UiPalette.Disabled : row.HasFocus() ? UiPalette.Creme : RowText);
            }
        }

        if (_options is { } options && options[index].Tooltip is { } tooltip)
        {
            _tooltip.ShowInfo(tooltip());
            var panel = _choicesPanel.GetGlobalRect();
            var size = _tooltip.GetCombinedMinimumSize();
            _tooltip.Position = new Vector2(Mathf.Max(4, panel.Position.X - size.X - 6), Mathf.Clamp(panel.Position.Y, 4, 360 - size.Y - 112));
        }
        else
        {
            _tooltip.Visible = false;
        }
    }

    private void HideChoices()
    {
        _choicesShown = false;
        _choicesPanel.Visible = false;
        _tooltip.Visible = false;
        Ui.Clear(_choices);
        _rows.Clear();
    }
}
