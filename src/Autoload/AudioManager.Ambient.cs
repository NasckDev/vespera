using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.World;

namespace Vespera.Autoload;

/// <summary>
/// Sons do motor do mundo (frente B4a, DECISIONS B4a-D47): o passo do jogador (<c>step</c>, um SFX comum) e os loops de
/// ambiente posicionais do layout do mapa (<c>water, fire, forge, crowd, wind, bells, birds</c>,
/// <c>tools/audio/gen_world.py</c>). Um pool fixo de <see cref="AmbientVoices"/> <see cref="AudioStreamPlayer2D"/>: a cada
/// quadro o mundo informa onde o jogador está (<see cref="UpdateAmbientListener"/>), os emissores mais próximos dentro do
/// raio ganham uma voz (<see cref="AmbientPlanner"/>) e o volume cai suave com a distância; a troca de mapa
/// (<see cref="SetAmbientEmitters"/>) faz crossfade: as vozes do mapa velho saem, as novas entram. Nada aloca por quadro.
/// </summary>
public partial class AudioManager
{
    /// <summary>Vozes de ambiente tocando ao mesmo tempo (as mais próximas).</summary>
    public const int AmbientVoices = 6;

    public const float DefaultAmbientFade = 1.2f;

    private const string AmbientDir = "res://assets/audio/ambient/";
    private const float AmbientMaxDb = -4f;

    /// <summary>Tipos de ambiente do layout (<c>ambient[].kind</c>).</summary>
    public static readonly string[] AmbientIds = ["water", "fire", "forge", "crowd", "wind", "bells", "birds"];

    /// <summary>Efeitos do motor do mundo, tocados por <see cref="PlaySfx"/> como os da v2.</summary>
    public static readonly string[] WorldSfxIds = ["step"];

    private readonly Dictionary<string, AudioStream> _ambient = [];
    private readonly int[] _audible = new int[AmbientVoices];
    private AmbientVoice[] _ambientVoices = [];
    private IReadOnlyList<LayoutAmbient> _emitters = [];
    private float _ambientFadeRate = 1f / DefaultAmbientFade;

    /// <summary>Emissores do mapa atual.</summary>
    public int AmbientEmitterCount => _emitters.Count;

    /// <summary>Vozes de ambiente tocando agora (inclui as que estão saindo num fade).</summary>
    public int ActiveAmbientVoices
    {
        get
        {
            var n = 0;
            foreach (var v in _ambientVoices)
            {
                n += v.Player.Playing ? 1 : 0;
            }

            return n;
        }
    }

    /// <summary>Volume linear (0–1) da voz que toca o emissor <paramref name="kind"/> agora, ou 0.</summary>
    public float AmbientLevel(string kind)
    {
        var best = 0f;
        foreach (var v in _ambientVoices)
        {
            if (v.Player.Playing && v.Emitter >= 0 && v.Emitter < _emitters.Count && _emitters[v.Emitter].Kind == kind)
            {
                best = MathF.Max(best, v.Level);
            }
        }

        return best;
    }

    public bool HasAmbient(string kind) => _ambient.ContainsKey(kind);

    /// <summary>
    /// Troca os emissores (mapa novo). As vozes que tocavam saem em <paramref name="fadeSeconds"/>; as do mapa novo
    /// entram no mesmo tempo quando o jogador estiver no alcance. Lista vazia = silêncio com fade.
    /// </summary>
    public void SetAmbientEmitters(IReadOnlyList<LayoutAmbient> emitters, float fadeSeconds = DefaultAmbientFade)
    {
        ArgumentNullException.ThrowIfNull(emitters);
        if (_released)
        {
            return;
        }

        _emitters = emitters;
        _ambientFadeRate = 1f / MathF.Max(0.05f, fadeSeconds);
        foreach (var v in _ambientVoices)
        {
            v.Emitter = -1; // sai com fade a partir do volume atual
        }
    }

    /// <summary>Chame a cada quadro com o pé do jogador (u do mundo). Escolhe as vozes e ajusta volumes.</summary>
    public void UpdateAmbientListener(Vector2 listener, float delta)
    {
        if (_released || _ambientVoices.Length == 0)
        {
            return;
        }

        var count = _emitters.Count == 0 ? 0 : AmbientPlanner.SelectAudible(_emitters, listener.X, listener.Y, _audible);

        // 1) por padrão toda voz sai (alvo 0); as dos emissores audíveis ganham alvo abaixo
        foreach (var v in _ambientVoices)
        {
            v.Target = 0f;
        }

        // 2) quem já toca um emissor audível continua com ele (sem recomeçar o loop)
        for (var k = 0; k < count; k++)
        {
            if (FindVoice(_audible[k]) is { } kept)
            {
                kept.Target = EmitterGain(listener, _audible[k]);
            }
        }

        // 3) emissores audíveis ainda sem voz pegam uma livre (parada ou saindo, a mais baixa)
        for (var k = 0; k < count; k++)
        {
            var index = _audible[k];
            var e = _emitters[index];
            var gain = EmitterGain(listener, index);
            if (gain <= 0f || FindVoice(index) is not null || !_ambient.TryGetValue(e.Kind, out var stream) || FreeVoice() is not { } voice)
            {
                continue; // na margem (ganho 0) ainda não começa: evita tocar e parar a cada quadro
            }

            voice.Emitter = index;
            voice.Level = 0f;
            voice.Target = gain;
            voice.Player.Stream = stream;
            voice.Player.VolumeDb = -80f;
            voice.Player.GlobalPosition = new Vector2(e.X, e.Y);
            voice.Player.Play((float)(_random.NextDouble() * stream.GetLength())); // loops iguais não começam juntos
        }

        // 4) fades (aproximação linear no tempo do crossfade)
        var step = MathF.Max(0f, delta) * _ambientFadeRate;
        foreach (var v in _ambientVoices)
        {
            v.Level = Mathf.MoveToward(v.Level, v.Target, step);
            if (v.Level <= 0.001f && v.Target <= 0f)
            {
                if (v.Player.Playing)
                {
                    v.Player.Stop();
                }

                if (v.Emitter >= 0 && v.Target <= 0f)
                {
                    v.Emitter = -1;
                }

                continue;
            }

            v.Player.VolumeDb = Mathf.LinearToDb(v.Level) + AmbientMaxDb;
        }
    }

    /// <summary>Chamado pelo <c>CreatePools</c>: efeitos do mundo e o pool de ambiente (nós criados uma vez).</summary>
    private void CreateWorldAudio()
    {
        foreach (var id in WorldSfxIds)
        {
            if (LoadStream(SfxDir + id + ".wav") is { } stream)
            {
                _sfx[id] = stream;
            }
        }

        foreach (var id in AmbientIds)
        {
            if (LoadStream(AmbientDir + id + ".wav") is { } stream)
            {
                _ambient[id] = stream;
            }
        }

        _ambientVoices = new AmbientVoice[AmbientVoices];
        for (var i = 0; i < _ambientVoices.Length; i++)
        {
            // volume pela nossa curva (AmbientPlanner.Gain); o Godot só faz o pan pela posição
            var p = new AudioStreamPlayer2D { Name = $"Ambient{i}", Bus = _sfxBusName, MaxDistance = 100000f, Attenuation = 0.0001f, PanningStrength = 0.6f };
            AddChild(p);
            _ambientVoices[i] = new AmbientVoice(p);
        }
    }

    /// <summary>Chamado pelo <c>ReleaseStreams</c> ao fechar o jogo, antes da espera do mixer. Devolve se havia stream.</summary>
    private bool StopWorldAudio()
    {
        var had = false;
        foreach (var v in _ambientVoices)
        {
            had |= v.Player.Stream is not null;
            v.Player.Stop();
            v.Player.Stream = null;
        }

        return had;
    }

    /// <summary>Chamado pelo <c>ReleaseStreams</c> depois da espera: solta os streams de ambiente.</summary>
    private void DisposeWorldAudio()
    {
        foreach (var stream in _ambient.Values)
        {
            stream.Dispose();
        }

        _ambient.Clear();
        _emitters = [];
    }

    private AmbientVoice? FindVoice(int emitter)
    {
        foreach (var v in _ambientVoices)
        {
            if (v.Emitter == emitter)
            {
                return v;
            }
        }

        return null;
    }

    private float EmitterGain(Vector2 listener, int index)
    {
        var e = _emitters[index];
        return AmbientPlanner.Gain(listener.DistanceTo(new Vector2(e.X, e.Y)), e.Radius);
    }

    /// <summary>Uma voz que não toca nenhum emissor audível agora: parada, senão a mais baixa das que saem.</summary>
    private AmbientVoice? FreeVoice()
    {
        AmbientVoice? best = null;
        foreach (var v in _ambientVoices)
        {
            if (v.Target > 0f)
            {
                continue; // tocando um emissor audível neste quadro
            }

            if (!v.Player.Playing)
            {
                return v;
            }

            if (best is null || v.Level < best.Level)
            {
                best = v;
            }
        }

        return best;
    }

    private sealed class AmbientVoice(AudioStreamPlayer2D player)
    {
        public AudioStreamPlayer2D Player { get; } = player;

        /// <summary>Índice do emissor na lista atual, ou −1 (livre ou saindo).</summary>
        public int Emitter { get; set; } = -1;

        public float Level { get; set; }

        public float Target { get; set; }
    }
}
