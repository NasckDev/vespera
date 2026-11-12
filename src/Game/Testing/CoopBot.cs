using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Hunt;
using Vespera.Game.Coop;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot do co-op local: duas fontes de input simuladas (uma por caçador, cada uma passando pelo comando do jogador) na
/// Praça e na Brecha E. Confere a câmera compartilhada (caçador ≥ 85 px, coleira), a conversa puxada pelo J2, a escala
/// dos monstros (vida ×1,5, +1 por onda, Guardião ×1,5), a queda e a reanimação (segurar 2 s), a Lodo-Mãe, o saque
/// individual e o XP compartilhado, o convidado gravado no save e a queda do grupo inteiro (Lanterna de cada um).
/// Caçadores invulneráveis e dano ×10 (modo do bot de combate) para caber no teste; a queda é forçada.
/// </summary>
public sealed partial class CoopBot(Level level, SmokeTest host)
{
    private const int RoomFrameLimit = 60 * 150;
    private const int StuckFrames = 75;
    private const float AttackReach = 48f;
    private const float MinHunterPx = 85f;

    private readonly List<string> _failures = [];
    private readonly ScriptedInput[] _inputs = [new(), new(1)];
    private readonly HashSet<string> _shot = [];
    private readonly HashSet<int> _lootOwnersSeen = [];
    private bool _revived;
    private int _bossFrame = -1;

    public bool Shots { get; init; }

    private LevelWorld World => level.World;

    private Player P1 => World.Player;

    private Player P2 => World.PlayerAt(1) ?? throw new InvalidOperationException("sem J2 na sala");

    /// <summary>Fonte crua de cada jogador (ligada em <see cref="CoopSession.SourceOverride"/>).</summary>
    public IInputSource SourceFor(CoopMember member) => _inputs[Math.Clamp(member.Slot, 0, 1)];

    public async Task<List<string>> Run()
    {
        await CheckPraca();
        await GuestTalks();
        StoryService.SetFlag(StoryFlags.StarterBlade);

        // Caçada com a Lodo-Mãe no fim (portão da história aberto) e queda do grupo de volta à cidade, não ao quarto.
        StoryService.SetFlag(StoryFlags.GuardianGate);
        var wakeInRoom = Level.WakeInRoom;
        Level.WakeInRoom = false;
        RiftSession.Cheat = true;
        var p1Xp = Progression.State.Xp;
        var p2Xp = PartyProgression.Guest.Progress.Xp;
        var p1Wallet = Progression.State.Fragmentos;
        var p2Wallet = PartyProgression.Guest.Progress.Fragmentos;
        await EnterTogether();
        await PlayHunt();
        await WaitUntil(() => !level.Switching && World.Data.Id == HubOf() && !RiftSession.InRift && RunResultScreen.Active is null, 900, "volta à cidade");
        CheckRewards(p1Xp, p2Xp, p1Wallet, p2Wallet);
        await ShotOnce("hud_praca_depois", 0.8);
        if (_failures.Count == 0)
        {
            await WipeAndLantern();
        }

        RiftSession.Cheat = false;
        Level.WakeInRoom = wakeInRoom;
        Release();
        return _failures;
    }

    /// <summary>Bairro para onde a caçada volta (a Praça nas Brechas da cidade velha).</summary>
    private static string HubOf() => RiftSession.Brecha.Hub.Length > 0 ? RiftSession.Brecha.Hub : Level.HubLevelId;

    /// <summary>As salas sorteadas da caçada (ver HuntService), uma a uma, até a volta.</summary>
    private async Task PlayHunt()
    {
        while (RiftSession.InRift && _failures.Count == 0)
        {
            var index = RiftSession.RoomIndex;
            await WaitUntil(() => !level.Switching && World.Director is not null && World.Data.Id == RiftSession.CurrentRoom, 900, $"sala {index + 1}");
            if (World.Director is not { } director)
            {
                return;
            }

            await ClearRoom(director, index);
            await WaitUntil(() => !host.GetTree().Paused && BlessingScreen.Active is null, 1200, "bênção escolhida ou celebração fechada");
            await CollectAndLeave(director);
            await WaitUntil(() => level.Switching || !RiftSession.InRift || RiftSession.RoomIndex != index, 600, $"saída da sala {index + 1}");
            Log.Info($"co-op: sala {index + 1}/{RiftSession.RoomCount} ({director.Kind}) limpa");
        }
    }

    private async Task CheckPraca()
    {
        Release();
        P1.Teleport(World.SpawnPoint);
        P2.Teleport(World.BesidePoint(World.SpawnPoint, 1));
        World.Camera.Snap();
        await host.PhysicsFrames(10);
        Check(P2.GlobalPosition.DistanceTo(P1.GlobalPosition) < 1.6f * World.Data.HumanHeight, "o J2 deveria nascer ao lado do J1");
        Check(P1.Flash.Outline > 0f && P2.Flash.Outline > 0f && P1.Flash.OutlineColor != P2.Flash.OutlineColor, "cada caçador deveria ter o contorno da cor dele");
        Check(P2.Flash.Cape == 0f, "classes diferentes não trocam a cor da capa");
        Check((P1.CollisionMask & Player.PlayerLayer) == 0 && (P2.CollisionMask & Player.PlayerLayer) == 0, "caçadores não colidem entre si");
        ExpectOnScreen("Praça, lado a lado");
        await ShotOnce("praca_dois", 0.8);

        // Juntos no mesmo ponto: o empurrão leve separa os dois.
        P2.Teleport(P1.GlobalPosition + new Vector2(2f, 0f));
        await host.PhysicsFrames(30);
        Check(P2.GlobalPosition.DistanceTo(P1.GlobalPosition) > 8f * World.Data.ActorScale, "caçadores sobrepostos deveriam se afastar um pouco");

        await Tether();
    }

    /// <summary>O J2 puxa conversa no mural: a caixa diz quem conversa e o botão do J2 avança a fala.</summary>
    private async Task GuestTalks()
    {
        var board = World.Props.First(p => p.Type == "mural");
        P2.Teleport(board.GlobalPosition + new Vector2(0, 50 * World.Data.ActorScale));
        P1.Teleport(World.ClampToFloor(board.GlobalPosition + new Vector2(-1.4f * World.Data.HumanHeight, 1.4f * World.Data.HumanHeight)));
        World.Camera.Snap();
        await host.PhysicsFrames(6);
        Check(P2.Focus?.Kind == "board", $"perto do mural o foco do J2 deveria ser o mural, foi '{P2.Focus?.Kind}'");
        Check(level.Hud.PromptText.StartsWith("J2", StringComparison.Ordinal), $"o aviso deveria ser do J2, foi '{level.Hud.PromptText}'");
        await ShotOnce("aviso_j2", 0.3);
        _inputs[1].Tap(InputActions.Interact);
        await host.PhysicsFrames(4);
        Check(level.DialogueOpen && level.Dialogue.InitiatorText.StartsWith("J2", StringComparison.Ordinal),
            $"o J2 deveria abrir a conversa e a caixa mostrar quem conversa (aberta {level.DialogueOpen}, '{level.Dialogue.InitiatorText}')");
        Check(P1.Frozen && P2.Frozen, "durante a conversa os dois caçadores ficam parados");
        level.Dialogue.Complete();
        await ShotOnce("conversa_j2", 0.4);
        for (var i = 0; i < 40 && level.DialogueOpen; i++)
        {
            _inputs[1].Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
        }

        Check(!level.DialogueOpen, "o botão do J2 deveria avançar e fechar a conversa");
        await host.PhysicsFrames(30);

        // Na v2 o mural é o quadro de contratos: a conversa termina abrindo a tela deles (que pausa o jogo).
        if (ContractsScreen.Active is { } contracts)
        {
            await ShotOnce("contratos", 0.2);
            contracts.Close();
            await host.PhysicsFrames(10);
        }

        Check(!P1.Frozen && !P2.Frozen, "ao fechar a conversa os dois voltam a andar");
    }

    private async Task EnterTogether()
    {
        var portal = World.Zones.First(z => z.Kind == "brecha");
        P1.Teleport(portal.GlobalPosition + new Vector2(0, 40f * World.Data.ActorScale));
        P2.Teleport(World.BesidePoint(P1.GlobalPosition, 1));
        await host.PhysicsFrames(4);
        for (var attempt = 0; attempt < 4 && !RiftSession.InRift && !level.Switching; attempt++)
        {
            // Como um jogador: se o aperto só fechou um aviso, aperta de novo.
            Log.Info($"co-op: J1 interage no portal (tentativa {attempt + 1}); foco '{P1.Focus?.Kind}', painel {level.Hud.PanelOpen}, " +
                $"conversa {level.DialogueOpen}, cena {level.SceneBusy}, congelado {P1.Frozen}");
            _inputs[0].Tap(InputActions.Interact);
            await host.PhysicsFrames(20);
        }

        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "primeira sala em grupo");
        Check(World.Players.Count == 2, $"os dois deveriam entrar na Brecha ({World.Players.Count})");
        Check(level.Players.Count == 2 && P2.Hunter == HunterClass.Arco && P1.Hunter == RiftSession.Hunter, "cada um entra com a classe que escolheu");
    }

    private async Task ClearRoom(RoomDirector director, int index)
    {
        var room = World.Data.Id;
        var frames = 0;
        var stuck = new int[2];
        var last = new[] { float.MaxValue, float.MaxValue };
        var scaleChecked = false;
        while (!director.Cleared && frames++ < RoomFrameLimit)
        {
            if (!scaleChecked && (director.LastWaveSize > 0 || director.Boss is not null))
            {
                scaleChecked = true;
                CheckScaling(director);
            }

            if (!_revived && index >= 1 && director.Alive.Any() && frames > 60)
            {
                await DownAndRevive();
            }

            for (var slot = 0; slot < 2; slot++)
            {
                Drive(slot, director, frames, stuck, last);
            }

            NoteLoot();
            await ShotMoments(index, frames, director);
            await host.PhysicsFrames(1);
        }

        Release();
        Check(director.Cleared, $"sala {room} não ficou limpa em {RoomFrameLimit / 60} s");
    }

    private void Drive(int slot, RoomDirector director, int frames, int[] stuck, float[] last)
    {
        var player = World.PlayerAt(slot);
        var input = _inputs[slot];
        if (player is null || player.Combat.IsDead)
        {
            input.Move = Vector2.Zero;
            return;
        }

        var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(player.GlobalPosition)).FirstOrDefault();
        if (target is null)
        {
            input.Move = Vector2.Zero;
            return;
        }

        var s = World.Data.ActorScale;
        var to = target.GlobalPosition - player.GlobalPosition;
        var distance = to.Length();
        stuck[slot] = distance < last[slot] - 0.5f || distance < AttackReach * s ? 0 : stuck[slot] + 1;
        last[slot] = distance;
        if (stuck[slot] > StuckFrames)
        {
            player.Teleport(World.ClampToFloor(target.GlobalPosition + (Vector2.Down * 30f * s) + (Vector2.Right * (slot == 0 ? -20f : 20f) * s)));
            stuck[slot] = 0;
        }

        // O Arco ataca de longe; a Lâmina/Martelo chegam perto.
        var reach = player.Hunter == HunterClass.Arco ? 3.2f * World.Data.HumanHeight : AttackReach * s * 0.8f;
        input.Move = distance > reach ? to.Normalized() : Vector2.Zero;
        if (distance <= reach * 1.6f + (target.HitRadius * 1.6f) && (frames + (slot * 4)) % 8 == 0)
        {
            if (distance <= reach)
            {
                player.Face(to);
            }

            input.Tap(frames % 96 == 0 ? InputActions.Heavy : InputActions.Attack);
        }
    }

    /// <summary>J2 cai no meio da luta (forçado), fica caído sem a caçada acabar, e o J1 segura interagir por 2 s ao lado dele.</summary>
    private async Task DownAndRevive()
    {
        _revived = true;
        CheckEnemyHp();
        var p2 = P2;
        p2.Combat.Digest(99999);
        await host.PhysicsFrames(20);
        Check(p2.Combat.IsDowned, $"no co-op o J2 deveria ficar caído, está {p2.Combat.State}");
        Check(RiftSession.InRift && !level.Switching, "um caído não termina a caçada");
        Check(World.Director!.Alive.All(e => World.NearestHunter(e.GlobalPosition) == P1), "os monstros deveriam mirar só o J1 de pé");
        await ShotOnce("caido", 0.3);

        // J1 vai até ele e segura interagir.
        _inputs[0].Move = Vector2.Zero;
        P1.Teleport(World.ClampToFloor(p2.GlobalPosition + new Vector2(0.5f * World.Data.HumanHeight, 0.1f * World.Data.HumanHeight)));
        await host.PhysicsFrames(2);
        _inputs[0].Hold(InputActions.Interact, true);
        await host.PhysicsFrames(60);
        Check(World.Party.ReviveFraction(p2) is > 0.35f and < 0.75f, $"depois de 1 s segurando, o anel deveria estar na metade ({World.Party.ReviveFraction(p2):0.00})");
        await ShotOnce("reanimando", 0);
        await host.PhysicsFrames(75);
        _inputs[0].Hold(InputActions.Interact, false);
        Check(!p2.Combat.IsDead && p2.Combat.Hp == CoopSession.Rules.RevivedHp(p2.Combat.MaxHp),
            $"segurar 2 s deveria reanimar o J2 com {CoopSession.Rules.RevivedHp(p2.Combat.MaxHp)} de vida (estado {p2.Combat.State}, vida {p2.Combat.Hp})");
        Log.Info($"co-op: J2 caiu e foi reanimado pelo J1 com {p2.Combat.Hp}/{p2.Combat.MaxHp}");
        await ShotOnce("reanimado", 0.3);
    }

    private async Task CollectAndLeave(RoomDirector director)
    {
        var s = World.Data.ActorScale;
        Log.Info($"co-op: saindo da sala {World.Data.Id} ({director.Kind}): baú {(director.Chest is null ? "não" : "sim")}, " +
            $"fenda {(director.ExitPortal is null ? "fechada" : "aberta")}, fenda no dado {World.Data.Rift?.ExitPortal is not null}, passagem {World.Data.Rift?.Exit is not null}");
        if (director.Chest is { } chest)
        {
            P1.Teleport(World.ClampToFloor(chest.GlobalPosition + (Vector2.Down * 40f * s)));
            P2.Teleport(World.BesidePoint(P1.GlobalPosition, 1));
            await host.PhysicsFrames(4);
            for (var attempt = 0; attempt < 5 && !chest.IsOpen; attempt++)
            {
                // Como um jogador: se o aperto só fechou um aviso, aperta de novo.
                Log.Info($"co-op: J1 abre o baú (tentativa {attempt + 1}); foco '{P1.Focus?.Kind}', painel {level.Hud.PanelOpen}");
                _inputs[0].Tap(InputActions.Interact);
                await host.PhysicsFrames(10);
            }

            Check(chest.IsOpen, "o J1 deveria abrir o baú");
            await ShotOnce("bau_saque", 0.25);
        }

        for (var i = 0; i < 90; i++)
        {
            NoteLoot();
            await host.PhysicsFrames(1);
        }

        if (director.ExitPortal is null && World.Data.Rift?.ExitPortal is not null)
        {
            await WaitUntil(() => director.ExitPortal is not null, 400, "fenda de saída");
        }

        await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 400, "celebração de rank fechada");
        if (director.ExitPortal is { } portal)
        {
            P1.Teleport(World.ClampToFloor(portal.GlobalPosition + (Vector2.Down * 40f * s)));
            await host.PhysicsFrames(6);
            _inputs[0].Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
            return;
        }

        if (World.Data.Rift?.Exit is { } exit)
        {
            // Basta o J2 chegar na passagem para levar o grupo (o J1 vem junto até perto, como numa caminhada de verdade).
            var t = exit.Trigger;
            var gate = new Vector2(t[0] + (t[2] / 2f), t[1] + (t[3] / 2f));
            P1.Teleport(World.ClampToFloor(gate + new Vector2(-1.2f * World.Data.HumanHeight, 0f)));
            P2.Teleport(gate);
            await host.PhysicsFrames(4);
        }
    }

    private async Task ShotMoments(int index, int frames, RoomDirector director)
    {
        if (!Shots)
        {
            return;
        }

        if (index == 0 && frames == 100)
        {
            await ShotOnce("corredor_luta", 0);
        }

        if (director.Boss is { } boss && _bossFrame < 0 && boss.Engaged)
        {
            _bossFrame = frames;
        }

        if (_bossFrame >= 0 && frames == _bossFrame + 40)
        {
            await ShotOnce("chefe_dois", 0);
        }

        if (_bossFrame >= 0 && frames == _bossFrame + 160)
        {
            await ShotOnce("chefe_dois_b", 0);
        }
    }

    private void NoteLoot()
    {
        foreach (var node in World.Entities.GetChildren())
        {
            if (node is FragmentPickup pickup)
            {
                _lootOwnersSeen.Add(pickup.OwnerSlot);
            }
        }
    }

    private async Task ShotOnce(string name, double settle)
    {
        if (!Shots || !_shot.Add(name))
        {
            return;
        }

        if (settle > 0)
        {
            await host.PhysicsFrames((int)(settle * 60));
        }

        await host.CaptureNow($"coop_{name}");
    }

    private void Release()
    {
        foreach (var input in _inputs)
        {
            input.Move = Vector2.Zero;
            input.Hold(InputActions.Interact, false);
        }
    }

    private async Task WaitUntil(Func<bool> condition, int frames, string what)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            if (BlessingScreen.Active is { } blessing)
            {
                // Bênção da Lanterna (vale para o grupo): o bot fica com a primeira carta.
                Log.Info($"co-op: bênção escolhida '{blessing.Offer[0].Name}'");
                blessing.Choose(0);
            }

            if (RunResultScreen.Active is { } results)
            {
                await ShotOnce("resultado", 0.2);
                results.Close();
            }

            if (RankUpScreen.Active is { Revealed: true } celebration)
            {
                await ShotOnce($"rank_{celebration.Change.To}", 0);
                celebration.Close();
            }

            await host.PhysicsFrames(1);
        }

        Check(condition(), $"bot do co-op: tempo esgotado esperando {what} (sala {World.Data.Id}, trocando {level.Switching}, na Brecha {RiftSession.InRift}, " +
            $"pausado {host.GetTree().Paused}, caçadores {World.Players.Count}, estados [{string.Join(",", World.Players.Select(p => p.Combat.State))}], " +
            $"telas [{string.Join(",", host.GetTree().Root.FindChild("OverlayRoot", true, false)?.GetChildren().Select(n => n.GetType().Name) ?? [])}])");
    }

    private void Check(bool ok, string failure)
    {
        if (!ok)
        {
            _failures.Add(failure);
        }
    }
}
