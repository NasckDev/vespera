namespace Vespera.Core.Combat;

/// <summary>
/// Regras de dano. A fórmula é provisória: só combina o dano base com multiplicadores
/// (nível da arma, golpe pesado, fraqueza…) que vêm de Resources, nunca de constantes aqui.
/// </summary>
public static class DamageCalculator
{
    /// <summary>Dano final de um golpe. Golpe com dano base positivo sempre causa pelo menos 1.</summary>
    public static int Calculate(int baseDamage, float multiplier = 1f)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseDamage);
        ArgumentOutOfRangeException.ThrowIfNegative(multiplier);

        if (baseDamage == 0 || multiplier == 0f)
        {
            return 0;
        }

        return Math.Max(1, (int)MathF.Round(baseDamage * multiplier, MidpointRounding.AwayFromZero));
    }

    /// <summary>HP depois do dano, nunca abaixo de zero.</summary>
    public static int ApplyDamage(int currentHp, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        return Math.Max(0, currentHp - damage);
    }
}
