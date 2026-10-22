using System;
using Godot;

namespace Vespera.Autoload;

/// <summary>Velocidade da máquina de escrever do diálogo (C1-D5).</summary>
public enum DialogSpeed
{
    Slow = 0,
    Normal = 1,
    Fast = 2,
    Instant = 3,
}

/// <summary>
/// Opções de jogo fora do áudio (tela cheia, tremor de tela, painel de missão; <c>OPT</c> da v2, <c>extra.js:2-4</c>) e
/// as de acessibilidade da frente C1 (escala da interface, tamanho do texto, velocidade do texto, intensidade do tremor,
/// reduzir flashes, alto contraste, legendas, arco segurar/alternar, mira assistida), gravadas no mesmo arquivo do
/// <see cref="AudioManager"/> (seções <c>[video]</c>, <c>[game]</c> e <c>[access]</c>; as outras seções ficam
/// intactas). Volumes continuam no AudioManager e as teclas em <see cref="InputBindings"/>. Tudo aplica na hora pelo
/// evento <see cref="Changed"/>.
/// </summary>
public static class GameSettings
{
    private const string VideoSection = "video";
    private const string GameSection = "game";
    private const string AccessSection = "access";

    /// <summary>Escalas da interface oferecidas (C1-D4).</summary>
    public static readonly float[] UiScales = [1f, 1.25f, 1.5f];

    /// <summary>Tamanhos do texto de leitura oferecidos (C1-D4).</summary>
    public static readonly float[] TextScales = [1f, 1.15f, 1.3f];

    private static bool _loaded;
    private static bool _fullscreen;
    private static float _shake = 1f;
    private static bool _questHints = true;
    private static float _uiScale = 1f;
    private static float _textScale = 1f;
    private static DialogSpeed _dialogSpeed = DialogSpeed.Normal;
    private static bool _reduceFlashes;
    private static bool _highContrast;
    private static bool _captions;
    private static bool _bowToggle;
    private static bool _aimAssist;

    /// <summary>Alguma opção mudou (o HUD esconde o painel de missão, a câmera ajusta o tremor…).</summary>
    public static event Action? Changed;

    public static bool Fullscreen
    {
        get => Get(ref _fullscreen);
        set
        {
            Set(ref _fullscreen, value);
            ApplyVideo();
        }
    }

    /// <summary>Tremor de tela ligado (intensidade &gt; 0). Ligar volta a 100%.</summary>
    public static bool ScreenShake
    {
        get => ShakeIntensity > 0f;
        set => ShakeIntensity = value ? 1f : 0f;
    }

    /// <summary>Intensidade do tremor de tela, 0–1 em passos de 10% (padrão da v2: 100%).</summary>
    public static float ShakeIntensity
    {
        get => Get(ref _shake);
        set => Set(ref _shake, Mathf.Clamp(Mathf.Snapped(value, 0.1f), 0f, 1f));
    }

    /// <summary>Painel da missão atual no HUD (padrão da v2: visível).</summary>
    public static bool QuestHints
    {
        get => Get(ref _questHints);
        set => Set(ref _questHints, value);
    }

    /// <summary>Escala da interface (100/125/150%): HUD, toasts, diálogo e telas, até caber na tela.</summary>
    public static float UiScale
    {
        get => Get(ref _uiScale);
        set => Set(ref _uiScale, Nearest(UiScales, value));
    }

    /// <summary>Tamanho do texto de leitura (diálogo, missão, notas, legendas, dicas): 100/115/130%.</summary>
    public static float TextScale
    {
        get => Get(ref _textScale);
        set => Set(ref _textScale, Nearest(TextScales, value));
    }

    public static DialogSpeed DialogSpeed
    {
        get => Get(ref _dialogSpeed);
        set => Set(ref _dialogSpeed, (DialogSpeed)Math.Clamp((int)value, 0, 3));
    }

    /// <summary>Caracteres por segundo da máquina de escrever (<see cref="float.PositiveInfinity"/> = instantâneo).</summary>
    public static float DialogCharsPerSecond => DialogSpeed switch
    {
        DialogSpeed.Slow => 40f,
        DialogSpeed.Fast => 150f,
        DialogSpeed.Instant => float.PositiveInfinity,
        _ => 75f, // ui.js:30
    };

    /// <summary>Reduzir flashes: clarões de tela cheia, brilho de acerto e explosões ficam bem mais fracos.</summary>
    public static bool ReduceFlashes
    {
        get => Get(ref _reduceFlashes);
        set => Set(ref _reduceFlashes, value);
    }

    /// <summary>Fator dos clarões (1 normal, 0,3 com "reduzir flashes").</summary>
    public static float FlashScale => ReduceFlashes ? 0.3f : 1f;

    /// <summary>HUD em alto contraste (fundos opacos, texto claro, contorno nos avisos de perigo).</summary>
    public static bool HighContrast
    {
        get => Get(ref _highContrast);
        set => Set(ref _highContrast, value);
    }

    /// <summary>Legendas para sons importantes (portal abrindo, chefe, perigo fora da tela).</summary>
    public static bool Captions
    {
        get => Get(ref _captions);
        set => Set(ref _captions, value);
    }

    /// <summary>Ações de segurar (carga do arco) por alternância: apertar começa, apertar de novo solta.</summary>
    public static bool BowToggle
    {
        get => Get(ref _bowToggle);
        set => Set(ref _bowToggle, value);
    }

    /// <summary>Mira assistida leve no gamepad (puxa a mira para o monstro mais perto do cone).</summary>
    public static bool AimAssist
    {
        get => Get(ref _aimAssist);
        set => Set(ref _aimAssist, value);
    }

    /// <summary>Lê o arquivo e aplica a tela cheia. Chamado no boot (Main).</summary>
    public static void Load()
    {
        _loaded = true;
        var config = new ConfigFile();
        if (config.Load(FilePath) == Error.Ok)
        {
            _fullscreen = (bool)config.GetValue(VideoSection, "fullscreen", false);
            // "shake" era liga/desliga (B3-D48); a intensidade nova vence quando existe.
            var shakeOn = (bool)config.GetValue(GameSection, "shake", true);
            _shake = Mathf.Clamp((float)config.GetValue(AccessSection, "shake_intensity", shakeOn ? 1f : 0f), 0f, 1f);
            _questHints = (bool)config.GetValue(GameSection, "hints", true);
            _uiScale = Nearest(UiScales, (float)config.GetValue(AccessSection, "ui_scale", 1f));
            _textScale = Nearest(TextScales, (float)config.GetValue(AccessSection, "text_scale", 1f));
            _dialogSpeed = (DialogSpeed)Math.Clamp((int)config.GetValue(AccessSection, "dialog_speed", (int)DialogSpeed.Normal), 0, 3);
            _reduceFlashes = (bool)config.GetValue(AccessSection, "reduce_flashes", false);
            _highContrast = (bool)config.GetValue(AccessSection, "high_contrast", false);
            _captions = (bool)config.GetValue(AccessSection, "captions", false);
            _bowToggle = (bool)config.GetValue(AccessSection, "bow_toggle", false);
            _aimAssist = (bool)config.GetValue(AccessSection, "aim_assist", false);
        }

        ApplyVideo();
    }

    /// <summary>Volta tudo da acessibilidade ao padrão (smoke test).</summary>
    public static void ResetAccessibility()
    {
        EnsureLoaded();
        _shake = 1f;
        _uiScale = 1f;
        _textScale = 1f;
        _dialogSpeed = DialogSpeed.Normal;
        _reduceFlashes = false;
        _highContrast = false;
        _captions = false;
        _bowToggle = false;
        _aimAssist = false;
        Save();
    }

    private static string FilePath => AudioManager.Instance?.SettingsFile ?? AudioManager.SettingsPath;

    private static T Get<T>(ref T field)
    {
        EnsureLoaded();
        return field;
    }

    private static void Set<T>(ref T field, T value)
    {
        EnsureLoaded();
        field = value;
        Save();
    }

    private static float Nearest(float[] options, float value)
    {
        var best = options[0];
        foreach (var option in options)
        {
            if (MathF.Abs(option - value) < MathF.Abs(best - value))
            {
                best = option;
            }
        }

        return best;
    }

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
        config.Load(FilePath); // preserva [audio], [input] e o resto
        config.SetValue(VideoSection, "fullscreen", _fullscreen);
        config.SetValue(GameSection, "shake", _shake > 0f);
        config.SetValue(GameSection, "hints", _questHints);
        config.SetValue(AccessSection, "shake_intensity", _shake);
        config.SetValue(AccessSection, "ui_scale", _uiScale);
        config.SetValue(AccessSection, "text_scale", _textScale);
        config.SetValue(AccessSection, "dialog_speed", (int)_dialogSpeed);
        config.SetValue(AccessSection, "reduce_flashes", _reduceFlashes);
        config.SetValue(AccessSection, "high_contrast", _highContrast);
        config.SetValue(AccessSection, "captions", _captions);
        config.SetValue(AccessSection, "bow_toggle", _bowToggle);
        config.SetValue(AccessSection, "aim_assist", _aimAssist);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            Log.Warning($"Não foi possível gravar as opções em {FilePath}: {error}");
        }

        Changed?.Invoke();
    }
}
