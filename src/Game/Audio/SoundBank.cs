using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Vespera.Game.Audio;

/// <summary>
/// Sons gravados de cada evento (data/audio/sons.json). Quem toca pergunta aqui primeiro e, se não houver arquivo,
/// cai no som sintetizado provisório; assim um evento novo nunca fica mudo.
/// </summary>
public static class SoundBank
{
    public const string MapPath = "res://data/audio/sons.json";

    private static readonly Dictionary<string, AudioStream?> Streams = [];
    private static Dictionary<string, Dictionary<string, string>>? _map;

    /// <summary>Arquivo de áudio do evento <paramref name="name"/> na seção <paramref name="section"/>, ou null.</summary>
    public static AudioStream? Get(string section, string name)
    {
        var path = PathOf(section, name);
        if (path.Length == 0)
        {
            return null;
        }

        if (!Streams.TryGetValue(path, out var stream))
        {
            stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            Streams[path] = stream;
        }

        return stream;
    }

    /// <summary>Caminho mapeado (pode ser vazio = silêncio, ou ausente = sem entrada).</summary>
    public static string PathOf(string section, string name) =>
        Map().TryGetValue(section, out var entries) && entries.TryGetValue(name, out var path) ? path : string.Empty;

    /// <summary>A seção tem entrada para este nome (mesmo que vazia).</summary>
    public static bool Has(string section, string name) =>
        Map().TryGetValue(section, out var entries) && entries.ContainsKey(name);

    private static Dictionary<string, Dictionary<string, string>> Map()
    {
        if (_map is not null)
        {
            return _map;
        }

        _map = [];
        using var file = FileAccess.Open(MapPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"[vespera] {MapPath} ausente: só sons sintetizados");
            return _map;
        }

        using var doc = JsonDocument.Parse(file.GetAsText());
        foreach (var section in doc.RootElement.EnumerateObject())
        {
            if (section.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var entries = new Dictionary<string, string>();
            foreach (var entry in section.Value.EnumerateObject())
            {
                entries[entry.Name] = entry.Value.GetString() ?? string.Empty;
            }

            _map[section.Name] = entries;
        }

        return _map;
    }
}
