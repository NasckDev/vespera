using Godot;
using Vespera.World;

namespace Vespera.UI.Common;

/// <summary>
/// Ícones da UI por chave do atlas da v2 (<c>i_coin</c>, <c>r_C</c>, <c>eq_hood</c>, <c>d_halo</c>…), via
/// <see cref="AtlasLibrary"/>. Também recorta a cabeça de um sprite de personagem para retratos.
/// </summary>
public static class UiIcons
{
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
