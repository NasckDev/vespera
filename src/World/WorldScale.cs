using System;
using System.Collections.Generic;
using Vespera.Autoload;
using Vespera.Core.Data;

namespace Vespera.World;

/// <summary>Classe de escala de um sprite do atlas (DECISIONS D41).</summary>
public enum SpriteScaleClass
{
    /// <summary>Pela chave: <c>c_*</c>, <c>npc_*</c>, <c>shadow</c> e monstros = personagem; <c>pr_*</c> = objeto; resto = 1.</summary>
    Auto,

    Character,

    Prop,

    /// <summary>Sem escala global (ícones, portais, efeitos).</summary>
    Fixed,
}

/// <summary>
/// Escalas globais do mundo lidas de <c>rules.json</c> (<c>world.characterScale</c>, <c>world.propScale</c>), aplicadas pelo
/// <see cref="AtlasSprite"/> a jogador, NPCs, monstros e objetos para a proporção bater com a arte do mapa (D41).
/// Sem o <see cref="Database"/> (editor), tudo é 1.
/// </summary>
public static class WorldScale
{
    private static readonly HashSet<string> MonsterSprites = new(StringComparer.Ordinal);
    private static bool _monstersLoaded;

    public static WorldRules? Rules => Database.IsLoaded ? Database.Db.Rules.World : null;

    /// <summary>Jogador, NPCs e monstros (1,75: o caçador fica com 0,875 da porta da Estalagem).</summary>
    public static float Character => (float)(Rules?.CharacterScale ?? 1.0);

    /// <summary>Barris, caixas, baús e afins (1,25: barril do atlas = barril pintado no mapa).</summary>
    public static float Prop => (float)(Rules?.PropScale ?? 1.0);

    /// <summary>Altura do corpo do jogador em u (32 u do <c>c_sword_front</c> × <see cref="Character"/>).</summary>
    public static float CharacterHeight => (float)(Rules?.Calibration.SpriteHeight ?? 32.0) * Character;

    public static float For(string key, SpriteScaleClass scaleClass) => Classify(key, scaleClass) switch
    {
        SpriteScaleClass.Character => Character,
        SpriteScaleClass.Prop => Prop,
        _ => 1f,
    };

    public static SpriteScaleClass Classify(string key, SpriteScaleClass scaleClass)
    {
        if (scaleClass != SpriteScaleClass.Auto)
        {
            return scaleClass;
        }

        if (key.StartsWith("c_", StringComparison.Ordinal) || key.StartsWith("npc_", StringComparison.Ordinal) || key == "shadow")
        {
            return SpriteScaleClass.Character;
        }

        if (key.StartsWith("pr_", StringComparison.Ordinal))
        {
            return SpriteScaleClass.Prop;
        }

        return IsMonster(key) ? SpriteScaleClass.Character : SpriteScaleClass.Fixed;
    }

    /// <summary>Chave de pose de monstro (<c>wolf_f</c>, <c>anglerB_a</c>, <c>cnev_d</c>…): o prefixo é o sprite de algum monstro dos dados.</summary>
    private static bool IsMonster(string key)
    {
        var cut = key.LastIndexOf('_');
        if (cut <= 0)
        {
            return false;
        }

        if (!_monstersLoaded && Database.IsLoaded)
        {
            foreach (var m in Database.Db.Monsters.Values)
            {
                MonsterSprites.Add(m.Sprite);
            }

            _monstersLoaded = true;
        }

        return MonsterSprites.Contains(key[..cut]);
    }
}
