using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.World;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Chão alcançável da sala: a sala só acaba quando a onda inteira cai, então nenhum monstro da onda pode nascer ou ficar
/// onde o caçador não chega a pé (atrás da barreira da saída, atrás de um selo, num bolsão murado). Quem nasceria fora
/// nasce no chão alcançável mais perto; quem for parar fora (teleporte do Rasgo, Espectro que atravessa parede, empurrão)
/// volta para dentro depois de <see cref="OutOfReachGrace"/> segundos.
/// </summary>
public partial class RoomDirector
{
    /// <summary>Tempo fora do chão alcançável antes de o monstro voltar (o Espectro passa por paredes de propósito).</summary>
    private const float OutOfReachGrace = 2f;

    private const float ReachCheckPeriod = 0.25f;

    private readonly Dictionary<Enemy, float> _outOfReach = [];
    private ReachableFloor? _reach;
    private float _reachClock;

    /// <summary>Calcula o chão alcançável a partir da entrada, com a barreira da saída e os selos fechados.</summary>
    private void BuildReach()
    {
        var closed = new List<RectF>();
        if (_data.Exit is { } exit)
        {
            closed.Add(ToRect(exit.Barrier));
        }

        closed.AddRange(_data.Sealed.Select(ToRect));
        var start = _world.ClampToFloor(_world.SpawnPoint);
        var reach = _world.Grid.Reach(start.X, start.Y, closed);
        if (reach.Count == 0)
        {
            Log.Warn($"sala {_world.Data.Id}: a entrada não tem chão alcançável; monstros sem correção de posição");
            return;
        }

        _reach = reach;
    }

    private static RectF ToRect(float[] r) => new(r[0], r[1], r[2], r[3]);

    /// <summary>O monstro está numa célula alcançável a pé desde a entrada (sem cálculo, tudo conta como alcançável).</summary>
    private bool IsReachable(Vector2 point)
    {
        if (_reach is null)
        {
            return true;
        }

        var cell = _world.Grid.Cell;
        return _reach.IsReachable(Mathf.FloorToInt(point.X / cell), Mathf.FloorToInt(point.Y / cell));
    }

    /// <summary>O ponto, se estiver no chão alcançável; senão o centro da célula alcançável mais perto.</summary>
    private Vector2 ReachPoint(Vector2 point)
    {
        if (_reach is null || IsReachable(point))
        {
            return _world.ClampToFloor(point);
        }

        var cell = _world.Grid.Cell;
        if (_reach.Nearest(Mathf.FloorToInt(point.X / cell), Mathf.FloorToInt(point.Y / cell)) is not { } near)
        {
            return _world.ClampToFloor(point);
        }

        var (x, y) = _world.Grid.CellCenter(near.Col, near.Row);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Quem precisa estar ao alcance a pé: a onda e, nas salas de Guardião, os invocados dele (Velinhas da Velória etc.),
    /// que também contam como vivos. O Guardião e o parceiro ficam de fora (se escondem e se movem de propósito).
    /// </summary>
    private IEnumerable<Enemy> TrackedForReach() => _wave
        .Concat(_world.Enemies.Where(e => IsInstanceValid(e) && !e.Dead && e != Guardian && e != Partner))
        .Distinct();

    /// <summary>Traz de volta ao chão alcançável o monstro da onda que ficou fora por mais de <see cref="OutOfReachGrace"/> s.</summary>
    private void KeepWaveReachable(float dt)
    {
        if (_reach is null)
        {
            return;
        }

        _reachClock -= dt;
        if (_reachClock > 0f)
        {
            return;
        }

        var step = ReachCheckPeriod - _reachClock;
        _reachClock = ReachCheckPeriod;
        var tracked = TrackedForReach().ToList();
        foreach (var enemy in _outOfReach.Keys.Where(e => !tracked.Contains(e)).ToList())
        {
            _outOfReach.Remove(enemy);
        }

        foreach (var enemy in tracked)
        {
            if (!IsInstanceValid(enemy) || enemy.Dead || IsReachable(enemy.GlobalPosition))
            {
                _outOfReach.Remove(enemy);
                continue;
            }

            var outFor = _outOfReach.GetValueOrDefault(enemy) + step;
            if (outFor < OutOfReachGrace)
            {
                _outOfReach[enemy] = outFor;
                continue;
            }

            _outOfReach.Remove(enemy);
            var from = enemy.GlobalPosition;
            enemy.GlobalPosition = ReachPoint(from);
            Log.Info($"sala {_world.Data.Id}: {enemy.MonsterId} fora do chão alcançável em ({from.X:0},{from.Y:0}); " +
                     $"volta para ({enemy.GlobalPosition.X:0},{enemy.GlobalPosition.Y:0})");
        }
    }
}
