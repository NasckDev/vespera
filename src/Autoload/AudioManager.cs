using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Audio;
using Vespera.Core.Ranks;

namespace Vespera.Autoload;

/// <summary>
/// Efeitos sonoros, música e volumes. Acesse por <see cref="Instance"/>; ids e uso em docs/design/api-audio.md.
/// SFX tocam num pool fixo de players (nenhum nó é criado por chamada) com limite de vozes por id;
/// música faz crossfade entre três players (D37) e escolhe a faixa pelo local (D36); volumes por barramento (0–1) ficam em user://settings.cfg.
/// </summary>
public partial class AudioManager : Node
{
    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";
    public const string UiBus = "UI";

    public const string SettingsPath = "user://settings.cfg";
    private const string SmokeSettingsPath = "user://settings_smoke.cfg";
    private const string SettingsSection = "audio";

    private const string SfxDir = "res://assets/audio/sfx/";
    private const string MusicDir = "res://assets/audio/music/";

    /// <summary>Players sem posição (UI, jogador, música de evento) e posicionais (mundo).</summary>
    public const int FlatVoices = 8;
    public const int PositionalVoices = 16;

    /// <summary>Máximo de cópias do mesmo id tocando ao mesmo tempo em cada pool (a mais antiga é cortada).</summary>
    public const int MaxVoicesPerId = 4;

    public const float DefaultMusicFade = 1.5f;

    /// <summary>Maior variação de pitch aceita por <see cref="PlaySfx"/> (acima disso o pitch chegaria a 0).</summary>
    public const float MaxPitchJitter = 0.5f;

    /// <summary>Música de um local sem registro: a cidade (DECISIONS D36).</summary>
    public const string DefaultLocationMusic = "city_day";

    /// <summary>Players de música: o crossfade entra num player parado (ou no mais baixo), sem cortar quem sai.</summary>
    private const int MusicPlayers = 3;

    /// <summary>Espera ao sair para o mixer descartar playbacks parados (um buffer de mixagem tem ~10–20 ms).</summary>
    private const int ExitDrainMs = 60;

    /// <summary>Os 18 efeitos do protótipo v2 (core.js), gerados por tools/audio/gen_sfx.py.</summary>
    public static readonly string[] SfxIds =
    [
        "swing", "hit", "brk", "dash", "pick", "coin", "hurt", "ui", "open",
        "boom", "bow", "magic", "rank", "die", "portal", "growl", "kill", "eco",
    ];

    /// <summary>Loops de música, gerados por tools/audio/gen_music.py.</summary>
    public static readonly string[] MusicIds = ["city_day", "vespera", "brecha"];

    public static readonly string[] Buses = [MasterBus, MusicBus, SfxBus, UiBus];

    /// <summary>Música (e reverb) dos locais registrados em <see cref="RegisterLocationMusic"/>; o resto toca a cidade.</summary>
    private readonly Dictionary<string, (string Music, bool Reverb)> _locationMusic = [];

    private readonly Dictionary<string, AudioStream> _sfx = [];
    private readonly Dictionary<string, AudioStream> _music = [];
    private readonly Dictionary<string, float> _volumes = [];
    private readonly HashSet<string> _warned = [];
    private readonly Random _random = new();

    private readonly StringName _sfxBusName = SfxBus;
    private readonly StringName _uiBusName = UiBus;
    private readonly StringName _musicBusName = MusicBus;

    private AudioStreamPlayer[] _flat = [];
    private AudioStreamPlayer2D[] _positional = [];
    private VoiceAllocator _flatVoices = null!;
    private VoiceAllocator _positionalVoices = null!;
    private Func<int, bool> _flatBusy = null!;
    private Func<int, bool> _positionalBusy = null!;

    private AudioStreamPlayer[] _musicPlayers = [];
    private AudioStreamPlayer _musicCurrent = null!;
    private Tween? _musicTween;
    private string _settingsPath = SettingsPath;
    private bool _released;

    public static AudioManager Instance { get; private set; } = null!;

    /// <summary>Id da música tocando (ou entrando em fade), ou null.</summary>
    public string? CurrentMusic { get; private set; }

    public bool MusicReverbEnabled { get; private set; }

    /// <summary>Arquivo de opções em uso (user://settings.cfg; no smoke test, um arquivo descartável).</summary>
    public string SettingsFile => _settingsPath;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always; // sons de menu e fades continuam com o jogo pausado
        if (Testing.SmokeTest.IsRequested())
        {
            // como o SaveService: o smoke test nunca toca nas opções reais do jogador
            _settingsPath = SmokeSettingsPath;
            DirAccess.RemoveAbsolute(_settingsPath);
        }
    }

    public override void _Ready()
    {
        LoadSettings();
        foreach (var id in SfxIds)
        {
            if (LoadStream(SfxDir + id + ".wav") is { } stream)
            {
                _sfx[id] = stream;
            }
        }

        // pré-carrega as faixas para a primeira troca (entrar na cidade ou no portal) não engasgar
        foreach (var id in MusicIds)
        {
            if (LoadStream(MusicDir + id + ".wav") is { } stream)
            {
                _music[id] = stream;
            }
        }

        CreatePools();

        var bus = EventBus.Instance;
        bus.RankChanged += OnRankChanged;
        bus.PlayerDamaged += OnPlayerDamaged;
        bus.FragmentsChanged += OnFragmentsChanged;
        bus.LocationChanged += OnLocationChanged;
        bus.PortalEntered += OnPortalEntered;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.RankChanged -= OnRankChanged;
        bus.PlayerDamaged -= OnPlayerDamaged;
        bus.FragmentsChanged -= OnFragmentsChanged;
        bus.LocationChanged -= OnLocationChanged;
        bus.PortalEntered -= OnPortalEntered;
        ReleaseStreams();
    }

    // --- SFX ---

    /// <summary>
    /// Toca o efeito <paramref name="id"/>. Com <paramref name="worldPos"/> usa um player 2D (volume e pan pela
    /// câmera); sem, toca "na cabeça". <paramref name="pitchJitter"/> varia o pitch em ±fração para não cansar.
    /// <paramref name="pitchJitter"/> é limitado a 0–<see cref="MaxPitchJitter"/>.
    /// Id desconhecido gera um aviso (uma vez) e é ignorado. Depois do encerramento não faz nada.
    /// </summary>
    public void PlaySfx(string id, Vector2? worldPos = null, float pitchJitter = 0.05f)
    {
        if (_released)
        {
            return;
        }

        if (!_sfx.TryGetValue(id, out var stream))
        {
            WarnOnce("sfx:" + id, $"AudioManager: efeito sonoro desconhecido \"{id}\".");
            return;
        }

        var bus = id == "ui" ? _uiBusName : _sfxBusName;
        var pitch = 1f + (((float)_random.NextDouble() * 2f) - 1f) * Mathf.Clamp(pitchJitter, 0f, MaxPitchJitter);

        if (worldPos is { } position)
        {
            var player = _positional[_positionalVoices.Acquire(id, MaxVoicesPerId, _positionalBusy)];
            player.Stop();
            player.Stream = stream;
            player.Bus = bus;
            player.PitchScale = pitch;
            player.GlobalPosition = position;
            player.Play();
            return;
        }

        var flat = _flat[_flatVoices.Acquire(id, MaxVoicesPerId, _flatBusy)];
        flat.Stop();
        flat.Stream = stream;
        flat.Bus = bus;
        flat.PitchScale = pitch;
        flat.Play();
    }

    public bool HasSfx(string id) => _sfx.ContainsKey(id);

    /// <summary>Quantos players de SFX o pool tem (fixo depois do _Ready).</summary>
    public int SfxPoolSize => _flat.Length + _positional.Length;

    /// <summary>Quantos players de SFX estão tocando agora.</summary>
    public int ActiveSfxVoices
    {
        get
        {
            var count = 0;
            foreach (var p in _flat)
            {
                count += p.Playing ? 1 : 0;
            }

            foreach (var p in _positional)
            {
                count += p.Playing ? 1 : 0;
            }

            return count;
        }
    }

    /// <summary>Quantos players estão tocando o efeito <paramref name="id"/> agora (somando os dois pools).</summary>
    public int ActiveVoices(string id)
    {
        var count = 0;
        for (var i = 0; i < _flat.Length; i++)
        {
            count += _flat[i].Playing && _flatVoices.IdOf(i) == id ? 1 : 0;
        }

        for (var i = 0; i < _positional.Length; i++)
        {
            count += _positional[i].Playing && _positionalVoices.IdOf(i) == id ? 1 : 0;
        }

        return count;
    }

    // --- Música ---

    /// <summary>
    /// Troca a música com crossfade de <paramref name="fadeSeconds"/>. Pedir a que já toca não faz nada.
    /// Uma troca no meio de outro fade não corta ninguém: quem estava saindo continua saindo do volume em que está.
    /// </summary>
    public void PlayMusic(string id, float fadeSeconds = DefaultMusicFade)
    {
        if (_released || id == CurrentMusic)
        {
            return;
        }

        if (GetMusicStream(id) is not { } stream)
        {
            WarnOnce("music:" + id, $"AudioManager: música desconhecida \"{id}\".");
            return;
        }

        _musicTween?.Kill();
        _musicTween = null;

        // Ida e volta no meio de um fade (portal e volta logo): a faixa ainda está saindo num player,
        // então ela volta dali, do volume em que está, em vez de recomeçar duplicada em outro player.
        var incoming = FindMusicPlayerWith(stream);
        var startLinear = 0f;
        if (incoming is not null)
        {
            startLinear = Mathf.DbToLinear(incoming.VolumeDb);
        }
        else
        {
            incoming = PickIncomingMusicPlayer();
            incoming.Stop();
            incoming.Stream = stream;
            incoming.VolumeDb = fadeSeconds > 0f ? AudioVolume.SilenceDb : 0f;
            incoming.Play();
        }

        _musicCurrent = incoming;
        CurrentMusic = id;

        if (fadeSeconds <= 0f)
        {
            incoming.VolumeDb = 0f;
            StopMusicPlayersExcept(incoming);
            return;
        }

        var fadingIn = incoming;
        _musicTween = CreateTween().SetParallel();
        _musicTween.TweenMethod(Callable.From<float>(v => fadingIn.VolumeDb = AudioVolume.ToDb(v)), startLinear, 1f, fadeSeconds);
        FadeOutMusicPlayersExcept(_musicTween, incoming, fadeSeconds);
    }

    /// <summary>Para a música com fade.</summary>
    public void StopMusic(float fadeSeconds = DefaultMusicFade)
    {
        CurrentMusic = null;
        _musicTween?.Kill();
        _musicTween = null;
        if (fadeSeconds <= 0f)
        {
            StopMusicPlayersExcept(null);
            return;
        }

        _musicTween = CreateTween().SetParallel();
        FadeOutMusicPlayersExcept(_musicTween, null, fadeSeconds);
    }

    public bool IsMusicPlaying => CurrentMusic is not null && _musicCurrent.Playing;

    /// <summary>Quantos players de música estão tocando (a que entra e as que ainda saem num fade).</summary>
    public int ActiveMusicPlayers
    {
        get
        {
            var count = 0;
            foreach (var p in _musicPlayers)
            {
                count += p.Playing ? 1 : 0;
            }

            return count;
        }
    }

    /// <summary>Liga o reverb do barramento Music (Brechas, GDD §6).</summary>
    public void SetMusicReverb(bool enabled)
    {
        var index = AudioServer.GetBusIndex(MusicBus);
        if (index < 0 || AudioServer.GetBusEffectCount(index) == 0)
        {
            WarnOnce("reverb", "AudioManager: barramento Music sem efeito de reverb (default_bus_layout.tres).");
            return;
        }

        AudioServer.SetBusEffectEnabled(index, 0, enabled);
        MusicReverbEnabled = enabled;
    }

    // --- Volumes ---

    /// <summary>Volume linear (0–1) de um barramento: Master, Music, SFX ou UI.</summary>
    public float GetBusVolume(string bus) => _volumes.TryGetValue(bus, out var v) ? v : 1f;

    /// <summary>
    /// Aplica o volume linear (0–1) de um barramento. Com <paramref name="persist"/> grava na hora em
    /// user://settings.cfg; um slider em arrasto passa <c>persist: false</c> e chama <see cref="SaveSettings"/> ao soltar.
    /// </summary>
    public void SetBusVolume(string bus, float linear, bool persist = true)
    {
        if (ApplyBusVolume(bus, linear) && persist)
        {
            SaveSettings();
        }
    }

    /// <summary>Grava os volumes atuais em user://settings.cfg, seção [audio]. As outras seções do arquivo ficam.</summary>
    public void SaveSettings()
    {
        // relê o arquivo antes de gravar para não apagar o que outras telas salvaram depois do boot
        var settings = new ConfigFile();
        settings.Load(_settingsPath);
        foreach (var (bus, volume) in _volumes)
        {
            settings.SetValue(SettingsSection, bus.ToLowerInvariant(), volume);
        }

        var error = settings.Save(_settingsPath);
        if (error != Error.Ok)
        {
            Log.Error($"AudioManager: não foi possível gravar {_settingsPath}: {error}");
        }
    }

    // --- Música por local ---

    /// <summary>
    /// Dá a um local (o texto de <c>LocationChanged</c>) uma música própria, por exemplo cada mapa de Brecha:
    /// <c>RegisterLocationMusic(map.Name, "brecha", reverb: true)</c>. Locais sem registro tocam
    /// <see cref="DefaultLocationMusic"/> sem reverb. Registrar de novo substitui.
    /// </summary>
    public void RegisterLocationMusic(string location, string musicId, bool reverb = false)
    {
        if (Array.IndexOf(MusicIds, musicId) < 0)
        {
            WarnOnce("music:" + musicId, $"AudioManager: música desconhecida \"{musicId}\" para o local \"{location}\".");
            return;
        }

        _locationMusic[location] = (musicId, reverb);
    }

    public void UnregisterLocationMusic(string location) => _locationMusic.Remove(location);

    /// <summary>Toca a música do local, como faz <c>LocationChanged</c> (exposto para testes e trocas de área sem o sinal).</summary>
    public void ApplyLocationMusic(string location)
    {
        var (music, reverb) = _locationMusic.TryGetValue(location, out var entry) ? entry : (DefaultLocationMusic, false);
        SetMusicReverb(reverb);
        PlayMusic(music);
    }

    // --- Internos ---

    private void CreatePools()
    {
        _flat = new AudioStreamPlayer[FlatVoices];
        for (var i = 0; i < _flat.Length; i++)
        {
            _flat[i] = new AudioStreamPlayer { Name = $"Sfx{i}", Bus = _sfxBusName };
            AddChild(_flat[i]);
        }

        _positional = new AudioStreamPlayer2D[PositionalVoices];
        for (var i = 0; i < _positional.Length; i++)
        {
            _positional[i] = new AudioStreamPlayer2D { Name = $"Sfx2D{i}", Bus = _sfxBusName };
            AddChild(_positional[i]);
        }

        _flatVoices = new VoiceAllocator(FlatVoices);
        _positionalVoices = new VoiceAllocator(PositionalVoices);
        _flatBusy = i => _flat[i].Playing;
        _positionalBusy = i => _positional[i].Playing;

        _musicPlayers = new AudioStreamPlayer[MusicPlayers];
        for (var i = 0; i < _musicPlayers.Length; i++)
        {
            _musicPlayers[i] = new AudioStreamPlayer { Name = $"Music{i}", Bus = _musicBusName };
            AddChild(_musicPlayers[i]);
        }

        _musicCurrent = _musicPlayers[0];
        CreateWorldAudio(); // passo e ambiente do mundo (AudioManager.Ambient.cs, frente B4a)
    }

    /// <summary>Player que ainda toca <paramref name="stream"/> (saindo num fade), se houver.</summary>
    private AudioStreamPlayer? FindMusicPlayerWith(AudioStream stream)
    {
        foreach (var p in _musicPlayers)
        {
            if (p.Playing && p.Stream == stream)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>Um player de música parado; se todos tocam (trocas seguidas no meio do fade), o mais baixo.</summary>
    private AudioStreamPlayer PickIncomingMusicPlayer()
    {
        AudioStreamPlayer? quietest = null;
        foreach (var p in _musicPlayers)
        {
            if (!p.Playing)
            {
                return p;
            }

            if (p != _musicCurrent && (quietest is null || p.VolumeDb < quietest.VolumeDb))
            {
                quietest = p;
            }
        }

        return quietest ?? _musicPlayers[0];
    }

    private void StopMusicPlayersExcept(AudioStreamPlayer? keep)
    {
        foreach (var p in _musicPlayers)
        {
            if (p != keep)
            {
                p.Stop();
            }
        }
    }

    /// <summary>Leva a 0, a partir do volume atual, e depois para todo player de música menos <paramref name="keep"/>.</summary>
    private void FadeOutMusicPlayersExcept(Tween tween, AudioStreamPlayer? keep, float fadeSeconds)
    {
        foreach (var p in _musicPlayers)
        {
            if (p == keep || !p.Playing)
            {
                continue;
            }

            var player = p;
            var from = Mathf.DbToLinear(player.VolumeDb);
            tween.TweenMethod(Callable.From<float>(v => player.VolumeDb = AudioVolume.ToDb(v)), from, 0f, fadeSeconds);
        }

        tween.TweenInterval(fadeSeconds); // garante que o Tween nunca fica vazio (o Godot acusa erro)
        tween.Chain().TweenCallback(Callable.From(() => StopMusicPlayersExcept(keep)));
    }

    private AudioStream? GetMusicStream(string id)
    {
        if (_music.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (_released || Array.IndexOf(MusicIds, id) < 0 || LoadStream(MusicDir + id + ".wav") is not { } stream)
        {
            return null;
        }

        _music[id] = stream;
        return stream;
    }

    private AudioStream? LoadStream(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            WarnOnce("file:" + path, $"AudioManager: arquivo de áudio ausente {path} (rode tools/audio/gen_*.py).");
            return null;
        }

        return GD.Load<AudioStream>(path);
    }

    private void LoadSettings()
    {
        var settings = new ConfigFile();
        var error = settings.Load(_settingsPath);
        if (error != Error.Ok && error != Error.FileNotFound)
        {
            Log.Warning($"AudioManager: {_settingsPath} ilegível ({error}); usando volumes padrão.");
        }

        foreach (var bus in Buses)
        {
            var value = settings.GetValue(SettingsSection, bus.ToLowerInvariant(), 1f);
            var linear = value.VariantType is Variant.Type.Float or Variant.Type.Int ? value.AsSingle() : 1f;
            ApplyBusVolume(bus, linear);
        }
    }

    private bool ApplyBusVolume(string bus, float linear)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index < 0)
        {
            WarnOnce("bus:" + bus, $"AudioManager: barramento de áudio desconhecido \"{bus}\".");
            return false;
        }

        var v = AudioVolume.Clamp01(linear);
        _volumes[bus] = v;
        AudioServer.SetBusVolumeDb(index, AudioVolume.ToDb(v));
        AudioServer.SetBusMute(index, v <= 0f);
        return true;
    }

    /// <summary>
    /// Ao fechar o jogo, para tudo e solta as referências C# aos streams. Os players já se param ao sair da árvore
    /// (antes deste _ExitTree), mas o AudioServer só descarta um playback parado no próximo ciclo de mixagem;
    /// esperamos um ciclo (60 ms, só no encerramento) e damos Dispose nos wrappers para o Godot .NET não acusar
    /// recursos vazados ao sair. Cuidado: o GD.Load devolve o mesmo wrapper C# para o mesmo recurso, então ninguém
    /// deve usar estes streams depois disso; com <c>_released</c>, PlaySfx/PlayMusic viram no-op silencioso.
    /// </summary>
    private void ReleaseStreams()
    {
        _released = true;
        _musicTween?.Kill();
        _musicTween = null;
        var hadStream = false;
        foreach (var p in _flat)
        {
            hadStream |= p.Stream is not null;
            p.Stop();
            p.Stream = null;
        }

        foreach (var p in _positional)
        {
            hadStream |= p.Stream is not null;
            p.Stop();
            p.Stream = null;
        }

        foreach (var p in _musicPlayers)
        {
            hadStream |= p.Stream is not null;
            p.Stop();
            p.Stream = null;
        }

        hadStream |= StopWorldAudio(); // frente B4a

        if (hadStream)
        {
            OS.DelayMsec(ExitDrainMs);
        }

        foreach (var stream in _sfx.Values)
        {
            stream.Dispose();
        }

        foreach (var stream in _music.Values)
        {
            stream.Dispose();
        }

        _sfx.Clear();
        _music.Clear();
        DisposeWorldAudio(); // frente B4a
    }

    private void WarnOnce(string key, string message)
    {
        if (_warned.Add(key))
        {
            Log.Warning(message);
        }
    }

    // --- Ganchos do EventBus ---

    private void OnRankChanged(Rank previousRank, Rank newRank)
    {
        if (newRank > previousRank)
        {
            PlaySfx("rank", pitchJitter: 0f);
        }
    }

    private void OnPlayerDamaged(int amount, int currentHp, int maxHp) => PlaySfx("hurt");

    private void OnFragmentsChanged(int total) => PlaySfx("coin");

    private void OnLocationChanged(string locationName) => ApplyLocationMusic(locationName);

    private void OnPortalEntered(string districtId)
    {
        SetMusicReverb(true);
        PlayMusic("brecha");
    }
}
