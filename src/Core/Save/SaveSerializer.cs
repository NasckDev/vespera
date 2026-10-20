using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Save;

public sealed class SaveFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Converte <see cref="SaveData"/> de/para JSON e aplica migrações de versão.</summary>
public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.Serialize(data, Options);
    }

    public static SaveData FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        SaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<SaveData>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new SaveFormatException("Save corrompido ou ilegível.", ex);
        }

        if (data is null)
        {
            throw new SaveFormatException("Save vazio.");
        }

        if (data.SaveVersion > SaveData.CurrentVersion)
        {
            throw new SaveFormatException($"Save da versão {data.SaveVersion} é mais novo que o jogo (versão {SaveData.CurrentVersion}).");
        }

        return Migrate(data);
    }

    private static SaveData Migrate(SaveData data)
    {
        // Ainda não há versões antigas. Quando houver, aplique um passo por versão:
        // if (data.SaveVersion == 1) { ...; data.SaveVersion = 2; }
        data.SaveVersion = SaveData.CurrentVersion;
        return data;
    }
}
