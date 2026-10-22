using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Components;

public enum WorldSound
{
    LampOn,
    LampOff,
    Water,
    Paper,
    DoorLocked,
    Chime,
}

/// <summary>
/// Sons do cenário sintetizados em código (provisórios até o pacote de áudio): clique do lampião, água, papel,
/// trinco de porta trancada e um sino curto. Tocados no espaço 2D, no ponto do objeto.
/// </summary>
public static class WorldSfx
{
    private const int MixRate = 22050;
    private const float Gain = 0.4f;

    private static readonly Dictionary<WorldSound, AudioStreamWav> Streams = [];

    public static void ClearCache() => Streams.Clear();

    /// <summary>Toca o som em <paramref name="position"/>, num tocador 2D que se descarta ao terminar.</summary>
    public static void Play(Node parent, WorldSound sound, Vector2 position)
    {
        if (!Streams.TryGetValue(sound, out var stream))
        {
            stream = Build(sound);
            Streams[sound] = stream;
        }

        var player = new AudioStreamPlayer2D { Stream = stream, Bus = "SFX", Position = position, MaxDistance = 2400f };
        parent.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    private static AudioStreamWav Build(WorldSound sound)
    {
        var rng = new Random((int)sound + 11);
        var samples = sound switch
        {
            WorldSound.LampOn => Concat(Click(rng, 0.02f), Tone(880f, 0.09f, 0.35f), Tone(1320f, 0.12f, 0.25f)),
            WorldSound.LampOff => Concat(Click(rng, 0.02f), Tone(520f, 0.12f, 0.3f)),
            WorldSound.Water => Water(rng),
            WorldSound.Paper => Concat(Noise(rng, 0.07f, 0.35f), Silence(0.03f), Noise(rng, 0.1f, 0.3f)),
            WorldSound.DoorLocked => Concat(Knock(110f, 0.07f), Silence(0.05f), Knock(95f, 0.08f), Click(rng, 0.03f)),
            _ => Concat(Tone(1046f, 0.35f, 0.4f)),
        };
        return ToWav(samples);
    }

    private static float[] Tone(float hz, float seconds, float level)
    {
        var n = (int)(seconds * MixRate);
        var data = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = (float)i / MixRate;
            var env = MathF.Exp(-5f * t / seconds) * MathF.Min(1f, i / 80f);
            data[i] = level * env * (MathF.Sin(MathF.Tau * hz * t) + (0.3f * MathF.Sin(MathF.Tau * hz * 2f * t)));
        }

        return data;
    }

    private static float[] Knock(float hz, float seconds)
    {
        var n = (int)(seconds * MixRate);
        var data = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = (float)i / MixRate;
            data[i] = 0.8f * MathF.Exp(-40f * t) * MathF.Sin(MathF.Tau * hz * t * (1f - (2f * t)));
        }

        return data;
    }

    private static float[] Noise(Random rng, float seconds, float level)
    {
        var n = (int)(seconds * MixRate);
        var data = new float[n];
        var last = 0f;
        for (var i = 0; i < n; i++)
        {
            var white = ((float)rng.NextDouble() * 2f) - 1f;
            last = (0.6f * last) + (0.4f * white);
            var env = MathF.Sin(MathF.PI * i / n);
            data[i] = level * env * (white - last);
        }

        return data;
    }

    private static float[] Click(Random rng, float seconds) => Noise(rng, seconds, 0.9f);

    private static float[] Water(Random rng)
    {
        var parts = new List<float[]> { Noise(rng, 0.25f, 0.18f) };
        for (var i = 0; i < 4; i++)
        {
            parts.Add(Silence(0.02f + ((float)rng.NextDouble() * 0.05f)));
            parts.Add(Bubble(500f + ((float)rng.NextDouble() * 500f)));
        }

        return Concat([.. parts]);
    }

    private static float[] Bubble(float hz)
    {
        const float Seconds = 0.07f;
        var n = (int)(Seconds * MixRate);
        var data = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = (float)i / MixRate;
            data[i] = 0.3f * MathF.Exp(-30f * t) * MathF.Sin(MathF.Tau * hz * t * (1f + (6f * t)));
        }

        return data;
    }

    private static float[] Silence(float seconds) => new float[(int)(seconds * MixRate)];

    private static float[] Concat(params float[][] parts)
    {
        var all = new List<float>();
        foreach (var part in parts)
        {
            all.AddRange(part);
        }

        return [.. all];
    }

    private static AudioStreamWav ToWav(float[] samples)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Math.Clamp(samples[i] * Gain, -1f, 1f) * short.MaxValue);
            data[2 * i] = (byte)(value & 0xFF);
            data[(2 * i) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = MixRate, Stereo = false, Data = data };
    }
}
