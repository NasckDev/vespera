using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Actors;

public enum HunterClass
{
    Lamina,
    Arco,
    Martelo,
}

public enum Facing
{
    Down,
    Up,
    Side,
}

/// <summary>
/// Monta as <see cref="SpriteFrames"/> do caçador a partir das tiras de andar (células 192x192, pé em (96,176)).
/// Andar = todos os quadros da tira; parado = um quadro neutro da mesma tira (ainda não há folha de idle).
/// O lado é desenhado olhando para a direita; a esquerda espelha.
/// </summary>
public static class HunterSprites
{
    public const int CellSize = 192;
    public const float FootY = 176f;

    /// <summary>Quadros por segundo da caminhada na velocidade máxima (6 quadros = 1 ciclo de 2 passos em 0,5 s).</summary>
    public const float WalkFps = 12f;

    private static readonly Dictionary<HunterClass, SpriteFrames> Cache = [];

    public static string ClassFolder(HunterClass hunter) => hunter switch
    {
        HunterClass.Arco => "arco",
        HunterClass.Martelo => "martelo",
        _ => "lamina",
    };

    public static string WalkAnimation(Facing facing) => $"walk_{Name(facing)}";

    public static string IdleAnimation(Facing facing) => $"idle_{Name(facing)}";

    /// <summary>Deslocamento do <see cref="AnimatedSprite2D"/> centralizado para que o pé fique na origem do nó.</summary>
    public static Vector2 FootOffset => new(0, (CellSize / 2f) - FootY);

    /// <summary>Solta as folhas em cache (chamado ao sair da fase, para não vazar recursos no fim do jogo).</summary>
    public static void ClearCache() => Cache.Clear();

    public static SpriteFrames Build(HunterClass hunter)
    {
        if (Cache.TryGetValue(hunter, out var cached))
        {
            return cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var facing in new[] { Facing.Down, Facing.Up, Facing.Side })
        {
            var folder = ClassFolder(hunter);
            var path = $"res://assets/v2/personagens/{folder}/{folder}_e1_andar_{StripName(facing)}.png";
            var strip = GD.Load<Texture2D>(path);
            var count = strip.GetWidth() / CellSize;
            var walk = WalkAnimation(facing);
            frames.AddAnimation(walk);
            frames.SetAnimationLoopMode(walk, SpriteFrames.LoopMode.Linear);
            frames.SetAnimationSpeed(walk, WalkFps * count / 6f);
            for (var i = 0; i < count; i++)
            {
                frames.AddFrame(walk, Cell(strip, i));
            }

            var idle = IdleAnimation(facing);
            frames.AddAnimation(idle);
            frames.SetAnimationLoopMode(idle, SpriteFrames.LoopMode.None);
            frames.AddFrame(idle, Cell(strip, ClosestStanceFrame(strip, count)));
        }

        Cache[hunter] = frames;
        return frames;
    }

    /// <summary>Quadro de passada mais fechada (menor largura opaca na faixa dos pés), usado como pose parada.</summary>
    private static int ClosestStanceFrame(Texture2D strip, int count)
    {
        const int FeetTop = 156;
        const float Opaque = 0.5f;
        var image = strip.GetImage();
        var best = 0;
        var bestSpan = int.MaxValue;
        for (var i = 0; i < count; i++)
        {
            int min = CellSize, max = -1;
            for (var y = FeetTop; y <= (int)FootY; y++)
            {
                for (var x = 0; x < CellSize; x++)
                {
                    if (image.GetPixel((i * CellSize) + x, y).A >= Opaque)
                    {
                        min = Mathf.Min(min, x);
                        max = Mathf.Max(max, x);
                    }
                }
            }

            if (max >= 0 && max - min < bestSpan)
            {
                bestSpan = max - min;
                best = i;
            }
        }

        return best;
    }

    private static AtlasTexture Cell(Texture2D strip, int index) =>
        new() { Atlas = strip, Region = new Rect2(index * CellSize, 0, CellSize, CellSize) };

    private static string Name(Facing facing) => facing switch
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
