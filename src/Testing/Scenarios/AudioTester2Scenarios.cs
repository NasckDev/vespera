using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;

namespace Vespera.Testing;

/// <summary>Cenários do tester de áudio (rodada 2): crossfade de ida e volta e volume final da música.</summary>
public partial class SmokeTest
{
    private static AudioStreamPlayer[] MusicPlayersOf(AudioManager audio) =>
        (AudioStreamPlayer[])(typeof(AudioManager).GetField("_musicPlayers", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(audio)
            ?? throw new SmokeFailure("AudioManager._musicPlayers não encontrado"));

    [SmokeScenario("áudio (tester 2): fade termina com a música em 0 dB e só um player", order: 26)]
    private async Task AudioMusicFadeEndsAtFullVolume()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        audio.PlayMusic("vespera", 0.3f);
        await WaitSeconds(0.6);

        var playing = 0;
        foreach (var p in MusicPlayersOf(audio))
        {
            if (!p.Playing)
            {
                continue;
            }

            playing++;
            Require(Mathf.Abs(p.VolumeDb) < 0.01f, $"player {p.Name} terminou o fade em {p.VolumeDb:0.00} dB, esperava 0 dB");
        }

        Require(playing == 1, $"esperava 1 player tocando depois do fade, há {playing}");
        audio.ApplyLocationMusic("Lumeira");
        await WaitSeconds(1.8);
    }

    [SmokeScenario("áudio (tester 2): ida e volta no meio do fade não duplica a faixa", order: 27)]
    private async Task AudioMusicBackAndForthNoDuplicate()
    {
        await WaitForAudioWorld();
        var audio = AudioManager.Instance;
        audio.PlayMusic("city_day", 0f);
        await WaitSeconds(0.1);

        // cidade → Brecha → cidade em menos de um fade (entrar no portal e sair na hora)
        audio.PlayMusic("brecha", 1.5f);
        await WaitSeconds(0.3);
        audio.PlayMusic("city_day", 1.5f);
        await WaitSeconds(0.1);

        var cityCopies = 0;
        var city = GD.Load<AudioStream>("res://assets/audio/music/city_day.wav");
        foreach (var p in MusicPlayersOf(audio))
        {
            cityCopies += p.Playing && p.Stream == city ? 1 : 0;
        }

        await WaitSeconds(1.6);
        Require(cityCopies <= 1, $"city_day tocando em {cityCopies} players ao mesmo tempo (eco/fase dupla no crossfade)");
    }
}
