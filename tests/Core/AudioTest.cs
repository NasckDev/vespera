namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Audio;
using static GdUnit4.Assertions;

[TestSuite]
public class AudioTest
{
    [TestCase]
    public void FreeVoicesAreUsedInOrder()
    {
        var pool = new VoiceAllocator(3);
        var busy = new bool[3];
        Func<int, bool> isBusy = i => busy[i];

        var a = pool.Acquire("hit", 4, isBusy);
        busy[a] = true;
        var b = pool.Acquire("hit", 4, isBusy);
        busy[b] = true;

        AssertThat(a).IsEqual(0);
        AssertThat(b).IsEqual(1);
    }

    [TestCase]
    public void LimitPerIdStealsOldestOfSameId()
    {
        var pool = new VoiceAllocator(6);
        var busy = new bool[6];
        Func<int, bool> isBusy = i => busy[i];

        var first = pool.Acquire("hit", 2, isBusy);
        busy[first] = true;
        busy[pool.Acquire("swing", 2, isBusy)] = true;
        var second = pool.Acquire("hit", 2, isBusy);
        busy[second] = true;

        // terceiro "hit" com limite 2: reaproveita o hit mais antigo, mesmo com vozes livres
        var third = pool.Acquire("hit", 2, isBusy);
        AssertThat(third).IsEqual(first);
        AssertThat(pool.IdOf(third)).IsEqual("hit");

        // e o próximo rouba o outro hit (agora o mais antigo)
        AssertThat(pool.Acquire("hit", 2, isBusy)).IsEqual(second);
    }

    [TestCase]
    public void FullPoolStealsOldestVoice()
    {
        var pool = new VoiceAllocator(2);
        var busy = new bool[2];
        Func<int, bool> isBusy = i => busy[i];

        busy[pool.Acquire("a", 4, isBusy)] = true;
        busy[pool.Acquire("b", 4, isBusy)] = true;

        AssertThat(pool.Acquire("c", 4, isBusy)).IsEqual(0);
        AssertThat(pool.Acquire("d", 4, isBusy)).IsEqual(1);
    }

    [TestCase]
    public void FinishedVoicesAreReused()
    {
        var pool = new VoiceAllocator(2);
        var busy = new bool[2];
        Func<int, bool> isBusy = i => busy[i];

        busy[pool.Acquire("a", 4, isBusy)] = true;
        busy[pool.Acquire("b", 4, isBusy)] = true;
        busy[0] = false; // "a" terminou

        AssertThat(pool.Acquire("c", 4, isBusy)).IsEqual(0);
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentOutOfRangeException))]
    public void RejectsEmptyPool() => _ = new VoiceAllocator(0);

    [TestCase(1f, 0f)]
    [TestCase(0f, AudioVolume.SilenceDb)]
    [TestCase(-1f, AudioVolume.SilenceDb)]
    [TestCase(2f, 0f)]
    [TestCase(0.0001f, AudioVolume.SilenceDb)]
    public void ToDbClampsRange(float linear, float expected)
        => AssertThat(AudioVolume.ToDb(linear)).IsEqualApprox(expected, 0.001f);

    [TestCase]
    public void HalfVolumeIsMinusSixDb()
        => AssertThat(AudioVolume.ToDb(0.5f)).IsEqualApprox(-6.0206f, 0.001f);

    [TestCase]
    public void Clamp01HandlesNaN()
        => AssertThat(AudioVolume.Clamp01(float.NaN)).IsEqual(1f);
}
