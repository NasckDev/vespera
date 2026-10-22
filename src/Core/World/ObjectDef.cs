namespace Vespera.Core.World;

/// <summary>Camada de desenho de um objeto do mundo.</summary>
public enum ObjectLayer
{
    /// <summary>No chão, com y-sort pelo pé junto do jogador (barril, poste, barraca).</summary>
    Entity,

    /// <summary>Piso andável desenhado por baixo de tudo (cais, tapete, escada, círculo de runas).</summary>
    Floor,

    /// <summary>Preso a uma parede da pintura (vitral, estandarte, grade, porta); sem colisão própria.</summary>
    Wall,

    /// <summary>Pendurado acima do jogador (lustre, corrente, sino suspenso); sempre na frente.</summary>
    Overhead,

    /// <summary>Efeito sem corpo (fogo-fátuo, brilho).</summary>
    Fx,
}

/// <summary>Luz sugerida: cor (#rrggbb), raio em u e posição relativa ao pé, em u.</summary>
public sealed record ObjectLight(string Color, float Radius, MapPoint Offset);

/// <summary>
/// Objeto da biblioteca (<c>src/Data/objects.json</c>, gerado por <c>tools/assets/build_objects.py</c>).
/// Unidades: <see cref="WorldWidth"/>, <see cref="WorldHeight"/>, <see cref="Footprint"/> e luz em u (1 u = 1 px do
/// mapa); <see cref="AnchorX"/>/<see cref="AnchorY"/> e <see cref="PixelWidth"/>/<see cref="PixelHeight"/> em px da
/// textura. Desenhe a textura com escala <c>1 / Density</c> e <c>offset = -âncora</c>: a origem do nó é o pé.
/// </summary>
public sealed record ObjectDef
{
    public required string Id { get; init; }

    /// <summary>Caminho <c>res://</c> do PNG (o Core não lê o arquivo).</summary>
    public required string File { get; init; }

    public required string Theme { get; init; }

    public required string Kind { get; init; }

    public required ObjectLayer Layer { get; init; }

    /// <summary>Folha e peça(s) de origem, ex.: <c>67#38</c>.</summary>
    public required string Source { get; init; }

    public required int PixelWidth { get; init; }

    public required int PixelHeight { get; init; }

    /// <summary>Largura visível do objeto em u (sem a margem transparente).</summary>
    public required float WorldWidth { get; init; }

    /// <summary>Altura visível do objeto em u (sem a margem transparente).</summary>
    public required float WorldHeight { get; init; }

    /// <summary>Pixels da textura por u.</summary>
    public required float Density { get; init; }

    public required float AnchorX { get; init; }

    public required float AnchorY { get; init; }

    /// <summary>Retângulo sólido em u relativo ao pé (y negativo = para trás), ou null se não bloqueia.</summary>
    public MapRect? Footprint { get; init; }

    /// <summary>Alto o bastante para esconder o jogador que passa por trás (fica translúcido).</summary>
    public required bool Occluder { get; init; }

    /// <summary>Balança com o vento (árvores, trepadeiras, estandartes).</summary>
    public required bool Sway { get; init; }

    public ObjectLight? Light { get; init; }

    /// <summary>Interação sugerida, no vocabulário de <c>tools/maps/layout.py</c> (<c>break</c>, <c>chest</c>, <c>lamp</c>, <c>bench</c>…), ou null.</summary>
    public string? Interaction { get; init; }

    /// <summary>Som ambiente posicional sugerido (<c>water</c>, <c>fire</c>, <c>forge</c>, <c>crowd</c>, <c>wind</c>, <c>bells</c>), ou null.</summary>
    public string? AmbientSound { get; init; }

    /// <summary>Material para sons de impacto/passos (<c>wood</c>, <c>stone</c>, <c>metal</c>…), ou null.</summary>
    public string? Material { get; init; }

    public bool Solid => Footprint is not null;

    /// <summary>Altura em múltiplos da altura do jogador.</summary>
    public float HeightRatio(float playerHeight) => WorldHeight / playerHeight;
}
