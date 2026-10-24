using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>O que uma caçada jogada pelo bot mediu (relatório de QA: tempo, vida perdida, quedas).</summary>
public sealed record HuntMetrics(int Rooms, int RoomCount, int Kills, int DamageTaken, int HitsTaken, int Dodges, int Potions,
    bool Died, bool Completed, bool Guardian, double Seconds, int HpStart, int HpEnd, int MaxHp);

/// <summary>
/// Caçada jogada "como gente" (sem invulnerabilidade quando <see cref="Invulnerable"/> é falso): anda até o monstro mais
/// perto (o Arco mantém distância), bate, às vezes usa o golpe forte, esquiva de parte dos avisos (<see cref="DodgeSkill"/>),
/// bebe poção com pouca vida, descansa na lanterna, quebra barris, abre o baú e sai. Mede tudo em <see cref="HuntMetrics"/>.
/// </summary>
public sealed partial class CombatBot
{
    private const float BowKeepHumans = 3.2f;
    private const float DodgeRadiusHumans = 2.4f;
    private const float PotionAt = 0.4f;

    private readonly Dictionary<ulong, bool> _wasTelegraph = [];
    private readonly Random _skillRoll = new(23);
    private int _dodges;
    private int _potionsDrunk;
    private int _potionCooldown;
    private int _kills;

    /// <summary>Chance de esquivar de um aviso (0 = nunca, 1 = sempre). Um jogador novo razoável fica perto de 0,5.</summary>
    public double DodgeSkill { get; init; } = 0.5;

    /// <summary>Caçada (a partir da Praça, sem trapaça): entra pelo portal, joga todas as salas e fecha a tela de resultado.</summary>
    public async Task<HuntMetrics> PlayHunt(bool invulnerable)
    {
        RiftSession.Cheat = invulnerable;
        await OpenPortalAndEnter(null);
        var metrics = await PlayRooms();
        RiftSession.Cheat = false;
        return metrics;
    }

    /// <summary>Joga as salas até voltar à Praça (fim da caçada ou queda) e fecha a tela de resultado.</summary>
    private async Task<HuntMetrics> PlayRooms()
    {
        _dodges = _potionsDrunk = _kills = 0;
        var hpStart = World.Player.Combat.Hp;
        var maxHp = World.Player.Combat.MaxHp;
        var guardian = HuntService.Plan?.HasGuardian == true;
        var roomCount = RiftSession.RoomCount;
        var rooms = 0;
        var died = false;
        while (RiftSession.InRift && _failures.Count == 0)
        {
            var index = RiftSession.RoomIndex;
            await WaitUntil(() => !level.Switching && World.Director is not null && World.Data.Id == RiftSession.CurrentRoom, 900, $"sala {index + 1}");
            Bind();
            var director = World.Director!;
            await ClearRoom(director, index);
            if (World.Player.Combat.IsDead)
            {
                died = true;
                break;
            }

            rooms++;
            await WaitUntil(() => !host.GetTree().Paused && BlessingScreen.Active is null, 1200, "bênção escolhida ou celebração fechada");
            var hpLeaving = World.Player.Combat.Hp;
            await CollectAndLeave(director);
            await WaitUntil(() => level.Switching || !RiftSession.InRift || RiftSession.RoomIndex != index, 600, $"saída da sala {index + 1}");
            Log.Info($"bot: sala {index + 1}/{roomCount} ({director.Kind}) limpa; vida {hpLeaving}/{maxHp}");
        }

        var report = HuntService.Report;
        var hpEnd = World.Player.Combat.Hp;
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId, 900, "volta à Praça");
        await CloseResults();
        var m = new HuntMetrics(rooms, roomCount, report?.Kills ?? _kills, report?.DamageTaken ?? 0, report?.HitsTaken ?? 0, _dodges,
            _potionsDrunk, died, report?.Completed == true, guardian, report?.Seconds ?? 0, hpStart, died ? 0 : hpEnd, maxHp);
        Log.Info($"bot caçada: {(died ? "CAIU" : "voltou")} | salas {m.Rooms}/{m.RoomCount} | {m.Kills} mortes | vida perdida {m.DamageTaken} " +
            $"({m.HitsTaken} golpes; vida máx {m.MaxHp}) | esquivas {m.Dodges} | poções {m.Potions} | {m.Seconds:0} s de jogo" +
            $"{(guardian ? " | com a Lodo-Mãe" : string.Empty)}");
        return m;
    }

    private async Task CloseResults()
    {
        await WaitUntil(() => RunResultScreen.Active is not null || HuntService.Report is null, 240, "tela de resultado");
        if (RunResultScreen.Active is { } results)
        {
            await host.PhysicsFrames(10);
            await ShotOnce("resultado");
            results.Close();
            await host.PhysicsFrames(4);
        }
    }

    private async Task ClearRoom(RoomDirector director, int index)
    {
        var frames = 0;
        var stuck = 0;
        var lastDistance = float.MaxValue;
        var human = World.Data.HumanHeight;
        var bow = World.Player.Hunter == HunterClass.Arco;
        var room = World.Data.Id;
        while (!director.Cleared && frames++ < RoomFrameLimit && !World.Player.Combat.IsDead)
        {
            if (RankUpScreen.Active is not null || host.GetTree().Paused)
            {
                await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 600, "pausa durante a luta");
            }

            var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(World.Player.GlobalPosition)).FirstOrDefault();
            if (target is null)
            {
                _input.Move = Vector2.Zero;
                await host.PhysicsFrames(1);
                continue;
            }

            if (TryDodge() || TryPotion())
            {
                await host.PhysicsFrames(1);
                continue;
            }

            var to = target.GlobalPosition - World.Player.GlobalPosition;
            var distance = to.Length();
            var keep = bow ? BowKeepHumans * human : AttackReach * S * 0.8f;
            stuck = distance < lastDistance - 0.5f || distance < keep * 1.2f ? 0 : stuck + 1;
            lastDistance = distance;
            if (stuck > StuckFrames * 2)
            {
                World.Player.Teleport(World.ClampToFloor(target.GlobalPosition + (Vector2.Down * 30f * S)));
                stuck = 0;
            }

            var inReach = bow ? distance < 6f * human : distance < (AttackReach + target.HitRadius) * S * 1.6f;
            _input.Move = distance > keep ? to.Normalized() : bow && distance < keep * 0.7f ? -to.Normalized() : Vector2.Zero;
            if (inReach && frames % 8 == 0)
            {
                // Mira: um quadro de "andar" para o alvo antes do golpe (a mira segue o movimento).
                _input.Move = to.Normalized() * 0.3f;
                _input.Tap(frames % 96 == 0 ? InputActions.Heavy : InputActions.Attack);
            }

            await ShotMoments(room, frames);
            await host.PhysicsFrames(1);
        }

        _input.Move = Vector2.Zero;
        if (!World.Player.Combat.IsDead)
        {
            Check(director.Cleared, $"sala {index + 1} ({room}) não ficou limpa em {RoomFrameLimit / 60} s");
        }
    }

    /// <summary>Esquiva de um aviso novo de monstro perto (com a chance da habilidade do bot).</summary>
    private bool TryDodge()
    {
        var player = World.Player;
        var near = DodgeRadiusHumans * World.Data.HumanHeight;
        foreach (var enemy in World.Enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy))
            {
                continue;
            }

            var id = enemy.GetInstanceId();
            var telegraph = enemy.State == EnemyState.Telegraph;
            var fresh = telegraph && !_wasTelegraph.GetValueOrDefault(id);
            _wasTelegraph[id] = telegraph;
            if (!fresh || enemy.GlobalPosition.DistanceTo(player.GlobalPosition) > near * (enemy is LodoMae ? 2.2f : 1f))
            {
                continue;
            }

            if (_skillRoll.NextDouble() > DodgeSkill)
            {
                return false;
            }

            var away = player.GlobalPosition - enemy.GlobalPosition;
            _input.Move = away.LengthSquared() < 1f ? Vector2.Left : away.Normalized();
            _input.Tap(InputActions.Dodge);
            _dodges++;
            return true;
        }

        return false;
    }

    private bool TryPotion()
    {
        var c = World.Player.Combat;
        if (c.Hp > c.MaxHp * PotionAt || HuntService.State.Potions <= 0)
        {
            return false;
        }

        if (_potionCooldown > 0)
        {
            _potionCooldown--;
            return false;
        }

        _input.Tap(InputActions.Potion);
        _potionsDrunk++;
        _potionCooldown = 45;
        return true;
    }

    /// <summary>Interagir só vale com o caçador livre (sem golpe em curso): espera e aperta.</summary>
    private async Task TapInteract()
    {
        _input.Move = Vector2.Zero;
        for (var i = 0; i < 90 && (World.Player.Combat.State != Combat.HunterState.Free || World.Player.Combat.Moves.Busy); i++)
        {
            await host.PhysicsFrames(1);
        }

        _input.Tap(InputActions.Interact);
    }

    /// <summary>Descansa na lanterna (se houver) e quebra os barris da sala (Lumes).</summary>
    private async Task RestAndBreak(RoomDirector director)
    {
        if (director.RestAvailable && World.Entities.GetNodeOrNull<Node2D>("Rest") is { } rest)
        {
            World.Player.Teleport(World.ClampToFloor(rest.GlobalPosition + (Vector2.Down * 30f * S)));
            await host.PhysicsFrames(6);
            await TapInteract();
            await host.PhysicsFrames(6);
            Check(!director.RestAvailable, "interagir com a lanterna de descanso deveria curar");
        }

        foreach (var breakable in World.Breakables.Where(b => GodotObject.IsInstanceValid(b) && !b.Broken).ToList())
        {
            var side = breakable.GlobalPosition + (Vector2.Down * 40f * S);
            World.Player.Teleport(World.ClampToFloor(side));
            await host.PhysicsFrames(3);
            _input.Move = Vector2.Up * 0.3f;
            _input.Tap(InputActions.Attack);
            await host.PhysicsFrames(30);
            _input.Move = Vector2.Zero;
        }
    }
}
