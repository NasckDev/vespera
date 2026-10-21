using Vespera.Autoload;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Relógio do dia (spec 01 §7, <c>updWorld</c>, <c>world.js:143-155</c>): 1 minuto de jogo por segundo real,
/// 16:00 → 06:00 em 840 s, via <see cref="Core.State.GameState.AdvanceClock"/> (que vira o dia e dispara
/// <c>DayChanged</c>). Emite <c>DayPhaseChanged</c> quando a fase muda e grava o jogo a cada 45 s (<c>world.js:155</c>).
/// Quem chama <see cref="Step"/> já descontou pausa e telas bloqueando o jogo (<see cref="CityLife.IsFrozen"/>).
/// </summary>
public sealed class DayClock(CityLife city)
{
    /// <summary>Autosave da v2 a cada 45 s de jogo.</summary>
    public const double AutosaveSeconds = 45;

    private DayPhase? _phase;
    private int _rawHour = -1;
    private double _saveT;

    /// <summary>Verdadeiro enquanto o relógio está virando o dia (o <c>DayChanged</c> veio da madrugada, não da cama).</summary>
    public bool RollingOver { get; private set; }

    /// <summary>Última fase emitida (null antes da primeira).</summary>
    public DayPhase? Phase => _phase;

    /// <summary>Relógio atual em minutos de jogo (960–1800).</summary>
    public static double Minutes => GameManager.Instance.State.Save.ClockMinutes;

    /// <summary>"HH:MM" para HUD e logs.</summary>
    public static string Formatted => SeasonCalendar.FormatClock(Minutes);

    public void Step(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        RollingOver = true;
        try
        {
            GameManager.Instance.State.AdvanceClock(seconds);
        }
        finally
        {
            RollingOver = false;
        }

        _saveT += seconds;
        if (_saveT > AutosaveSeconds)
        {
            _saveT = 0;
            GameManager.Instance.SaveGame();
        }

        Refresh();
    }

    /// <summary>Recalcula a fase e a hora; emite <c>DayPhaseChanged</c> se a fase mudou.</summary>
    public void Refresh()
    {
        var minutes = Minutes;
        var phase = DayPhases.Of(minutes, GameManager.Db.SeasonData.Clock);
        if (phase != _phase)
        {
            _phase = phase;
            Log.Info($"Fase do dia: {DayPhases.Label(phase)} ({SeasonCalendar.FormatClock(minutes)}).");
            EventBus.Instance.EmitSignal(EventBus.SignalName.DayPhaseChanged, (int)phase);
        }

        var hour = SeasonCalendar.RawHour(minutes);
        if (hour != _rawHour)
        {
            var first = _rawHour < 0;
            _rawHour = hour;
            if (!first)
            {
                city.OnHourChanged();
            }
        }
    }

    /// <summary>Esquece a fase emitida (jogo carregado ou simulação religada): o próximo <see cref="Refresh"/> emite de novo.</summary>
    public void Reset()
    {
        _phase = null;
        _rawHour = -1;
        _saveT = 0;
    }
}
