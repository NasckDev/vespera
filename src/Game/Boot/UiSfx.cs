using System;
using Godot;

namespace Vespera.Game.Boot;

public enum UiSound
{
    Move,
    Confirm,
    Back,
    Deny,
}

/// <summary>Sons de navegação sintetizados em código (sem arquivos): mover foco, confirmar, voltar e negar.</summary>
public static class UiSfx
{
    private const int MixRate = 22050;
    private const float Gain = 0.32f;
    private const int PlayerCount = 3;

    private static readonly AudioStream?[] Streams = new AudioStream?[Enum.GetValues<UiSound>().Length];
    private static AudioStreamPlayer[]? _players;
    private static int _next;

    /// <summary>Cria os tocadores sob <paramref name="owner"/>; chamado uma vez pelo SceneRouter.</summary>
    public static void Initialize(Node owner)
    {
        _players = new AudioStreamPlayer[PlayerCount];
        for (var i = 0; i < PlayerCount; i++)
        {
            _players[i] = new AudioStreamPlayer { Bus = "SFX", ProcessMode = Node.ProcessModeEnum.Always };
            owner.AddChild(_players[i]);
        }
    }

    public static void Play(UiSound sound)
    {
        if (_players is null)
        {
            return;
        }

        var player = _players[_next];
        _next = (_next + 1) % PlayerCount;
        player.Stream = Streams[(int)sound] ??= Vespera.Game.Audio.SoundBank.Get("ui", sound.ToString()) ?? Build(sound);
        player.Play();
    }

    private static AudioStreamWav Build(UiSound sound)
    {
        var samples = sound switch
        {
            UiSound.Move => Tone([(680f, 0.045f)], 0.8f),
            UiSound.Confirm => Tone([(520f, 0.06f), (780f, 0.11f)], 1f),
            UiSound.Back => Tone([(520f, 0.05f), (390f, 0.09f)], 0.9f),
            _ => Tone([(190f, 0.12f)], 0.8f, square: true),
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

    private static float[] Tone((float Hz, float Seconds)[] notes, float level, bool square = false)
    {
        var all = new System.Collections.Generic.List<float>();
        foreach (var (hz, seconds) in notes)
        {
            var count = (int)(seconds * MixRate);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)MixRate;
                var phase = 2f * MathF.PI * hz * t;
                var wave = square ? MathF.Sign(MathF.Sin(phase)) * 0.5f : MathF.Sin(phase) + (0.3f * MathF.Sin(2f * phase));
                var attack = Math.Min(1f, i / 80f);
                var release = 1f - (i / (float)count);
                all.Add(wave * attack * release * release * level * Gain);
            }
        }

        return [.. all];
    }
}
