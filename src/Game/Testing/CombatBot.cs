using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Story;
using Vespera.Core.Advancement;
using Vespera.Game.Hunter;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.UI;
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

    /// <summary>Ranks celebrados durante a caçada (a tela de rank abre sozinha depois do Guardião).</summary>
    public List<Vespera.Core.Ranks.Rank> RankUps { get; } = [];

    private LevelWorld World => level.World;

    private float S => World.Data.ActorScale;

    public async Task<List<string>> Run(HunterClass hunter)
    {
        var save = RiftSession.Save;
        var shardsBefore = RiftSession.WalletFragments;
        var guardiansBefore = Progression.State.GuardiansDefeated.Count;
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
        var gained = RiftSession.WalletFragments - shardsBefore;
        Check(!RiftSession.InRift && !Progression.Engine.InRun, "depois do portal de saída a caçada deveria ter acabado");
        Check(save.BrechasClosed == closedBefore + 1, $"Brecha deveria contar como fechada ({save.BrechasClosed})");
        Check(gained > 0, $"a carteira deveria receber Fragmentos (ganhou {gained})");
        Check(Progression.State.GuardiansDefeated.Contains(RiftSession.Brecha.GuardianId), "a progressão deveria registrar a Lodo-Mãe");
        Check(Progression.State.GuardiansDefeated.Count >= guardiansBefore, "conquistas não podem sumir");
        Check(RankUps.Contains(Vespera.Core.Ranks.Rank.D) || Progression.Rank > Vespera.Core.Ranks.Rank.E, $"a primeira caçada completa deveria subir E -> D (XP {Progression.State.Xp}, rank {Progression.Rank})");
        Log.Info($"bot de combate: rank {Progression.Rank} com {Progression.State.Xp} XP; celebrações: [{string.Join(",", RankUps)}]");
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
        if (!StoryService.Engine.HasFlag(StoryFlags.StarterBlade))
        {
            // Sem a Lâmina da Odete a fenda não deixa entrar (o bot de combate testa a Brecha isolada: arma o caçador).
            _input.Tap(InputActions.Interact);
            await host.PhysicsFrames(4);
            Check(!level.PickerOpen && level.Hud.PanelOpen, "sem a Lâmina da Odete o portal deveria recusar com um aviso");
            _input.Tap(InputActions.Interact);
            await host.PhysicsFrames(4);
            StoryService.SetFlag(StoryFlags.StarterBlade);
        }

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

        await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 400, "celebração de rank fechada");
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

    /// <summary>
    /// Lanterna (regra da progressão): cai com Fragmentos da caçada, volta à Praça sem eles (ficam na Lanterna do bairro),
    /// entra de novo e eles voltam à caçada logo na entrada; sair vivo leva tudo para a carteira.
    /// </summary>
    private async Task DieAndRecoverLantern()
    {
        RiftSession.Cheat = false;
        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "sala para a queda");
        Bind();
        Progression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        var held = RiftSession.Fragments;
        var wallet = RiftSession.WalletFragments;
        Check(held > 0, "o saque dentro da Brecha deveria ficar na caçada");
        World.Player.Teleport(World.ClampToFloor(World.SpawnPoint + (new Vector2(260f, 0f) * S)));
        await host.PhysicsFrames(2);
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta à Praça depois da queda");
        Check(RiftSession.LanternFragments == held, $"a queda deveria deixar {held} Fragmentos na Lanterna (tem {RiftSession.LanternFragments})");
        Check(RiftSession.WalletFragments == wallet, "os Fragmentos da caçada não deveriam entrar na carteira na queda");
        Check(!World.Player.Combat.IsDead && World.Player.Combat.Hp > 0, "o caçador deveria acordar vivo na Praça");

        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "reentrada na Brecha");
        Bind();
        Check(RiftSession.LanternFragments == 0 && RiftSession.Fragments >= held, "entrar de novo deveria devolver os Fragmentos da Lanterna à caçada");
        Check(level.CombatHud.LastToast.Contains("Lanterna recuperada", System.StringComparison.Ordinal), $"a volta da Lanterna deveria ser avisada (último aviso: '{level.CombatHud.LastToast}')");

        RiftSession.Cheat = true;
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta final");
        World.Player.Combat.RefreshStats(fullHeal: true);
        Log.Info($"bot de combate: {held} Fragmentos ficaram na Lanterna na queda e voltaram na reentrada");
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
            if (RankUpScreen.Active is { Revealed: true } celebration)
            {
                // O Guardião sobe o rank (E -> D): a celebração pausa o jogo; o bot a registra e fecha.
                RankUps.Add(celebration.Change.To);
                celebration.Close();
            }

            await host.PhysicsFrames(1);
        }

        Check(condition(), $"bot de combate: tempo esgotado esperando {what} (sala {World.Data.Id}, trocando {level.Switching}, " +
            $"na Brecha {RiftSession.InRift}, pausado {host.GetTree().Paused}, celebração {RankUpScreen.Active is not null}, " +
            $"tela {SceneRouter.Instance.Current?.Name}, foco '{World.Player.Focus?.Kind}', congelado {World.Player.Frozen}, estado {World.Player.Combat.State}, golpe ocupado {World.Player.Combat.Moves.Busy}, anim {World.Player.CurrentAnimation})");
    }

    private void Check(bool ok, string failure)
    {
        if (!ok)
        {
            _failures.Add(failure);
        }
    }
}
