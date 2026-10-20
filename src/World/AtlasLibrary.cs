using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Atlas;

namespace Vespera.World;

/// <summary>Recorte pronto para desenhar: textura (AtlasTexture ou PNG solto), pivô em px da textura e densidade (px por u).</summary>
public sealed record SpriteFrameInfo(Texture2D Texture, Vector2 Anchor, float Density);

/// <summary>
/// Acesso aos sprites da v2 por nome: primeiro o atlas (<c>assets/sprites/atlas</c>, spec 01 §4.1), depois os PNG
/// soltos de <c>assets/sprites/enemies</c> e <c>assets/sprites/items</c> (DECISIONS D10: pivô = centro da base do
/// recorte não transparente; densidade do <c>meta.json</c>). Cada recorte vira uma textura compartilhada, em cache.
/// </summary>
public static class AtlasLibrary
{
    public const string AtlasJsonPath = "res://assets/sprites/atlas/atlas.json";
    public const string AtlasTexturePath = "res://assets/sprites/atlas/atlas.png";
    public const string LooseMetaPath = "res://assets/sprites/enemies/meta.json";

    private static readonly string[] LooseDirs = ["res://assets/sprites/enemies/", "res://assets/sprites/items/"];
    private static readonly Dictionary<string, SpriteFrameInfo?> Cache = [];
    private static AtlasIndex? _index;
    private static Texture2D? _atlas;
    private static IReadOnlyDictionary<string, double>? _looseDensities;

    public static AtlasIndex Index => _index ??= AtlasIndex.Parse(FileAccess.GetFileAsString(AtlasJsonPath));

    private static Texture2D Atlas => _atlas ??= GD.Load<Texture2D>(AtlasTexturePath);

    private static IReadOnlyDictionary<string, double> LooseDensities =>
        _looseDensities ??= AtlasIndex.ParseLooseDensities(FileAccess.GetFileAsString(LooseMetaPath));

    /// <summary>Recorte pelo nome (ex.: <c>c_sword_front</c>, <c>shadow</c>, <c>cnev_f</c>). Falso se não existir.</summary>
    public static bool TryGet(string key, out SpriteFrameInfo frame)
    {
        if (!Cache.TryGetValue(key, out var cached))
        {
            cached = Index.TryGet(key, out var f) ? FromAtlas(f) : FromLoose(key);
            Cache[key] = cached;
        }

        frame = cached!;
        return cached is not null;
    }

    private static SpriteFrameInfo FromAtlas(AtlasFrame f) => new(
        new AtlasTexture { Atlas = Atlas, Region = new Rect2(f.X, f.Y, f.Width, f.Height) },
        new Vector2((float)f.AnchorX, (float)f.AnchorY),
        (float)f.Density);

    private static SpriteFrameInfo? FromLoose(string key)
    {
        foreach (var dir in LooseDirs)
        {
            var path = $"{dir}{key}.png";
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            var texture = GD.Load<Texture2D>(path);
            var used = texture.GetImage() is { } image ? image.GetUsedRect() : new Rect2I(Vector2I.Zero, (Vector2I)texture.GetSize());
            var (ax, ay) = AtlasIndex.BaseCenterAnchor(used.Position.X, used.Position.Y, used.Size.X, used.Size.Y);
            var density = LooseDensities.TryGetValue(key, out var k) ? (float)k : 1f;
            return new SpriteFrameInfo(texture, new Vector2((float)ax, (float)ay), density);
        }

        Log.Warning($"Sprite '{key}' não existe no atlas nem em sprites soltos.");
        return null;
    }
}
