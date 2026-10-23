using Godot;
using Vespera.Core.Ranks;

namespace Vespera.Game.UI;

/// <summary>
/// Espaço de item: moldura pintada do kit de interface (<c>slot_normal</c> / <c>slot_selecionado</c>), o ícone em escala inteira com
/// filtro nearest, contorno e brilho na cor do rank e um selo pequeno do rank no canto. Sem ícone, mostra só a moldura (slot vazio).
/// </summary>
public partial class ItemIconSlot : Control
{
    private const int Pad = 10;
    private const int MiniSeal = 30;

    private readonly Texture2D? _icon;
    private readonly Rank? _rank;
    private readonly bool _ghost;
    private readonly RankBadge? _seal;
    private bool _selected;

    /// <param name="size">Lado do espaço em pixels.</param>
    /// <param name="icon">Ícone (null = só moldura).</param>
    /// <param name="rank">Rank do item (cor do contorno e selo); null = sem destaque.</param>
    /// <param name="ghost">Ícone apagado (dica de qual peça cabe no slot vazio).</param>
    public ItemIconSlot(int size, Texture2D? icon, Rank? rank, bool ghost = false)
    {
        _icon = icon;
        _rank = rank;
        _ghost = ghost;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        if (rank is { } r && !ghost && icon is not null)
        {
            var seal = size >= 140 ? MiniSeal + 12 : MiniSeal;
            _seal = new RankBadge(r, seal) { Position = new Vector2(size - seal - 2, size - seal - 2) };
            AddChild(_seal);
        }
    }

    public Texture2D? Icon => _icon;

    public Rank? ItemRank => _rank;

    public bool IsGhost => _ghost;

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var frame = UiAssets.SlotStyle(_selected);
        DrawStyleBox(frame, rect);

        var color = _rank is { } r && !_ghost ? RankColors.Of(r) : Palette.CreamDim;
        if (_icon is null)
        {
            return;
        }

        var inner = Mathf.Min(Size.X, Size.Y) - (Pad * 2);
        var scale = Mathf.Max(1, Mathf.FloorToInt(inner / Mathf.Max(_icon.GetWidth(), _icon.GetHeight())));
        var drawSize = new Vector2(_icon.GetWidth(), _icon.GetHeight()) * scale;
        var origin = (Size - drawSize) / 2f;
        if (_rank is not null && !_ghost)
        {
            DrawRect(rect.Grow(-4), new Color(color, _selected ? 0.9f : 0.6f), filled: false, width: 2f);
            DrawRect(new Rect2(origin, drawSize).Grow(2), new Color(color, 0.1f));
        }

        DrawTextureRect(_icon, new Rect2(origin, drawSize), false, _ghost ? new Color(0.5f, 0.55f, 0.7f, 0.22f) : Colors.White);
    }
}
