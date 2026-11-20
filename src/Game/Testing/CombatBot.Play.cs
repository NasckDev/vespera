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
    private int _ultimates;
    private bool _ultimateWasActive;

    /// <summary>Golpes Supremos que dispararam de verdade durante as caçadas deste bot.</summary>
    public int UltimatesCast => _ultimates;

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

    /// <summary>Só entra pelo portal da Praça até a primeira sala (o smoke da queda derruba o caçador ali).</summary>
    internal Task EnterHunt() => OpenPortalAndEnter(null);

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

            if (!director.Cleared)
            {
                // Sala presa (já registrada como falha): parar aqui, sem seguir para a coleta e a saída (evita falhas em cascata).
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
        await WaitUntil(() => !level.Switching && World.Data.Id == RiftSession.Brecha.Hub, 900, "volta ao bairro da Brecha");
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
        var idle = 0;
        var limit = director.Guardian is null ? RoomFrameLimit : GuardianRoomFrameLimit;
        var duel = director.Guardian is null ? null : DuelGear.Equip(World.Player.Combat);
        while (!director.Cleared && frames++ < limit && !World.Player.Combat.IsDead)
        {
            if (RankUpScreen.Active is not null || host.GetTree().Paused)
            {
                await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 600, "pausa durante a luta");
            }

            var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(World.Player.GlobalPosition)).FirstOrDefault();
            if (target is null)
            {
                // Sem alvo e a sala não limpa: registra o que a sala ainda espera (a cada 5 s).
                if (++idle % 300 == 0)
                {
                    Log.Info($"bot: sala {index + 1} ({room}) sem alvo há {idle / 60} s; {director.Describe()}");
                }

                _input.Move = Vector2.Zero;
                await host.PhysicsFrames(1);
                continue;
            }

            idle = 0;
            if (frames % ProgressLogFrames == 0)
            {
                Log.Info($"bot: sala {index + 1} ({room}) há {frames / 60} s; {director.Describe()}{DescribeVelora(director)}");
            }

            if (TryDodge() || TryPotion())
            {
                await host.PhysicsFrames(1);
                continue;
            }

            var aim = target.GlobalPosition;
            var aimRadius = target.HitRadius;
            // Velória escondida (fase 3): apaga as velas acesas, a mais perto primeiro, até sobrar a que não treme.
            if (target is Velora { Hidden: true } velora && PickCandle(velora) is { } candle)
            {
                aim = candle.GlobalPosition;
                aimRadius = candle.HitRadius;
            }

            var to = aim - World.Player.GlobalPosition;
            var distance = to.Length();
            var keep = bow ? BowKeepHumans * human : AttackReach * S * 0.8f;
            stuck = distance < lastDistance - 0.5f || distance < keep * 1.2f ? 0 : stuck + 1;
            lastDistance = distance;
            if (stuck > StuckFrames * 2)
            {
                World.Player.Teleport(World.ClampToFloor(aim + (Vector2.Down * 30f * S)));
                stuck = 0;
            }

            TrackAndUseUltimate();
            var inReach = bow ? distance < 6f * human : distance < (AttackReach + aimRadius) * S * 1.6f;
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
        duel?.Restore();
        if (!World.Player.Combat.IsDead)
        {
            if (!director.Cleared)
            {
                Log.Info($"bot: sala {index + 1} ({room}) presa; {director.Describe()}{DescribeVelora(director)}");
            }

            Check(director.Cleared, $"sala {index + 1} ({room}) não ficou limpa em {limit / 60} s");
        }
    }

    /// <summary>Golpe Supremo (issue #80): o bot o solta quando está pronto e conta quantas vezes ele de fato disparou (fora da medição de duelo).</summary>
    private void TrackAndUseUltimate()
    {
        var combat = World.Player.Combat;
        var active = combat.Moves.UltimateActive;
        if (active && !_ultimateWasActive)
        {
            _ultimates++;
        }

        _ultimateWasActive = active;
        if (!RiftSession.BossDuel && !active && combat.UltimateCooldownFraction <= 0f)
        {
            _input.Tap(InputActions.Supremo);
        }
    }

    /// <summary>
    /// Vela a apagar na fase 3 da Velória, como um jogador: as erradas tremem quando a lanterna passa perto, então vela que
    /// tremeu é descartada (não gasta golpe nem solta labareda) e a que não treme de perto é a Velória. Antes o bot apagava
    /// a mais próxima, errada ou não: cada ciclo de 8 s custava até quatro labaredas e o tempo da luta variava muito.
    /// </summary>
    private VeloraCandle? PickCandle(Velora velora)
    {
        var lit = velora.Candles.Where(c => GodotObject.IsInstanceValid(c) && c.Lit).ToList();
        foreach (var candle in lit.Where(c => c.Trembling))
        {
            _wrongCandles.Add(candle);
        }

        var origin = World.Player.GlobalPosition;
        return lit.Where(c => !_wrongCandles.Contains(c)).OrderBy(c => c.GlobalPosition.DistanceTo(origin)).FirstOrDefault()
            ?? lit.OrderBy(c => c.GlobalPosition.DistanceTo(origin)).FirstOrDefault();
    }

    /// <summary>Estado da Velória (fase, escondida, velas acesas) para o registro de progresso e de sala presa.</summary>
    private static string DescribeVelora(RoomDirector director) => director.Guardian is Velora v
        ? $"; Velória fase {v.Phase} escondida {v.Hidden} derretida {v.Melted} velas acesas {v.Candles.Count(c => GodotObject.IsInstanceValid(c) && c.Lit)}/{v.Candles.Count}"
        : string.Empty;

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
