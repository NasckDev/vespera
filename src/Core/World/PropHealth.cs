namespace Vespera.Core.World;

public enum PropHitResult
{
    /// <summary>Já estava quebrado ou o golpe não tinha dano.</summary>
    Ignored,

    Damaged,

    Broken,
}

/// <summary>Vida de um quebrável (DECISIONS D45): pontos de dano por tipo em <c>rules.world.props.hp</c>.</summary>
public sealed class PropHealth(int maxHp)
{
    public int MaxHp { get; } = Math.Max(1, maxHp);

    public int Hp { get; private set; } = Math.Max(1, maxHp);

    public bool IsBroken => Hp <= 0;

    /// <summary>Fração de vida restante (0–1), para a força do tremor.</summary>
    public float Fraction => (float)Hp / MaxHp;

    public PropHitResult Hit(int damage)
    {
        if (IsBroken || damage <= 0)
        {
            return PropHitResult.Ignored;
        }

        Hp = Math.Max(0, Hp - damage);
        return IsBroken ? PropHitResult.Broken : PropHitResult.Damaged;
    }

    /// <summary>Vida do tipo pelos dados, ou <paramref name="fallback"/> se o tipo não estiver na tabela.</summary>
    public static int HpFor(string kind, IReadOnlyDictionary<string, int> table, int fallback) =>
        table.TryGetValue(kind, out var hp) ? hp : fallback;
}
