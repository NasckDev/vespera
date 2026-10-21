using Godot;
using Vespera.UI.Common;

namespace Vespera.UI.Widgets;

/// <summary>
/// Tooltip de item do guia hud-kit (e painel de detalhe do inventário da v2, <c>ui.js:161-172</c>): ícone emoldurado,
/// nome na cor da raridade, tipo, "◆ Raridade", atributos com a diferença para o equipado, classe/Selo mínimo, motivo do
/// bloqueio em vermelho e a descrição.
/// </summary>
[GlobalClass]
public partial class ItemTooltip : OrnatePanel
{
    private static readonly Color Better = new("#7cf0a0");
    private static readonly Color Worse = new("#ff6a7a");

    private readonly IconRect _icon = IconRect.Create(string.Empty, 24);
    private readonly Label _name = Ui.Title(string.Empty, 9);
    private readonly Label _type = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.SubLight);
    private readonly Label _rarity = Ui.Text(string.Empty, UiFonts.Tiny);
    private readonly VBoxContainer _stats = Ui.Column(1);
    private readonly Label _compare = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Dim);
    private readonly HBoxContainer _lockRow;
    private readonly Label _lock = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Locked, wrap: true);
    private readonly Label _desc = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Sub, wrap: true);
    private readonly Label _note = Ui.Text(string.Empty, UiFonts.Tiny, UiPalette.Ambar);

    public ItemTooltip()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(150, 0);
        _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _name.CustomMinimumSize = new Vector2(104, 0);
        _lock.CustomMinimumSize = new Vector2(124, 0);
        _desc.CustomMinimumSize = new Vector2(134, 0);
        var frame = Ui.Panel("SlotPanel", _icon);
        frame.CustomMinimumSize = new Vector2(28, 28);
        frame.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var header = Ui.Row(5, frame, Ui.Column(0, _name, _type, _rarity));
        var lockIcon = new LockGlyph { CustomMinimumSize = new Vector2(7, 7), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        _lockRow = Ui.Row(3, lockIcon, _lock);
        AddChild(Ui.Margin(0, 0, 0, 0, Ui.Column(3, header, Ui.Rule(), _stats, _compare, _lockRow, _desc, _note)));
    }

    public string ItemName => _name.Text;

    public string? LockText => _lockRow.Visible ? _lock.Text : null;

    public void ShowInfo(ItemInfo info)
    {
        _icon.Key = info.Icon;
        _name.Text = info.Name;
        _name.AddThemeColorOverride("font_color", info.RarityColor);
        _type.Text = info.Type;
        _rarity.Text = $"◆ {info.RarityName}";
        _rarity.AddThemeColorOverride("font_color", info.RarityColor);
        Ui.Clear(_stats);
        foreach (var line in info.Stats)
        {
            var value = Ui.Bold(line.Value, UiFonts.Small, UiPalette.Creme, HorizontalAlignment.Right);
            var row = Ui.Row(4, Ui.Text(line.Label, UiFonts.Small, UiPalette.Body), Ui.Spacer(), value);
            if (line.DeltaText is { } delta)
            {
                row.AddChild(Ui.Text(delta, UiFonts.Tiny, line.Delta > 0 ? Better : Worse));
            }

            _stats.AddChild(row);
        }

        _compare.Text = info.CompareWith ?? string.Empty;
        _compare.Visible = info.CompareWith is not null;
        _lock.Text = info.Locked ?? string.Empty;
        _lockRow.Visible = info.Locked is not null;
        _desc.Text = info.Desc;
        _note.Text = info.Note ?? string.Empty;
        _note.Visible = info.Note is not null;
        Visible = true;
        ResetSize();
    }
}

/// <summary>Cadeado pequeno (<c>drawLock</c>, <c>ui.js:259</c>).</summary>
[GlobalClass]
public partial class LockGlyph : Control
{
    public LockGlyph() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => HudKit.HudDraw.Lock(this, Size / 2f, Size.X / 2.4f);
}
