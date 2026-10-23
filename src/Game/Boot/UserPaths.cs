using System;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>
/// Raiz dos arquivos do usuário (<c>user://</c>). O smoke test aponta para uma subpasta própria por execução
/// (<c>user://smoke/&lt;nome&gt;/</c>) para não tocar nas configurações e saves reais do jogador nem dividir saves entre
/// execuções paralelas ou seguidas. O nome vem de <c>--user-dir=&lt;nome&gt;</c>; sem ele, do id do processo.
/// </summary>
public static class UserPaths
{
    public const string DefaultRoot = "user://";
    public const string UserDirPrefix = "--user-dir=";
    public const string KeepUserDirFlag = "--keep-user-dir";
    public const string TestFolder = "smoke";

    public static string Root { get; private set; } = DefaultRoot;

    public static string SettingsFile => Root + "settings.json";

    public static string BindingsFile => Root + "bindings.json";

    public static string SavesDir => Root + "saves";

    public static string SlotFile(int slot) => $"{SavesDir}/slot_{slot}.json";

    public static bool Isolated => Root != DefaultRoot;

    public static void UseIsolatedRoot(string folder) => Root = DefaultRoot + folder.Trim('/') + "/";

    /// <summary>Pasta isolada desta execução de teste: <c>smoke/&lt;--user-dir ou run_pid&gt;</c>.</summary>
    public static void UseTestRoot(string[] args)
    {
        var name = $"run_{OS.GetProcessId()}";
        foreach (var arg in args)
        {
            if (arg.StartsWith(UserDirPrefix, StringComparison.Ordinal) && arg.Length > UserDirPrefix.Length)
            {
                name = SanitizeFolder(arg[UserDirPrefix.Length..]);
            }
        }

        UseIsolatedRoot($"{TestFolder}/{name}");
    }

    /// <summary>Apaga a pasta isolada inteira (fim do smoke); não faz nada na raiz real do jogador.</summary>
    public static void DeleteIsolatedRoot()
    {
        if (!Isolated)
        {
            return;
        }

        DeleteRecursive(ProjectSettings.GlobalizePath(Root.TrimEnd('/')));
    }

    private static string SanitizeFolder(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private static void DeleteRecursive(string folder)
    {
        using var dir = DirAccess.Open(folder);
        if (dir is null)
        {
            return;
        }

        foreach (var file in dir.GetFiles())
        {
            DirAccess.RemoveAbsolute($"{folder}/{file}");
        }

        foreach (var sub in dir.GetDirectories())
        {
            DeleteRecursive($"{folder}/{sub}");
        }

        DirAccess.RemoveAbsolute(folder);
    }
}
