using System;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Audio;
using Vespera.Core.Ranks;

namespace Vespera.Testing;

/// <summary>Cenários de áudio: todos os efeitos e músicas tocam, o pool não cresce e os ganchos do EventBus soam.</summary>
public partial class SmokeTest
{
    /// <summary>Espera qualquer cena de jogo com o Player (Lumeira hoje, World depois do port), sem depender do nome.</summary>
    private async Task WaitForAudioWorld()
    {
        await WaitUntil(() => FindPlayer() is not null, timeoutSeconds: 10, "cena de jogo com Player");
        await WaitSeconds(0.5); // fade + _Ready dos filhos + LocationChanged
    }

    [SmokeScenario("áudio: 18 efeitos e pool sem nós novos", order: 20)]
    private async Task AudioSfxPool()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        Require(AudioManager.SfxIds.Length == 18, $"esperava 18 ids de SFX, há {AudioManager.SfxIds.Length}");
        foreach (var id in AudioManager.SfxIds)
        {
            Require(audio.HasSfx(id), $"efeito {id} não carregou (assets/audio/sfx/{id}.wav)");
        }

        // aquecimento: cada efeito uma vez, sem posição e no mundo
        foreach (var id in AudioManager.SfxIds)
        {
            audio.PlaySfx(id);
            audio.PlaySfx(id, player.GlobalPosition);
        }

        await Frames(2);
        var children = audio.GetChildCount();
        var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);

        // rajada: muitos acertos no mesmo frame não podem criar nós nem passar do limite por id
        for (var round = 0; round < 5; round++)
        {
            foreach (var id in AudioManager.SfxIds)
            {
                audio.PlaySfx(id);
                audio.PlaySfx(id, player.GlobalPosition + new Vector2(round * 20, 0));
            }
        }

        for (var i = 0; i < 80; i++)
        {
            audio.PlaySfx("hit", player.GlobalPosition);
        }

        Require(audio.ActiveVoices("hit") <= AudioManager.MaxVoicesPerId * 2,
            $"hit com {audio.ActiveVoices("hit")} vozes, limite {AudioManager.MaxVoicesPerId} por pool");
        Require(audio.ActiveSfxVoices <= audio.SfxPoolSize, "mais vozes ativas que o pool");

        audio.PlaySfx("hit", pitchJitter: 5f); // limitado a MaxPitchJitter: o pitch nunca chega a 0
        audio.PlaySfx("nao_existe");
        audio.PlaySfx("nao_existe", player.GlobalPosition);

        await Frames(5);
        Require(audio.GetChildCount() == children, $"AudioManager criou nós: {children} → {audio.GetChildCount()}");
        var nodesAfter = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        Require(Math.Abs(nodesAfter - nodes) < 0.5, $"contagem de nós mudou ao tocar sons: {nodes} → {nodesAfter}");
    }

    [SmokeScenario("áudio: 3 músicas em loop com crossfade e reverb", order: 21)]
    private async Task AudioMusic()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        Require(audio.CurrentMusic == AudioManager.DefaultLocationMusic,
            $"entrar na cidade deveria tocar city_day, toca {audio.CurrentMusic ?? "nada"}");

        foreach (var id in AudioManager.MusicIds)
        {
            var stream = GD.Load<AudioStreamWav>($"res://assets/audio/music/{id}.wav");
            Require(stream.LoopMode == AudioStreamWav.LoopModeEnum.Forward, $"{id}.wav sem loop Forward no import ({stream.LoopMode})");
            Require(stream.GetLength() >= 30, $"{id}.wav curto demais: {stream.GetLength():0.0}s");

            audio.PlayMusic(id, 0.3f);
            await WaitSeconds(0.45);
            Require(audio.CurrentMusic == id && audio.IsMusicPlaying, $"música {id} não está tocando");
        }

        audio.PlayMusic("nao_existe");
        Require(audio.CurrentMusic == "brecha", "id de música desconhecido não pode trocar a música");

        // trocas seguidas no meio do fade: quem sai continua tocando (sem corte), quem entra é a última pedida
        audio.PlayMusic("city_day", 1.5f);
        await WaitSeconds(0.2);
        audio.PlayMusic("vespera", 1.5f);
        Require(audio.ActiveMusicPlayers == 3, $"brecha e city_day deveriam seguir em fade, tocam {audio.ActiveMusicPlayers} players");
        audio.PlayMusic("brecha", 1.5f);
        Require(audio.CurrentMusic == "brecha" && audio.IsMusicPlaying, "a última música pedida deveria entrar");
        await WaitSeconds(1.8);
        Require(audio.ActiveMusicPlayers == 1, $"depois do fade só a música atual toca, tocam {audio.ActiveMusicPlayers}");

        audio.SetMusicReverb(true);
        var music = AudioServer.GetBusIndex(AudioManager.MusicBus);
        Require(AudioServer.IsBusEffectEnabled(music, 0), "reverb do bus Music não ligou");
        audio.SetMusicReverb(false);
        Require(!AudioServer.IsBusEffectEnabled(music, 0), "reverb do bus Music não desligou");

        // música por local: registrado (mapa de Brecha) liga brecha + reverb; qualquer outro local volta à cidade
        const string testBrecha = "Brecha do smoke";
        audio.RegisterLocationMusic(testBrecha, "brecha", reverb: true);
        audio.PlayMusic("city_day", 0f);
        audio.ApplyLocationMusic(testBrecha);
        Require(audio.CurrentMusic == "brecha" && audio.MusicReverbEnabled, "local registrado deveria tocar brecha com reverb");
        audio.ApplyLocationMusic("Rua da Forja");
        Require(audio.CurrentMusic == "city_day" && !audio.MusicReverbEnabled, "local sem registro deveria voltar a city_day sem reverb");
        audio.UnregisterLocationMusic(testBrecha);
        audio.ApplyLocationMusic(testBrecha);
        Require(audio.CurrentMusic == "city_day", "local desregistrado deveria tocar city_day");
        audio.RegisterLocationMusic("Local inválido", "nao_existe"); // só avisa

        var master = AudioServer.GetBusIndex(AudioManager.MasterBus);
        Require(AudioServer.GetBusEffectCount(master) >= 1 && AudioServer.GetBusEffect(master, 0) is AudioEffectHardLimiter
            && AudioServer.IsBusEffectEnabled(master, 0), "Master deveria ter o limitador com ganho de make-up (D32)");
        await WaitSeconds(0.2);
    }

    [SmokeScenario("áudio: volumes por barramento persistem", order: 22)]
    private async Task AudioVolumes()
    {
        var audio = AudioManager.Instance;
        foreach (var bus in AudioManager.Buses)
        {
            Require(AudioServer.GetBusIndex(bus) >= 0, $"barramento {bus} ausente em default_bus_layout.tres");
        }

        audio.SetBusVolume(AudioManager.MusicBus, 0.5f);
        audio.SetBusVolume(AudioManager.SfxBus, 0f);
        var musicIndex = AudioServer.GetBusIndex(AudioManager.MusicBus);
        var sfxIndex = AudioServer.GetBusIndex(AudioManager.SfxBus);
        Require(Mathf.Abs(AudioServer.GetBusVolumeDb(musicIndex) - AudioVolume.ToDb(0.5f)) < 0.01f, "volume do Music não aplicou");
        Require(AudioServer.IsBusMute(sfxIndex), "volume 0 deveria mutar o SFX");

        var saved = new ConfigFile();
        Require(saved.Load(audio.SettingsFile) == Error.Ok, $"{audio.SettingsFile} não foi gravado");
        Require(Mathf.Abs(saved.GetValue("audio", "music", 1f).AsSingle() - 0.5f) < 0.001f, "volume do Music não persistiu");

        // slider em arrasto: aplica sem gravar; SaveSettings grava ao soltar
        audio.SetBusVolume(AudioManager.MusicBus, 0.75f, persist: false);
        Require(Mathf.Abs(AudioServer.GetBusVolumeDb(musicIndex) - AudioVolume.ToDb(0.75f)) < 0.01f, "persist:false deveria aplicar");
        saved.Load(audio.SettingsFile);
        Require(Mathf.Abs(saved.GetValue("audio", "music", 1f).AsSingle() - 0.5f) < 0.001f, "persist:false não pode gravar");
        audio.SaveSettings();
        saved.Load(audio.SettingsFile);
        Require(Mathf.Abs(saved.GetValue("audio", "music", 1f).AsSingle() - 0.75f) < 0.001f, "SaveSettings deveria gravar 0.75");

        audio.SetBusVolume(AudioManager.MusicBus, 1f);
        audio.SetBusVolume(AudioManager.SfxBus, 1f);
        Require(!AudioServer.IsBusMute(sfxIndex), "SFX deveria desmutar");
        audio.SetBusVolume("NaoExiste", 0.3f); // só avisa
        await NextPhysicsFrame();
    }

    [SmokeScenario("áudio: ganchos de rank, dano e Fragmentos", order: 23)]
    private async Task AudioHooks()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        var game = GameManager.Instance;

        game.AddFragments(1);
        Require(audio.ActiveVoices("coin") > 0, "FragmentsChanged deveria tocar coin");
        Require(game.TrySpendFragments(1), "devolver o Fragmento do teste");

        game.DamagePlayer(1);
        Require(audio.ActiveVoices("hurt") > 0, "PlayerDamaged deveria tocar hurt");
        game.HealPlayer(1);

        // subida de rank de verdade (save descartável do smoke), sem emitir RankChanged falso no barramento
        var startRank = game.Rank;
        Require(startRank != Rank.Alvor, "o smoke não deveria chegar a Alvor antes deste cenário");
        for (var guard = 0; game.Rank == startRank && guard < 100; guard++)
        {
            game.AddXp(500);
        }

        Require(game.Rank > startRank, $"AddXp não subiu de {startRank}");
        Require(audio.ActiveVoices("rank") > 0, "RankChanged deveria tocar rank");
        await WaitSeconds(1.0); // deixa o arpejo terminar
        Require(audio.ActiveVoices("rank") == 0, "o player do rank deveria liberar a voz ao terminar");
    }
}
