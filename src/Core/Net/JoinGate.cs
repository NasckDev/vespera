using System.Security.Cryptography;
using System.Text;

namespace Vespera.Core.Net;

/// <summary>
/// Porteiro da sala do host (puro, sem Godot): decide se um <see cref="HelloMessage"/> entra. Confere a versão, o código da
/// sala (se o host definiu um), segura quem erra o código muitas vezes e só então vê vaga e classe
/// (<see cref="NetProtocol.Validate"/>). O código é comparado em tempo constante. A conexão não é criptografada: o código
/// barra curiosos e tentativas às cegas, não quem escuta a rede.
/// </summary>
public sealed class JoinGate
{
    public const int DefaultMaxBadCodes = 5;
    public const double DefaultLockSeconds = 30;

    private readonly byte[] _code;
    private readonly int _maxBadCodes;
    private readonly double _lockSeconds;
    private double _lockedUntil = double.NegativeInfinity;

    public JoinGate(string code, int maxBadCodes = DefaultMaxBadCodes, double lockSeconds = DefaultLockSeconds)
    {
        if (!NetProtocol.IsValidCode(code))
        {
            throw new ArgumentException("o código da sala deve ter de 4 a 6 dígitos (ou ficar vazio)", nameof(code));
        }

        Code = code;
        _code = Encoding.ASCII.GetBytes(code);
        _maxBadCodes = Math.Max(1, maxBadCodes);
        _lockSeconds = Math.Max(0, lockSeconds);
    }

    /// <summary>Código da sala ("" = sala aberta, entra sem código).</summary>
    public string Code { get; }

    public bool RequiresCode => Code.Length > 0;

    /// <summary>Códigos errados seguidos desde o último acerto/bloqueio.</summary>
    public int BadCodes { get; private set; }

    public bool IsLocked(double now) => now < _lockedUntil;

    /// <summary>Null = pode entrar; senão o motivo da recusa. <paramref name="now"/> em segundos (relógio do host).</summary>
    public RejectReason? Evaluate(HelloMessage hello, int playersNow, int maxPlayers, IReadOnlyCollection<string> classIds, double now)
    {
        if (hello.Version != NetProtocol.Version)
        {
            return RejectReason.Version;
        }

        if (RequiresCode)
        {
            if (IsLocked(now))
            {
                return RejectReason.Locked;
            }

            if (!CodeMatches(hello.Code))
            {
                BadCodes++;
                if (BadCodes >= _maxBadCodes)
                {
                    BadCodes = 0;
                    _lockedUntil = now + _lockSeconds;
                }

                return RejectReason.BadCode;
            }

            BadCodes = 0;
        }

        return NetProtocol.Validate(hello, playersNow, maxPlayers, classIds);
    }

    private bool CodeMatches(string attempt) =>
        CryptographicOperations.FixedTimeEquals(_code, Encoding.ASCII.GetBytes(attempt ?? string.Empty));
}

/// <summary>Código de sala sorteado (4 dígitos) para o host não precisar inventar um.</summary>
public static class RoomCodes
{
    public static string Generate() => RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
}
