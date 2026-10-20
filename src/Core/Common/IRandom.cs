namespace Vespera.Core.Common;

/// <summary>
/// Fonte de aleatoriedade injetável (DECISIONS D8). Toda regra de Core que sorteia algo recebe um
/// <see cref="IRandom"/>; produção usa <see cref="SeededRandom"/>, testes usam sequências fixas.
/// </summary>
public interface IRandom
{
    /// <summary>Real uniforme em [0, 1), como <c>Math.random()</c>.</summary>
    double NextDouble();
}

/// <summary>Sorteios do protótipo (<c>core.js:11-13</c>) sobre qualquer <see cref="IRandom"/>.</summary>
public static class RandomExtensions
{
    /// <summary><c>R(a,b)</c>: real uniforme em [a, b).</summary>
    public static double Range(this IRandom random, double min, double max) => min + (random.NextDouble() * (max - min));

    /// <summary><c>R(a,b)</c> sobre um intervalo dos dados.</summary>
    public static double Range(this IRandom random, NumRange range) => random.Range(range.Min, range.Max);

    /// <summary><c>RI(a,b)</c>: inteiro uniforme em [a, b], inclusivo nas duas pontas.</summary>
    public static int RangeInt(this IRandom random, int min, int max) => (int)Math.Floor(random.Range(min, max + 1));

    /// <summary><c>RI(a,b)</c> sobre um intervalo dos dados.</summary>
    public static int RangeInt(this IRandom random, IntRange range) => random.RangeInt(range.Min, range.Max);

    /// <summary>Verdadeiro com probabilidade <paramref name="probability"/> (<c>Math.random() &lt; p</c>).</summary>
    public static bool Chance(this IRandom random, double probability) => random.NextDouble() < probability;

    /// <summary><c>pick(a)</c>: um elemento uniforme da lista.</summary>
    public static T Pick<T>(this IRandom random, IReadOnlyList<T> items)
    {
        ArgumentOutOfRangeException.ThrowIfZero(items.Count);
        return items[Math.Min(items.Count - 1, (int)Math.Floor(random.NextDouble() * items.Count))];
    }
}

/// <summary><see cref="IRandom"/> de produção: <see cref="Random"/> com seed (reprodutível por sessão).</summary>
public sealed class SeededRandom(int seed) : IRandom
{
    private readonly Random _random = new(seed);

    public int Seed { get; } = seed;

    public double NextDouble() => _random.NextDouble();
}
