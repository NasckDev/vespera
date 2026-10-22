using Vespera.Core.Data;

namespace Vespera.Core.World;

/// <summary>
/// Fases do dia de Lumeira (spec 01 §7 e §4.5). O dia começa às 16:00 e vira às 06:00 (relógio de 960 a 1800).
/// <list type="bullet">
/// <item><see cref="Vespera"/>: 16:00–19:00, rótulo "véspera" do HUD; a cidade escurece a partir das 17:00.</item>
/// <item><see cref="Night"/>: 19:00–05:00 (horas 19–29 sem módulo), escuridão subindo até a noite fechada.</item>
/// <item><see cref="Dawn"/>: 05:00–06:00 (horas 29–30), a escuridão volta a cair até o dia virar.</item>
/// </list>
/// O valor inteiro é o que viaja no sinal <c>DayPhaseChanged</c>.
/// </summary>
public enum DayPhase
{
    Vespera = 0,
    Night = 1,
    Dawn = 2,
}

/// <summary>Fase do dia a partir do relógio (minutos de jogo, sem módulo 24).</summary>
public static class DayPhases
{
    /// <summary>Hora (sem módulo) em que a escuridão começa a cair: 29 = 05:00 (<c>render.js:5</c>).</summary>
    public const double DawnHour = 29;

    public static DayPhase Of(double clockMinutes, ClockDef clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var hour = clockMinutes / 60;
        if (hour < clock.VesperaToHour)
        {
            return DayPhase.Vespera;
        }

        return hour < DawnHour ? DayPhase.Night : DayPhase.Dawn;
    }

    /// <summary>Nome em português para logs e textos ("véspera", "noite", "aurora").</summary>
    public static string Label(DayPhase phase) => phase switch
    {
        DayPhase.Vespera => "véspera",
        DayPhase.Night => "noite",
        _ => "aurora",
    };
}
