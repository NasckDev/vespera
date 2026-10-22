using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Save;

namespace Vespera.Autoload;

/// <summary>Lê e grava <see cref="SaveData"/> como JSON em <c>user://save.json</c>.</summary>
public partial class SaveService : Node
{
    public const string DefaultSavePath = "user://save.json";
    private const string SmokeSavePrefix = "user://save_smoke_";
    private const string CorruptedSuffix = ".corrupted";

    public static SaveService Instance { get; private set; } = null!;

    /// <summary>No smoke test usa um arquivo descartável para nunca tocar no save real.</summary>
    public string SavePath { get; private set; } = DefaultSavePath;

    public override void _EnterTree()
    {
        Instance = this;
        if (Testing.SmokeTest.IsRequested())
        {
            // POLI-16: um arquivo por processo, para rodadas de smoke em paralelo (outros worktrees) não se pisarem.
            SavePath = SmokeSavePrefix + OS.GetProcessId() + ".json";
            DirAccess.RemoveAbsolute(SavePath);
        }
    }

    public override void _ExitTree()
    {
        if (SavePath != DefaultSavePath)
        {
            DirAccess.RemoveAbsolute(SavePath);
            DirAccess.RemoveAbsolute(SavePath + CorruptedSuffix);
        }
    }

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

    /// <summary>Carrega o save (migrando versões antigas), ou devolve um jogo novo se não existir ou estiver corrompido.</summary>
    public SaveData LoadOrNew()
    {
        if (!HasSave())
        {
            return NewGame();
        }

        try
        {
            var warnings = new List<string>();
            var data = SaveSerializer.FromJson(FileAccess.GetFileAsString(SavePath), Database.Db, warnings);
            foreach (var warning in warnings)
            {
                Log.Warning($"Save: {warning}");
            }

            return data;
        }
        catch (SaveFormatException ex)
        {
            // Guarda o arquivo ruim para investigação em vez de sobrescrever em silêncio.
            var backup = SavePath + CorruptedSuffix;
            DirAccess.RenameAbsolute(SavePath, backup);
            Log.Error($"{ex.Message} O arquivo foi movido para {backup}; começando um jogo novo.");
            return NewGame();
        }
    }

    /// <summary>Jogo novo com a classe padrão (a escolha de classe entra com o menu inicial).</summary>
    private static SaveData NewGame() => SaveData.NewGame(Database.Db, null, Database.Db.Rules.Start.DefaultClass);
}
