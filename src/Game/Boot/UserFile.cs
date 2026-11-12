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

        // Escrita atômica: grava num temporário, confere e só então troca; o arquivo anterior vira ".bak".
        var temp = path + TempSuffix;
        if (!WriteAndVerify(temp, text))
        {
            Delete(temp);
            return false;
        }

        var absolute = ProjectSettings.GlobalizePath(path);
        if (FileAccess.FileExists(path))
        {
            var backed = DirAccess.CopyAbsolute(absolute, absolute + BackupSuffix);
            if (backed != Error.Ok)
            {
                Log.Warn($"Não foi possível guardar o backup de '{path}' ({backed}).");
            }
        }

        var renamed = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(temp), absolute);
        if (renamed != Error.Ok)
        {
            Log.Error($"Não foi possível concluir a escrita de '{path}' ({renamed}).");
            Delete(temp);
            return false;
        }

        return true;
    }

    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";

    private static bool WriteAndVerify(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            Log.Error($"Não foi possível escrever '{path}' ({FileAccess.GetOpenError()}).");
            return false;
        }

        file.StoreString(text);
        file.Flush();
        var error = file.GetError();
        if (error != Error.Ok)
        {
            Log.Error($"Falha ao gravar '{path}' ({error}).");
            return false;
        }

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
