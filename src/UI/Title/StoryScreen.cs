using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Screens;
using Vespera.UI.Widgets;

namespace Vespera.UI.Title;

/// <summary>
/// História de Lumeira antes da escolha de classe (<c>drawStory</c>, <c>main.js:150-171</c>): 5 capítulos com o mapa
/// da cidade à esquerda. Enter/Espaço/E/→/D/clique avança, ←/A volta, Esc pula.
/// </summary>
public partial class StoryScreen : Screen
{
    private const string MapPath = "res://assets/sprites/ui/lumeira_map.png";

    private readonly Label _chapter;
    private readonly Label _title;
    private readonly Label _text;
    private readonly Label _nextLabel;
    private readonly List<Diamond> _dots = [];
    private readonly Control _focusAnchor;
    private int _page;

    public StoryScreen()
    {
        AddChild(new ColorRect { Color = new Color(10 / 255f, 8 / 255f, 18 / 255f, 0.82f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = Ui.Ornate();
        panel.CustomMinimumSize = new Vector2(490, 250);
        center.AddChild(panel);

        var map = new TextureRect
        {
            Texture = GD.Load<Texture2D>(MapPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            CustomMinimumSize = new Vector2(226, 196),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var mapFrame = Ui.Panel("InsetPanel", map);
        mapFrame.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _chapter = Ui.Text(string.Empty, UiFonts.Small, UiPalette.Sub);
        _title = Ui.Title(string.Empty, UiFonts.Title);
        _text = Ui.Text(string.Empty, UiFonts.Body, UiPalette.Body, wrap: true);
        _text.CustomMinimumSize = new Vector2(220, 0);
        var rule = Ui.Row(0, Ui.Rule(60, 1f), Diamond.Create(5, UiPalette.Gold));

        var dots = Ui.Row(5);
        for (var i = 0; i < UiText.Story.Length; i++)
        {
            var dot = Diamond.Create(5, UiPalette.Disabled);
            dots.AddChild(dot);
            _dots.Add(dot);
        }

        _nextLabel = Ui.Text(UiText.StoryNext, UiFonts.Small);
        var keys = Ui.Column(3, Ui.Row(3, KeyCap.Create("Enter", "A"), _nextLabel), Ui.Hint("Esc", "B", UiText.StorySkip, UiFonts.Tiny, UiPalette.Sub));
        var footer = Ui.Row(6, dots, Ui.Spacer(), keys);

        var right = Ui.Column(6, _chapter, _title, rule, _text, Ui.Spacer(horizontal: false), footer);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(Ui.Margin(4, 4, 6, 2, Ui.Row(14, mapFrame, right)));

        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    /// <summary>Chamado ao terminar (último capítulo ou Esc).</summary>
    public Action? Finished { get; set; }

    public int Page => _page;

    public override bool WasdNavigation => false;

    public override bool CloseOnCancel => false;

    protected override Control? DefaultFocus => _focusAnchor;

    public void Next()
    {
        UiSound();
        if (_page < UiText.Story.Length - 1)
        {
            ShowPage(_page + 1);
        }
        else
        {
            Finish();
        }
    }

    public override bool HandleInput(InputEvent @event)
    {
        if (!InputReady || @event.IsEcho())
        {
            return false;
        }

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed("ui_right")
            || @event.IsActionPressed(InputActions.MoveRight))
        {
            Next();
            return true;
        }

        if ((@event.IsActionPressed("ui_left") || @event.IsActionPressed(InputActions.MoveLeft)) && _page > 0)
        {
            UiSound();
            ShowPage(_page - 1);
            return true;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            UiSound();
            Finish();
            return true;
        }

        return false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (InputReady && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
            Next();
        }
    }

    protected override void OnOpened() => ShowPage(0);

    private void Finish()
    {
        Close();
        Finished?.Invoke();
    }

    private void ShowPage(int page)
    {
        _page = page;
        var (title, text) = UiText.Story[page];
        _chapter.Text = UiText.StoryChapter.Replace("{n}", (page + 1).ToString(), StringComparison.Ordinal)
            .Replace("{total}", UiText.Story.Length.ToString(), StringComparison.Ordinal);
        _title.Text = title;
        _text.Text = text;
        _nextLabel.Text = page < UiText.Story.Length - 1 ? UiText.StoryNext : UiText.StoryLast;
        for (var i = 0; i < _dots.Count; i++)
        {
            _dots[i].Color = i == page ? UiPalette.Ambar : UiPalette.Disabled;
        }
    }
}
