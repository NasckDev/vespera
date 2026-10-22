namespace Vespera.Core.Combat;

/// <summary>Geometria do combate da v2 (<c>core.js:16</c>, <c>combat.js:153-163</c>). Tudo em u (1 u = 1 px do mapa).</summary>
public static class CombatMath
{
    /// <summary><c>angDiff(a, b)</c>: diferença absoluta entre dois ângulos, em [0, π].</summary>
    public static double AngDiff(double a, double b)
    {
        var d = a - b;
        while (d > Math.PI)
        {
            d -= 2 * Math.PI;
        }

        while (d < -Math.PI)
        {
            d += 2 * Math.PI;
        }

        return Math.Abs(d);
    }

    /// <summary>
    /// Setor do golpe corpo a corpo (<c>meleeHit</c>): a partir de (<paramref name="ox"/>, <paramref name="oy"/>), o alvo em
    /// (<paramref name="tx"/>, <paramref name="ty"/>) com raio <paramref name="targetRadius"/> é atingido se
    /// <c>d = dist − r &lt;= range</c> e (<c>angDiff &lt;= arc/2</c> ou <c>d &lt; 4</c>).
    /// </summary>
    public static bool InSector(double ox, double oy, double tx, double ty, double targetRadius, double aim, double arc, double range)
    {
        var dx = tx - ox;
        var dy = ty - oy;
        var d = Math.Sqrt((dx * dx) + (dy * dy)) - targetRadius;
        return d <= range && (AngDiff(Math.Atan2(dy, dx), aim) <= arc / 2 || d < 4);
    }

    /// <summary>Props quebráveis no golpe: <c>dist &lt; range + 6</c> e <c>angDiff &lt;= arc/2 + 0.3</c> (alvo em <c>pp.y − 6</c>).</summary>
    public static bool PropInSector(double ox, double oy, double px, double py, double aim, double arc, double range)
    {
        var dx = px - ox;
        var dy = py - 6 - oy;
        return Math.Sqrt((dx * dx) + (dy * dy)) < range + 6 && AngDiff(Math.Atan2(dy, dx), aim) <= (arc / 2) + 0.3;
    }

    public static double Hypot(double x, double y) => Math.Sqrt((x * x) + (y * y));

    public static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>Gira o ângulo <paramref name="current"/> em direção a <paramref name="target"/> no máximo <paramref name="maxStep"/> rad (orbe teleguiado).</summary>
    public static double TurnTowards(double current, double target, double maxStep)
    {
        var diff = ((target - current + (Math.PI * 3)) % (Math.PI * 2)) - Math.PI;
        return current + Math.Clamp(diff, -maxStep, maxStep);
    }
}

/// <summary>
/// Hitstop da v2 (<c>main.js:104</c>): enquanto há hitstop, o mundo do combate anda a <c>dt × 0.08</c>; o hitstop
/// decai em tempo real. A câmera e o fade usam o dt normal.
/// </summary>
public sealed class CombatClock
{
    public const double HitstopScale = 0.08;

    public double Hitstop { get; private set; }

    /// <summary>Tempo de combate acumulado (relógio de jogo para recargas).</summary>
    public double Time { get; private set; }

    /// <summary><c>G.hitstop = max(G.hitstop, s)</c>.</summary>
    public void AtLeast(double seconds) => Hitstop = Math.Max(Hitstop, seconds);

    /// <summary><c>G.hitstop = s</c> (dano no jogador, morte, chefe, supremo).</summary>
    public void Set(double seconds) => Hitstop = Math.Max(0, seconds);

    /// <summary>Avança um frame real e devolve o dt do combate.</summary>
    public double Step(double dt)
    {
        var world = Hitstop > 0 ? dt * HitstopScale : dt;
        Hitstop = Math.Max(0, Hitstop - dt);
        Time += world;
        return world;
    }

    public void Reset() => Hitstop = 0;
}
