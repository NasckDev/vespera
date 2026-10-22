using Vespera.Core.Common;

namespace Vespera.Core.Combat;

/// <summary>
/// Física de um item no chão (<c>dropPick</c>/<c>updPicks</c>, <c>combat.js:476-492</c>; spec 02 §3.12): sai em ângulo
/// aleatório a <c>R(20, 60)</c> u/s (vy ×0.6), sobe de z 8 com <c>vz = R(50, 90)</c>, gravidade 260, quica se
/// <c>vz &lt; −30</c>; depois de 0.45 s, a menos de 48 u é atraído pelo ímã e é coletado a menos de 9 u.
/// </summary>
public struct PickupMotion
{
    public const double MagnetDelay = 0.45;
    public const double MagnetRadius = 48;
    public const double CollectRadius = 9;
    public const double Gravity = 260;

    public double X;
    public double Y;
    public double Vx;
    public double Vy;
    public double Z;
    public double Vz;
    public double T;

    /// <summary>Drop que salta do ponto (x, y); o item fica em <c>y + 6</c>.</summary>
    public static PickupMotion Drop(double x, double y, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var a = random.Range(0, Math.PI * 2);
        var sp = random.Range(20, 60);
        return new PickupMotion
        {
            X = x,
            Y = y + 6,
            Vx = Math.Cos(a) * sp,
            Vy = Math.Sin(a) * sp * 0.6,
            Z = 8,
            Vz = random.Range(50, 90),
        };
    }

    /// <summary>Item parado no chão (Lanternas, Eco já pousado).</summary>
    public static PickupMotion Still(double x, double y) => new() { X = x, Y = y };

    /// <summary>Gravidade e quique.</summary>
    public void Step(double dt)
    {
        T += dt;
        if (Z > 0 || Vz > 0)
        {
            Vz -= Gravity * dt;
            Z += Vz * dt;
            X += Vx * dt;
            Y += Vy * dt;
            if (Z <= 0)
            {
                Z = 0;
                Vz = Vz < -30 ? -Vz * 0.35 : 0;
                Vx *= 0.5;
                Vy *= 0.5;
            }
        }
    }

    /// <summary>Ímã em direção ao pé do jogador (alvo <c>p.y − 4</c>). Devolve true quando deve ser coletado.</summary>
    public bool Magnet(double px, double py, double dt)
    {
        var d = CombatMath.Hypot(px - X, py - Y);
        if (T > MagnetDelay && d < MagnetRadius)
        {
            var s = Math.Min(1, dt * (5 + ((MagnetRadius - d) / 6)));
            X += (px - X) * s;
            Y += (py - 4 - Y) * s;
        }

        return T > MagnetDelay && d < CollectRadius;
    }
}
