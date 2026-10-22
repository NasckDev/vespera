using System;
using Godot;

namespace Vespera.Autoload;

/// <summary>
/// Opções de jogo fora do áudio (tela cheia, tremor de tela, painel de missão; <c>OPT</c> da v2, <c>extra.js:2-4</c>),
/// gravadas no mesmo arquivo do <see cref="AudioManager"/> (seções <c>[video]</c> e <c>[game]</c>; as outras seções
/// ficam intactas). Volumes continuam no AudioManager.
/// </summary>
public static class GameSettings
{
    private const string VideoSection = "video";
    private const string GameSection = "game";

    private static bool _loaded;
    private static bool _fullscreen;
    private static bool _screenShake = true;
    private static bool _questHints = true;

    /// <summary>Alguma opção mudou (o HUD esconde o painel de missão, a câmera desliga o tremor…).</summary>
    public static event Action? Changed;

    public static bool Fullscreen
    {
        get
        {
            EnsureLoaded();
            return _fullscreen;
        }
        set
        {
            EnsureLoaded();
            _fullscreen = value;
            ApplyVideo();
            Save();
        }
    }

    /// <summary>Tremor de tela ligado (padrão da v2: sim).</summary>
    public static bool ScreenShake
    {
        get
        {
            EnsureLoaded();
            return _screenShake;
        }
        set
        {
            EnsureLoaded();
            _screenShake = value;
            Save();
        }
    }

    /// <summary>Painel da missão atual no HUD (padrão da v2: visível).</summary>
    public static bool QuestHints
    {
        get
        {
            EnsureLoaded();
            return _questHints;
        }
        set
        {
            EnsureLoaded();
            _questHints = value;
            Save();
        }
    }

    /// <summary>Lê o arquivo e aplica a tela cheia. Chamado no boot (Main).</summary>
    public static void Load()
    {
        _loaded = true;
        var config = new ConfigFile();
        if (config.Load(FilePath) == Error.Ok)
        {
            _fullscreen = (bool)config.GetValue(VideoSection, "fullscreen", false);
            _screenShake = (bool)config.GetValue(GameSection, "shake", true);
            _questHints = (bool)config.GetValue(GameSection, "hints", true);
        }

        ApplyVideo();
    }

    private static string FilePath => AudioManager.Instance?.SettingsFile ?? AudioManager.SettingsPath;

    private static void EnsureLoaded()
    {
        if (!_loaded)
        {
            Load();
        }
    }

    private static void ApplyVideo()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        var mode = DisplayServer.WindowGetMode();
        var wantFullscreen = _fullscreen;
        var isFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (wantFullscreen != isFullscreen)
        {
            DisplayServer.WindowSetMode(wantFullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        }
    }

    private static void Save()
    {
        var config = new ConfigFile();
        config.Load(FilePath); // preserva [audio] e o resto
        config.SetValue(VideoSection, "fullscreen", _fullscreen);
        config.SetValue(GameSection, "shake", _screenShake);
        config.SetValue(GameSection, "hints", _questHints);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            Log.Warning($"Não foi possível gravar as opções em {FilePath}: {error}");
        }

        Changed?.Invoke();
    }
}
