using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Combat;

public enum CombatSound
{
    Swing,
    SwingHeavy,
    Spin,
    HammerSwing,
    Hit,
    HitHeavy,
    Kill,
    Dodge,
    Hurt,
    Fall,
    BowDraw,
    BowShot,
    BowCharged,
    ArrowHit,
    Slam,
    LodoHop,
    LodoLand,
    Ghost,
    GhostLunge,
    Telegraph,
    BossLand,
    BossRoar,
    Swallow,
    Split,
    CoreBreak,
    Pickup,
    Chest,
    BarrierUp,
    BarrierDown,
    Portal,
}

/// <summary>
/// Sons de combate sintetizados em código (provisórios até o pacote de áudio, PLANO-V2 §2): um por ação, com leve
/// variação de altura a cada toque para não cansar. Tocados no espaço 2D, no ponto do evento.
/// </summary>
public static class CombatSfx
{
    private const int Rate = 22050;
    private const float Gain = 0.42f;

    private static readonly Dictionary<CombatSound, AudioStreamWav> Cache = [];
    private static readonly Random Rng = new(7);

    public static void ClearCache() => Cache.Clear();

    public static void Play(Node parent, CombatSound sound, Vector2 at, float volumeDb = 0f)
    {
        if (!Cache.TryGetValue(sound, out var stream))
        {
            stream = Wav(Build(sound));
            Cache[sound] = stream;
        }

        var player = new AudioStreamPlayer2D
        {
            Stream = stream,
            Bus = "SFX",
            Position = at,
            MaxDistance = 3000f,
            VolumeDb = volumeDb,
            PitchScale = 0.93f + (0.14f * (float)Rng.NextDouble()),
        };
        parent.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    private static float[] Build(CombatSound sound)
    {
        var rng = new Random((int)sound * 31 + 5);
        return sound switch
        {
            CombatSound.Swing => Whoosh(rng, 0.14f, 1800f, 600f, 0.5f),
            CombatSound.SwingHeavy => Whoosh(rng, 0.24f, 1200f, 300f, 0.7f),
            CombatSound.Spin => Mix(Whoosh(rng, 0.42f, 1500f, 500f, 0.6f), Sweep(380f, 760f, 0.42f, 0.12f)),
            CombatSound.HammerSwing => Whoosh(rng, 0.3f, 700f, 180f, 0.8f),
            CombatSound.Hit => Mix(Thump(150f, 0.09f, 0.9f), Noise(rng, 0.06f, 0.55f, 0.5f)),
            CombatSound.HitHeavy => Mix(Thump(95f, 0.18f, 1f), Noise(rng, 0.12f, 0.6f, 0.3f), Sweep(600f, 200f, 0.1f, 0.2f)),
            CombatSound.Kill => Mix(Thump(80f, 0.22f, 0.9f), Bubbles(rng, 5, 0.3f)),
            CombatSound.Dodge => Whoosh(rng, 0.2f, 900f, 2200f, 0.45f),
            CombatSound.Hurt => Mix(Thump(120f, 0.12f, 0.8f), Sweep(520f, 260f, 0.16f, 0.35f, square: true)),
            CombatSound.Fall => Concat(Sweep(440f, 110f, 0.7f, 0.4f), Thump(70f, 0.3f, 0.9f)),
            CombatSound.BowDraw => Sweep(180f, 320f, 0.25f, 0.12f, square: true),
            CombatSound.BowShot => Mix(Thump(330f, 0.05f, 0.5f), Whoosh(rng, 0.16f, 2500f, 1200f, 0.4f)),
            CombatSound.BowCharged => Mix(Sweep(660f, 1320f, 0.18f, 0.25f), Whoosh(rng, 0.25f, 3000f, 900f, 0.5f)),
            CombatSound.ArrowHit => Mix(Thump(260f, 0.06f, 0.6f), Noise(rng, 0.04f, 0.4f, 0.6f)),
            CombatSound.Slam => Mix(Thump(55f, 0.45f, 1f), Noise(rng, 0.35f, 0.6f, 0.15f), Thump(110f, 0.15f, 0.6f)),
            CombatSound.LodoHop => Bubbles(rng, 2, 0.14f),
            CombatSound.LodoLand => Mix(Thump(90f, 0.12f, 0.7f), Bubbles(rng, 3, 0.2f)),
            CombatSound.Ghost => Mix(Sweep(300f, 420f, 0.5f, 0.18f), Noise(rng, 0.5f, 0.15f, 0.1f)),
            CombatSound.GhostLunge => Mix(Sweep(700f, 220f, 0.3f, 0.3f), Whoosh(rng, 0.3f, 1600f, 400f, 0.4f)),
            CombatSound.Telegraph => Concat(Tone(988f, 0.07f, 0.35f), Tone(1318f, 0.12f, 0.3f)),
            CombatSound.BossLand => Mix(Thump(45f, 0.6f, 1f), Noise(rng, 0.5f, 0.5f, 0.12f), Bubbles(rng, 6, 0.5f)),
            CombatSound.BossRoar => Mix(Sweep(140f, 70f, 1.1f, 0.6f, square: true), Noise(rng, 1.1f, 0.35f, 0.08f)),
            CombatSound.Swallow => Mix(Sweep(220f, 60f, 0.6f, 0.5f), Bubbles(rng, 8, 0.6f)),
            CombatSound.Split => Mix(Bubbles(rng, 10, 0.7f), Sweep(200f, 500f, 0.6f, 0.25f)),
            CombatSound.CoreBreak => Mix(Noise(rng, 0.3f, 0.8f, 0.9f), Tone(1568f, 0.4f, 0.35f), Thump(100f, 0.2f, 0.7f)),
            CombatSound.Pickup => Concat(Tone(1046f, 0.05f, 0.3f), Tone(1568f, 0.09f, 0.28f)),
            CombatSound.Chest => Concat(Thump(140f, 0.1f, 0.6f), Tone(784f, 0.1f, 0.3f), Tone(1046f, 0.1f, 0.3f), Tone(1318f, 0.25f, 0.3f)),
            CombatSound.BarrierUp => Mix(Sweep(120f, 480f, 0.45f, 0.35f), Noise(rng, 0.45f, 0.2f, 0.2f)),
            CombatSound.BarrierDown => Mix(Sweep(480f, 90f, 0.5f, 0.35f), Tone(784f, 0.3f, 0.2f)),
            _ => Mix(Sweep(160f, 640f, 1.0f, 0.3f), Sweep(240f, 960f, 1.0f, 0.2f), Noise(rng, 1f, 0.15f, 0.05f)),
        };
    }

    private static float[] Tone(float hz, float seconds, float level)
    {
        var data = new float[(int)(seconds * Rate)];
        for (var i = 0; i < data.Length; i++)
        {
            var t = (float)i / Rate;
            var env = MathF.Exp(-4f * t / seconds) * MathF.Min(1f, i / 60f);
            data[i] = level * env * (MathF.Sin(MathF.Tau * hz * t) + (0.25f * MathF.Sin(MathF.Tau * hz * 2f * t)));
        }

        return data;
    }

    private static float[] Sweep(float from, float to, float seconds, float level, bool square = false)
    {
        var data = new float[(int)(seconds * Rate)];
        var phase = 0f;
        for (var i = 0; i < data.Length; i++)
        {
            var k = (float)i / data.Length;
            phase += MathF.Tau * (from + ((to - from) * k)) / Rate;
            var wave = square ? MathF.Sign(MathF.Sin(phase)) * 0.5f : MathF.Sin(phase);
            data[i] = level * wave * MathF.Sin(MathF.PI * k) * MathF.Min(1f, i / 60f);
        }

        return data;
    }

    private static float[] Thump(float hz, float seconds, float level)
    {
        var data = new float[(int)(seconds * Rate)];
        for (var i = 0; i < data.Length; i++)
        {
            var t = (float)i / Rate;
            data[i] = level * MathF.Exp(-9f * t / seconds) * MathF.Sin(MathF.Tau * hz * t * (1f - (0.5f * t / seconds)));
        }

        return data;
    }

    /// <summary>Ruído filtrado com envelope em sino; <paramref name="bright"/> 0 (grave) a 1 (agudo).</summary>
    private static float[] Noise(Random rng, float seconds, float level, float bright)
    {
        var data = new float[(int)(seconds * Rate)];
        var low = 0f;
        var a = 0.05f + (0.9f * bright);
        for (var i = 0; i < data.Length; i++)
        {
            var white = ((float)rng.NextDouble() * 2f) - 1f;
            low += a * (white - low);
            var k = (float)i / data.Length;
            data[i] = level * low * MathF.Exp(-3f * k) * MathF.Min(1f, i / 40f);
        }

        return data;
    }

    /// <summary>Sopro de lâmina/arma: ruído com filtro que varre de <paramref name="from"/> a <paramref name="to"/> Hz.</summary>
    private static float[] Whoosh(Random rng, float seconds, float from, float to, float level)
    {
        var data = new float[(int)(seconds * Rate)];
        var low = 0f;
        for (var i = 0; i < data.Length; i++)
        {
            var k = (float)i / data.Length;
            var cutoff = from + ((to - from) * k);
            var a = MathF.Min(0.95f, MathF.Tau * cutoff / Rate);
            var white = ((float)rng.NextDouble() * 2f) - 1f;
            low += a * (white - low);
            data[i] = level * low * MathF.Sin(MathF.PI * MathF.Pow(k, 0.6f)) * 2.2f;
        }

        return data;
    }

    private static float[] Bubbles(Random rng, int count, float seconds)
    {
        var data = new float[(int)(seconds * Rate)];
        for (var b = 0; b < count; b++)
        {
            var start = (int)(rng.NextDouble() * data.Length * 0.7);
            var hz = 250f + ((float)rng.NextDouble() * 450f);
            var length = (int)(0.06f * Rate);
            for (var i = 0; i < length && start + i < data.Length; i++)
            {
                var t = (float)i / Rate;
                data[start + i] += 0.35f * MathF.Exp(-28f * t) * MathF.Sin(MathF.Tau * hz * t * (1f + (8f * t)));
            }
        }

        return data;
    }

    private static float[] Mix(params float[][] parts)
    {
        var length = 0;
        foreach (var p in parts)
        {
            length = Math.Max(length, p.Length);
        }

        var data = new float[length];
        foreach (var p in parts)
        {
            for (var i = 0; i < p.Length; i++)
            {
                data[i] += p[i];
            }
        }

        return data;
    }

    private static float[] Concat(params float[][] parts)
    {
        var all = new List<float>();
        foreach (var p in parts)
        {
            all.AddRange(p);
        }

        return [.. all];
    }

    private static AudioStreamWav Wav(float[] samples)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Math.Clamp(MathF.Tanh(samples[i] * 1.2f) * Gain, -1f, 1f) * short.MaxValue);
            data[2 * i] = (byte)(value & 0xFF);
            data[(2 * i) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }
}
