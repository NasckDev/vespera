using Godot;

namespace Vespera.Autoload;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Wrapper fino de log com níveis. Use no lugar de GD.Print.
/// Chama-se Log (e não Logger) porque Godot 4.5+ já tem uma classe nativa Godot.Logger.
/// </summary>
public static class Log
{
    /// <summary>Em build de release, Debug é descartado.</summary>
    public static LogLevel MinimumLevel { get; set; } = OS.IsDebugBuild() ? LogLevel.Debug : LogLevel.Info;

    public static void Debug(string message) => Write(LogLevel.Debug, message);

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Warning(string message) => Write(LogLevel.Warning, message);

    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var line = $"[{level.ToString().ToUpperInvariant()}] {message}";
        switch (level)
        {
            case LogLevel.Error:
                GD.PushError(line);
                break;
            case LogLevel.Warning:
                GD.PushWarning(line);
                break;
            default:
                GD.Print(line);
                break;
        }
    }
}
