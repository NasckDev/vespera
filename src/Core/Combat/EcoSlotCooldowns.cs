namespace Vespera.Core.Combat;

/// <summary>Recarga de cada um dos dois slots de Eco do caçador (a tecla 1 e a tecla 2 são independentes).</summary>
public sealed class EcoSlotCooldowns
{
    public const int Slots = 2;

    private readonly double[] _left = new double[Slots];
    private readonly double[] _total = new double[Slots];

    public bool Ready(int slot) => InRange(slot) && _left[slot] <= 0;

    /// <summary>0 = pronto; 1 = recém-usado (ícone da HUD).</summary>
    public float Fraction(int slot) => InRange(slot) && _total[slot] > 0 ? (float)Math.Clamp(_left[slot] / _total[slot], 0, 1) : 0f;

    public void Start(int slot, double seconds)
    {
        if (InRange(slot))
        {
            _left[slot] = seconds;
            _total[slot] = seconds;
        }
    }

    public void Tick(double dt)
    {
        for (var i = 0; i < Slots; i++)
        {
            _left[i] = Math.Max(0, _left[i] - dt);
        }
    }

    private static bool InRange(int slot) => slot >= 0 && slot < Slots;
}
