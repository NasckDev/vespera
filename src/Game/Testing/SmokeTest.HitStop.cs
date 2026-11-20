using System.Threading.Tasks;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// A pausa de impacto e a câmera lenta contam ticks de física, não milissegundos de relógio: com <c>--fixed-fps</c> o
/// runner do CI roda ~3x mais quadros por segundo que um PC, e a mesma pausa virava 3x mais quadros de jogo quase parado
/// (o bot, que conta quadros, batia pouco e os Guardiões "não caíam" no Linux). O smoke atrasa cada quadro em alguns
/// milissegundos de relógio: com a contagem por tempo real a pausa durava muito menos quadros que o esperado.
/// </summary>
public partial class SmokeTest
{
    private const double HitStopProbeSeconds = 0.07;
    private const int HitStopProbeDelayMs = 4;
    private const int HitStopProbeMaxFrames = 600;

    private async Task CheckHitStopIsFrameBased(Level level)
    {
        var feel = level.World.Feel;
        var expected = (int)System.Math.Ceiling(HitStopProbeSeconds * Engine.PhysicsTicksPerSecond);
        feel.HitStop(HitStopProbeSeconds);
        var frames = 0;
        while (feel.Slowed && frames < HitStopProbeMaxFrames)
        {
            OS.DelayMsec(HitStopProbeDelayMs);
            await PhysicsFrames(1);
            frames++;
        }

        Check(frames >= expected - 1 && frames <= expected + 2,
            $"a pausa de impacto de {HitStopProbeSeconds} s deveria durar ~{expected} quadros de física (independente da velocidade da máquina), durou {frames}");
        Check(!feel.Slowed && Engine.TimeScale >= 1.0, $"depois da pausa o tempo deveria voltar ao normal (escala {Engine.TimeScale})");
    }
}
