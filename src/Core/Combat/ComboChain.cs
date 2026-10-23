namespace Vespera.Core.Combat;

/// <summary>
/// Combo com fila de entrada: um aperto fica guardado por <see cref="BufferSeconds"/> e sai assim que o golpe atual
/// libera (<c>ready</c>). Se o próximo golpe começar até <see cref="WindowSeconds"/> depois do fim do anterior, o combo
/// avança; senão volta ao primeiro. Depois do último passo recomeça. Tempo em segundos de jogo (relógio do chamador).
/// </summary>
public sealed class ComboChain
{
    public ComboChain(int steps, double bufferSeconds, double windowSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        Steps = steps;
        BufferSeconds = bufferSeconds;
        WindowSeconds = windowSeconds;
    }

    public int Steps { get; }

    public double BufferSeconds { get; }

    public double WindowSeconds { get; }

    /// <summary>Passo do golpe em curso (ou do último), 0-based; -1 antes do primeiro.</summary>
    public int Step { get; private set; } = -1;

    public bool HasBufferedPress => _pressedAt is not null;

    private double? _pressedAt;
    private double _finishedAt = double.NegativeInfinity;

    /// <summary>Registra um aperto (fica na fila até ser consumido ou vencer).</summary>
    public void Press(double now) => _pressedAt = now;

    /// <summary>
    /// Se há aperto válido na fila e o personagem está livre, consome e devolve o passo que começa agora.
    /// </summary>
    public bool TryStart(double now, bool ready, out int step)
    {
        step = -1;
        if (_pressedAt is not { } pressed || now - pressed > BufferSeconds)
        {
            _pressedAt = null;
            return false;
        }

        if (!ready)
        {
            return false;
        }

        _pressedAt = null;
        var chained = Step >= 0 && Step < Steps - 1 && now - _finishedAt <= WindowSeconds;
        Step = chained ? Step + 1 : 0;
        step = Step;
        return true;
    }

    /// <summary>O golpe atual terminou (abre a janela do próximo).</summary>
    public void Finish(double now) => _finishedAt = now;

    /// <summary>Esquiva, dano ou troca de arma zeram o combo e a fila.</summary>
    public void Reset()
    {
        Step = -1;
        _pressedAt = null;
        _finishedAt = double.NegativeInfinity;
    }
}
