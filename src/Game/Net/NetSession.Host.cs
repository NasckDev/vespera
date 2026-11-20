using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Coop;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>Um convidado conectado ao host: par do ENet, slot no grupo, classe e o relógio de silêncio.</summary>
public sealed class OnlineGuest(long peerId, LinkWatch watch)
{
    public long PeerId { get; } = peerId;

    /// <summary>Antes do Hello: relógio curto (<see cref="NetProtocol.HelloTimeoutSeconds"/>); depois de aceito: o de silêncio.</summary>
    public LinkWatch Watch { get; set; } = watch;

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
    private JoinGate _gate = new(string.Empty);
    private readonly List<NetEffect> _effects = [];
    private SnapshotPacer _scenePacer = new(4);
    private byte[] _lastScene = [];
    private bool _sceneResend;

    /// <summary>Conexões a mais que o ENet aceita além do grupo: deixam o 2º convidado receber "grupo cheio" em vez de um erro mudo.</summary>
    private const int PendingConnectionSlots = 3;

    /// <summary>Código da sala que o convidado precisa digitar ("" = sala aberta).</summary>
    public string RoomCode => _gate.Code;

    /// <summary>Entradas recusadas nesta hospedagem e o motivo da última (bots e diagnóstico).</summary>
    public int RefusalCount { get; private set; }

    public RejectReason? LastRefusal { get; private set; }

    /// <summary>Por que o último "Hospedar" falhou (pt-BR); vazio se deu certo ou nunca falhou.</summary>
    public string HostError { get; private set; } = string.Empty;

    public IReadOnlyCollection<OnlineGuest> Guests => _guests.Values;

    /// <summary>Snapshots mandados (para os bots).</summary>
    public int SnapshotsSent { get; private set; }

    /// <summary>Começa a hospedar na porta (padrão: <c>data/coop.json</c> → online.port), com código de sala opcional.</summary>
    public Error Host(int port, string code = "")
    {
        Stop();
        HostError = string.Empty;
        if (!NetProtocol.IsValidCode(code))
        {
            HostError = $"O código da sala deve ter de {NetProtocol.MinCodeLength} a {NetProtocol.MaxCodeLength} números (ou ficar vazio para sala aberta).";
            Status = HostError;
            return Error.InvalidParameter;
        }

        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, MaxOnlinePlayers - 1 + PendingConnectionSlots, ChannelCount);
        if (error != Error.Ok)
        {
            HostError = error == Error.CantCreate
                ? $"Não deu para hospedar: a porta {port} já está em uso (outro jogo aberto?). Tente outra porta."
                : $"Não deu para hospedar na porta {port} ({error}). Tente outra porta.";
            Status = HostError;
            Log.Warn($"online: falha ao hospedar na porta {port}: {error}");
            return error;
        }

        _gate = new JoinGate(code);
        _effects.Clear();
        _lastScene = [];
        _scenePacer = new SnapshotPacer(SceneHz);
        CombatFeel.EffectEmitted -= OnFeelEffect;
        CombatFeel.EffectEmitted += OnFeelEffect;
        RefusalCount = 0;
        LastRefusal = null;

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

    /// <summary>Quantas vezes por segundo o host confere se o cenário mudou (só manda se mudou).</summary>
    private const int SceneHz = 4;

    /// <summary>Efeito de combate do host: vai para a fila e sai no próximo snapshot (só enquanto há convidado).</summary>
    private void OnFeelEffect(NetEffect effect)
    {
        if (Role != NetRole.Host || !_guests.Values.Any(g => g.Accepted))
        {
            return;
        }

        if (_effects.Count >= EffectBatch.MaxEffects)
        {
            _effects.RemoveAt(0);
        }

        _effects.Add(effect);
    }

    private void SendToGuests(byte[] bytes, MultiplayerPeer.TransferModeEnum mode, int channel)
    {
        foreach (var guest in _guests.Values.Where(g => g.Accepted))
        {
            Send(bytes, (int)guest.PeerId, mode, channel);
        }
    }

    private void OnPeerConnected(long id)
    {
        if (Role != NetRole.Host)
        {
            return;
        }

        var watch = new LinkWatch(NetProtocol.HelloTimeoutSeconds);
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

        // Tamanho acima do teto do tipo = lixo ou ataque: descarta sem ler.
        switch (NetProtocol.KindOfBounded(packet))
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
        if (_gate.Evaluate(hello, playersNow, MaxOnlinePlayers, CoopSession.ClassIds, Now) is { } reason)
        {
            Refuse(guest, reason);
            return;
        }

        guest.Watch = new LinkWatch(Tuning.TimeoutSeconds);
        guest.Watch.Touch(Now);
        guest.Slot = (byte)playersNow;
        guest.ClassId = hello.ClassId;
        Providers.Remove(guest.Slot);
        _sceneResend = true;
        Send(NetProtocol.WriteWelcome(guest.Slot), (int)guest.PeerId, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
        Log.Info($"online: {CoopSession.TagOf(guest.Slot)} aceito ({hello.ClassId})");
    }

    private void Refuse(OnlineGuest guest, RejectReason reason)
    {
        Log.Info($"online: par {guest.PeerId} recusado ({reason})");
        RefusalCount++;
        LastRefusal = reason;
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
                Log.Info($"online: par {guest.PeerId} sem notícia há {guest.Watch.TimeoutSeconds:0.#} s{(guest.Accepted ? string.Empty : " (sem Hello)")}, desconectando");
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
            SendToGuests(snapshot.Write(), MultiplayerPeer.TransferModeEnum.UnreliableOrdered, StreamChannel);
            SnapshotsSent++;
            FlushEffects();
        }

        if (_guests.Values.Any(g => g.Accepted) && _scenePacer.Due(GameNow))
        {
            SendSceneIfChanged(level);
        }
    }

    /// <summary>Manda os efeitos de combate do último intervalo (confiável: um golpe não pode se perder).</summary>
    private void FlushEffects()
    {
        if (_effects.Count == 0)
        {
            return;
        }

        SendToGuests(EffectBatch.Write(_effects), MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
        _effects.Clear();
    }

    /// <summary>Cenário da sala (objetos e variante do chão): confiável, só quando muda ou quando entra um convidado.</summary>
    private void SendSceneIfChanged(Level? level)
    {
        if (level is null || !IsInstanceValid(level) || SceneBuilder.Build(level) is not { } scene)
        {
            return;
        }

        var bytes = scene.Write();
        if (!_sceneResend && bytes.AsSpan().SequenceEqual(_lastScene))
        {
            return;
        }

        _sceneResend = false;
        _lastScene = bytes;
        SendToGuests(bytes, MultiplayerPeer.TransferModeEnum.Reliable, ReliableChannel);
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
