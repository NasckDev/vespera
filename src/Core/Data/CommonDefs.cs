namespace Vespera.Core.Data;

/// <summary>Custo em Lumes (antes do multiplicador de preço) e materiais (nunca multiplicados).</summary>
public sealed record Cost
{
    public static readonly IReadOnlyDictionary<string, int> NoMaterials = new Dictionary<string, int>();

    public int Lumes { get; init; }

    public IReadOnlyDictionary<string, int> Mats { get; init; } = NoMaterials;

    public static Cost Of(int lumes, IReadOnlyDictionary<string, int>? mats = null) => new() { Lumes = lumes, Mats = mats ?? NoMaterials };
}

/// <summary>Paleta da v2 (<c>COL</c>) e cores de luz RGB (<c>LIGHTC</c>, <c>data.js:3-4</c>).</summary>
public sealed record PaletteFile
{
    public required IReadOnlyDictionary<string, string> Colors { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<int>> Lights { get; init; }
}
