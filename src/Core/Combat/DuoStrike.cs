namespace Vespera.Core.Combat;

/// <summary>O que o golpe combinado da dupla de Guardiões fez neste quadro.</summary>
public enum DuoEvent
{
    None,

    /// <summary>Os dois se alinharam: o aviso no chão começa agora.</summary>
    WarningStarted,

    /// <summary>O golpe cai agora.</summary>
    Strike,

    /// <summary>Um dos dois caiu durante o aviso: o golpe não acontece.</summary>
    Cancelled,
}

/// <summary>Situação da dupla neste quadro.</summary>
/// <param name="AAlive">O primeiro Guardião está vivo.</param>
/// <param name="BAlive">O segundo Guardião está vivo.</param>
/// <param name="BothReady">Os dois podem agir (vivos e fora de intro, voo ou desaparecidos).</param>
/// <param name="Gap">Distância entre os dois, em unidades do mapa (já dividida pela escala do ator).</param>
/// <param name="HunterStanding">Há ao menos um caçador de pé.</param>
public readonly record struct DuoInput(bool AAlive, bool BAlive, bool BothReady, double Gap, bool HunterStanding);

/// <summary>
/// Golpe combinado dos Guardiões em dupla (ROTEIRO-V2 §13, ato VII: "Guardiões em dupla"). Só começa com os dois vivos,
/// prontos e a menos de <see cref="MaxGap"/> um do outro; avisa no chão por <see cref="WarnSeconds"/> (três vezes mais que
/// o aviso de 0,4 s de cada um, para dar tempo de sair do elo ou esquivar) e então cai. Se um deles cai no aviso, cancela.
/// Depois do golpe espera <see cref="Cooldown"/>. Lógica pura; o desenho e o dano ficam em <c>Game/World/Rift/DuoStrike</c>.
/// </summary>
public sealed class DuoStrike
{
    /// <summary>Espera antes do primeiro golpe da luta (o caçador vê cada um lutar sozinho antes).</summary>
    public const double FirstDelay = 7;

    /// <summary>Pausa entre dois golpes combinados.</summary>
    public const double Cooldown = 11;

    /// <summary>Aviso no chão: sempre mais longo que o aviso de 0,4 s da dupla.</summary>
    public const double WarnSeconds = 1.3;

    /// <summary>Distância máxima entre os dois (unidades do mapa) para eles se alinharem.</summary>
    public const double MaxGap = 560;

    /// <summary>Largura da faixa (elo) entre os dois, em unidades.</summary>
    public const double LaneWidth = 90;

    /// <summary>Raio do pulso aos pés de cada Guardião, em unidades.</summary>
    public const double PulseRadius = 120;

    /// <summary>Multiplicador do dano: a média dos dois golpes base ×1,25 (no teto dos golpes de chefe, que vão de ×0,6 a ×1,3).</summary>
    public const double DamageMult = 1.25;

    private double _timer = FirstDelay;

    public bool Warning { get; private set; }

    /// <summary>Quantos golpes já caíram nesta luta.</summary>
    public int Strikes { get; private set; }

    /// <summary>Segundos até o golpe (no aviso) ou até poder avisar (parado).</summary>
    public double Remaining => _timer;

    /// <summary>Pula a espera inicial (testes e bots).</summary>
    public void SkipDelay() => _timer = Math.Min(_timer, 0);

    public DuoEvent Tick(double dt, in DuoInput input)
    {
        if (Warning)
        {
            if (!input.AAlive || !input.BAlive)
            {
                Warning = false;
                _timer = Cooldown;
                return DuoEvent.Cancelled;
            }

            _timer -= dt;
            if (_timer > 0)
            {
                return DuoEvent.None;
            }

            Warning = false;
            Strikes++;
            _timer = Cooldown;
            return DuoEvent.Strike;
        }

        if (!input.BothReady || !input.HunterStanding || input.Gap > MaxGap)
        {
            // Só conta a espera enquanto o elo é possível: separados ou atordoados, o relógio para.
            return DuoEvent.None;
        }

        _timer -= dt;
        if (_timer > 0)
        {
            return DuoEvent.None;
        }

        Warning = true;
        _timer = WarnSeconds;
        return DuoEvent.WarningStarted;
    }

    /// <summary>Dano do golpe: média dos danos base dos dois (já com o bônus de cada um) vezes <see cref="DamageMult"/>.</summary>
    public static double Damage(double damageA, double damageB) => (damageA + damageB) / 2 * DamageMult;

    /// <summary>Distância do ponto à reta finita de A a B.</summary>
    public static double DistanceToLane(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = (dx * dx) + (dy * dy);
        var t = len2 <= 0 ? 0 : Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / len2, 0, 1);
        var cx = ax + (t * dx);
        var cy = ay + (t * dy);
        return Math.Sqrt(((px - cx) * (px - cx)) + ((py - cy) * (py - cy)));
    }

    /// <summary>
    /// O caçador (centro em <paramref name="px"/>, <paramref name="py"/>, corpo de raio <paramref name="bodyRadius"/>) está
    /// na zona do golpe: na faixa entre A e B ou no pulso aos pés de qualquer um. <paramref name="scale"/> é a escala do ator.
    /// </summary>
    public static bool Hits(double px, double py, double ax, double ay, double bx, double by, double bodyRadius, double scale)
    {
        var lane = DistanceToLane(px, py, ax, ay, bx, by) <= (LaneWidth * scale / 2) + bodyRadius;
        var pulse = Dist(px, py, ax, ay) <= (PulseRadius * scale) + bodyRadius || Dist(px, py, bx, by) <= (PulseRadius * scale) + bodyRadius;
        return lane || pulse;
    }

    private static double Dist(double x1, double y1, double x2, double y2) => Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));
}
