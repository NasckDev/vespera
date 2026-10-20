namespace Vespera.Core.Audio;

/// <summary>
/// Escolhe qual voz de um pool fixo toca o próximo som, sem alocar. Regras, em ordem:
/// 1. se o id já tem <c>maxPerId</c> vozes tocando, rouba a mais antiga desse id (muitos acertos não estouram);
/// 2. senão usa uma voz livre;
/// 3. senão rouba a voz mais antiga do pool.
/// Quem toca o som informa se cada voz ainda está ocupada (<c>isBusy</c>), então o estado real do player manda.
/// </summary>
public sealed class VoiceAllocator
{
    private readonly string?[] _ids;
    private readonly long[] _startedAt;
    private long _clock;

    public VoiceAllocator(int voiceCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voiceCount);
        _ids = new string?[voiceCount];
        _startedAt = new long[voiceCount];
    }

    public int Count => _ids.Length;

    /// <summary>Id do último som atribuído à voz (pode já ter terminado).</summary>
    public string? IdOf(int voice) => _ids[voice];

    /// <summary>Reserva uma voz para <paramref name="id"/> e devolve o índice dela.</summary>
    public int Acquire(string id, int maxPerId, Func<int, bool> isBusy)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPerId);

        var sameIdCount = 0;
        var oldestSameId = -1;
        var firstFree = -1;
        var oldestAny = -1;
        for (var i = 0; i < _ids.Length; i++)
        {
            if (!isBusy(i))
            {
                if (firstFree < 0)
                {
                    firstFree = i;
                }

                continue;
            }

            if (oldestAny < 0 || _startedAt[i] < _startedAt[oldestAny])
            {
                oldestAny = i;
            }

            if (_ids[i] == id)
            {
                sameIdCount++;
                if (oldestSameId < 0 || _startedAt[i] < _startedAt[oldestSameId])
                {
                    oldestSameId = i;
                }
            }
        }

        int chosen;
        if (sameIdCount >= maxPerId)
        {
            chosen = oldestSameId;
        }
        else if (firstFree >= 0)
        {
            chosen = firstFree;
        }
        else
        {
            chosen = oldestAny < 0 ? 0 : oldestAny;
        }

        _ids[chosen] = id;
        _startedAt[chosen] = ++_clock;
        return chosen;
    }
}
