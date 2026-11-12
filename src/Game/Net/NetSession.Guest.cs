using System;
using Godot;
using Vespera.Core.Coop;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.UI;

namespace Vespera.Game.Net;

/// <summary>Em que pé está a entrada do convidado.</summary>
public enum GuestState
{
    Idle,
    Connecting,
    Waiting,
    Joined,
}

/// <summary>
/// Lado do convidado: conecta, manda o Hello com a classe, recebe o slot, manda um comando por passo de física e guarda os
/// snapshots no <see cref="SnapshotInterpolator"/>. Queda (host fechou, silêncio, recusa) → título com aviso em pt-BR.
/// </summary>
public partial class NetSession
{
    public const string GuestScreenKey = "online_guest";

    private LinkWatch? _guestWatch;
    private LocalCommandProvider? _guestCommands;

    public GuestState GuestState { get; private set; }

    public byte Slot { get; private set; }

    public string ClassId { get; private set; } = string.Empty;

    public SnapshotInterpolator? Interpolator { get; private set; }

    public WorldSnapshot? LatestSnapshot => Interpolator?.Latest;

    public int SnapshotsReceived { get; private set; }

    public int CommandsSent { get; private set; }

    /// <summary>Bots: fonte de input do convidado no lugar do teclado/controle.</summary>
    public static IInputSource? GuestInputOverride { get; set; }

    /// <summary>Entrou (Welcome) ou falhou (texto do motivo).</summary>
    public event Action<bool, string>? JoinResult;

    /// <summary>Conecta ao host e pede a entrada com a classe (id da arma).</summary>
    public Error Join(string address, int port, string classId)
    {
        Stop();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address.Trim(), port, ChannelCount);
        if (error != Error.Ok)
        {
            Status = $"Não deu para conectar em {address}:{port} ({error}).";
            return error;
        }

        _peer = peer;
        _mp.MultiplayerPeer = peer;
        Role = NetRole.Guest;
        Port = port;
        ClassId = classId;
        Slot = 0;
        GuestState = GuestState.Connecting;
        SnapshotsReceived = 0;
        CommandsSent = 0;
        Status = $"Conectando a {address}:{port}…";
        Log.Info($"online: conectando a {address}:{port} como {classId}");
        return Error.Ok;
    }

    /// <summary>O convidado sai por vontade própria (pausa → Sair do online).</summary>
    public void LeaveAsGuest()
    {
        if (Role != NetRole.Guest)
        {
            return;
        }

        Stop();
        GuestState = GuestState.Idle;
        PendingNotice = "Você saiu da partida online.";
        SceneRouter.Instance.GoTo(ScreenKeys.Title);
    }

    private void OnConnectedToServer()
    {
        if (Role != NetRole.Guest)
        {
            return;
        }

        SetTimeout(1);
        GuestState = GuestState.Waiting;
        _guestWatch = new LinkWatch(Tuning.TimeoutSeconds);
        _guestWatch.Touch(Now);
        Status = "Conectado. Esperando o host aceitar…";
        Send(NetProtocol.WriteHello(new HelloMessage(NetProtocol.Version, ClassId)), 1, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
    }

    private void OnConnectionFailed()
    {
        if (Role == NetRole.Guest)
        {
            FailGuest("Não deu para conectar ao host. Confira o IP, a porta e se ele abriu a porta no roteador/firewall.");
        }
    }

    private void OnServerDisconnected()
    {
        if (Role == NetRole.Guest)
        {
            FailGuest(GuestState == GuestState.Joined ? "A conexão com o host caiu (ou ele encerrou a partida)." : "O host fechou a conexão.");
        }
    }

    private void OnGuestPacket(byte[] packet)
    {
        _guestWatch?.Touch(Now);
        switch (NetProtocol.KindOf(packet))
        {
            case NetMessageKind.Welcome when NetProtocol.TryReadWelcome(packet, out var slot):
                Slot = slot;
                GuestState = GuestState.Joined;
                Interpolator = new SnapshotInterpolator(Tuning.InterpolationMs / 1000.0);
                _guestCommands = new LocalCommandProvider(GuestInputOverride ?? MergedInput.LocalSolo(), slot);
                Status = $"Dentro do grupo como {CoopSession.TagOf(slot)}.";
                Log.Info($"online: aceito como {CoopSession.TagOf(slot)}");
                JoinResult?.Invoke(true, Status);
                SceneRouter.Instance.GoTo(GuestScreenKey);
                break;
            case NetMessageKind.Reject when NetProtocol.TryReadReject(packet, out var reason):
                FailGuest(NetProtocol.Describe(reason));
                break;
            case NetMessageKind.Snapshot when GuestState == GuestState.Joined && WorldSnapshot.TryRead(packet, out var snapshot):
                if (Interpolator?.Add(Now, snapshot) == true)
                {
                    SnapshotsReceived++;
                }

                break;
        }
    }

    private void GuestTick()
    {
        if (_guestWatch is { } watch && watch.TimedOut(Now))
        {
            FailGuest($"O host ficou {Tuning.TimeoutSeconds:0} s sem responder. A partida online terminou.");
        }
    }

    private void SendGuestCommand()
    {
        if (GuestState != GuestState.Joined || _guestCommands is null)
        {
            return;
        }

        var tick = (uint)Engine.GetPhysicsFrames();
        // Com um menu aberto (pausa do convidado) o caçador fica parado: o menu usa as teclas.
        var command = SceneRouter.Instance.OverlayDepth > 0 ? PlayerCommand.Idle(tick, Slot) : _guestCommands.Next(tick);
        Send(NetProtocol.WriteCommand(command), 1, MultiplayerPeer.TransferModeEnum.UnreliableOrdered, StreamChannel);
        CommandsSent++;
    }

    /// <summary>Queda ou recusa: fecha a conexão e, se já estava no jogo do host, volta ao título com o aviso.</summary>
    private void FailGuest(string reason)
    {
        var wasJoined = GuestState == GuestState.Joined;
        Log.Info($"online: convidado sem conexão: {reason}");
        Stop();
        GuestState = GuestState.Idle;
        Status = reason;
        JoinResult?.Invoke(false, reason);
        if (wasJoined)
        {
            PendingNotice = reason;
            SceneRouter.Instance.GoTo(ScreenKeys.Title);
        }
    }
}
