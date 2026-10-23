using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

public enum HunterSound
{
    /// <summary>Martelada na bigorna com tilintar, ao forjar.</summary>
    Forge,

    /// <summary>Fanfarra curta de sino ao subir de rank.</summary>
    RankUp,

    /// <summary>Peça vestida.</summary>
    Equip,
}

/// <summary>Sons das telas de progressão, sintetizados em código (sem arquivos), no mesmo espírito do <see cref="UiSfx"/>.</summary>
public static class HunterSfx
{
    private const int MixRate = 22050;
    private const float Gain = 0.4f;

    private static readonly AudioStream?[] Streams = new AudioStream?[Enum.GetValues<HunterSound>().Length];

    public static void Play(HunterSound sound)
    {
        if (SceneRouter.Instance is not { } host || !GodotObject.IsInstanceValid(host))
        {
            return;
        }

        var player = new AudioStreamPlayer { Bus = "SFX", ProcessMode = Node.ProcessModeEnum.Always, Stream = Streams[(int)sound] ??= Vespera.Game.Audio.SoundBank.Get("hunter", sound.ToString()) ?? Build(sound) };
        host.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    private static AudioStreamWav Build(HunterSound sound)
    {
        var samples = sound switch
        {
            HunterSound.Forge => ForgeHit(),
            HunterSound.RankUp => Fanfare(),
            _ => Equip(),
        };

        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            data[2 * i] = (byte)(value & 0xFF);
            data[(2 * i) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = MixRate, Stereo = false, Data = data };
    }

    /// <summary>Estalo de ruído + parciais inarmônicos de metal que caem + brilho subindo.</summary>
    private static float[] ForgeHit()
    {
        var count = (int)(0.9f * MixRate);
        var rng = new Random(7);
        var buffer = new float[count];
        float[] partials = [310f, 487f, 811f, 1290f, 1870f];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)MixRate;
            var sample = 0f;
            for (var p = 0; p < partials.Length; p++)
            {
                sample += MathF.Sin(2f * MathF.PI * partials[p] * t) * MathF.Exp(-t * (5f + (p * 3.5f))) / (1 + p);
            }

            var click = ((float)rng.NextDouble() - 0.5f) * MathF.Exp(-t * 90f) * 1.6f;
            var shimmer = t > 0.12f ? MathF.Sin(2f * MathF.PI * (900f + (1800f * (t - 0.12f))) * t) * MathF.Exp(-(t - 0.12f) * 5f) * 0.12f : 0f;
            buffer[i] = (sample + click + shimmer) * Gain;
        }

        return buffer;
    }

    private static float[] Fanfare()
    {
        float[] notes = [392f, 523.25f, 659.25f, 783.99f, 1046.5f];
        var all = new List<float>();
        var total = (int)(2.2f * MixRate);
        var buffer = new float[total];
        for (var n = 0; n < notes.Length; n++)
        {
            var start = (int)(n * 0.14f * MixRate);
            var length = (int)((n == notes.Length - 1 ? 1.5f : 0.5f) * MixRate);
            for (var i = 0; i < length && start + i < total; i++)
            {
                var t = i / (float)MixRate;
                var env = MathF.Min(1f, i / 120f) * MathF.Exp(-t * (n == notes.Length - 1 ? 2.2f : 4.5f));
                var wave = MathF.Sin(2f * MathF.PI * notes[n] * t) + (0.35f * MathF.Sin(4f * MathF.PI * notes[n] * t)) + (0.12f * MathF.Sin(6f * MathF.PI * notes[n] * t));
                buffer[start + i] += wave * env * Gain * 0.55f;
            }
        }

        all.AddRange(buffer);
        return [.. all];
    }

    private static float[] Equip()
    {
        var count = (int)(0.22f * MixRate);
        var buffer = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)MixRate;
            buffer[i] = (MathF.Sin(2f * MathF.PI * 240f * t) + (0.5f * MathF.Sin(2f * MathF.PI * 610f * t))) * MathF.Exp(-t * 22f) * Gain * 0.6f;
        }

        return buffer;
    }
}
