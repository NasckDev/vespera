using System.Collections.Generic;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;

namespace Vespera.Game.UI;

/// <summary>
/// Ícones de item: pixel art 64x64 do Arsenal (<c>assets/v2/itens/icones</c>, um por rank e slot) e ícones pintados de material da
/// v0.1 (<c>assets/v2/itens/materiais</c>). Arma e armadura usam o item do catálogo do mesmo slot e rank.
/// </summary>
public static class ItemIcons
{
    public const string FragmentosIcon = "d_vitral";
    public const string LumesIcon = "d_polen";

    private const string GearFolder = "res://assets/v2/itens/icones/";
    private const string MaterialFolder = "res://assets/v2/itens/materiais/";

    private static readonly Dictionary<string, string> SlotFiles = new()
    {
        ["sword"] = "lamina_01",
        ["bow"] = "arco_02",
        ["hammer"] = "martelo_04",
        ["cabeca"] = "cabeca_06",
        ["peito"] = "peito_07",
        ["maos"] = "maos_08",
        ["botas"] = "botas_10",
        ["eco"] = "amuleto_14",
    };

    private static readonly Dictionary<string, Texture2D?> Cache = [];

    /// <summary>Ícone por slot de equipamento ("cabeca", "peito", "maos", "botas"), classe ("sword", "bow", "hammer") ou "eco".</summary>
    public static Texture2D? Gear(string slotOrClass, Rank rank)
    {
        if (!SlotFiles.TryGetValue(slotOrClass, out var file))
        {
            return null;
        }

        return Load($"{GearFolder}{rank.ToString().ToLowerInvariant()}_{file}.png");
    }

    public static Texture2D? Item(ItemInstance item) => Gear(item.Slot, item.Rank);

    public static Texture2D? Echo(EchoDef echo) => Gear("eco", echo.Rank);

    public static Texture2D? Material(HunterMaterialDef material) => Load($"{MaterialFolder}{material.Icon}.png");

    public static Texture2D? Fragmentos => Load($"{MaterialFolder}{FragmentosIcon}.png");

    public static Texture2D? Lumes => Load($"{MaterialFolder}{LumesIcon}.png");

    private static Texture2D? Load(string path)
    {
        if (!Cache.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[path] = texture;
        }

        return texture;
    }
}
