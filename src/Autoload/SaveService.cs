using System;
using Godot;
using Vespera.Core.Save;

namespace Vespera.Autoload;

/// <summary>Lê e grava <see cref="SaveData"/> como JSON em <c>user://save.json</c>.</summary>
public partial class SaveService : Node
{
    public const string SavePath = "user://save.json";
    private const string CorruptedSuffix = ".corrupted";

    public static SaveService Instance { get; private set; } = null!;

    public override void _EnterTree() => Instance = this;

    public bool HasSave() => FileAccess.FileExists(SavePath);

    public void Save(SaveData data)
    {
        data.SavedAt = DateTimeOffset.UtcNow;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            Log.Error($"Não foi possível gravar o save: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(SaveSerializer.ToJson(data));
        Log.Debug($"Jogo salvo em {ProjectSettings.GlobalizePath(SavePath)}");
    }

    /// <summary>Carrega o save, ou devolve um novo se não existir ou estiver corrompido.</summary>
    public SaveData LoadOrNew()
    {
        if (!HasSave())
        {
            return new SaveData();
        }

        try
        {
            return SaveSerializer.FromJson(FileAccess.GetFileAsString(SavePath));
        }
        catch (SaveFormatException ex)
        {
            // Guarda o arquivo ruim para investigação em vez de sobrescrever em silêncio.
            var backup = SavePath + CorruptedSuffix;
            DirAccess.RenameAbsolute(SavePath, backup);
            Log.Error($"{ex.Message} O arquivo foi movido para {backup}; começando um jogo novo.");
            return new SaveData();
        }
    }
}
