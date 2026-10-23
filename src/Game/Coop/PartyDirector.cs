using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Coop;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World;

namespace Vespera.Game.Coop;

/// <summary>
/// Simulação do grupo numa sala do co-op (roda junto da física): reanimar (o aliado de pé segura interagir perto do caído
/// por 2 s, <see cref="ReviveTracker"/>) e a queda do grupo inteiro (todos caídos → regra da Lanterna para a caçada,
/// <see cref="ReviveRules"/>). Só lê comandos e estado; quem troca de sala é a fase. No online, roda só no host.
/// </summary>
public partial class PartyDirector(LevelWorld world) : Node
{
    /// <summary>Tempo da animação de queda antes de a fase levar o grupo de volta (igual à morte solo).</summary>
    public const float WipeDelay = 2.4f;

    private readonly Dictionary<Player, ReviveTracker> _trackers = [];
    private float _wipeTimer = -1f;
    private bool _wiped;

    /// <summary>Um caçador caído levantou (caído, quem ajudou).</summary>
    public event Action<Player, Player?>? Revived;

    /// <summary>Todos caíram (depois da animação): a fase aplica a Lanterna e volta à Praça.</summary>
    public event Action? Wiped;

    /// <summary>Alcance da reanimação, em px de mundo.</summary>
    public float ReviveReach => (float)CoopSession.Rules.Revive.Radius * world.Data.HumanHeight;

    public bool WipePending => _wipeTimer >= 0f || _wiped;

    /// <summary>Progresso da reanimação de um caído (0..1).</summary>
    public float ReviveFraction(Player downed) => _trackers.TryGetValue(downed, out var t) ? (float)t.Fraction : 0f;

    /// <summary>Aliado de pé mais próximo dentro do alcance de reanimar <paramref name="downed"/>, ou null.</summary>
    public Player? HelperFor(Player downed)
    {
        Player? best = null;
        var bestDistance = float.MaxValue;
        foreach (var p in world.Players)
        {
            if (p == downed || !IsInstanceValid(p))
            {
                continue;
            }

            var d = p.GlobalPosition.DistanceTo(downed.GlobalPosition);
            if (ReviveRules.CanRevive(Life(p), Life(downed), d, ReviveReach) && d < bestDistance)
            {
                bestDistance = d;
                best = p;
            }
        }

        return best;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (world.Players.Count < 2 || _wiped)
        {
            return;
        }

        var dt = (float)delta;
        foreach (var downed in world.Players.Where(p => IsInstanceValid(p) && p.Combat.IsDowned).ToList())
        {
            if (!_trackers.TryGetValue(downed, out var tracker))
            {
                tracker = new ReviveTracker(CoopSession.Rules.Revive);
                _trackers[downed] = tracker;
            }

            var helper = HelperFor(downed);
            var holding = helper is not null && !helper.Frozen && helper.Input.IsPressed(InputActions.Interact);
            if (tracker.Tick(dt, holding))
            {
                _trackers.Remove(downed);
                downed.Combat.ReviveFromDown(CoopSession.Rules.RevivedHp(downed.Combat.MaxHp));
                Log.Info($"co-op: {CoopSession.TagOf(downed.Slot)} reanimado por {(helper is null ? "?" : CoopSession.TagOf(helper.Slot))}");
                Revived?.Invoke(downed, helper);
            }
        }

        foreach (var gone in _trackers.Keys.Where(p => !IsInstanceValid(p) || !p.Combat.IsDowned).ToList())
        {
            _trackers.Remove(gone);
        }

        TickWipe(dt);
    }

    private void TickWipe(float dt)
    {
        var lives = world.Players.Where(IsInstanceValid).Select(Life).ToList();
        if (ReviveRules.Evaluate(lives) != PartyStatus.Wiped)
        {
            _wipeTimer = -1f;
            return;
        }

        if (_wipeTimer < 0f)
        {
            _wipeTimer = WipeDelay;
            Log.Info("co-op: o grupo inteiro caiu");
        }

        _wipeTimer -= dt;
        if (_wipeTimer <= 0f)
        {
            _wiped = true;
            Wiped?.Invoke();
        }
    }

    private static HunterLife Life(Player p) => p.Combat.IsDead ? HunterLife.Down : HunterLife.Up;
}
