using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>
/// Estações e relógio do jogo (spec 01 §7; <c>seasonOf</c>, <c>core.js:224</c>; <c>world.js:137-143</c>).
/// O relógio conta minutos de jogo, 1 por segundo real, e passa de 1440 na madrugada; às 06:00 (1800) começa outro dia.
/// </summary>
public sealed class SeasonCalendar(SeasonsFile data)
{
    private const int MinutesPerDay = 24 * 60;

    public ClockDef Clock => data.Clock;

    public IReadOnlyList<SeasonDef> Seasons => data.Seasons;

    /// <summary><c>(2 + floor((dia-1)/3)) % 4</c>: dias 1–3 Outono, 4–6 Inverno, 7–9 Primavera, 10–12 Verão.</summary>
    public int SeasonIndex(int day) =>
        (data.FirstSeasonIndex + ((Math.Max(1, day) - 1) / data.DaysPerSeason)) % data.Seasons.Count;

    public SeasonDef SeasonOf(int day) => data.Seasons[SeasonIndex(day)];

    /// <summary>Relógio depois de <paramref name="seconds"/> reais.</summary>
    public double Advance(double clockMinutes, double seconds) => clockMinutes + (seconds * data.Clock.MinutesPerSecond);

    /// <summary>Chegou a hora de começar outro dia (06:00 do dia seguinte).</summary>
    public bool IsNewDayDue(double clockMinutes) => clockMinutes >= data.Clock.NewDayAtMinutes;

    /// <summary>Relógio do começo de um dia novo (16:00).</summary>
    public double NewDayClock => data.Clock.NewDayStartMinutes;

    /// <summary>Hora sem módulo 24 (usada nos horários de NPC: Lauro fica até 27 = 03:00).</summary>
    public static int RawHour(double clockMinutes) => (int)Math.Floor(clockMinutes / 60);

    /// <summary>Hora do dia, 0–23.</summary>
    public static int HourOfDay(double clockMinutes) => RawHour(clockMinutes) % 24;

    /// <summary>"véspera" no HUD: 16h ≤ hora &lt; 19h.</summary>
    public bool IsVespera(double clockMinutes)
    {
        var h = HourOfDay(clockMinutes);
        return h >= data.Clock.VesperaFromHour && h < data.Clock.VesperaToHour;
    }

    /// <summary><c>fmtClock</c>: "HH:MM" com módulo de 24 h.</summary>
    public static string FormatClock(double clockMinutes)
    {
        var m = (int)Math.Floor(clockMinutes) % MinutesPerDay;
        return $"{m / 60:00}:{m % 60:00}";
    }

    /// <summary><c>fmtT</c>: segundos como "m:ss", arredondando para cima.</summary>
    public static string FormatTimer(double seconds)
    {
        var s = Math.Max(0, (int)Math.Ceiling(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>NPC com rotina está no mapa neste horário (<c>hh &gt;= h0 &amp;&amp; hh &lt; h1</c>, <c>hh = clock/60</c> sem módulo).</summary>
    public static bool IsWalkerPresent(WalkerDef walker, double clockMinutes)
    {
        var hh = clockMinutes / 60;
        return hh >= walker.HourFrom && hh < walker.HourTo;
    }
}
