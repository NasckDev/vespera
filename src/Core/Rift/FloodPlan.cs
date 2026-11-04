namespace Vespera.Core.Rift;

/// <summary>
/// Arena que inunda (Ato III, Pé do Farol — CAMPANHA, Ato III): plataforma redonda com um centro seco e um anel
/// externo mais baixo. Fase 1 seca; fase 2 o anel alaga; fase 3 tudo alaga menos uma ilha seca que muda de lugar a cada
/// <see cref="DryCycleSeconds"/> s, com aviso <see cref="WarnSeconds"/> s antes. Sem Godot: o Guardião só pergunta.
/// </summary>
public sealed class FloodPlan
{
    public const double DryCycleSeconds = 15;
    public const double WarnSeconds = 1.5;

    /// <summary>Lugares da ilha seca na fase 3, em fração dos raios do centro seco (sai do meio e roda pela plataforma).</summary>
    private static readonly (double X, double Y)[] Spots = [(0, 0), (0.75, -0.3), (-0.75, 0.35), (0.55, 0.6), (-0.6, -0.55)];

    public FloodPlan(double centerX, double centerY, double dryRx, double dryRy, double ringRx, double ringRy, double islandRadius)
    {
        if (dryRx <= 0 || dryRy <= 0 || ringRx < dryRx || ringRy < dryRy || islandRadius <= 0)
        {
            throw new ArgumentException("Raios da arena inválidos: o anel precisa conter o centro seco.");
        }

        CenterX = centerX;
        CenterY = centerY;
        DryRx = dryRx;
        DryRy = dryRy;
        RingRx = ringRx;
        RingRy = ringRy;
        IslandRadius = islandRadius;
    }

    public double CenterX { get; }

    public double CenterY { get; }

    public double DryRx { get; }

    public double DryRy { get; }

    public double RingRx { get; }

    public double RingRy { get; }

    public double IslandRadius { get; }

    /// <summary>Índice da ilha seca da fase 3 depois de <paramref name="darkSeconds"/> s de escuro.</summary>
    public static int SpotIndex(double darkSeconds) => (int)(Math.Max(0, darkSeconds) / DryCycleSeconds) % Spots.Length;

    /// <summary>Faltam até <see cref="WarnSeconds"/> s para a ilha mudar (hora do aviso âmbar no lugar novo).</summary>
    public static bool WarnNow(double darkSeconds)
    {
        var into = Math.Max(0, darkSeconds) % DryCycleSeconds;
        return into >= DryCycleSeconds - WarnSeconds;
    }

    /// <summary>Centro da ilha seca de índice <paramref name="index"/> (px do mundo).</summary>
    public (double X, double Y) Island(int index)
    {
        var (fx, fy) = Spots[((index % Spots.Length) + Spots.Length) % Spots.Length];
        return (CenterX + (fx * DryRx), CenterY + (fy * DryRy));
    }

    /// <summary>O ponto está alagado na <paramref name="phase"/> (1–3), com <paramref name="darkSeconds"/> s de fase 3.</summary>
    public bool Flooded(double x, double y, int phase, double darkSeconds)
    {
        if (phase <= 1 || Ellipse(x, y, RingRx, RingRy) > 1)
        {
            return false; // fase 1 seca; fora da plataforma não é chão (a grade já bloqueia o mar).
        }

        if (phase == 2)
        {
            return Ellipse(x, y, DryRx, DryRy) > 1;
        }

        var (ix, iy) = Island(SpotIndex(darkSeconds));
        var dx = x - ix;
        var dy = y - iy;
        return (dx * dx) + (dy * dy) > IslandRadius * IslandRadius;
    }

    private double Ellipse(double x, double y, double rx, double ry)
    {
        var dx = (x - CenterX) / rx;
        var dy = (y - CenterY) / ry;
        return (dx * dx) + (dy * dy);
    }
}
