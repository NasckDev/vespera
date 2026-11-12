using System.Numerics;

namespace Vespera.Core.Net;

/// <summary>
/// Guarda os últimos snapshots com a hora (segundos locais) em que chegaram e responde onde desenhar cada caçador e
/// monstro em <c>agora - atraso</c>: interpola entre os dois snapshots em volta; antes do primeiro ou depois do último,
/// segura a ponta (sem extrapolar). Um id que só existe no mais novo aparece direto; troca de sala zera o histórico.
/// </summary>
public sealed class SnapshotInterpolator(double delaySeconds, int capacity = 32)
{
    private readonly List<(double Time, WorldSnapshot Snapshot)> _buffer = [];

    public WorldSnapshot? Latest => _buffer.Count == 0 ? null : _buffer[^1].Snapshot;

    public int Count => _buffer.Count;

    /// <summary>Guarda um snapshot; ignora um mais velho que o último (UDP fora de ordem).</summary>
    public bool Add(double now, WorldSnapshot snapshot)
    {
        if (Latest is { } latest)
        {
            if (snapshot.Tick <= latest.Tick)
            {
                return false;
            }

            if (snapshot.LevelId != latest.LevelId)
            {
                _buffer.Clear();
            }
        }

        _buffer.Add((now, snapshot));
        if (_buffer.Count > capacity)
        {
            _buffer.RemoveAt(0);
        }

        return true;
    }

    public Vector2? HunterAt(double now, int slot) =>
        Sample(now, s => s.Hunter(slot) is { } h ? new Vector2(h.X, h.Y) : null);

    public Vector2? EnemyAt(double now, int id) =>
        Sample(now, s =>
        {
            foreach (var e in s.Enemies)
            {
                if (e.Id == id)
                {
                    return new Vector2(e.X, e.Y);
                }
            }

            return null;
        });

    private Vector2? Sample(double now, Func<WorldSnapshot, Vector2?> pick)
    {
        if (_buffer.Count == 0)
        {
            return null;
        }

        var target = now - delaySeconds;
        var newest = _buffer[^1];
        if (target >= newest.Time || _buffer.Count == 1)
        {
            return pick(newest.Snapshot);
        }

        for (var i = _buffer.Count - 1; i > 0; i--)
        {
            var (t0, s0) = _buffer[i - 1];
            var (t1, s1) = _buffer[i];
            if (target < t0)
            {
                continue;
            }

            var b = pick(s1);
            if (pick(s0) is not { } a)
            {
                return b;
            }

            if (b is null)
            {
                return a;
            }

            var span = t1 - t0;
            var f = span <= 0 ? 1f : (float)Math.Clamp((target - t0) / span, 0, 1);
            return Vector2.Lerp(a, b.Value, f);
        }

        return pick(_buffer[0].Snapshot) ?? pick(newest.Snapshot);
    }
}
