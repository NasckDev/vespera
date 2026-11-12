using System.Buffers.Binary;
using System.Text;
using Vespera.Core.Coop;

namespace Vespera.Core.Net;

/// <summary>Tipo de cada mensagem do co-op online (primeiro byte do pacote).</summary>
public enum NetMessageKind : byte
{
    /// <summary>Convidado → host: versão do protocolo e classe escolhida.</summary>
    Hello = 1,

    /// <summary>Host → convidado: aceito, com o número do jogador (slot).</summary>
    Welcome = 2,

    /// <summary>Host → convidado: recusado (versão diferente, grupo cheio, classe inválida).</summary>
    Reject = 3,

    /// <summary>Convidado → host: um <see cref="PlayerCommand"/> por passo de física.</summary>
    Command = 4,

    /// <summary>Host → convidado: estado do mundo (~20/s).</summary>
    Snapshot = 5,
}

/// <summary>Por que o host recusou a entrada.</summary>
public enum RejectReason : byte
{
    Version = 1,
    Full = 2,
    BadClass = 3,
    Malformed = 4,
}

/// <summary>Pedido de entrada do convidado.</summary>
public readonly record struct HelloMessage(ushort Version, string ClassId);

/// <summary>
/// Protocolo do co-op online (host autoritativo, ENet). Puro, sem Godot: só bytes. Toda leitura valida tamanho, limites e
/// valores (nunca confia no pacote) e devolve <c>false</c> em vez de lançar, para o host descartar pacote ruim sem cair.
/// </summary>
public static class NetProtocol
{
    /// <summary>Muda quando o formato muda: host e convidado precisam da mesma.</summary>
    public const ushort Version = 1;

    /// <summary>Teto de um id de texto (classe, sala) em bytes UTF-8.</summary>
    public const int MaxIdBytes = 64;

    public static NetMessageKind? KindOf(ReadOnlySpan<byte> packet) =>
        packet.Length == 0 || !Enum.IsDefined(typeof(NetMessageKind), packet[0]) ? null : (NetMessageKind)packet[0];

    public static byte[] WriteHello(HelloMessage hello)
    {
        var w = new NetWriter();
        w.Byte((byte)NetMessageKind.Hello);
        w.UShort(hello.Version);
        w.Id(hello.ClassId);
        return w.ToArray();
    }

    public static bool TryReadHello(ReadOnlySpan<byte> packet, out HelloMessage hello)
    {
        hello = default;
        var r = new NetReader(packet);
        if (r.Byte() != (byte)NetMessageKind.Hello || !r.TryUShort(out var version) || !r.TryId(out var classId) || !r.AtEnd)
        {
            return false;
        }

        hello = new HelloMessage(version, classId);
        return true;
    }

    /// <summary>O host decide se aceita: mesma versão, vaga livre e classe da tela de entrada.</summary>
    public static RejectReason? Validate(HelloMessage hello, int playersNow, int maxPlayers, IReadOnlyCollection<string> classIds)
    {
        if (hello.Version != Version)
        {
            return RejectReason.Version;
        }

        if (playersNow >= maxPlayers)
        {
            return RejectReason.Full;
        }

        return classIds.Contains(hello.ClassId) ? null : RejectReason.BadClass;
    }

    public static byte[] WriteWelcome(byte slot) => [(byte)NetMessageKind.Welcome, slot];

    public static bool TryReadWelcome(ReadOnlySpan<byte> packet, out byte slot)
    {
        slot = 0;
        if (packet.Length != 2 || packet[0] != (byte)NetMessageKind.Welcome || packet[1] == 0 || packet[1] >= 8)
        {
            return false;
        }

        slot = packet[1];
        return true;
    }

    public static byte[] WriteReject(RejectReason reason) => [(byte)NetMessageKind.Reject, (byte)reason];

    public static bool TryReadReject(ReadOnlySpan<byte> packet, out RejectReason reason)
    {
        reason = RejectReason.Malformed;
        if (packet.Length != 2 || packet[0] != (byte)NetMessageKind.Reject || !Enum.IsDefined(typeof(RejectReason), packet[1]))
        {
            return false;
        }

        reason = (RejectReason)packet[1];
        return true;
    }

    /// <summary>Texto do aviso no convidado (pt-BR).</summary>
    public static string Describe(RejectReason reason) => reason switch
    {
        RejectReason.Version => "O host está numa versão diferente do jogo.",
        RejectReason.Full => "O grupo do host já está cheio.",
        RejectReason.BadClass => "Classe inválida.",
        _ => "O host recusou a entrada.",
    };

    public static byte[] WriteCommand(PlayerCommand command)
    {
        var packet = new byte[1 + PlayerCommand.WireSize];
        packet[0] = (byte)NetMessageKind.Command;
        command.Write(packet.AsSpan(1));
        return packet;
    }

    /// <summary>Lê um comando e força o slot do par que mandou (um convidado nunca comanda outro caçador).</summary>
    public static bool TryReadCommand(ReadOnlySpan<byte> packet, byte senderSlot, out PlayerCommand command)
    {
        command = default;
        if (packet.Length != 1 + PlayerCommand.WireSize || packet[0] != (byte)NetMessageKind.Command)
        {
            return false;
        }

        command = (PlayerCommand.Read(packet[1..]) with { Slot = senderSlot }).Normalized();
        return true;
    }

    /// <summary>Porta digitada na tela "Entrar": número em 1024..65535.</summary>
    public static bool TryParsePort(string? text, out int port) =>
        int.TryParse(text?.Trim(), out port) && port is >= 1024 and <= 65535;

    /// <summary>Endereço digitado: IPv4/IPv6 literal ou nome simples (letras, dígitos, ponto, hífen), até 253 caracteres.</summary>
    public static bool IsValidAddress(string? text)
    {
        var t = text?.Trim() ?? string.Empty;
        if (t.Length is 0 or > 253)
        {
            return false;
        }

        if (System.Net.IPAddress.TryParse(t, out _))
        {
            return true;
        }

        return t.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-') && !t.StartsWith('-') && !t.StartsWith('.');
    }

    internal static bool IsValidId(string id) =>
        id.Length > 0 && Encoding.UTF8.GetByteCount(id) <= MaxIdBytes && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '/' or '.');
}

/// <summary>Escreve little-endian num buffer que cresce.</summary>
internal sealed class NetWriter
{
    private readonly List<byte> _bytes = new(256);

    public void Byte(byte value) => _bytes.Add(value);

    public void UShort(ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        _bytes.AddRange(b.ToArray());
    }

    public void UInt(uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        _bytes.AddRange(b.ToArray());
    }

    public void Int(int value) => UInt(unchecked((uint)value));

    public void Float(float value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, float.IsFinite(value) ? value : 0f);
        _bytes.AddRange(b.ToArray());
    }

    public void Id(string id)
    {
        if (!NetProtocol.IsValidId(id))
        {
            throw new ArgumentException($"id inválido para a rede: '{id}'", nameof(id));
        }

        var bytes = Encoding.UTF8.GetBytes(id);
        Byte((byte)bytes.Length);
        _bytes.AddRange(bytes);
    }

    public byte[] ToArray() => [.. _bytes];
}

/// <summary>Lê little-endian com checagem de limites (nada lança: leitura além do fim marca falha).</summary>
internal ref struct NetReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _pos;

    public bool Failed { get; private set; }

    public readonly bool AtEnd => !Failed && _pos == _data.Length;

    public byte Byte()
    {
        if (!Has(1))
        {
            return 0;
        }

        return _data[_pos++];
    }

    public bool TryUShort(out ushort value)
    {
        value = 0;
        if (!Has(2))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(_data[_pos..]);
        _pos += 2;
        return true;
    }

    public bool TryUInt(out uint value)
    {
        value = 0;
        if (!Has(4))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(_data[_pos..]);
        _pos += 4;
        return true;
    }

    public bool TryInt(out int value)
    {
        var ok = TryUInt(out var u);
        value = unchecked((int)u);
        return ok;
    }

    /// <summary>Float finito (NaN/infinito no pacote = pacote inválido).</summary>
    public bool TryFloat(out float value)
    {
        value = 0f;
        if (!Has(4))
        {
            return false;
        }

        value = BinaryPrimitives.ReadSingleLittleEndian(_data[_pos..]);
        _pos += 4;
        if (!float.IsFinite(value))
        {
            Failed = true;
            return false;
        }

        return true;
    }

    public bool TryId(out string id)
    {
        id = string.Empty;
        var length = Byte();
        if (Failed || length == 0 || length > NetProtocol.MaxIdBytes || !Has(length))
        {
            Failed = true;
            return false;
        }

        id = Encoding.UTF8.GetString(_data.Slice(_pos, length));
        _pos += length;
        if (!NetProtocol.IsValidId(id))
        {
            Failed = true;
            return false;
        }

        return true;
    }

    private bool Has(int count)
    {
        if (Failed || _pos + count > _data.Length)
        {
            Failed = true;
            return false;
        }

        return true;
    }
}
