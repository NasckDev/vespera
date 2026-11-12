using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>Um convidado conectado ao host: par do ENet, slot no grupo, classe e o relógio de silêncio.</summary>
public sealed class OnlineGuest(long peerId, LinkWatch watch)
{
    public long PeerId { get; } = peerId;

    public LinkWatch Watch { get; } = watch;

    /// <summary>0 enquanto o Hello não chegou (ou foi recusado).</summary>
    public byte Slot { get; set; }

    public string ClassId { get; set; } = string.Empty;

    public bool Accepted => Slot > 0;
}

/// <summary>
/// Lado do host: aceita ou recusa a entrada, põe o convidado no grupo quando a sala deixa (fora da Brecha), passa o comando
/// dele ao caçador (<see cref="NetworkCommandProvider"/>), manda o snapshot ~20/s e tira o convidado quando ele cai.
/// </summary>
public partial class NetSession
{
    private readonly Dictionary<long, OnlineGuest> _guests = [];
    private readonly NetIdMap _enemyIds = new();
    private SnapshotPacer _pacer = new(20);

    public IReadOnlyCollection<OnlineGuest> Guests => _guests.Values;

    /// <summary>Snapshots mandados (para os bots).</summary>
    public int SnapshotsSent { get; private set; }

    /// <summary>Começa a hospedar na porta (padrão: <c>data/coop.json</c> → online.port).</summary>
    public Error Host(int port)
    {
        Stop();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, MaxOnlinePlayers - 1, ChannelCount);
        if (error != Error.Ok)
        {
            Status = $"Não deu para hospedar na porta {port} ({error}). Ela pode estar em uso.";
            Log.Warn($"online: falha ao hospedar na porta {port}: {error}");
            return error;
        }

        _peer = peer;
        _mp.MultiplayerPeer = peer;
        Role = NetRole.Host;
        Port = port;
        _pacer = new SnapshotPacer(Tuning.SnapshotHz);
        var addresses = LocalAddresses();
        Status = $"Hospedando na porta {port}. IP local: {(addresses.Count > 0 ? string.Join(", ", addresses) : "127.0.0.1")}";
        Log.Info($"online: hospedando na porta {port}");
        return Error.Ok;
    }

    private void OnPeerConnected(long id)
    {
        if (Role != NetRole.Host)
        {
            return;
        }

        var watch = new LinkWatch(Tuning.TimeoutSeconds);
        watch.Touch(Now);
        _guests[id] = new OnlineGuest(id, watch);
        SetTimeout(id);
        Log.Info($"online: par {id} conectou, esperando o Hello");
    }

    private void OnPeerDisconnected(long id)
    {
        if (Role != NetRole.Host || !_guests.Remove(id, out var guest))
        {
            return;
        }

        Log.Info($"online: par {id} saiu (slot {guest.Slot})");
        if (guest.Accepted)
        {
            DropOnlineGuests($"{CoopSession.TagOf(guest.Slot)} saiu do online.");
        }
    }

    private void OnHostPacket(long id, byte[] packet)
    {
        if (!_guests.TryGetValue(id, out var guest))
        {
            return;
        }

        switch (NetProtocol.KindOf(packet))
        {
            case NetMessageKind.Hello when !guest.Accepted:
                OnHello(guest, packet);
                break;
            case NetMessageKind.Command when guest.Accepted:
                if (NetProtocol.TryReadCommand(packet, guest.Slot, out var command))
                {
                    guest.Watch.Touch(Now);
                    CommandsFor(guest.Slot).Push(RemoteCommands.Sanitize(command));
                }

                break;
        }
    }

    private void OnHello(OnlineGuest guest, byte[] packet)
    {
        if (!NetProtocol.TryReadHello(packet, out var hello))
        {
            Refuse(guest, RejectReason.Malformed);
            return;
        }

        // Co-op local já em curso (J2 no mesmo PC): o grupo está cheio para o online.
        var playersNow = CoopSession.Active && !CoopSession.Online ? MaxOnlinePlayers : 1 + _guests.Values.Count(g => g.Accepted);
        if (NetProtocol.Validate(hello, playersNow, MaxOnlinePlayers, CoopSession.ClassIds) is { } reason)
        {
            Refuse(guest, reason);
            return;
        }

        guest.Watch.Touch(Now);
        guest.Slot = (byte)playersNow;
        guest.ClassId = hello.ClassId;
        Providers.Remove(guest.Slot);
        Send(NetProtocol.WriteWelcome(guest.Slot), (int)guest.PeerId, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
        Log.Info($"online: {CoopSession.TagOf(guest.Slot)} aceito ({hello.ClassId})");
    }

    private void Refuse(OnlineGuest guest, RejectReason reason)
    {
        Log.Info($"online: par {guest.PeerId} recusado ({reason})");
        Send(NetProtocol.WriteReject(reason), (int)guest.PeerId, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
        _peer?.GetPeer((int)guest.PeerId)?.PeerDisconnectLater();
    }

    private void HostTick()
    {
        var now = Now;
        foreach (var guest in _guests.Values.ToList())
        {
            if (guest.Watch.TimedOut(now))
            {
                Log.Info($"online: par {guest.PeerId} sem notícia há {Tuning.TimeoutSeconds} s, desconectando");
                _peer?.DisconnectPeer((int)guest.PeerId);
                OnPeerDisconnected(guest.PeerId);
            }
        }

        var level = SceneRouter.Instance.Current as Level;
        foreach (var guest in _guests.Values.Where(g => g.Accepted))
        {
            if (level is not null && IsInstanceValid(level) && level.World is not null && level.World.PlayerAt(guest.Slot) is null && level.CanChangeParty)
            {
                level.ApplyOnlineGuest(guest.Slot, guest.ClassId);
            }
        }

        if (_guests.Values.Any(g => g.Accepted) && _pacer.Due(GameNow))
        {
            var snapshot = level is not null && IsInstanceValid(level) && level.World is not null && IsInstanceValid(level.World)
                ? SnapshotBuilder.Build(level, _enemyIds)
                : SnapshotBuilder.Waiting();
            Send(snapshot.Write(), 0, MultiplayerPeer.TransferModeEnum.UnreliableOrdered, StreamChannel);
            SnapshotsSent++;
        }
    }

    /// <summary>Tira os convidados online da sala (o host segue sozinho) e avisa na HUD.</summary>
    private static void DropOnlineGuests(string toast)
    {
        if (!CoopSession.Online)
        {
            return;
        }

        if (SceneRouter.Instance.Current is Level level && IsInstanceValid(level))
        {
            level.LeaveOnline(toast);
        }
        else
        {
            CoopSession.End();
        }
    }
}
