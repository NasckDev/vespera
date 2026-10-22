using Godot;

namespace Vespera.Game.Boot;

/// <summary>Leitura e escrita de texto em <c>user://</c> / <c>res://</c> com falha explícita (devolve null / false e loga).</summary>
public static class UserFile
{
    public static string? ReadText(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            Log.Warn($"Não foi possível abrir '{path}' ({FileAccess.GetOpenError()}).");
            return null;
        }

        return file.GetAsText();
    }

    public static bool WriteText(string path, string text)
    {
        var folder = path[..path.LastIndexOf('/')];
        var made = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        if (made != Error.Ok)
        {
            Log.Error($"Não foi possível criar '{folder}' ({made}).");
            return false;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            Log.Error($"Não foi possível escrever '{path}' ({FileAccess.GetOpenError()}).");
            return false;
        }

        file.StoreString(text);
        return true;
    }

    public static void Delete(string path)
    {
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
    }
}
