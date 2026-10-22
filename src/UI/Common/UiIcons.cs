using Godot;
using Vespera.World;

namespace Vespera.UI.Common;

/// <summary>
/// Ícones da UI por chave do atlas da v2 (<c>i_coin</c>, <c>r_C</c>, <c>eq_hood</c>, <c>d_halo</c>…), via
/// <see cref="AtlasLibrary"/>. Também recorta a cabeça de um sprite de personagem para retratos.
/// </summary>
public static class UiIcons
{
    public const string BustDir = "res://assets/sprites/npcs/portraits/";
    public const string HudDir = "res://assets/sprites/ui/hud/";

    private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> Loose = [];

    /// <summary>
    /// Busto neutro do designer (pacote v2, issue #6; DECISIONS C2-D6) para a chave de retrato do atlas
    /// (<c>npc_odete_f</c> → <c>npcs/portraits/odete.png</c>), ou null se o NPC não tem busto.
    /// </summary>
    public static Texture2D? Bust(string? portraitKey)
    {
        if (portraitKey is not { Length: > 6 } key || !key.StartsWith("npc_", System.StringComparison.Ordinal))
        {
            return null;
        }

        var cut = key.LastIndexOf('_');
        return cut > 4 ? LoadLoose($"{BustDir}{key[4..cut]}.png") : null;
    }

    /// <summary>Ícone do HUD do designer (<c>ui/hud/&lt;id&gt;.png</c>, 29 do v1 + 2 do v4, com a moldura do slot), ou null.</summary>
    public static Texture2D? Hud(string id) => LoadLoose($"{HudDir}{id}.png");

    /// <summary>Miolo do ícone do HUD (sem os 8 px da moldura de slot), para botões redondos.</summary>
    public static Texture2D? HudInner(string id)
    {
        if (Hud(id) is not { } texture)
        {
            return null;
        }

        var size = texture.GetSize();
        var inset = Mathf.Round(size.X * 0.125f);
        return new AtlasTexture { Atlas = texture, Region = new Rect2(inset, inset, size.X - (2 * inset), size.Y - (2 * inset)) };
    }

    private static Texture2D? LoadLoose(string path)
    {
        if (!Loose.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Loose[path] = texture;
        }

        return texture;
    }

    /// <summary>Textura do recorte, ou null se a chave não existe.</summary>
    public static Texture2D? Get(string? key) =>
        key is { Length: > 0 } && AtlasLibrary.TryGet(key, out var frame) ? frame.Texture : null;

    /// <summary>
    /// Parte de cima (cabeça e ombros) de um sprite de personagem, para molduras de retrato do HUD.
    /// <paramref name="fraction"/> = fração da altura que fica (a partir do topo). Cria um recorte novo a cada chamada
    /// (só ao trocar de classe/save), sem cache estático.
    /// </summary>
    public static Texture2D? Head(string key, float fraction = 0.5f)
    {
        if (!AtlasLibrary.TryGet(key, out var frame) || frame.Texture is not AtlasTexture atlas)
        {
            return null;
        }

        var region = atlas.Region;
        var height = Mathf.Round(region.Size.Y * fraction);
        var width = Mathf.Min(region.Size.X, height);
        var x = region.Position.X + ((region.Size.X - width) / 2f);
        return new AtlasTexture { Atlas = atlas.Atlas, Region = new Rect2(x, region.Position.Y, width, height) };
    }
}
