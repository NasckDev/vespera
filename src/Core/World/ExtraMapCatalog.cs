using System.Text.Json;

namespace Vespera.Core.World;

/// <summary>Papel de um mapa candidato.</summary>
public enum ExtraMapRole
{
    /// <summary>Sala extra de um mapa de Brecha que já existe no maps.json.</summary>
    ExtraRoom,

    /// <summary>Sala de uma Brecha nova (6–10).</summary>
    Brecha,
}

/// <summary>Mapa candidato, ainda sem colisão (<c>src/Data/maps_extra.json</c>, DECISIONS B5-3).</summary>
public sealed record ExtraMapDef(
    string Id,
    string Image,
    int ImageWidth,
    int ImageHeight,
    string Source,
    ExtraMapRole Role,
    string? Map,
    int? Brecha,
    string? BrechaName,
    bool HasCollision,
    string Note);

/// <summary>
/// Registro dos mapas novos que ainda não são jogáveis: variações das 5 Brechas (salas extras) e as Brechas 6–10.
/// Gerado por <c>tools/maps/extra_maps.py</c>. Quando um ganhar layout e colisão, ele sai daqui e entra no maps.json.
/// </summary>
public sealed class ExtraMapCatalog
{
    private readonly List<ExtraMapDef> _maps;

    private ExtraMapCatalog(List<ExtraMapDef> maps, IReadOnlyDictionary<int, string> brechas)
    {
        _maps = maps;
        Brechas = brechas;
    }

    public IReadOnlyList<ExtraMapDef> Maps => _maps;

    /// <summary>Número da Brecha nova → nome.</summary>
    public IReadOnlyDictionary<int, string> Brechas { get; }

    public IEnumerable<ExtraMapDef> RoomsOf(string mapId) => _maps.Where(m => m.Role == ExtraMapRole.ExtraRoom && m.Map == mapId);

    public IEnumerable<ExtraMapDef> RoomsOfBrecha(int number) => _maps.Where(m => m.Role == ExtraMapRole.Brecha && m.Brecha == number);

    /// <summary>Lê e valida. <paramref name="maps"/> (opcional) confere se as salas extras apontam para mapas que existem.</summary>
    public static ExtraMapCatalog Parse(string json, MapCatalog? maps = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var brechas = new Dictionary<int, string>();
            foreach (var b in doc.RootElement.GetProperty("brechas").EnumerateObject())
            {
                brechas[int.Parse(b.Name, System.Globalization.CultureInfo.InvariantCulture)] = b.Value.GetString()!;
            }

            var list = doc.RootElement.GetProperty("maps").EnumerateArray().Select(e =>
            {
                var role = e.GetProperty("role").GetString() switch
                {
                    "extraRoom" => ExtraMapRole.ExtraRoom,
                    "brecha" => ExtraMapRole.Brecha,
                    var r => throw new FormatException($"papel desconhecido '{r}'"),
                };
                return new ExtraMapDef(
                    e.GetProperty("id").GetString()!,
                    e.GetProperty("image").GetString()!,
                    e.GetProperty("imageSize")[0].GetInt32(),
                    e.GetProperty("imageSize")[1].GetInt32(),
                    e.GetProperty("source").GetString()!,
                    role,
                    e.TryGetProperty("map", out var m) ? m.GetString() : null,
                    e.TryGetProperty("brecha", out var b) ? b.GetInt32() : null,
                    e.TryGetProperty("brechaName", out var n) ? n.GetString() : null,
                    e.GetProperty("collision").GetBoolean(),
                    e.TryGetProperty("note", out var note) ? note.GetString()! : string.Empty);
            }).ToList();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in list)
            {
                if (!seen.Add(m.Id))
                {
                    throw new MapFormatException($"maps_extra.json: id repetido '{m.Id}'.");
                }

                if (maps is not null && maps.TryGet(m.Id, out _))
                {
                    throw new MapFormatException($"maps_extra.json: '{m.Id}' já está no maps.json; tire daqui.");
                }

                switch (m.Role)
                {
                    case ExtraMapRole.ExtraRoom when m.Map is null || (maps is not null && !maps.TryGet(m.Map, out _)):
                        throw new MapFormatException($"maps_extra.json: sala extra '{m.Id}' aponta para mapa inexistente '{m.Map}'.");
                    case ExtraMapRole.Brecha when m.Brecha is not { } n || !brechas.ContainsKey(n):
                        throw new MapFormatException($"maps_extra.json: '{m.Id}' aponta para Brecha desconhecida {m.Brecha}.");
                }
            }

            return new ExtraMapCatalog(list, brechas);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new MapFormatException($"maps_extra.json inválido: {ex.Message}", ex);
        }
    }
}
