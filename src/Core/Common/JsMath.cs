namespace Vespera.Core.Common;

/// <summary>
/// Matemática herdada do protótipo JS. Toda conta portada da v2 arredonda com <see cref="Round"/>
/// (DECISIONS D7): <c>Math.round</c> do JS é <c>floor(x + 0.5)</c>, enquanto <c>Math.Round</c> do C#
/// usa banker's rounding (30 Lumes em invasão: JS 41, C# 40).
/// </summary>
public static class JsMath
{
    /// <summary>Equivalente a <c>Math.round(x)</c> do JavaScript.</summary>
    public static int Round(double x) => (int)Math.Floor(x + 0.5);
}
