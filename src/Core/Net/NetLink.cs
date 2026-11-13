using Vespera.Core.Coop;

namespace Vespera.Core.Net;

/// <summary>
/// Relógio de "sem notícia" de um lado da conexão: cada pacote bom chama <see cref="Touch"/>; passado
/// <c>timeoutSeconds</c> sem nada, <see cref="TimedOut"/> diz que o outro lado caiu (além do timeout do próprio ENet).
/// </summary>
public sealed class LinkWatch(double timeoutSeconds)
{
    private double _last = double.NaN;

    public double TimeoutSeconds => timeoutSeconds;

    public bool Started => !double.IsNaN(_last);

    public void Touch(double now) => _last = now;

    public bool TimedOut(double now) => Started && now - _last > timeoutSeconds;
}

/// <summary>Quando o host manda o próximo snapshot: no máximo <c>hz</c> por segundo, sem acumular atraso em quadro lento.</summary>
public sealed class SnapshotPacer(int hz)
{
    private readonly double _interval = 1.0 / Math.Clamp(hz, 1, 120);
    private double _next = double.NegativeInfinity;

    public bool Due(double now)
    {
        if (now < _next)
        {
            return false;
        }

        // Quadro muito atrasado: recomeça do agora em vez de mandar uma rajada para "alcançar".
        _next = now - _next > _interval * 2 ? now + _interval : _next + _interval;
        return true;
    }
}

/// <summary>
/// Ids de rede pequenos e estáveis para objetos do host (monstros): a mesma chave (id da instância no motor) recebe sempre
/// o mesmo número enquanto existir; quem some é esquecido em <see cref="Retain"/>. Números nunca se repetem na sessão.
/// </summary>
public sealed class NetIdMap
{
    private readonly Dictionary<ulong, int> _ids = [];
    private int _next = 1;

    public int Count => _ids.Count;

    public int IdOf(ulong key)
    {
        if (!_ids.TryGetValue(key, out var id))
        {
            id = _next++;
            _ids[key] = id;
        }

        return id;
    }

    /// <summary>Esquece as chaves que não estão mais vivas.</summary>
    public void Retain(IReadOnlyCollection<ulong> alive)
    {
        foreach (var key in _ids.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _ids.Remove(key);
        }
    }
}

/// <summary>Regras do comando que chega da rede ao host.</summary>
public static class RemoteCommands
{
    /// <summary>Botões que abrem telas do host (pausa, inventário, diário): o convidado não pausa nem abre menus no jogo do host.</summary>
    public const CommandButtons HostOnly = CommandButtons.Pause | CommandButtons.Inventory | CommandButtons.Journal;

    public static PlayerCommand Sanitize(PlayerCommand command) =>
        command with { Held = command.Held & ~HostOnly, Pressed = command.Pressed & ~HostOnly };
}
