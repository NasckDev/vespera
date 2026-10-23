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
/// Folhas de animação do caçador (assets/v2/sprites/&lt;classe&gt;/&lt;classe&gt;_e1.tres): células 256x256 com o pé em
/// (128, 232) e ~112 px de altura em pé. Nomes das animações = verbo_direção (andar_baixo, ataque1_lado...);
/// o lado olha para a direita e a esquerda espelha. Também guarda os quadros de impacto conferidos à mão.
/// </summary>
public static class HunterSprites
{
    public const int CellSize = 256;
    public const float FootY = 232f;

    /// <summary>Altura em pé do caçador na folha (px).</summary>
    public const float StandHeight = 112f;

    private static readonly Dictionary<HunterClass, SpriteFrames> Cache = [];

    public static string ClassFolder(HunterClass hunter) => hunter switch
    {
        HunterClass.Arco => "arco",
        HunterClass.Martelo => "martelo",
        _ => "lamina",
    };

    /// <summary>Id da arma no save/Core para a classe.</summary>
    public static string WeaponId(HunterClass hunter) => hunter switch
    {
        HunterClass.Arco => "bow",
        HunterClass.Martelo => "hammer",
        _ => "sword",
    };

    public static string DisplayName(HunterClass hunter) => hunter switch
    {
        HunterClass.Arco => "Arco",
        HunterClass.Martelo => "Martelo",
        _ => "Lâmina",
    };

    public static string WalkAnimation(Facing facing) => Anim("andar", facing);

    public static string IdleAnimation(Facing facing) => Anim("parado", facing);

    public static string Anim(string verb, Facing facing) => $"{verb}_{Suffix(facing)}";

    /// <summary>Deslocamento do sprite centralizado para que o pé fique na origem do nó.</summary>
    public static Vector2 FootOffset => new(0, (CellSize / 2f) - FootY);

    public static void ClearCache() => Cache.Clear();

    public static SpriteFrames Build(HunterClass hunter)
    {
        if (Cache.TryGetValue(hunter, out var cached))
        {
            return cached;
        }

        var folder = ClassFolder(hunter);
        var frames = GD.Load<SpriteFrames>($"res://assets/v2/sprites/{folder}/{folder}_e1.tres");
        Cache[hunter] = frames;
        return frames;
    }

    /// <summary>Animação do verbo na direção; cai para o lado quando a folha só tem o lado (pesado, morte).</summary>
    public static string Resolve(SpriteFrames frames, string verb, Facing facing)
    {
        var name = Anim(verb, facing);
        return frames.HasAnimation(name) ? name : Anim(verb, Facing.Side);
    }

    /// <summary>
    /// Quadros de impacto (base 0) conferidos nas folhas de contato. O JSON da arte usa "maior área opaca", que erra
    /// no Martelo (ataque1_lado apontava o quadro 0, ainda parado): ali o impacto é o 4 (golpe) e o 5 (chão).
    /// </summary>
    public static int[] HitFrames(HunterClass hunter, string verb, Facing facing) => (hunter, verb) switch
    {
        (HunterClass.Lamina, "pesado") => [2, 4, 6],
        (HunterClass.Lamina, _) => [2],
        (HunterClass.Martelo, "ataque1") => [4],
        (HunterClass.Martelo, "ataque2") => [2],
        (HunterClass.Martelo, "pesado") => [6],
        (HunterClass.Arco, "tiro") => [facing == Facing.Down ? 2 : 3],
        (HunterClass.Arco, "tiro_carregado") => [facing == Facing.Up ? 3 : 4],
        _ => [2],
    };

    /// <summary>Quadro em que o Arco segura a corda puxada enquanto carrega.</summary>
    public const int BowHoldFrame = 2;

    private static string Suffix(Facing facing) => facing switch
    {
        Facing.Up => "cima",
        Facing.Side => "lado",
        _ => "baixo",
    };
}
