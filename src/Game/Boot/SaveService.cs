using System;
using Godot;
using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Game.Boot;

/// <summary>Resumo de um espaço de save para a lista da tela de espaços (sem carregar o jogo inteiro).</summary>
public sealed record SlotSummary(int Slot, bool Exists, string Name, int Day, DateTimeOffset SavedAt);

/// <summary>
/// Saves em <c>user://saves/slot_N.json</c> usando o <see cref="SaveData"/> versionado do Core
/// (<see cref="SaveSerializer"/> migra versões antigas). Falhas viram retorno null/false com log, nunca exceção para a UI.
/// </summary>
public static class SaveService
{
    public const int SlotCount = 3;
    public const string DefaultClassId = "sword";
    public const string DefaultName = "Caçador";

    private static GameDatabase? _db;

    /// <summary>Banco de dados do Core lido de <c>res://src/Data</c> (carregado uma vez).</summary>
    public static GameDatabase Database => _db ??= GameDatabase.Load(ReadDataFile);

    public static bool HasAnySave()
    {
        for (var slot = 1; slot <= SlotCount; slot++)
        {
            if (FileAccess.FileExists(UserPaths.SlotFile(slot)))
            {
                return true;
            }
        }

        return false;
    }

    public static SlotSummary Summarize(int slot)
    {
        var data = Load(slot);
        return data is null
            ? new SlotSummary(slot, false, string.Empty, 0, default)
            : new SlotSummary(slot, true, data.Name, data.Day, data.SavedAt);
    }

    public static SaveData? Load(int slot)
    {
        var json = UserFile.ReadText(UserPaths.SlotFile(slot));
        if (json is null)
        {
            return null;
        }

        try
        {
            return SaveSerializer.FromJson(json, Database);
        }
        catch (Exception ex) when (ex is SaveFormatException or System.Text.Json.JsonException)
        {
            Log.Error($"Save do espaço {slot} ilegível: {ex.Message}");
            return null;
        }
    }

    public static bool Save(int slot, SaveData data)
    {
        data.SavedAt = DateTimeOffset.UtcNow;
        return UserFile.WriteText(UserPaths.SlotFile(slot), SaveSerializer.ToJson(data));
    }

    public static void Delete(int slot) => UserFile.Delete(UserPaths.SlotFile(slot));

    /// <summary>Cria e grava um jogo novo no espaço (a classe é escolhida só na camada de classes).</summary>
    public static SaveData? StartNew(int slot)
    {
        var data = SaveData.NewGame(Database, DefaultName, DefaultClassId);
        return Save(slot, data) ? data : null;
    }

    private static string ReadDataFile(string name) =>
        UserFile.ReadText($"res://src/Data/{name}")
        ?? throw new InvalidOperationException($"Dado do jogo ausente: res://src/Data/{name}");
}
