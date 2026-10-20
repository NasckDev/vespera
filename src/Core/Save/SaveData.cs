namespace Vespera.Core.Save;

/// <summary>
/// Estado persistido do jogador. Serializado como JSON em <c>user://save.json</c> pelo SaveService.
/// Ao mudar o formato: incremente <see cref="CurrentVersion"/> e adicione a migração em <see cref="SaveSerializer"/>.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int SaveVersion { get; set; } = CurrentVersion;

    public int TotalXp { get; set; }

    /// <summary>Moeda do Avesso (forja). Lumes, a moeda da cidade, entram depois do MVP.</summary>
    public int Fragments { get; set; }

    public int WeaponLevel { get; set; } = 1;

    public int Day { get; set; } = 1;

    /// <summary>Lanterna deixada na última Brecha em que o jogador morreu, se houver (GDD, seção 5.5).</summary>
    public LanternData? Lantern { get; set; }

    public DateTimeOffset SavedAt { get; set; }
}

/// <summary>Fragmentos que ficaram na Brecha até o jogador voltar ao mesmo bairro.</summary>
public sealed class LanternData
{
    public string DistrictId { get; set; } = string.Empty;

    public int Fragments { get; set; }
}
