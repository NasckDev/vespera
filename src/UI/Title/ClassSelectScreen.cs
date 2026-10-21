using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Data;
using Vespera.UI.Common;
using Vespera.UI.Screens;
using Vespera.UI.Widgets;

namespace Vespera.UI.Title;

/// <summary>
/// Escolha de classe com nome (<c>classFrame</c>, <c>extra.js:300-324</c>): 5 cartões com sprite animado, dificuldade,
/// descrição e Supremo. Digitar escreve o nome (até 14: letras, números, espaço, apóstrofo e hífen), ←/→ escolhe,
/// Enter/A começa, Esc volta.
/// </summary>
public partial class ClassSelectScreen : Screen
{
    private const float SpriteToggleRate = 1.2f; // extra.js:309

    private readonly List<(SelectableRow Card, IconRect Sprite, string ClassId)> _cards = [];
    private readonly Label _nameLabel;
    private string _name = string.Empty;
    private string _nameShown = string.Empty;
    private string _nameWithCaret = string.Empty;
    private int _selected;
    private double _time;

    public ClassSelectScreen()
    {
        AddChild(new ColorRect { Color = new Color(10 / 255f, 8 / 255f, 18 / 255f, 0.82f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var root = Ui.Column(6);
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(root);

        root.AddChild(Ui.Title(UiText.ClassTitle, UiFonts.Title + 1, null, HorizontalAlignment.Center));
        root.AddChild(Ui.Text(UiText.ClassSubtitle, UiFonts.Small, UiPalette.SubLight, align: HorizontalAlignment.Center));

        _nameLabel = Ui.Text(string.Empty, 9);
        var field = Ui.Panel("InsetPanel", _nameLabel);
        field.CustomMinimumSize = new Vector2(150, 0);
        var nameRow = Ui.Row(5, Ui.Text(UiText.ClassName, UiFonts.Small, UiPalette.Sub), field);
        nameRow.Alignment = BoxContainer.AlignmentMode.Center;
        root.AddChild(nameRow);

        var cards = Ui.Row(6);
        cards.Alignment = BoxContainer.AlignmentMode.Center;
        var db = Database.Db;
        foreach (var cls in db.ClassList)
        {
            cards.AddChild(BuildCard(db, cls));
        }

        root.AddChild(cards);
        var keys = Ui.Row(12, Ui.Hint("← →", "← →", UiText.ClassChoose), Ui.Hint("Enter", "A", UiText.ClassStart), Ui.Hint("Esc", "B", UiText.Back));
        keys.Alignment = BoxContainer.AlignmentMode.Center;
        root.AddChild(keys);
    }

    /// <summary>Jogador confirmou (nome limpo, classe).</summary>
    public Action<string, string>? Started { get; set; }

    public string PlayerName => _name;

    public string SelectedClass => _cards[_selected].ClassId;

    public override bool WasdNavigation => false; // as letras digitam o nome

    protected override Control? DefaultFocus => _cards[_selected].Card;

    /// <summary>Nome inicial (o do save ou "Rui", <c>extra.js:261</c>).</summary>
    public void Setup(string name)
    {
        _name = name;
        _selected = 0;
        RefreshName();
    }

    /// <summary>Seleciona a classe pelo índice (smoke test e mouse).</summary>
    public void Select(int index) => _cards[Math.Clamp(index, 0, _cards.Count - 1)].Card.GrabFocus();

    /// <summary>Digita como o teclado faria (smoke test).</summary>
    public void Type(string text)
    {
        foreach (var ch in text)
        {
            TypeChar(ch);
        }
    }

    public void Erase()
    {
        if (_name.Length > 0)
        {
            _name = _name[..^1];
            RefreshName();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (Stack?.Top != this || !InputReady || @event is not InputEventKey { Pressed: true } key)
        {
            return;
        }

        if (key.Keycode == Key.Backspace)
        {
            Erase();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Unicode >= 32 && key.Keycode is not (Key.Enter or Key.KpEnter) && TypeChar((char)key.Unicode))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _time += delta;
        var blink = ((int)(_time * 2)) % 2 == 1;
        var shown = blink ? _nameWithCaret : _name;
        if (!ReferenceEquals(shown, _nameShown))
        {
            _nameShown = shown;
            _nameLabel.Text = shown;
        }

        var atk = ((int)(_time * SpriteToggleRate)) % 2 == 1;
        for (var i = 0; i < _cards.Count; i++)
        {
            var (card, sprite, id) = _cards[i];
            if (card.HasFocus())
            {
                _selected = i;
            }

            sprite.Key = card.HasFocus() && atk ? AtkKeys[id] : FrontKeys[id];
        }
    }

    private static readonly Dictionary<string, string> FrontKeys = [];
    private static readonly Dictionary<string, string> AtkKeys = [];

    private SelectableRow BuildCard(GameDatabase db, ClassDef cls)
    {
        FrontKeys[cls.Id] = $"c_{cls.Id}_front";
        AtkKeys[cls.Id] = $"c_{cls.Id}_atk";
        var weapon = db.Weapons[cls.Id];
        var ultimate = db.Ultimates.TryGetValue(cls.Id, out var u) ? u : null;

        var sprite = IconRect.Create(FrontKeys[cls.Id], 0);
        sprite.CustomMinimumSize = new Vector2(92, 74);
        sprite.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        var top = new Control { CustomMinimumSize = new Vector2(92, 74), MouseFilter = MouseFilterEnum.Ignore };
        top.AddChild(sprite);
        top.AddChild(IconRect.Create(weapon.Icon, 13));

        var desc = Ui.Text(cls.Desc, UiFonts.Tiny, UiPalette.Body, wrap: true);
        desc.CustomMinimumSize = new Vector2(92, 0);
        var column = Ui.Column(2,
            top,
            Ui.Title(cls.Name, UiFonts.Large, null, HorizontalAlignment.Center),
            Ui.Text(UiText.ClassDifficulty + cls.Difficulty, UiFonts.Tiny, UiPalette.Difficulty(cls.Difficulty), align: HorizontalAlignment.Center),
            Ui.Gap(0, 2),
            desc,
            Ui.Spacer(horizontal: false),
            Ui.Text(UiText.ClassUltimate, UiFonts.Tiny, UiPalette.Sub),
            Ui.Title(ultimate?.Name ?? string.Empty, UiFonts.Small, UiPalette.FromHex(ultimate?.Color, UiPalette.Ambar)));

        var card = new SelectableRow
        {
            NormalStyle = "CardNormal",
            FocusStyle = "CardFocus",
            Marker = false,
            CustomMinimumSize = new Vector2(104, 206),
        };
        card.AddChild(column);
        var classId = cls.Id;
        card.OnActivate = () => Started?.Invoke(CleanName(), classId);
        _cards.Add((card, sprite, cls.Id));
        return card;
    }

    private bool TypeChar(char ch)
    {
        var allowed = char.IsLetterOrDigit(ch) || ch is ' ' or '\'' or '-';
        if (!allowed)
        {
            return false;
        }

        if (_name.Length < Database.Db.Rules.Start.NameMaxLength && !(ch == ' ' && _name.Length == 0))
        {
            _name += ch;
            RefreshName();
        }

        return true;
    }

    private string CleanName() => _name.Trim().Length > 0 ? _name.Trim() : Database.Db.Rules.Start.Name;

    private void RefreshName()
    {
        _nameWithCaret = _name + "|";
        _nameShown = string.Empty;
    }
}
