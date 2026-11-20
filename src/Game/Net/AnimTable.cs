using System;
using System.Runtime.CompilerServices;
using Godot;
using Vespera.Core.Net;

namespace Vespera.Game.Net;

/// <summary>
/// Animação por índice: host e convidado carregam a mesma folha (<see cref="SpriteFrames"/>), então o índice do nome na lista
/// ORDENADA das animações dela é o mesmo nos dois lados e cabe em 1 byte no snapshot (em vez de mandar o nome toda vez).
/// O convidado confere o índice contra a própria folha (pacote ruim não passa dos limites).
/// </summary>
public static class AnimTable
{
    private static readonly ConditionalWeakTable<SpriteFrames, string[]> Names = new();

    private static string[] NamesOf(SpriteFrames frames) => Names.GetValue(frames, f =>
    {
        var list = f.GetAnimationNames();
        Array.Sort(list, StringComparer.Ordinal);
        return list;
    });

    /// <summary>Índice da animação que o sprite toca agora (<see cref="WorldSnapshot.NoAnim"/> se não há).</summary>
    public static byte IndexOf(AnimatedSprite2D? sprite)
    {
        if (sprite?.SpriteFrames is not { } frames || !GodotObject.IsInstanceValid(sprite))
        {
            return WorldSnapshot.NoAnim;
        }

        var index = Array.BinarySearch(NamesOf(frames), sprite.Animation.ToString(), StringComparer.Ordinal);
        return index is >= 0 and < WorldSnapshot.NoAnim ? (byte)index : WorldSnapshot.NoAnim;
    }

    /// <summary>Quadro atual do sprite, limitado a um byte.</summary>
    public static byte FrameOf(AnimatedSprite2D? sprite) =>
        sprite is null || !GodotObject.IsInstanceValid(sprite) ? (byte)0 : (byte)Math.Clamp(sprite.Frame, 0, 255);

    /// <summary>
    /// Põe o sprite do convidado na pose do host: troca de animação recomeça no quadro dele, e uma deriva de mais de 1 quadro
    /// é corrigida a cada snapshot. Devolve false se o índice não existe nesta folha (nada muda).
    /// </summary>
    public static bool Apply(AnimatedSprite2D sprite, byte anim, byte frame)
    {
        if (anim == WorldSnapshot.NoAnim || sprite.SpriteFrames is not { } frames)
        {
            return false;
        }

        var names = NamesOf(frames);
        if (anim >= names.Length)
        {
            return false;
        }

        var name = names[anim];
        var last = Math.Max(0, frames.GetFrameCount(name) - 1);
        if (sprite.Animation.ToString() != name)
        {
            sprite.Play(name);
            sprite.Frame = Math.Min(frame, last);
            return true;
        }

        if (!sprite.IsPlaying() && frame < last)
        {
            sprite.Play(name);
        }

        if (Math.Abs(sprite.Frame - frame) > 1)
        {
            sprite.Frame = Math.Min(frame, last);
        }

        return true;
    }
}
