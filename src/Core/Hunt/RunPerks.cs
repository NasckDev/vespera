namespace Vespera.Core.Hunt;

/// <summary>
/// Vantagens de cidade que valem só na próxima caçada (ROTEIRO-V2 §3): a sopa da Nilza (vida máxima maior), o mapa da Lis
/// (mostra o tipo de cada sala e onde está o tesouro) e os suportes de arma das Brechas 1 e 2 do Ato I (§4.3: um Arco e um
/// Martelo largados por caçadores que não voltaram; pegar troca a arma só naquela caçada).
/// </summary>
public static class RunPerks
{
    /// <summary>Bônus da sopa da Nilza sobre a vida máxima, em %.</summary>
    public const double SoupMaxHpPct = 10;

    /// <summary>Linha do HUD durante a caçada com a sopa ativa.</summary>
    public const string SoupLabel = "Sopa da Nilza: +10% de vida máxima";

    /// <summary>Vida extra da sopa (arredonda; ao menos 1 quando há vida).</summary>
    public static int SoupBonus(int baseMaxHp) =>
        baseMaxHp <= 0 ? 0 : Math.Max(1, (int)Math.Round(baseMaxHp * SoupMaxHpPct / 100, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Arma deixada no suporte da primeira sala da caçada do Ato I: Arco na 1ª, Martelo na 2ª, nada no exame (Guardião) e
    /// depois dele. <paramref name="runsCompleted"/> conta as caçadas completas antes desta.
    /// </summary>
    public static string? StandWeapon(int runsCompleted, bool guardianHunt) => guardianHunt ? null : runsCompleted switch
    {
        0 => "bow",
        1 => "hammer",
        _ => null,
    };

    /// <summary>Nome curto da sala no mapa da Lis.</summary>
    public static string RoomLabel(RoomKind kind) => kind switch
    {
        RoomKind.Combat => "Combate",
        RoomKind.Elite => "Elite",
        RoomKind.Treasure => "TESOURO",
        RoomKind.Rest => "Descanso",
        RoomKind.Guardian => "Guardião",
        _ => "?",
    };

    /// <summary>Linha do mapa da Lis: todas as salas numeradas, a atual entre colchetes, o tesouro em maiúsculas.</summary>
    public static string MapLine(IReadOnlyList<RoomKind> rooms, int current)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        var parts = rooms.Select((kind, i) => i == current ? $"[{i + 1} {RoomLabel(kind)}]" : $"{i + 1} {RoomLabel(kind)}");
        return "Mapa da Lis: " + string.Join(" · ", parts);
    }
}
