using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.Net;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Co-op online com duas instâncias de verdade em 127.0.0.1 (<c>tools/online-bot.sh</c>):
/// <c>--online-bot-host</c> começa um jogo, hospeda pela pausa → Online → Hospedar, espera o convidado nascer, confere que
/// ele anda, leva o grupo à Brecha, espera um monstro morrer pelas mãos do convidado e para de hospedar (segue sozinho).
/// <c>--online-bot-join</c> entra pelo título → Online → Entrar (Martelo), anda, vê a troca de sala, mata um monstro
/// (some do snapshot) e volta ao título com o aviso quando o host encerra.
/// </summary>
public partial class SmokeTest
{
    public const string OnlineHostFlag = "--online-bot-host";
    public const string OnlineJoinFlag = "--online-bot-join";

    private const string OnlineGuestClass = "hammer";

    private bool IsOnlineBot => Array.IndexOf(_args, OnlineHostFlag) >= 0 || Array.IndexOf(_args, OnlineJoinFlag) >= 0;

    private async Task RunOnlineBot()
    {
        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy && FocusText() is not null, "título");
        if (Array.IndexOf(_args, OnlineHostFlag) >= 0)
        {
            await RunOnlineHost();
        }
        else
        {
            await RunOnlineGuest();
        }
    }

    // ---------------------------------------------------------------- host

    private async Task RunOnlineHost()
    {
        await OpenFromTitle("Nova história");
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is PrologueScreen && !_router.IsBusy, "cartelas");
        await Tap(Key.Space);
        await Seconds(0.6);
        await Tap(Key.Space);
        await WaitFor(() => _router.Current is Level && !_router.IsBusy, "jogo");
        var level = await LeaveRoad((Level)_router.Current!);
        await WaitFor(() => _router.Current is Level { Switching: false } && !GetTree().Paused, "Praça");
        level = (Level)_router.Current!;
        StoryService.SetFlag(StoryFlags.StarterBlade);
        StoryService.SetFlag(StoryFlags.GuardianGate);
        Level.WakeInRoom = false;
        RiftSession.Cheat = true;
        var lead = new ScriptedInput();
        CoopSession.SourceOverride = m => lead;

        // Pausa → Online → Hospedar: mostra IP local e porta.
        await Tap(Key.Escape);
        await FocusButton("Online");
        await Tap(Key.Enter);
        await WaitFor(() => FindOverlay<OnlineScreen>() is not null, "tela Online na pausa");
        await FocusButton("Hospedar");
        await Tap(Key.Enter);
        var screen = FindOverlay<OnlineScreen>()!;
        var port = CoopSession.Rules.Online.Port;
        Check(NetSession.Instance?.Role == NetRole.Host, "Hospedar deveria abrir a porta");
        Check(screen.HostInfo.Contains(port.ToString(), StringComparison.Ordinal) && screen.HostInfo.Contains("IP local", StringComparison.Ordinal),
            $"a tela deveria mostrar IP local e porta, mostrou '{screen.HostInfo}'");
        Log.Info($"online-host: {screen.HostInfo.Split('\n')[0]}");
        await Tap(Key.Escape);
        await Tap(Key.Escape);
        await WaitFor(() => _router.OverlayDepth == 0 && !GetTree().Paused, "jogo de volta depois de hospedar");
        if (_failures.Count > 0)
        {
            return;
        }

        // Convidado entra (a outra instância tenta conectar até o host abrir a porta).
        await WaitFrames(() => level.Players.Count == 2, 60 * 150, "o convidado nascer na Praça");
        await PhysicsFrames(10);
        var guest = level.World.PlayerAt(1);
        Check(guest is not null && guest.Hunter == HunterClass.Martelo, $"o J2 online deveria entrar com o Martelo ({guest?.Hunter})");
        Check(CoopSession.Online && level.CombatHud.GuestPanelVisible, "o grupo deveria ser online e a HUD mostrar o painel do J2");
        if (guest is null)
        {
            return;
        }

        var start = guest.GlobalPosition;
        await WaitFrames(() => IsInstanceValid(guest) && guest.GlobalPosition.DistanceTo(start) > 0.6f * level.World.Data.HumanHeight, 60 * 40, "o J2 andar pelos comandos da rede");
        Log.Info($"online-host: J2 andou {start} → {guest.GlobalPosition} ({NetSession.CommandsFor(1).Received} comandos recebidos)");

        await HostEntersRift(level, lead);
        await HostWaitsForKill(level);
        await HostStops(level);
        RiftSession.Cheat = false;
        CoopSession.SourceOverride = null;
    }

    private async Task HostEntersRift(Level level, ScriptedInput lead)
    {
        var world = level.World;
        var portal = world.Zones.First(z => z.Kind == "brecha");
        world.Player.Teleport(portal.GlobalPosition + new Vector2(0, 40f * world.Data.ActorScale));
        world.PlayerAt(1)?.Teleport(world.BesidePoint(world.Player.GlobalPosition, 1));
        await PhysicsFrames(4);
        for (var attempt = 0; attempt < 6 && !RiftSession.InRift && !level.Switching; attempt++)
        {
            lead.Tap(InputActions.Interact);
            await PhysicsFrames(20);
        }

        await WaitFrames(() => !level.Switching && level.World.Director is not null && level.World.Players.Count == 2, 60 * 20, "primeira sala da Brecha em grupo");
        Log.Info($"online-host: grupo na sala '{level.World.Data.Id}'");
    }

    private async Task HostWaitsForKill(Level level)
    {
        await WaitFrames(() => level.World.Enemies.Any(e => IsInstanceValid(e) && !e.Dead), 60 * 30, "monstros na primeira sala");
        // O J1 fica parado (o bot não aperta nada): quem mata é o convidado, pelos comandos da rede. Ondas novas entram na lista.
        var seen = new List<Enemy>();
        bool AnyDied()
        {
            foreach (var e in level.World.Enemies)
            {
                if (IsInstanceValid(e) && !seen.Contains(e))
                {
                    seen.Add(e);
                }
            }

            return seen.Any(e => !IsInstanceValid(e) || e.Dead);
        }

        await WaitFrames(AnyDied, 60 * 180, "o convidado matar um monstro");
        Log.Info($"online-host: monstro morto pelo J2 ({NetSession.Instance?.SnapshotsSent} snapshots mandados)");
        await PhysicsFrames(60);
    }

    private async Task HostStops(Level level)
    {
        await Tap(Key.Escape);
        await FocusButton("Online");
        await Tap(Key.Enter);
        await WaitFor(() => FindOverlay<OnlineScreen>() is not null, "tela Online na pausa (Brecha)");
        await FocusButton("Parar de hospedar");
        await Tap(Key.Enter);
        Check(NetSession.Instance?.Role == NetRole.None, "Parar de hospedar deveria fechar a porta");
        await Tap(Key.Escape);
        await Tap(Key.Escape);
        await WaitFor(() => _router.OverlayDepth == 0 && !GetTree().Paused, "jogo de volta depois de parar de hospedar");
        Check(!CoopSession.Active && level.World.Players.Count == 1, $"o host deveria seguir sozinho ({level.World.Players.Count} caçadores)");
        Check(!level.CombatHud.GuestPanelVisible, "o painel do J2 deveria sumir");
        await PhysicsFrames(120);
        Check(IsInstanceValid(level) && _router.Current == level && RiftSession.InRift, "a caçada do host deveria continuar depois da saída do convidado");
        Log.Info("online-host: convidado fora, host segue sozinho na Brecha");
    }

    // ---------------------------------------------------------------- convidado

    private async Task RunOnlineGuest()
    {
        var input = new ScriptedInput();
        NetSession.GuestInputOverride = input;
        await OpenFromTitle("Online");
        var screen = FindOverlay<OnlineScreen>()!;
        Check(!screen.InGame, "do título a tela Online deveria ter Entrar");
        var port = CoopSession.Rules.Online.Port.ToString();
        screen.Fill("127.0.0.1", port, OnlineGuestClass);
        Check(screen.ClassId == OnlineGuestClass, "a classe escolhida deveria ser o Martelo");

        var deadline = Time.GetTicksMsec() + 150_000UL;
        while (NetSession.Instance?.GuestState != GuestState.Joined && Time.GetTicksMsec() < deadline)
        {
            if (NetSession.Instance?.Role != NetRole.Guest && FindOverlay<OnlineScreen>() is { } open)
            {
                await PressButton(open, "Entrar");
            }

            await Seconds(1.0);
        }

        Check(NetSession.Instance?.GuestState == GuestState.Joined, $"o convidado deveria entrar (estado: {NetSession.Instance?.Status})");
        await WaitFrames(() => _router.Current is OnlineGuestView && !_router.IsBusy, 600, "tela do convidado");
        if (_failures.Count > 0 || _router.Current is not OnlineGuestView view)
        {
            return;
        }

        var session = NetSession.Instance!;
        await WaitFrames(() => session.LatestSnapshot?.Hunter(session.Slot) is not null, 60 * 120, "o próprio caçador no snapshot");
        var levelBefore = session.LatestSnapshot!.LevelId;
        var startX = session.LatestSnapshot.Hunter(session.Slot)!.Value.X;
        // Os dois processos rodam sem sincronia com o relógio (--fixed-fps): anda até o snapshot do host mostrar o passo.
        input.Move = new Vector2(1, 0);
        await WaitFrames(() => (session.LatestSnapshot?.Hunter(session.Slot)?.X ?? startX) > startX + 40f, 60 * 30, "o passo do convidado voltar no snapshot");
        input.Move = Vector2.Zero;
        await PhysicsFrames(20);
        var movedX = session.LatestSnapshot!.Hunter(session.Slot)?.X ?? startX;
        Check(movedX > startX + 20f, $"andar para a direita deveria mover o caçador no host ({startX} → {movedX})");
        Check(view.HunterDrawnAt(session.Slot) is not null && view.HunterDrawnAt(0) is not null, "o convidado deveria desenhar os dois caçadores");
        Log.Info($"online-guest: andou {startX:0} → {movedX:0} na sala '{levelBefore}' ({session.SnapshotsReceived} snapshots)");

        await WaitFrames(() => session.LatestSnapshot?.LevelId != levelBefore && view.LevelId == session.LatestSnapshot?.LevelId, 60 * 120, "troca de sala vinda do host");
        Log.Info($"online-guest: sala trocou para '{view.LevelId}'");
        await GuestHunts(view, session, input);

        await WaitFrames(() => _router.Current is TitleScreen && !_router.IsBusy, 60 * 400, "volta ao título quando o host encerra");
        Check(NetSession.Instance?.Role == NetRole.None, "a conexão deveria estar fechada");
        Check(TitleScreen.LastNotice.Length > 0, "o título deveria mostrar o aviso da queda");
        Log.Info($"online-guest: de volta ao título com aviso '{TitleScreen.LastNotice}'");
        NetSession.GuestInputOverride = null;
    }

    /// <summary>Anda até o monstro mais perto e bate até um monstro visto sumir do snapshot (morto no host).</summary>
    private async Task GuestHunts(OnlineGuestView view, NetSession session, ScriptedInput input)
    {
        var seen = new HashSet<int>();
        var killed = 0;
        var reach = 0.7f * (view.HumanHeight > 0 ? view.HumanHeight : 100f);
        for (var frame = 0; frame < 60 * 120 && killed == 0 && session.LatestSnapshot is { } snap; frame += 2)
        {
            var ids = snap.Enemies.Select(e => e.Id).ToHashSet();
            killed += seen.Count(id => !ids.Contains(id));
            seen.IntersectWith(ids);
            seen.UnionWith(ids);
            if (snap.Hunter(session.Slot) is { } me && snap.Enemies.Count > 0)
            {
                var target = snap.Enemies.OrderBy(e => new Vector2(e.X - me.X, e.Y - me.Y).LengthSquared()).First();
                var to = new Vector2(target.X - me.X, target.Y - me.Y);
                input.Move = to.Length() > reach * 0.6f ? to.Normalized() : Vector2.Zero;
                if (to.Length() < reach && frame % 12 == 0)
                {
                    input.Move = to.Normalized() * 0.2f;
                    input.Tap(InputActions.Attack);
                }
            }
            else
            {
                input.Move = Vector2.Zero;
            }

            await PhysicsFrames(2);
        }

        input.Move = Vector2.Zero;
        Check(killed > 0, "um monstro deveria morrer no host e sumir do snapshot do convidado");
        Log.Info($"online-guest: {killed} monstro(s) morto(s) vistos no snapshot");
    }

    // ---------------------------------------------------------------- ajudas

    private async Task PressButton(Control root, string text)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Button { Disabled: false } b && b.Text == text)
            {
                b.GrabFocus();
                await Tap(Key.Enter);
                return;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    /// <summary>Espera em quadros de física (até <paramref name="limit"/>); falha com o texto se não acontecer.</summary>
    private async Task WaitFrames(Func<bool> condition, int limit, string what)
    {
        for (var i = 0; i < limit && !condition(); i++)
        {
            await PhysicsFrames(1);
        }

        Check(condition(), $"tempo esgotado esperando: {what}");
    }
}
