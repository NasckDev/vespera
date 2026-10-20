namespace Vespera.Core.World;

/// <summary>Saída por retângulo: o ponto do pé do jogador dentro de <see cref="Area"/> leva a <see cref="To"/> em <see cref="At"/>.</summary>
public sealed record MapExit(MapRect Area, string To, MapPoint At);

/// <summary>
/// Ponto de interação fixo do mapa (<c>world.js:182-197</c>). <see cref="Action"/> é o nome da função da v2
/// (<c>talkClerk</c>, <c>portalDialog</c>…) e vira o <c>kind</c> do sinal <c>InteractionRequested</c>.
/// <see cref="Condition"/> não nulo = começa desligado (ex.: portal só com Brecha aberta).
/// </summary>
public sealed record MapInteractable(string Id, string Label, float X, float Y, float Radius, string Action, string? Condition)
{
    public bool StartsEnabled => Condition is null;
}

/// <summary>Luz do mapa <c>[x, y, raio, tipo]</c> (tipo = chave de <c>LIGHTC</c>).</summary>
public sealed record MapLight(float X, float Y, float Radius, string Kind);

/// <summary>Rotina de NPC (<c>data.js:83-92</c>): horas em <c>clock/60</c> sem módulo 24.</summary>
public sealed record NpcRoute(string Id, string Name, int HourFrom, int HourTo, float Speed, IReadOnlyList<MapPoint> Path);

/// <summary>Recepcionista desenhada por cima da arte da Guilda (spec 01 §2.7).</summary>
public sealed record ClerkSprite(string Sprite, float X, float Y, float Scale, MapRect ForegroundSlice);

/// <summary>Cópia de um recorte da imagem do mapa para outro ponto (spec 01 §2.7).</summary>
public sealed record ImagePatch(MapRect CopyFrom, MapPoint PasteAt);

/// <summary>Definição de um mapa da v2 (spec 01 §9.3). Coordenadas em u = px da imagem.</summary>
public sealed class MapDef
{
    /// <summary>Volta da Brecha cai em <c>PORTAL_SPOTS[spot] + (0, 24)</c> (<c>world.js:126</c>).</summary>
    public static readonly MapPoint PortalReturnOffset = new(0, 24);

    public required string Id { get; init; }

    /// <summary>Caminho <c>res://</c> da pintura.</summary>
    public required string Image { get; init; }

    public required int ImageWidth { get; init; }

    public required int ImageHeight { get; init; }

    /// <summary>Caminho <c>res://</c> da máscara (só mapas hd).</summary>
    public string? Mask { get; init; }

    public int MaskCell { get; init; } = MaskWalkArea.DefaultCellSize;

    public required string Name { get; init; }

    public bool Indoor { get; init; }

    public bool Outdoor { get; init; }

    public bool Hd { get; init; }

    public bool Dungeon { get; init; }

    public bool Shop { get; init; }

    public string? Theme { get; init; }

    /// <summary>Chave em <c>assets/maps/lights.json</c> (mapas hd).</summary>
    public string? LightsKey { get; init; }

    public required MapPoint Spawn { get; init; }

    public IReadOnlyList<MapExit> Exits { get; init; } = [];

    public IReadOnlyList<WalkShape> Walk { get; init; } = [];

    public IReadOnlyList<MapRect> Block { get; init; } = [];

    public IReadOnlyList<MapLight> Lights { get; init; } = [];

    public IReadOnlyList<MapInteractable> Interactables { get; init; } = [];

    /// <summary>Pontos de chegada nomeados (só os que são coordenadas).</summary>
    public IReadOnlyDictionary<string, MapPoint> Arrivals { get; init; } = new Dictionary<string, MapPoint>();

    /// <summary>Porta para a Câmara do Núcleo (dungeon, depois de limpar as ondas).</summary>
    public MapRect? Door { get; init; }

    /// <summary>Área de fuga da dungeon.</summary>
    public MapRect? Back { get; init; }

    public MapPoint? BossHere { get; init; }

    public MapPoint? ExitAt { get; init; }

    public MapPoint? BossDefault { get; init; }

    public MapPoint? ExitDefault { get; init; }

    public IReadOnlyList<MapPoint> PortalSpots { get; init; } = [];

    public IReadOnlyList<MapPoint> Dummies { get; init; } = [];

    public IReadOnlyList<MapPoint> CrystalProps { get; init; } = [];

    public IReadOnlyList<NpcRoute> Npcs { get; init; } = [];

    public ClerkSprite? Clerk { get; init; }

    public ImagePatch? Patch { get; init; }

    public bool HasMask => Mask is not null;

    /// <summary>Altura visível em u (spec 01 §3): 400 em mapas hd, 300 nos demais.</summary>
    public float ViewHeight => Hd ? 400f : 300f;

    /// <summary>Área andável por formas. Só para mapas sem máscara.</summary>
    public ShapeWalkArea CreateShapeWalkArea() =>
        HasMask
            ? throw new InvalidOperationException($"Mapa '{Id}' usa máscara; crie um MaskWalkArea a partir dela.")
            : new ShapeWalkArea(ImageWidth, ImageHeight, Walk, Block);

    /// <summary>Saída cujo retângulo contém o ponto (primeira na ordem do JSON, como <c>main.js:110</c>), ou null.</summary>
    public MapExit? FindExit(float x, float y)
    {
        foreach (var exit in Exits)
        {
            if (exit.Area.Contains(x, y))
            {
                return exit;
            }
        }

        return null;
    }
}
