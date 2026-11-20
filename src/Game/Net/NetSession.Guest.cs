using System;
using System.Collections.Generic;
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

    private string _joinCode = string.Empty;

    public SnapshotInterpolator? Interpolator { get; private set; }

    /// <summary>Último cenário que o host mandou (objetos e variante do chão da sala); <see cref="SceneSerial"/> sobe a cada um.</summary>
    public SceneSnapshot? LatestScene { get; private set; }

    public int SceneSerial { get; private set; }

    /// <summary>Efeitos de combate vindos do host (faíscas, anéis, números, tremor, rastros): a tela do convidado os toca.</summary>
    public event Action<IReadOnlyList<NetEffect>>? EffectsReceived;

    public WorldSnapshot? LatestSnapshot => Interpolator?.Latest;

    public int SnapshotsReceived { get; private set; }

    public int CommandsSent { get; private set; }

    /// <summary>Bots: fonte de input do convidado no lugar do teclado/controle.</summary>
    public static IInputSource? GuestInputOverride { get; set; }

    /// <summary>Entrou (Welcome) ou falhou (texto do motivo).</summary>
    public event Action<bool, string>? JoinResult;

    /// <summary>Conecta ao host e pede a entrada com a classe (id da arma) e o código da sala (vazio se o host não usa).</summary>
    public Error Join(string address, int port, string classId, string code = "")
    {
        Stop();
        if (!NetProtocol.IsValidCode(code))
        {
            Status = $"O código da sala tem de {NetProtocol.MinCodeLength} a {NetProtocol.MaxCodeLength} números.";
            return Error.InvalidParameter;
        }

        _joinCode = code;
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
        Send(NetProtocol.WriteHello(new HelloMessage(NetProtocol.Version, ClassId, _joinCode)), 1, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
    }

    private void OnConnectionFailed()
    {
        if (Role == NetRole.Guest)
        {
            FailGuest("Não deu para conectar ao host. Confira o IP e a porta, e se o host já está hospedando (pela internet, a porta UDP precisa estar aberta no roteador e no firewall).");
        }
    }

    private void OnServerDisconnected()
    {
        if (Role == NetRole.Guest)
        {
            FailGuest(GuestState == GuestState.Joined
                ? "A conexão com o host caiu (ou ele encerrou a partida)."
                : "O host fechou a conexão antes de aceitar você. Ele pode ter parado de hospedar ou não respondeu ao pedido a tempo.");
        }
    }

    private void OnGuestPacket(byte[] packet)
    {
        _guestWatch?.Touch(Now);
        switch (NetProtocol.KindOfBounded(packet))
        {
            case NetMessageKind.Welcome when NetProtocol.TryReadWelcome(packet, out var slot):
                Slot = slot;
                GuestState = GuestState.Joined;
                LatestScene = null;
                SceneSerial = 0;
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
            case NetMessageKind.Events when GuestState == GuestState.Joined && EffectBatch.TryRead(packet, out var effects):
                EffectsReceived?.Invoke(effects);
                break;
            case NetMessageKind.Scene when GuestState == GuestState.Joined && SceneSnapshot.TryRead(packet, out var scene):
                LatestScene = scene;
                SceneSerial++;
                break;
            case NetMessageKind.Snapshot when GuestState == GuestState.Joined && WorldSnapshot.TryRead(packet, out var snapshot):
                if (Interpolator?.Add(GameNow, snapshot) == true)
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
