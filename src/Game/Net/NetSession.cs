using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Coop;
using Vespera.Core.Net;
using Vespera.Game.Boot;
using Vespera.Game.Coop;

namespace Vespera.Game.Net;

/// <summary>Papel deste PC no co-op online.</summary>
public enum NetRole
{
    None,
    Host,
    Guest,
}

/// <summary>
/// Sessão do co-op online (nó fixo <c>/root/NetLink</c>, vive fora das cenas): <see cref="ENetMultiplayerPeer"/> com os
/// bytes do <see cref="NetProtocol"/> por <see cref="SceneMultiplayer.SendBytes"/>. Host autoritativo: o host roda o jogo
/// de sempre, recebe o <see cref="PlayerCommand"/> do convidado (canal não confiável e ordenado) e manda
/// <see cref="WorldSnapshot"/> ~20/s; o convidado só manda comandos e desenha (<see cref="OnlineGuestView"/>).
/// Entrada (Hello/Welcome/Reject) vai no canal confiável. Grupo online: o host e um convidado (a HUD é de dois).
/// </summary>
public partial class NetSession : Node
{
    public const string NodeName = "NetLink";

    /// <summary>Host e um convidado: a HUD, as cores e a tela de entrada são de dois jogadores.</summary>
    public const int MaxOnlinePlayers = 2;

    private const int ReliableChannel = 0;
    private const int StreamChannel = 1;
    private const int ChannelCount = 2;

    private static readonly Dictionary<int, NetworkCommandProvider> Providers = [];

    private ENetMultiplayerPeer? _peer;
    private SceneMultiplayer _mp = null!;

    public static NetSession? Instance { get; private set; }

    public NetRole Role { get; private set; }

    public int Port { get; private set; }

    /// <summary>Linha de estado para as telas (pt-BR).</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Aviso a mostrar no título depois de cair (convidado) — lido e limpo pelo título.</summary>
    public static string? PendingNotice { get; set; }

    public static double Now => Time.GetTicksMsec() / 1000.0;

    private static OnlineTuning Tuning => CoopSession.Rules.Online;

    /// <summary>Cria o nó na raiz (uma vez por execução).</summary>
    public static void Ensure(Node anyNode)
    {
        if (Instance is not null && IsInstanceValid(Instance))
        {
            return;
        }

        var session = new NetSession { Name = NodeName };
        anyNode.GetTree().Root.CallDeferred(Node.MethodName.AddChild, session);
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _mp = (SceneMultiplayer)GetTree().GetMultiplayer();
        _mp.PeerConnected += OnPeerConnected;
        _mp.PeerDisconnected += OnPeerDisconnected;
        _mp.ConnectedToServer += OnConnectedToServer;
        _mp.ConnectionFailed += OnConnectionFailed;
        _mp.ServerDisconnected += OnServerDisconnected;
        _mp.PeerPacket += OnPeerPacket;
    }

    public override void _ExitTree()
    {
        Stop();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Comandos do convidado <paramref name="slot"/> no host (a fonte de input do caçador dele).</summary>
    public static NetworkCommandProvider CommandsFor(int slot)
    {
        if (!Providers.TryGetValue(slot, out var provider))
        {
            provider = new NetworkCommandProvider((byte)slot);
            Providers[slot] = provider;
        }

        return provider;
    }

    /// <summary>Endereços IPv4 desta máquina na rede local (para o amigo digitar em "Entrar").</summary>
    public static IReadOnlyList<string> LocalAddresses() =>
        IP.GetLocalAddresses()
            .Where(a => a.Contains('.') && !a.StartsWith("127.", StringComparison.Ordinal) && !a.StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct()
            .ToList();

    /// <summary>Encerra a conexão (hospedagem ou entrada) sem avisar ninguém.</summary>
    public void Stop()
    {
        if (Role == NetRole.None && _peer is null)
        {
            return;
        }

        Log.Info($"online: encerrando ({Role})");
        var wasHost = Role == NetRole.Host;
        Role = NetRole.None;
        _guests.Clear();
        _guestWatch = null;
        _peer?.Close();
        _peer = null;
        if (_mp is not null && IsInstanceValid(_mp))
        {
            _mp.MultiplayerPeer = new OfflineMultiplayerPeer();
        }

        Interpolator = null;
        if (wasHost)
        {
            DropOnlineGuests("O online foi encerrado.");
        }

        Status = string.Empty;
    }

    private void Send(byte[] bytes, int peer, MultiplayerPeer.TransferModeEnum mode, int channel)
    {
        if (_peer is null)
        {
            return;
        }

        var error = _mp.SendBytes(bytes, peer, mode, channel);
        if (error != Error.Ok)
        {
            Log.Warn($"online: envio falhou ({error})");
        }
    }

    private void SetTimeout(long peerId)
    {
        if (_peer?.GetPeer((int)peerId) is { } packetPeer)
        {
            var ms = (int)(Tuning.TimeoutSeconds * 1000);
            packetPeer.SetTimeout(32, ms, ms);
        }
    }

    private void OnPeerPacket(long id, byte[] packet)
    {
        switch (Role)
        {
            case NetRole.Host:
                OnHostPacket(id, packet);
                break;
            case NetRole.Guest:
                OnGuestPacket(packet);
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (Role == NetRole.Host)
        {
            HostTick();
        }
        else if (Role == NetRole.Guest)
        {
            GuestTick();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Role == NetRole.Guest)
        {
            SendGuestCommand();
        }
    }
}
