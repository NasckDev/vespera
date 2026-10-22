using Godot;

namespace Vespera.Game.Boot;

/// <summary>Wrapper fino de log: único ponto do jogo que escreve no console da engine.</summary>
public static class Log
{
    public static void Info(string message) => GD.Print($"[vespera] {message}");

    public static void Warn(string message) => GD.PushWarning($"[vespera] {message}");

    public static void Error(string message) => GD.PushError($"[vespera] {message}");
}
