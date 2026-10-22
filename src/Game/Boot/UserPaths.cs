namespace Vespera.Game.Boot;

/// <summary>
/// Raiz dos arquivos do usuário (<c>user://</c>). O smoke test aponta para uma subpasta própria
/// para não tocar nas configurações e saves reais do jogador.
/// </summary>
public static class UserPaths
{
    public const string DefaultRoot = "user://";

    public static string Root { get; private set; } = DefaultRoot;

    public static string SettingsFile => Root + "settings.json";

    public static string BindingsFile => Root + "bindings.json";

    public static string SavesDir => Root + "saves";

    public static string SlotFile(int slot) => $"{SavesDir}/slot_{slot}.json";

    public static void UseIsolatedRoot(string folder) => Root = DefaultRoot + folder.Trim('/') + "/";
}
