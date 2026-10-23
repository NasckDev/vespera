using Godot;

namespace Vespera.Game.Audio;

/// <summary>
/// Trilha de fundo: uma faixa por mapa ou tela (seção "musica" de data/audio/sons.json), em loop, com transição suave.
/// Chave sem entrada mantém a faixa atual (ex.: pausa, diário); entrada vazia silencia (vídeos têm áudio próprio).
/// Mesma faixa em mapas seguidos (salas da Brecha) não recomeça.
/// </summary>
public static class MusicDirector
{
    private const string Section = "musica";
    private const float FadeSeconds = 1.2f;
    private const float SilentDb = -40f;

    private static AudioStreamPlayer? _current;
    private static string _playing = string.Empty;

    public static string PlayingPath => _playing;

    public static void Play(string key)
    {
        if (!SoundBank.Has(Section, key))
        {
            return;
        }

        var path = SoundBank.PathOf(Section, key);
        if (path == _playing)
        {
            return;
        }

        _playing = path;
        FadeOut(_current);
        _current = null;
        if (SoundBank.Get(Section, key) is not { } stream || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        if (stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
        }

        var player = new AudioStreamPlayer { Stream = stream, Bus = "Music", VolumeDb = SilentDb, ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.AddChild(player);
        player.Play();
        player.CreateTween().TweenProperty(player, "volume_db", 0f, FadeSeconds);
        _current = player;
    }

    /// <summary>Toca um jingle curto (seção "jingle") por cima da trilha.</summary>
    public static void Jingle(string name)
    {
        if (SoundBank.Get("jingle", name) is not { } stream || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        var player = new AudioStreamPlayer { Stream = stream, Bus = "SFX", ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    private static void FadeOut(AudioStreamPlayer? player)
    {
        if (player is null || !GodotObject.IsInstanceValid(player))
        {
            return;
        }

        var tween = player.CreateTween();
        tween.TweenProperty(player, "volume_db", SilentDb, FadeSeconds);
        tween.TweenCallback(Callable.From(player.QueueFree));
    }
}
