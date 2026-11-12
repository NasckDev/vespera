using Vespera.Core.Coop;

namespace Vespera.Core.Net;

/// <summary>
/// Fila de comandos de um convidado no host. A rede entrega em rajadas: o host consome, a cada passo de física, tudo o que
/// chegou desde o passo anterior juntado com <see cref="PlayerCommand.Merge"/> (direção mais nova, nenhum aperto perdido).
/// Sem nada novo, repete o último comando sem os apertos (segura a direção, não repete o golpe). Comandos repetidos ou
/// fora de ordem (tick do convidado menor ou igual ao último recebido) são descartados.
/// </summary>
public sealed class CommandQueue(byte slot, int capacity = 30)
{
    private readonly Queue<PlayerCommand> _pending = new();
    private PlayerCommand _last = PlayerCommand.Idle(0, slot);
    private uint? _lastReceivedTick;

    public byte Slot => slot;

    public int Pending => _pending.Count;

    /// <summary>Comandos descartados (duplicados ou atrasados).</summary>
    public int Dropped { get; private set; }

    public bool Push(PlayerCommand command)
    {
        if (_lastReceivedTick is { } lastTick && command.Tick <= lastTick)
        {
            Dropped++;
            return false;
        }

        _lastReceivedTick = command.Tick;
        if (_pending.Count >= capacity)
        {
            // Atraso grande: junta os dois mais velhos para não crescer sem limite (o aperto continua lá).
            var oldest = _pending.Dequeue();
            var next = _pending.Dequeue();
            var rest = _pending.ToArray();
            _pending.Clear();
            _pending.Enqueue(oldest.Merge(next));
            foreach (var c in rest)
            {
                _pending.Enqueue(c);
            }
        }

        _pending.Enqueue(command with { Slot = slot });
        return true;
    }

    /// <summary>Comando para o passo <paramref name="hostTick"/> do host.</summary>
    public PlayerCommand Next(uint hostTick)
    {
        if (_pending.Count == 0)
        {
            _last = _last with { Tick = hostTick, Pressed = CommandButtons.None };
            return _last;
        }

        var merged = _pending.Dequeue();
        while (_pending.Count > 0)
        {
            merged = merged.Merge(_pending.Dequeue());
        }

        _last = merged with { Tick = hostTick, Slot = slot };
        return _last;
    }
}
