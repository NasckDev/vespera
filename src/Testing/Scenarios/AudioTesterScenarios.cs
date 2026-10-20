using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Audio;

namespace Vespera.Testing;

/// <summary>Cenários do tester de áudio: PlaySfx não aloca no heap gerenciado e os volumes gravados são lidos no boot.</summary>
public partial class SmokeTest
{
    [SmokeScenario("áudio (tester): PlaySfx em rajada não aloca", order: 24)]
    private async Task AudioSfxNoAllocation()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        var pos = FindPlayer()?.GlobalPosition ?? Vector2.Zero;

        // aquecimento (JIT, caches de StringName)
        for (var i = 0; i < 50; i++)
        {
            audio.PlaySfx("hit");
            audio.PlaySfx("hit", pos);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 500; i++)
        {
            audio.PlaySfx(AudioManager.SfxIds[i % AudioManager.SfxIds.Length]);
            audio.PlaySfx(AudioManager.SfxIds[i % AudioManager.SfxIds.Length], pos);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated < 1024, $"1000 chamadas de PlaySfx alocaram {allocated} bytes no heap gerenciado");
        await Frames(2);
    }

    [SmokeScenario("áudio (tester): volumes gravados são lidos no boot", order: 25)]
    private async Task AudioVolumesReadOnBoot()
    {
        var audio = AudioManager.Instance;
        var cfg = new ConfigFile();
        cfg.Load(audio.SettingsFile);
        cfg.SetValue("audio", "music", 0.25f);
        cfg.SetValue("audio", "sfx", 0.5f);
        cfg.SetValue("audio", "ui", 0);
        cfg.SetValue("video", "fullscreen", true); // outra seção, de outra tela
        Require(cfg.Save(audio.SettingsFile) == Error.Ok, "não gravou o settings de teste");

        // simula o boot: o _Ready chama LoadSettings
        var load = typeof(AudioManager).GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new SmokeFailure("AudioManager.LoadSettings não encontrado");
        load.Invoke(audio, null);

        var music = AudioServer.GetBusIndex(AudioManager.MusicBus);
        var sfx = AudioServer.GetBusIndex(AudioManager.SfxBus);
        var ui = AudioServer.GetBusIndex(AudioManager.UiBus);
        Require(Mathf.Abs(audio.GetBusVolume(AudioManager.MusicBus) - 0.25f) < 0.001f, $"Music lido {audio.GetBusVolume(AudioManager.MusicBus)}");
        Require(Mathf.Abs(AudioServer.GetBusVolumeDb(music) - AudioVolume.ToDb(0.25f)) < 0.01f, "Music não aplicou no barramento");
        Require(Mathf.Abs(AudioServer.GetBusVolumeDb(sfx) - AudioVolume.ToDb(0.5f)) < 0.01f, "SFX não aplicou no barramento");
        Require(AudioServer.IsBusMute(ui), "ui=0 (inteiro) deveria mutar o UI");

        // gravar um volume preserva a outra seção
        audio.SetBusVolume(AudioManager.MusicBus, 1f);
        var reread = new ConfigFile();
        reread.Load(audio.SettingsFile);
        Require(reread.GetValue("video", "fullscreen", false).AsBool(), "SetBusVolume apagou a seção [video]");

        audio.SetBusVolume(AudioManager.SfxBus, 1f);
        audio.SetBusVolume(AudioManager.UiBus, 1f);
        Require(!AudioServer.IsBusMute(ui), "UI deveria desmutar");
        await NextPhysicsFrame();
    }
}
