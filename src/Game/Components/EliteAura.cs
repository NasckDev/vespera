using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Components;

/// <summary>Anel animado em volta de um elite com modificador (blindado, veloz, faminto, gêmeo): 6 quadros em loop.</summary>
public static class EliteAura
{
    private const int FrameCount = 6;
    private const float Fps = 9f;
    private const float RingToBody = 1.3f;
    private const float Opacity = 0.85f;

    private static readonly Dictionary<string, SpriteFrames> Cache = [];

    public static void ClearCache() => Cache.Clear();

    /// <summary>Cria o anel para o modificador, ou null se não houver arte dele. O anel tem diâmetro (altura) de ~1,3x o corpo.</summary>
    public static AnimatedSprite2D? Make(string modifier, float bodySize)
    {
        var frames = Load(modifier);
        if (frames is null)
        {
            return null;
        }

        var cell = frames.GetFrameTexture("aura", 0).GetSize();
        var scale = bodySize * RingToBody / cell.Y;
        var sprite = new AnimatedSprite2D
        {
            Name = "EliteAura",
            SpriteFrames = frames,
            Scale = Vector2.One * scale,
            Modulate = new Color(1f, 1f, 1f, Opacity),
            ZIndex = 1,
        };
        sprite.Play("aura");
        return sprite;
    }

    private static SpriteFrames? Load(string modifier)
    {
        if (Cache.TryGetValue(modifier, out var cached))
        {
            return cached;
        }

        var path = $"res://assets/v2/efeitos/fx_elite_{modifier}.png";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        var texture = GD.Load<Texture2D>(path);
        var cellWidth = texture.GetWidth() / FrameCount;
        var frames = new SpriteFrames();
        frames.RenameAnimation("default", "aura");
        frames.SetAnimationSpeed("aura", Fps);
        for (var i = 0; i < FrameCount; i++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(i * cellWidth, 0, cellWidth, texture.GetHeight()),
            };
            frames.AddFrame("aura", atlas);
        }

        Cache[modifier] = frames;
        return frames;
    }
}
