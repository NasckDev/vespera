namespace Vespera.Core.World;

/// <summary>
/// Memória da sessão para objetos do mapa (DECISIONS D45): quebráveis quebrados, baús abertos e flores colhidas ficam
/// assim ao sair e voltar ao mapa, e voltam no dia seguinte (<see cref="NewDay"/>). Flores também crescem de novo depois
/// de um tempo. Lampiões guardam aceso/apagado. A chave é estável por mapa (<see cref="Key"/>), criada uma vez por objeto.
/// </summary>
public sealed class WorldObjectRegistry
{
    private readonly Dictionary<string, (int Day, double Time)> _spent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _flags = new(StringComparer.Ordinal);

    /// <summary>Quantos objetos estão gastos agora (para testes).</summary>
    public int SpentCount => _spent.Count;

    /// <summary>Chave do objeto: <c>mapa:tipo:índice</c> (índice na lista do layout).</summary>
    public static string Key(string mapId, string kind, int index) => $"{mapId}:{kind}:{index}";

    /// <summary>Marca como gasto (quebrado, aberto, colhido) no dia e no tempo de jogo dados.</summary>
    public void MarkSpent(string key, int day, double time) => _spent[key] = (day, time);

    /// <summary>
    /// Gasto hoje? Com <paramref name="regrowSeconds"/>, deixa de estar gasto depois desse tempo (flor que cresce de novo).
    /// Um dia diferente do registrado também libera.
    /// </summary>
    public bool IsSpent(string key, int day, double time, double? regrowSeconds = null)
    {
        if (!_spent.TryGetValue(key, out var entry) || entry.Day != day)
        {
            return false;
        }

        return regrowSeconds is not { } regrow || time - entry.Time < regrow;
    }

    /// <summary>Tempo restante até voltar (0 se já não está gasto).</summary>
    public double RemainingSeconds(string key, int day, double time, double regrowSeconds) =>
        _spent.TryGetValue(key, out var entry) && entry.Day == day ? Math.Max(0, regrowSeconds - (time - entry.Time)) : 0;

    public void Clear(string key) => _spent.Remove(key);

    /// <summary>Novo dia: tudo volta (quebráveis inteiros, baús fechados, flores crescidas). Lampiões ficam como estão.</summary>
    public void NewDay() => _spent.Clear();

    public bool GetFlag(string key, bool fallback) => _flags.TryGetValue(key, out var v) ? v : fallback;

    public void SetFlag(string key, bool value) => _flags[key] = value;
}
