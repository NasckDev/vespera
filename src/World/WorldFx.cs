namespace Vespera.World;

/// <summary>
/// Liga/desliga os enfeites do mapa vivo (DECISIONS D44), para a tela de opções futura e para máquinas fracas. Lidos na
/// montagem de cada mapa (<see cref="WorldDressing.Build"/>); mudar no meio do mapa vale na próxima troca.
/// </summary>
public static class WorldFx
{
    /// <summary>Halos dos lampiões e braseiros (com tremulação).</summary>
    public static bool Lights { get; set; } = true;

    /// <summary>Brilho animado nas formas de água.</summary>
    public static bool WaterShimmer { get; set; } = true;

    /// <summary>Balanço dos oclusores com <c>sway</c>.</summary>
    public static bool Sway { get; set; } = true;

    /// <summary>Fumaça de chaminé, vagalumes e brasas.</summary>
    public static bool Particles { get; set; } = true;

    /// <summary>Sons ambiente posicionais.</summary>
    public static bool AmbientSound { get; set; } = true;
}
