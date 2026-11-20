using System;
using System.Collections.Generic;
using System.IO;
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
/// Roteiro determinístico: nenhuma espera é "tempo fixo". Cada passo espera o estado/evento que o próximo precisa (tempo
/// REAL só como teto, porque com <c>--fixed-fps</c> o relógio do jogo dos dois processos anda em ritmos diferentes) e os
/// dois lados se sincronizam por arquivo marcador em <c>--online-sync=</c> (o convidado avisa "vi o monstro morrer").
/// <c>--online-port=</c> escolhe a porta (uma livre por execução) e o convidado erra o código da sala antes de acertar.
/// </summary>
public partial class SmokeTest
{
    public const string OnlineHostFlag = "--online-bot-host";
    public const string OnlineJoinFlag = "--online-bot-join";

    public const string OnlinePortPrefix = "--online-port=";
    public const string OnlineSyncPrefix = "--online-sync=";

    private const string OnlineGuestClass = "hammer";
    private const string OnlineRoomCode = "4821";
    private const string OnlineWrongCode = "0000";
    private const string KillSeenMarker = "guest-saw-kill";
    private const string PauseSeenMarker = "guest-saw-pause";

    /// <summary>Tetos em tempo REAL (s): só para não travar a prova se algo quebrar; o fluxo normal espera estados, não relógio.</summary>
    private const double JoinSeconds = 120;
    private const double WorldSeconds = 240;
    private const double KillSeconds = 300;
    private const double OnlineRealWatchdogSeconds = 900;

    private ulong _onlineDeadlineMs;
    private string? _onlineSyncDir;

    private bool IsOnlineBot => Array.IndexOf(_args, OnlineHostFlag) >= 0 || Array.IndexOf(_args, OnlineJoinFlag) >= 0;

    /// <summary>Teto de tempo real do bot online (o cronômetro do jogo anda no ritmo do <c>--fixed-fps</c>, não do relógio).</summary>
    public override void _Process(double delta)
    {
        if (_onlineDeadlineMs > 0 && Time.GetTicksMsec() > _onlineDeadlineMs)
        {
            _onlineDeadlineMs = 0;
            Log.Error("smoke: tempo real esgotado");
            GetTree().Quit(ExitTimeout);
        }
    }

    private void ApplyOnlineArgs()
    {
        _onlineDeadlineMs = Time.GetTicksMsec() + (ulong)(OnlineRealWatchdogSeconds * 1000);
        foreach (var arg in _args)
        {
            if (arg.StartsWith(OnlinePortPrefix, StringComparison.Ordinal) && int.TryParse(arg[OnlinePortPrefix.Length..], out var port))
            {
                CoopSession.Rules.Online.Port = port;
            }
            else if (arg.StartsWith(OnlineSyncPrefix, StringComparison.Ordinal))
            {
                _onlineSyncDir = arg[OnlineSyncPrefix.Length..];
            }
        }
    }

    private string SyncPath(string marker) => Path.Combine(_onlineSyncDir ?? Path.GetTempPath(), marker);

    private async Task RunOnlineBot()
    {
        ApplyOnlineArgs();
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
        var screen = FindOverlay<OnlineScreen>()!;
        screen.SetHostCode(OnlineRoomCode);
        await FocusButton("Hospedar");
        await Tap(Key.Enter);
        var port = CoopSession.Rules.Online.Port;
        Check(NetSession.Instance?.Role == NetRole.Host, "Hospedar deveria abrir a porta");
        Check(screen.HostInfo.Contains(port.ToString(), StringComparison.Ordinal) && screen.HostInfo.Contains("IP local", StringComparison.Ordinal),
            $"a tela deveria mostrar IP local e porta, mostrou '{screen.HostInfo}'");
        Check(screen.HostInfo.Contains($"Código da sala: {OnlineRoomCode}", StringComparison.Ordinal), $"a tela deveria mostrar o código da sala, mostrou '{screen.HostInfo}'");
        Log.Info($"online-host: {screen.HostInfo.Split('\n')[0]}");
        await Tap(Key.Escape);
        await Tap(Key.Escape);
        await WaitFor(() => _router.OverlayDepth == 0 && !GetTree().Paused, "jogo de volta depois de hospedar");
        if (_failures.Count > 0)
        {
            return;
        }

        // Convidado entra (a outra instância tenta conectar até o host abrir a porta).
        await WaitReal(() => level.Players.Count == 2, JoinSeconds, "o convidado nascer na Praça");
        await PhysicsFrames(10);
        var hostSession = NetSession.Instance!;
        Check(hostSession.RefusalCount >= 1 && hostSession.LastRefusal == Vespera.Core.Net.RejectReason.BadCode,
            $"o host deveria ter recusado a tentativa com código errado antes (recusas: {hostSession.RefusalCount}, última: {hostSession.LastRefusal})");
        var guest = level.World.PlayerAt(1);
        Check(guest is not null && guest.Hunter == HunterClass.Martelo, $"o J2 online deveria entrar com o Martelo ({guest?.Hunter})");
        Check(CoopSession.Online && level.CombatHud.GuestPanelVisible, "o grupo deveria ser online e a HUD mostrar o painel do J2");
        if (guest is null)
        {
            return;
        }

        var start = guest.GlobalPosition;
        await WaitReal(() => IsInstanceValid(guest) && guest.GlobalPosition.DistanceTo(start) > 0.6f * level.World.Data.HumanHeight, WorldSeconds, "o J2 andar pelos comandos da rede");
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

        await WaitReal(() => !level.Switching && level.World.Director is not null && level.World.Players.Count == 2, WorldSeconds, "primeira sala da Brecha em grupo");
        Log.Info($"online-host: grupo na sala '{level.World.Data.Id}'");
    }

    private async Task HostWaitsForKill(Level level)
    {
        await WaitReal(() => level.World.Enemies.Any(e => IsInstanceValid(e) && !e.Dead), WorldSeconds, "monstros na primeira sala");
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

        await WaitReal(AnyDied, KillSeconds, "o convidado matar um monstro");
        Log.Info($"online-host: monstro morto pelo J2 ({NetSession.Instance?.SnapshotsSent} snapshots mandados)");
        // Sem tempo fixo: só fecha a porta quando o convidado avisar (arquivo marcador) que viu o monstro sumir do snapshot.
        await WaitReal(() => File.Exists(SyncPath(KillSeenMarker)), WorldSeconds, "o convidado ver o monstro morrer no snapshot");
        Log.Info("online-host: convidado confirmou que viu a morte no snapshot");
    }

    private async Task HostStops(Level level)
    {
        await Tap(Key.Escape);
        // O jogo do host está pausado (e continua hospedando): o convidado precisa mostrar "O host pausou o jogo".
        await WaitReal(() => File.Exists(SyncPath(PauseSeenMarker)), WorldSeconds, "o convidado ver o aviso de pausa do host");
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
        var session = NetSession.Instance!;
        string? failure = null;
        void OnResult(bool ok, string text) => failure = ok ? null : text;
        session.JoinResult += OnResult;

        // 1) Código errado: o host recusa e o convidado vê o aviso em pt-BR (e continua na tela Online).
        screen.Fill("127.0.0.1", port, OnlineGuestClass, OnlineWrongCode);
        Check(screen.ClassId == OnlineGuestClass, "a classe escolhida deveria ser o Martelo");
        await PressButton(screen, "Entrar");
        await WaitReal(() => failure is not null, JoinSeconds, "a recusa do host pelo código errado");
        Check(failure?.Contains("Código da sala incorreto", StringComparison.Ordinal) == true, $"o aviso deveria falar do código errado, foi '{failure}'");
        Check(session.GuestState != GuestState.Joined && _router.Current is TitleScreen, "com o código errado o convidado não deveria entrar");
        Log.Info($"online-guest: código errado recusado: '{failure}'");

        // 2) Código certo: entra.
        failure = null;
        screen = FindOverlay<OnlineScreen>()!;
        screen.Fill("127.0.0.1", port, OnlineGuestClass, OnlineRoomCode);
        await PressButton(screen, "Entrar");
        await WaitReal(() => session.GuestState == GuestState.Joined || failure is not null, JoinSeconds, "o host aceitar o convidado");
        session.JoinResult -= OnResult;
        Check(session.GuestState == GuestState.Joined, $"o convidado deveria entrar (recusa: '{failure}', estado: {session.Status})");
        await WaitReal(() => _router.Current is OnlineGuestView && !_router.IsBusy, JoinSeconds, "tela do convidado");
        if (_failures.Count > 0 || _router.Current is not OnlineGuestView view)
        {
            return;
        }

        await WaitReal(() => session.LatestSnapshot?.Hunter(session.Slot) is not null, WorldSeconds, "o próprio caçador no snapshot");
        await GuestChecksScene(view, session);
        var levelBefore = session.LatestSnapshot!.LevelId;
        var startX = session.LatestSnapshot.Hunter(session.Slot)!.Value.X;
        // Os dois processos rodam sem sincronia com o relógio (--fixed-fps): anda até o snapshot do host mostrar o passo.
        input.Move = new Vector2(1, 0);
        await WaitReal(() => (session.LatestSnapshot?.Hunter(session.Slot)?.X ?? startX) > startX + 40f, WorldSeconds, "o passo do convidado voltar no snapshot");
        input.Move = Vector2.Zero;
        await PhysicsFrames(20);
        var movedX = session.LatestSnapshot!.Hunter(session.Slot)?.X ?? startX;
        Check(movedX > startX + 20f, $"andar para a direita deveria mover o caçador no host ({startX} → {movedX})");
        Check(view.HunterDrawnAt(session.Slot) is not null && view.HunterDrawnAt(0) is not null, "o convidado deveria desenhar os dois caçadores");
        Log.Info($"online-guest: andou {startX:0} → {movedX:0} na sala '{levelBefore}' ({session.SnapshotsReceived} snapshots)");

        await WaitReal(() => session.LatestSnapshot?.LevelId != levelBefore && view.LevelId == session.LatestSnapshot?.LevelId, WorldSeconds, "troca de sala vinda do host");
        Log.Info($"online-guest: sala trocou para '{view.LevelId}'");
        await GuestHunts(view, session, input);
        await GuestChecksCombatView(view);
        await WaitReal(() => view.HostPausedShown, WorldSeconds, "o aviso de que o host pausou");
        File.WriteAllText(SyncPath(PauseSeenMarker), "1");
        Log.Info("online-guest: viu o aviso 'host pausou'");

        await WaitReal(() => _router.Current is TitleScreen && !_router.IsBusy, WorldSeconds, "volta ao título quando o host encerra");
        Check(NetSession.Instance?.Role == NetRole.None, "a conexão deveria estar fechada");
        Check(TitleScreen.LastNotice.Length > 0, "o título deveria mostrar o aviso da queda");
        Log.Info($"online-guest: de volta ao título com aviso '{TitleScreen.LastNotice}'");
        NetSession.GuestInputOverride = null;
    }

    /// <summary>Cenário do host na tela do convidado: objetos e NPCs da sala, na mesma conta do que o host mandou.</summary>
    private async Task GuestChecksScene(OnlineGuestView view, NetSession session)
    {
        await WaitReal(() => session.LatestScene is { Entries.Count: > 0 } && view.SceneryDrawn > 0, WorldSeconds, "o cenário (objetos) da sala do host");
        var scene = session.LatestScene!;
        Check(scene.LevelId == view.LevelId, $"o cenário deveria ser da sala desenhada ({scene.LevelId} x {view.LevelId})");
        Check(view.SceneryDrawn == scene.Entries.Count, $"o convidado deveria desenhar todos os objetos do host ({view.SceneryDrawn} de {scene.Entries.Count})");
        var npcs = session.LatestSnapshot!.NpcList.Count;
        if (npcs > 0)
        {
            await WaitReal(() => view.NpcsDrawn > 0, WorldSeconds, "os NPCs da sala do host");
        }

        Log.Info($"online-guest: cenário '{scene.LevelId}': {view.SceneryDrawn} objetos, {view.NpcsDrawn} NPCs (host tem {npcs})");
    }

    /// <summary>Golpes e dano do host na tela do convidado (números/efeitos tocados) e o HUD de vida e fôlego.</summary>
    private async Task GuestChecksCombatView(OnlineGuestView view)
    {
        await WaitReal(() => view.DamageNumbersPlayed > 0, JoinSeconds, "números de dano vindos do host");
        Check(view.EffectsPlayed > 0, "o convidado deveria ter tocado efeitos de golpe do host");
        Check(view.HudLifeText.Contains("Vida", StringComparison.Ordinal) && view.HudLifeFraction > 0.05f, $"o HUD deveria mostrar a vida do convidado ('{view.HudLifeText}', {view.HudLifeFraction:0.00})");
        Log.Info($"online-guest: {view.EffectsPlayed} efeitos de golpe ({view.DamageNumbersPlayed} números de dano); HUD '{view.HudLifeText}'");
    }

    /// <summary>Anda até o monstro mais perto e bate até um monstro visto sumir do snapshot (morto no host).</summary>
    private async Task GuestHunts(OnlineGuestView view, NetSession session, ScriptedInput input)
    {
        var seen = new HashSet<int>();
        var killed = 0;
        var reach = 0.7f * (view.HumanHeight > 0 ? view.HumanHeight : 100f);
        var deadline = Time.GetTicksMsec() + (ulong)(KillSeconds * 1000);
        for (var frame = 0; Time.GetTicksMsec() < deadline && killed == 0 && session.LatestSnapshot is { } snap; frame += 2)
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
        // Avisa o host (que espera este arquivo, sem tempo fixo) que o snapshot já mostrou a morte.
        File.WriteAllText(SyncPath(KillSeenMarker), killed.ToString());
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

    /// <summary>
    /// Espera o estado em tempo REAL (até <paramref name="seconds"/>, teto de segurança): com <c>--fixed-fps</c> o relógio do jogo
    /// dos dois processos anda em ritmos diferentes, então quadros e <c>CreateTimer</c> não medem a espera pelo outro lado.
    /// </summary>
    private async Task WaitReal(Func<bool> condition, double seconds, string what)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!condition() && Time.GetTicksMsec() < deadline)
        {
            await Frames(1);
            OS.DelayMsec(2);
        }

        Check(condition(), $"tempo esgotado esperando: {what}");
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
