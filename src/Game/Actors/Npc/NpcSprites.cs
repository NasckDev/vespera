using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Actors;

/// <summary>
/// Monta as <see cref="SpriteFrames"/> de um NPC a partir das tiras importadas por tools/art/import_npcs.py
/// (células 192x192, pé em (96,176); a folha do caçador usa outra célula, por isso a âncora é própria). A altura em pé
/// fica em <c>npcs.json</c> e é lida pelo <see cref="Vespera.Game.World.ScaleRegistry"/> (fonte única da escala).
/// Animações: <c>idle_down/up/side</c> (4 quadros) e <c>walk_down/up/side</c> (6 quadros). O lado olha para a direita.
/// </summary>
public static class NpcSprites
{
    public const string Folder = "res://assets/v2/sprites/npcs";
    public const float IdleFps = 4f;
    public const float WalkFps = 12f;
    public const int CellSize = 192;
    public const float FootY = 176f;

    private static readonly Dictionary<string, SpriteFrames> Cache = [];

    /// <summary>O NPC tem arte aprovada (ao menos a tira "parado" de frente).</summary>
    public static bool Exists(string id) => ResourceLoader.Exists($"{Folder}/npc_{id}_parado_baixo.png");

    /// <summary>Tem tiras de andar (Benício não tem: fica estacionário).</summary>
    public static bool HasWalk(string id) => ResourceLoader.Exists($"{Folder}/npc_{id}_andar_baixo.png");

    public static void ClearCache() => Cache.Clear();

    /// <summary>Desloca o quadro para o pé (96,176) cair na origem do nó.</summary>
    public static Vector2 FootOffset => new(0, (CellSize / 2f) - FootY);

    public static string Animation(bool walking, Facing facing) => $"{(walking ? "walk" : "idle")}_{FacingName(facing)}";

    public static SpriteFrames Build(string id)
    {
        if (Cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var facing in new[] { Facing.Down, Facing.Up, Facing.Side })
        {
            AddStrip(frames, id, facing, walking: false);
            if (HasWalk(id))
            {
                AddStrip(frames, id, facing, walking: true);
            }
        }

        Cache[id] = frames;
        return frames;
    }

    private static void AddStrip(SpriteFrames frames, string id, Facing facing, bool walking)
    {
        var strip = GD.Load<Texture2D>($"{Folder}/npc_{id}_{(walking ? "andar" : "parado")}_{StripName(facing)}.png");
        var count = strip.GetWidth() / CellSize;
        var name = Animation(walking, facing);
        frames.AddAnimation(name);
        frames.SetAnimationLoopMode(name, SpriteFrames.LoopMode.Linear);
        frames.SetAnimationSpeed(name, walking ? WalkFps * count / 6f : (count >= 4 ? IdleFps : IdleFps / 2.5f));
        for (var i = 0; i < count; i++)
        {
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = strip,
                Region = new Rect2(i * CellSize, 0, CellSize, CellSize),
            });
        }
    }

    private static string FacingName(Facing facing) => facing switch
    {
        Facing.Up => "up",
        Facing.Side => "side",
        _ => "down",
    };

    private static string StripName(Facing facing) => facing switch
    {
        Facing.Up => "cima",
        Facing.Side => "lado",
        _ => "baixo",
    };
}
