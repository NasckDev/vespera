using System;
using System.Text.Json;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>Opções do jogador, persistidas em <c>user://settings.json</c>.</summary>
public sealed class GameSettings
{
    public const int FileVersion = 1;
    /// <summary>Alturas de janela oferecidas (largura = altura x 16/9); o índice é <see cref="WindowSize"/>.</summary>
    public static readonly int[] WindowHeights = [540, 720, 900, 1080, 1440];
    public const int TextNormal = 0;
    public const int TextLarge = 1;

    private const string MusicBus = "Music";
    private const string SfxBus = "SFX";

    public int Version { get; set; } = FileVersion;

    /// <summary>Volumes de 0 a 100.</summary>
    public int MasterVolume { get; set; } = 100;

    public int MusicVolume { get; set; } = 80;

    public int SfxVolume { get; set; } = 100;

    public bool Fullscreen { get; set; }

    /// <summary>Índice em <see cref="WindowHeights"/> (padrão 720p).</summary>
    public int WindowSize { get; set; } = 1;

    /// <summary>Quando verdadeiro (smoke test) a janela nunca é movida nem redimensionada.</summary>
    public static bool SuppressWindowChanges { get; set; }

    /// <summary>Intensidade do tremor de tela, 0 a 100.</summary>
    public int ScreenShake { get; set; } = 100;

    public bool ReduceFlashes { get; set; }

    public int TextSize { get; set; } = TextNormal;

    public static GameSettings Current { get; private set; } = new();

    public static event Action? Changed;

    public static void Load()
    {
        var json = UserFile.ReadText(UserPaths.SettingsFile);
        Current = new GameSettings();
        if (json is null)
        {
            return;
        }

        try
        {
            Current = JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
            Current.Sanitize();
        }
        catch (JsonException ex)
        {
            Log.Warn($"settings.json inválido, usando padrões: {ex.Message}");
            Current = new GameSettings();
        }
    }

    /// <summary>Persiste, aplica no motor e avisa quem escuta (tema de texto, por exemplo).</summary>
    public static void Commit(SceneTree tree)
    {
        Current.Sanitize();
        UserFile.WriteText(UserPaths.SettingsFile, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        Apply(tree);
        Changed?.Invoke();
    }

    public static void Apply(SceneTree tree)
    {
        ApplyAudio(Current);
        ApplyWindow(tree.Root, Current);
    }

    /// <summary>Maior altura de <see cref="WindowHeights"/> (até a pedida) que cabe na área útil, reservando a barra de título.</summary>
    public static int FittingHeight(int requestedIndex, Vector2I usable)
    {
        const int titleBarAllowance = 48;
        var maxByHeight = usable.Y - titleBarAllowance;
        var maxByWidth = usable.X * 9 / 16;
        var limit = Math.Min(maxByHeight, maxByWidth);
        for (var i = Math.Clamp(requestedIndex, 0, WindowHeights.Length - 1); i >= 0; i--)
        {
            if (WindowHeights[i] <= limit)
            {
                return WindowHeights[i];
            }
        }

        return WindowHeights[0];
    }

    public void Sanitize()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0, 100);
        MusicVolume = Math.Clamp(MusicVolume, 0, 100);
        SfxVolume = Math.Clamp(SfxVolume, 0, 100);
        WindowSize = Math.Clamp(WindowSize, 0, WindowHeights.Length - 1);
        ScreenShake = Math.Clamp(ScreenShake, 0, 100);
        TextSize = Math.Clamp(TextSize, TextNormal, TextLarge);
        Version = FileVersion;
    }

    private static void ApplyAudio(GameSettings s)
    {
        SetBusVolume("Master", s.MasterVolume);
        SetBusVolume(MusicBus, s.MusicVolume);
        SetBusVolume(SfxBus, s.SfxVolume);
    }

    private static void SetBusVolume(string bus, int percent)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index < 0)
        {
            AudioServer.AddBus();
            index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, bus);
            AudioServer.SetBusSend(index, "Master");
        }

        AudioServer.SetBusMute(index, percent == 0);
        AudioServer.SetBusVolumeDb(index, percent == 0 ? -80f : Mathf.LinearToDb(percent / 100f));
    }

    private static void ApplyWindow(Window window, GameSettings s)
    {
        if (SuppressWindowChanges || DisplayServer.GetName() == "headless")
        {
            return;
        }

        if (s.Fullscreen)
        {
            window.Mode = Window.ModeEnum.Fullscreen;
            return;
        }

        window.Mode = Window.ModeEnum.Windowed;
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        var height = FittingHeight(s.WindowSize, usable.Size);
        window.Size = new Vector2I(height * 16 / 9, height);
        window.Position = usable.Position + (usable.Size - window.Size) / 2;
    }
}
