using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Town;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Mural de Contratos da Guilda (<c>uiBoard</c>, <c>ui.js:55-79</c>): até 5 Brechas (mais recentes primeiro) com selo,
/// "bairro — título", estação e modificador, situação, e Enter para rastrear; ao lado, o contrato atual do Diário
/// (missão de <c>QuestRules</c>). Sem o timer da v2: a Cidade não publica o tempo restante (DECISIONS B3-D40).
/// </summary>
public partial class BoardScreen : Screen
{
    private readonly OrnatePanel _panel;
    private readonly Label _subtitle;
    private readonly VBoxContainer _list;
    private readonly Label _closed;
    private readonly Label _questName;
    private readonly Label _questDesc;
    private readonly Label _questProgress;
    private readonly Label _questReward;
    private readonly Control _focusAnchor;
    private readonly List<(SelectableRow Row, BrechaInfo Brecha)> _rows = [];

    public BoardScreen()
    {
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.7f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = _panel = Ui.Ornate();
        panel.CustomMinimumSize = new Vector2(440, 0); // altura do conteúdo: com poucas Brechas não sobra painel vazio
        center.AddChild(panel);

        _subtitle = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Sub, wrap: true);
        _list = Ui.Column(4);
        _list.CustomMinimumSize = new Vector2(275, 0);

        _questName = Ui.Title(string.Empty, UiFonts.Body, UiPalette.Ambar);
        _questName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _questDesc = Ui.Text(string.Empty, UiFonts.Small, UiPalette.Body, wrap: true);
        _questProgress = Ui.Bold(string.Empty, UiFonts.Small, UiPalette.Creme);
        _questReward = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Done, wrap: true);
        var quest = Ui.Panel("InsetPanel", Ui.Column(4,
            Ui.Row(4, IconRect.Create("ic_scroll", 10), Ui.Text(UiText.BoardContract, UiFonts.Tiny, UiPalette.Sub)),
            _questName, _questDesc, _questProgress, Ui.Rule(), _questReward));
        quest.CustomMinimumSize = new Vector2(130, 0);
        quest.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        _closed = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Sub);
        var footer = Ui.Row(8, _closed, Ui.Spacer(), Ui.Hint("Enter", "A", UiText.BoardTrack), Ui.Hint("Esc", "B", UiText.Close));
        var column = Ui.Column(5,
            Ui.Title(UiText.BoardTitle, UiFonts.Heading),
            _subtitle,
            Ui.Rule(),
            Ui.Row(10, _list, quest),
            footer);
        column.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.AddChild(Ui.Margin(6, 2, 6, 2, column));

        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    /// <summary>Tamanho que o mural precisa (u): a escala da interface para antes de ele sair da janela (POLI-2).</summary>
    public override Vector2 DesignSize => _panel.GetCombinedMinimumSize() + new Vector2(16, 16);

    public int Count => _rows.Count;

    public override bool HidesHud => true;

    protected override Control? DefaultFocus => _rows.Count > 0 ? _rows[0].Row : _focusAnchor;

    public override bool HandleInput(InputEvent @event)
    {
        // Tab também fecha (ui.js:78).
        if (InputReady && !@event.IsEcho() && @event.IsActionPressed(InputActions.Inventory))
        {
            UiSound();
            Close();
            return true;
        }

        return base.HandleInput(@event);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (InputReady && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Close(); // clique fora do painel fecha
        }
    }

    protected override void OnOpened() => Refresh();

    private void Refresh()
    {
        var state = GameManager.Instance.State;
        var tracker = BrechaTracker.Current;
        _subtitle.Text = UiText.BoardSubtitle + ((tracker?.Invasion ?? state.Invasion) ? UiText.BoardInvasion : string.Empty);
        _closed.Text = UiText.BoardClosedCount.Replace("{n}", state.Save.BrechasClosed.ToString(), StringComparison.Ordinal);

        Ui.Clear(_list);
        _rows.Clear();
        var brechas = tracker?.All ?? [];
        if (brechas.Count == 0)
        {
            _list.AddChild(Ui.Text(UiText.BoardEmpty, UiFonts.Body, UiPalette.SubLight, wrap: true));
        }

        foreach (var brecha in brechas)
        {
            AddCard(tracker!, brecha);
        }

        var quest = state.CurrentQuest;
        _questName.Text = quest?.Name ?? string.Empty;
        _questDesc.Text = KeyText.Expand(quest?.Desc ?? string.Empty);
        _questProgress.Text = quest is null ? string.Empty : state.QuestRules.ProgressText(quest, state.Save) ?? string.Empty;
        _questReward.Text = quest is null ? string.Empty : state.QuestRules.RewardText(quest);
    }

    private void AddCard(BrechaTracker tracker, BrechaInfo brecha)
    {
        var db = Database.Db;
        var open = brecha.Status == BrechaStatus.Open;
        var ruptured = brecha.Status == BrechaStatus.Ruptured;
        var active = open || ruptured;
        var rank = db.Ranks[Math.Clamp(brecha.Rank, 0, db.Ranks.Count - 1)];
        var seasonName = brecha.SeasonId;
        var mod = string.Empty;
        foreach (var s in db.SeasonData.Seasons)
        {
            if (s.Id == brecha.SeasonId)
            {
                seasonName = s.Name;
                mod = s.ModText;
            }
        }

        var status = open ? UiText.BoardOpen + (tracker.TrackedId == brecha.Id ? UiText.BoardTracked : string.Empty) : ruptured ? UiText.BoardRuptured : UiText.BoardClosed;
        var statusColor = ruptured ? UiPalette.Brecha : open ? UiPalette.Ambar : UiPalette.Disabled;
        var text = Ui.Column(1,
            Ui.Title($"{BrechaTracker.Bairro(brecha.ThemeId)} — {BrechaTracker.Title(brecha)}", UiFonts.Body, active ? UiPalette.Creme : UiPalette.Dim),
            Ui.Text($"{seasonName} · {mod}", UiFonts.Tiny, UiPalette.Sub),
            Ui.Text(status, UiFonts.Tiny, statusColor));
        var card = new SelectableRow
        {
            NormalStyle = ruptured ? "SoftPanelDanger" : tracker.TrackedId == brecha.Id ? "OrnatePanelAmber" : "SoftPanel",
            FocusStyle = "CardFocus",
            Marker = false,
            CustomMinimumSize = new Vector2(0, 34),
        };
        card.AddChild(Ui.Row(6, IconRect.Create(rank.Icon, 20, active ? 1f : 0.4f), text));
        card.OnActivate = () =>
        {
            if (brecha.Status == BrechaStatus.Open)
            {
                tracker.TrackedId = brecha.Id; // G.track
                Hud.Current?.RefreshBrechas();
                Common.UiSound.Open();
                Refresh();
                FocusDefault();
            }
        };
        _list.AddChild(card);
        _rows.Add((card, brecha));
    }
}
