using Godot;
using Vespera.UI.Common;

namespace Vespera.UI.Widgets;

/// <summary>
/// Ícone do atlas da v2 por chave (<c>i_coin</c>, <c>r_C</c>…), centralizado e proporcional. Os recortes do atlas
/// são grandes e reduzidos na UI, então usa filtro Linear com mipmaps (DECISIONS D3/D31) para ficar nítido.
/// </summary>
[GlobalClass]
public partial class IconRect : TextureRect
{
    private string _key = string.Empty;

    [Export]
    public string Key
    {
        get => _key;
        set
        {
            if (_key == value)
            {
                return;
            }

            _key = value;
            Texture = UiIcons.Get(value);
        }
    }

    public IconRect()
    {
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public static IconRect Create(string key, float size, float alpha = 1f) => new()
    {
        Key = key,
        CustomMinimumSize = new Vector2(size, size),
        Modulate = new Color(1, 1, 1, alpha),
    };
}
