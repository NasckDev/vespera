using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot de combate (headless, <c>--combat-bot</c>): na Praça abre o portal, escolhe a arma, atravessa as quatro salas
/// da Brecha E matando tudo com golpes de verdade (caçador invulnerável e dano ×10 para caber no teste), abre o baú,
/// derrota a Lodo-Mãe e volta pela fenda. Depois cai de propósito para provar a Lanterna (bolsa fica na Brecha) e a
/// recupera. Falha se algo travar ou se o save não receber o saque.
/// </summary>
public sealed class CombatBot(Level level, SmokeTest host)
{
    private const int RoomFrameLimit = 60 * 120;
    private const int StuckFrames = 75;
    private const float AttackReach = 48f;

    private readonly List<string> _failures = [];
    private readonly ScriptedInput _input = new();

    private LevelWorld World => level.World;

    private float S => World.Data.ActorScale;

    public async Task<List<string>> Run(HunterClass hunter)
    {
        var save = RiftSession.Save;
        var shardsBefore = save.MaterialCount(RiftSession.Rules.FragmentMaterial);
        var closedBefore = save.BrechasClosed;
        RiftSession.Cheat = true;
        await OpenPortalAndEnter(hunter);
        var killed = 0;
        foreach (var room in RiftSession.Brecha.Rooms)
        {
            killed += await ClearRoom(room);
            if (_failures.Count > 0)
            {
                break;
            }
        }

        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId, 600, "volta à Praça");
        var gained = save.MaterialCount(RiftSession.Rules.FragmentMaterial) - shardsBefore;
        Check(!RiftSession.InRift, "depois do portal de saída a caçada deveria ter acabado");
        Check(save.BrechasClosed == closedBefore + 1, $"Brecha deveria contar como fechada ({save.BrechasClosed})");
        Check(gained > 0, $"o save deveria receber Fragmentos (ganhou {gained})");
        Check(save.MaterialCount(RiftSession.Brecha.GuardianMaterial) >= RiftSession.Brecha.GuardianMaterialCount, "o save deveria ter o Gel de Lodo do Guardião");
        Log.Info($"bot de combate: {hunter} limpou {RiftSession.Brecha.Rooms.Count} salas, {killed} monstros, +{gained} Fragmentos");
        if (_failures.Count == 0)
        {
            await DieAndRecoverLantern();
        }

        RiftSession.Cheat = false;
        return _failures;
    }

    private async Task OpenPortalAndEnter(HunterClass hunter)
    {
        var portal = World.Zones.First(z => z.Kind == "brecha");
        Bind();
        World.Player.Teleport(portal.GlobalPosition + new Vector2(0, 40f * S));
        await host.PhysicsFrames(4);
        Check(World.Player.Focus?.Kind == "brecha", $"perto do portal o foco deveria ser a Brecha, foi '{World.Player.Focus?.Kind}'");
        _input.Tap(InputActions.Interact);
        await host.PhysicsFrames(4);
        Check(level.PickerOpen, "interagir com a Brecha deveria abrir a escolha de arma");
        level.EnterRift(hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "primeira sala");
        Check(World.Data.Id == RiftSession.Brecha.Rooms[0], $"primeira sala deveria ser {RiftSession.Brecha.Rooms[0]}, foi {World.Data.Id}");
    }

    private async Task<int> ClearRoom(string room)
    {
        await WaitUntil(() => !level.Switching && World.Data.Id == room && World.Director is not null, 600, $"sala {room}");
        Bind();
        var director = World.Director!;
        var seen = new HashSet<Enemy>();
        var frames = 0;
        var stuck = 0;
        var lastDistance = float.MaxValue;
        while (!director.Cleared && frames++ < RoomFrameLimit)
        {
            var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(World.Player.GlobalPosition)).FirstOrDefault();
            if (target is null)
            {
                _input.Move = Vector2.Zero;
                await host.PhysicsFrames(1);
                continue;
            }

            seen.Add(target);
            var to = target.GlobalPosition - World.Player.GlobalPosition;
            var distance = to.Length();
            stuck = distance < lastDistance - 0.5f || distance < AttackReach * S ? 0 : stuck + 1;
            lastDistance = distance;
            if (stuck > StuckFrames)
            {
                World.Player.Teleport(World.ClampToFloor(target.GlobalPosition + (Vector2.Down * 30f * S)));
                stuck = 0;
            }

            _input.Move = distance > AttackReach * S * 0.8f ? to.Normalized() : Vector2.Zero;
            if (distance < (AttackReach + target.HitRadius) * S * 1.6f && frames % 8 == 0)
            {
                _input.Tap(frames % 96 == 0 ? InputActions.Heavy : InputActions.Attack);
            }

            await host.PhysicsFrames(1);
        }

        _input.Move = Vector2.Zero;
        Check(director.Cleared, $"sala {room} não ficou limpa em {RoomFrameLimit / 60} s");
        await CollectAndLeave(director);
        return seen.Count;
    }

    private async Task CollectAndLeave(RoomDirector director)
    {
        if (director.Chest is { } chest)
        {
            World.Player.Teleport(World.ClampToFloor(chest.GlobalPosition + (Vector2.Down * 40f * S)));
            await host.PhysicsFrames(4);
            _input.Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
            Check(chest.IsOpen, "interagir com o baú deveria abri-lo");
        }

        await host.PhysicsFrames(90);
        if (director.ExitPortal is null && World.Data.Rift?.ExitPortal is not null)
        {
            await WaitUntil(() => director.ExitPortal is not null, 400, "fenda de saída");
        }

        if (director.ExitPortal is { } portal)
        {
            World.Player.Teleport(World.ClampToFloor(portal.GlobalPosition + (Vector2.Down * 40f * S)));
            await host.PhysicsFrames(6);
            _input.Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
            return;
        }

        if (World.Data.Rift?.Exit is { } exit)
        {
            var t = exit.Trigger;
            World.Player.Teleport(new Vector2(t[0] + (t[2] / 2f), t[1] + (t[3] / 2f)));
            await host.PhysicsFrames(4);
        }
    }

    /// <summary>Lanterna: cai com Fragmentos na bolsa, volta à Praça sem eles, entra de novo e recupera.</summary>
    private async Task DieAndRecoverLantern()
    {
        RiftSession.Cheat = false;
        var save = RiftSession.Save;
        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "sala para a queda");
        Bind();
        RiftSession.Rules.AddFragments(RiftSession.Bag, 5);
        var shards = save.MaterialCount(RiftSession.Rules.FragmentMaterial);
        World.Player.Teleport(World.ClampToFloor(World.SpawnPoint + (new Vector2(260f, 0f) * S)));
        await host.PhysicsFrames(2);
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta à Praça depois da queda");
        Check(save.LostLantern is not null, "a queda com Fragmentos deveria deixar a Lanterna na Brecha");
        Check(save.MaterialCount(RiftSession.Rules.FragmentMaterial) == shards, "os Fragmentos da caçada não deveriam entrar no save na queda");
        Check(!World.Player.Combat.IsDead && World.Player.Combat.Hp > 0, "o caçador deveria acordar vivo na Praça");

        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "sala da Lanterna");
        Bind();
        var lantern = World.Entities.GetChildren().OfType<LanternPickup>().FirstOrDefault();
        Check(lantern is not null, $"a Lanterna deveria estar na sala onde o caçador caiu (sala {World.Data.Id}, " +
            $"lanterna em {save.LostLantern?.Room ?? "nenhuma"} {save.LostLantern?.X:0},{save.LostLantern?.Y:0}, caçador em {World.Player.GlobalPosition.Round()})");
        if (lantern is not null)
        {
            World.Player.Teleport(lantern.GlobalPosition);
            await host.PhysicsFrames(6);
            Check(save.LostLantern is null && RiftSession.Fragments >= 5, "encostar na Lanterna deveria devolver os Fragmentos à bolsa");
        }

        RiftSession.Cheat = true;
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta final");
        save.LostLantern = null;
        World.Player.Combat.RefreshStats(fullHeal: true);
        Log.Info("bot de combate: Lanterna deixada na queda e recuperada na volta");
    }

    private void Bind()
    {
        World.Player.Input = _input;
        _input.Move = Vector2.Zero;
    }

    private async Task WaitUntil(Func<bool> condition, int frames, string what)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            await host.PhysicsFrames(1);
        }

        Check(condition(), $"bot de combate: tempo esgotado esperando {what}");
    }

    private void Check(bool ok, string failure)
    {
        if (!ok)
        {
            _failures.Add(failure);
        }
    }
}
