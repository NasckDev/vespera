using System.Text.Json;
using System.Text.Json.Serialization;
using Vespera.Core.Data;

namespace Vespera.Core.Save;

public sealed class SaveFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Converte <see cref="SaveData"/> de/para JSON, migra versões antigas e saneia ids que não existem mais nos dados.</summary>
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

    /// <summary>Lê um save de qualquer versão suportada (1 ou 2) e devolve no formato atual.</summary>
    public static SaveData FromJson(string json, GameDatabase db) => FromJson(json, db, warnings: null);

    /// <summary>Como <see cref="FromJson(string, GameDatabase)"/>; <paramref name="warnings"/> recebe o que foi descartado ao sanear.</summary>
    public static SaveData FromJson(string json, GameDatabase db, List<string>? warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(db);

        try
        {
            var version = ReadVersion(json);
            var data = version switch
            {
                1 => MigrateFromV1(Deserialize<SaveDataV1>(json), db),
                SaveData.CurrentVersion => Deserialize<SaveData>(json),
                > SaveData.CurrentVersion => throw new SaveFormatException($"Save da versão {version} é mais novo que o jogo (versão {SaveData.CurrentVersion})."),
                _ => throw new SaveFormatException($"Versão de save desconhecida: {version}."),
            };

            data.SaveVersion = SaveData.CurrentVersion;
            Sanitize(data, db, warnings ?? []);
            return data;
        }
        catch (JsonException ex)
        {
            throw new SaveFormatException("Save corrompido ou ilegível.", ex);
        }
    }

    private static int ReadVersion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("saveVersion", out var v)
            || !v.TryGetInt32(out var version))
        {
            throw new SaveFormatException("Save sem saveVersion.");
        }

        return version;
    }

    private static T Deserialize<T>(string json)
        where T : class => JsonSerializer.Deserialize<T>(json, Options) ?? throw new SaveFormatException("Save vazio.");

    /// <summary>
    /// v1 (fundação) → v2. Mapeamento: totalXp → xp; fragments → material Estilhaço; weaponLevel → nível da Lâmina
    /// (única arma da v1, classe padrão); day → day; lantern → Lanterna perdida com os Estilhaços na bolsa.
    /// O resto vem do jogo novo (DECISIONS D19).
    /// </summary>
    private static SaveData MigrateFromV1(SaveDataV1 old, GameDatabase db)
    {
        var start = db.Rules.Start;
        var shard = db.Rules.Economy.ShardMaterial;
        var data = SaveData.NewGame(db, start.Name, start.DefaultClass);
        data.Xp = Math.Max(0, old.TotalXp);
        data.Day = Math.Max(1, old.Day);
        data.SavedAt = old.SavedAt;
        data.Weapons[start.DefaultClass] = Math.Clamp(old.WeaponLevel, 1, db.WeaponData.MaxLevel);
        if (old.Fragments > 0)
        {
            data.Materials[shard] = old.Fragments;
        }

        if (old.Lantern is { } lantern)
        {
            var bag = new RunBag();
            if (lantern.Fragments > 0)
            {
                bag.Materials[shard] = lantern.Fragments;
            }

            data.LostLantern = new LostLantern { Bairro = lantern.DistrictId, Bag = bag, Room = "theme" };
        }

        return data;
    }

    /// <summary>Garante coleções não nulas, classe válida e ids existentes (equivalente aos defaults de <c>loadSave</c>).</summary>
    private static void Sanitize(SaveData data, GameDatabase db, List<string> warnings)
    {
        data.Name = SaveData.CleanName(data.Name, db.Rules.Start);
        data.Materials ??= [];
        data.Backpack ??= [];
        data.Equipment ??= [];
        data.Weapons ??= [];
        data.Ecos ??= [];
        data.EcoLevels ??= [];
        data.Infusions ??= [];
        data.ActiveInfusion ??= [];
        data.CarriedLanterns ??= [];
        data.Quests ??= new QuestProgress();
        data.Quests.Counters ??= [];
        data.Quests.ClosedThemes ??= [];
        if (data.LostLantern is { } lost)
        {
            lost.Bag ??= new RunBag();
            Clean(lost.Bag, db, warnings);
        }

        if (!db.Classes.ContainsKey(data.ClassId))
        {
            warnings.Add($"classe '{data.ClassId}' desconhecida; usando '{db.Rules.Start.DefaultClass}'");
            data.ClassId = db.Rules.Start.DefaultClass;
        }

        // Classe é permanente (extra.js:161): a arma empunhada é sempre a da classe.
        data.CurrentWeapon = data.ClassId;
        RemoveWhere(data.Weapons, (id, _) => !db.Weapons.ContainsKey(id), "arma", warnings);
        foreach (var id in data.Weapons.Keys.ToList())
        {
            data.Weapons[id] = Math.Clamp(data.Weapons[id], 1, db.WeaponData.MaxLevel);
        }

        data.Weapons.TryAdd(data.ClassId, 1);

        RemoveWhere(data.Materials, (id, n) => !db.Materials.ContainsKey(id) || n <= 0, "material", warnings);
        RemoveAll(data.Backpack, id => !db.Gear.ContainsKey(id), "equipamento", warnings);
        RemoveWhere(data.Equipment, (slot, id) => !db.Gear.TryGetValue(id, out var g) || g.Slot != slot, "equipado", warnings);
        RemoveAll(data.Ecos, id => !db.Ecos.ContainsKey(id), "Eco", warnings);
        if (data.EquippedEco is not null && !data.Ecos.Contains(data.EquippedEco))
        {
            data.EquippedEco = null;
        }

        RemoveWhere(data.EcoLevels, (id, _) => !db.Ecos.ContainsKey(id), "afinação", warnings);
        RemoveWhere(data.Infusions, (id, _) => !db.Weapons.ContainsKey(id), "infusão", warnings);
        foreach (var list in data.Infusions.Values)
        {
            list.RemoveAll(e => !db.Elements.ContainsKey(e));
        }

        RemoveWhere(data.ActiveInfusion, (w, e) => !data.Infusions.TryGetValue(w, out var owned) || !owned.Contains(e), "infusão ativa", warnings);
        data.Quests.Index = Math.Clamp(data.Quests.Index, 0, db.Quests.Count);
        data.Potions = Math.Max(0, data.Potions);
        data.Lumes = Math.Max(0, data.Lumes);
        data.Day = Math.Max(1, data.Day);
    }

    private static void Clean(RunBag bag, GameDatabase db, List<string> warnings)
    {
        bag.Materials ??= [];
        bag.Gear ??= [];
        bag.Lanterns ??= [];
        RemoveWhere(bag.Materials, (id, n) => !db.Materials.ContainsKey(id) || n <= 0, "material da bolsa", warnings);
        RemoveAll(bag.Gear, id => !db.Gear.ContainsKey(id), "equipamento da bolsa", warnings);
    }

    private static void RemoveWhere<TValue>(Dictionary<string, TValue> map, Func<string, TValue, bool> drop, string what, List<string> warnings)
    {
        foreach (var (key, value) in map.ToList())
        {
            if (drop(key, value))
            {
                map.Remove(key);
                warnings.Add($"{what} '{key}' descartado");
            }
        }
    }

    private static void RemoveAll(List<string> list, Predicate<string> drop, string what, List<string> warnings)
    {
        foreach (var id in list.Where(id => drop(id)).Distinct().ToList())
        {
            warnings.Add($"{what} '{id}' descartado");
        }

        list.RemoveAll(drop);
    }

    /// <summary>Formato v1 (fundação), só para migração.</summary>
    private sealed class SaveDataV1
    {
        public int TotalXp { get; set; }

        public int Fragments { get; set; }

        public int WeaponLevel { get; set; } = 1;

        public int Day { get; set; } = 1;

        public LanternV1? Lantern { get; set; }

        public DateTimeOffset SavedAt { get; set; }
    }

    private sealed class LanternV1
    {
        public string DistrictId { get; set; } = string.Empty;

        public int Fragments { get; set; }
    }
}
