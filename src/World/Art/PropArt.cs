using System;
using System.Collections.Generic;

namespace Vespera.World;

/// <summary>Papel de um objeto com arte de estados (DECISIONS C2-D5).</summary>
public enum PropArtRole
{
    None,
    Breakable,
    Lamp,
    Brazier,
    Flower,
    Lever,
    Door,
    Board,
}

/// <summary>
/// Onde está a arte aprovada de cada objeto com estados (<c>assets/sprites/props/…/frames.tres</c>, gerada por
/// <c>tools/sprites/build_art.py</c>) e quais sprites do catálogo B5 ela substitui: a arte do designer saiu desses
/// mesmos PNG (barril/caixote/lampião/braseiro/flores/alavanca/porta/quadro), então o objeto do catálogo ganha os estados
/// sem mudar de tamanho. Quem não tem arte nova continua no atlas ou no PNG do catálogo.
/// </summary>
public static class PropArt
{
    public const string Root = "res://assets/sprites/props/";

    private static readonly Dictionary<string, (string Path, PropArtRole Role)> ByCatalogSprite = new(StringComparer.Ordinal)
    {
        ["res://assets/sprites/objects/common/barrel_common.png"] = (Root + "breakable/barrel/frames.tres", PropArtRole.Breakable),
        ["res://assets/sprites/objects/common/crate_common.png"] = (Root + "breakable/crate/frames.tres", PropArtRole.Breakable),
        ["res://assets/sprites/objects/common/lamp_post_common.png"] = (Root + "lights/lamp_post/frames.tres", PropArtRole.Lamp),
        ["res://assets/sprites/objects/common/brazier_stand.png"] = (Root + "lights/brazier/frames.tres", PropArtRole.Brazier),
        ["res://assets/sprites/objects/greenhouse/flowers_blue.png"] = (Root + "flowers/blue/frames.tres", PropArtRole.Flower),
        ["res://assets/sprites/objects/greenhouse/flowers_yellow.png"] = (Root + "flowers/yellow/frames.tres", PropArtRole.Flower),
        ["res://assets/sprites/objects/greenhouse/flower_violet_glow.png"] = (Root + "flowers/violet/frames.tres", PropArtRole.Flower),
        ["res://assets/sprites/objects/clockwork/lever_red.png"] = (Root + "mechanisms/lever/frames.tres", PropArtRole.Lever),
        ["res://assets/sprites/objects/cathedral/door_arched.png"] = (Root + "mechanisms/door/frames.tres", PropArtRole.Door),
        ["res://assets/sprites/objects/common/notice_board.png"] = (Root + "mechanisms/board/frames.tres", PropArtRole.Board),
    };

    /// <summary>Quebráveis com arte de estados (inteiro/rachado/quebrado + estilhaços): barril e caixote.</summary>
    public static string? Breakable(string kind)
    {
        var path = $"{Root}breakable/{kind}/frames.tres";
        return ArtFrames.Exists(path) ? path : null;
    }

    /// <summary>Baú do tier (fechado/abrindo 1/abrindo 2/aberto/vazio).</summary>
    public static string? Chest(int tier)
    {
        var path = $"{Root}chests/tier_{Math.Clamp(tier, 0, 4)}/frames.tres";
        return ArtFrames.Exists(path) ? path : null;
    }

    /// <summary>Barco da travessia: <c>raft</c> (balsa) ou <c>rowboat</c> (barco a remo).</summary>
    public static string? Boat(string kind)
    {
        var path = $"{Root}boats/{kind}/frames.tres";
        return ArtFrames.Exists(path) ? path : null;
    }

    /// <summary>Vara de pesca na mão (animações <c>right</c>/<c>left</c>).</summary>
    public static string? FishingRod()
    {
        var path = $"{Root}fishing/frames.tres";
        return ArtFrames.Exists(path) ? path : null;
    }

    /// <summary>Arte de estados para um objeto do catálogo pelo PNG dele, ou (null, None).</summary>
    public static (string? Path, PropArtRole Role) ForCatalogSprite(string sprite)
    {
        if (ByCatalogSprite.TryGetValue(sprite, out var entry) && ArtFrames.Exists(entry.Path))
        {
            return (entry.Path, entry.Role);
        }

        return (null, PropArtRole.None);
    }
}
