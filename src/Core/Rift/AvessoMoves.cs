namespace Vespera.Core.Rift;

/// <summary>
/// Geometria dos verbos dos monstros dos Atos VI e VII (CAMPANHA): o Rasgo aparece às costas do caçador, o Ceifador de
/// Pomar só anda fora do cone da lanterna e a Sombra de Caçador copia a classe de quem caça. Lógica pura (px do mundo).
/// </summary>
public static class AvessoMoves
{
    /// <summary>Meia abertura do cone da lanterna do caçador (radianos): ~70° no total.</summary>
    public const double LanternHalfAngle = 0.61;

    /// <summary>
    /// Ponto de chegada do Rasgo: do outro lado do caçador, na linha que vem do Rasgo, a <paramref name="distance"/> px
    /// atrás dele. Rasgo em cima do caçador = aparece à direita.
    /// </summary>
    public static (double X, double Y) BlinkBehind(double fromX, double fromY, double targetX, double targetY, double distance)
    {
        var dx = targetX - fromX;
        var dy = targetY - fromY;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-6)
        {
            return (targetX + distance, targetY);
        }

        return (targetX + (dx / length * distance), targetY + (dy / length * distance));
    }

    /// <summary>
    /// O Ceifador está no cone da lanterna? Cone com vértice no caçador, apontado para onde ele mira, de alcance
    /// <paramref name="range"/> e meia abertura <paramref name="halfAngle"/>. Mira nula = a lanterna ilumina em volta
    /// só até um terço do alcance.
    /// </summary>
    public static bool InLanternCone(double hunterX, double hunterY, double aimX, double aimY, double x, double y, double range, double halfAngle = LanternHalfAngle)
    {
        var dx = x - hunterX;
        var dy = y - hunterY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance > range)
        {
            return false;
        }

        var aim = Math.Sqrt((aimX * aimX) + (aimY * aimY));
        if (aim < 1e-6)
        {
            return distance <= range / 3;
        }

        if (distance < 1e-6)
        {
            return true;
        }

        var cos = ((dx * aimX) + (dy * aimY)) / (distance * aim);
        return cos >= Math.Cos(halfAngle);
    }

    /// <summary>Variante da Sombra de Caçador que usa o verbo da classe do caçador (id da arma: sword, bow, hammer).</summary>
    public static string ShadowFor(string weaponId) => weaponId switch
    {
        "bow" => "sombra_cacador_arco",
        "hammer" => "sombra_cacador_martelo",
        _ => "sombra_cacador_lamina",
    };
}
