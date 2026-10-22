namespace Vespera.Core.Combat;

/// <summary>
/// Fôlego curto só das esquivas (GDD §5.2): gasta um custo fixo por esquiva e regenera depois de uma pausa sem gastar.
/// </summary>
public sealed class StaminaMeter(double max, double regenPerSecond, double regenDelay)
{
    private double _sinceSpend = double.PositiveInfinity;

    public double Max { get; } = max;

    public double Value { get; private set; } = max;

    public double Fraction => Max <= 0 ? 0 : Value / Max;

    public bool CanSpend(double cost) => Value >= cost;

    /// <summary>Gasta se houver fôlego; senão não gasta nada e devolve falso.</summary>
    public bool TrySpend(double cost)
    {
        if (!CanSpend(cost))
        {
            return false;
        }

        Value -= cost;
        _sinceSpend = 0;
        return true;
    }

    public void Tick(double dt)
    {
        _sinceSpend += dt;
        if (_sinceSpend >= regenDelay)
        {
            Value = Math.Min(Max, Value + (regenPerSecond * dt));
        }
    }

    public void Refill()
    {
        Value = Max;
        _sinceSpend = double.PositiveInfinity;
    }
}
