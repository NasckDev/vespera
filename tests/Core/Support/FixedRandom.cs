namespace Vespera.Tests.Core.Support;

using Vespera.Core.Common;

/// <summary>RNG de teste: devolve os valores dados, em ordem, e falha se a regra pedir mais sorteios que o esperado.</summary>
public sealed class FixedRandom(params double[] values) : IRandom
{
    private int _next;

    public int Consumed => _next;

    public double NextDouble()
    {
        if (_next >= values.Length)
        {
            throw new InvalidOperationException($"FixedRandom esgotado após {values.Length} sorteios.");
        }

        return values[_next++];
    }
}

/// <summary>RNG de teste que sempre devolve o mesmo valor.</summary>
public sealed class ConstantRandom(double value) : IRandom
{
    public double NextDouble() => value;
}
