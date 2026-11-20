namespace Vespera.Tests.Core.Net;

using GdUnit4;
using Vespera.Core.Net;
using static GdUnit4.Assertions;

/// <summary>Autenticação e validação da entrada no online: código da sala, versão, bloqueio por tentativas, ids e tamanhos.</summary>
[TestSuite]
public class JoinGateTest
{
    private static readonly string[] Classes = ["sword", "bow", "hammer"];

    private static HelloMessage Hello(string code = "", string cls = "bow", ushort? version = null) =>
        new(version ?? NetProtocol.Version, cls, code);

    [TestCase]
    public void OpenRoomAcceptsWithoutCode()
    {
        var gate = new JoinGate(string.Empty);
        AssertThat(gate.RequiresCode).IsFalse();
        AssertThat(gate.Evaluate(Hello(), 1, 2, Classes, 0) is null).IsTrue();
    }

    [TestCase]
    public void RoomWithCodeRequiresTheRightCode()
    {
        var gate = new JoinGate("4821");
        AssertThat(gate.Evaluate(Hello(), 1, 2, Classes, 0) == RejectReason.BadCode).IsTrue();
        AssertThat(gate.Evaluate(Hello("1234"), 1, 2, Classes, 0) == RejectReason.BadCode).IsTrue();
        AssertThat(gate.Evaluate(Hello("4821"), 1, 2, Classes, 0) is null).IsTrue();
    }

    [TestCase]
    public void WrongVersionIsRefusedBeforeTheCode()
    {
        var gate = new JoinGate("4821");
        AssertThat(gate.Evaluate(Hello("0000", version: 1), 1, 2, Classes, 0) == RejectReason.Version).IsTrue();
        AssertThat(gate.BadCodes).IsEqual(0);
    }

    [TestCase]
    public void FullGroupAndBadClassStillApplyAfterTheCode()
    {
        var gate = new JoinGate("4821");
        AssertThat(gate.Evaluate(Hello("4821"), 2, 2, Classes, 0) == RejectReason.Full).IsTrue();
        AssertThat(gate.Evaluate(Hello("4821", "lança"), 1, 2, Classes, 0) == RejectReason.BadClass).IsTrue();
        // Quem não sabe o código não descobre se o grupo está cheio.
        AssertThat(gate.Evaluate(Hello("0000"), 2, 2, Classes, 0) == RejectReason.BadCode).IsTrue();
    }

    [TestCase]
    public void TooManyWrongCodesLockTheRoomUntilTheTimeoutPasses()
    {
        var gate = new JoinGate("4821", maxBadCodes: 3, lockSeconds: 30);
        for (var i = 0; i < 3; i++)
        {
            AssertThat(gate.Evaluate(Hello("0000"), 1, 2, Classes, 10) == RejectReason.BadCode).IsTrue();
        }

        // Bloqueado: nem o código certo entra agora.
        AssertThat(gate.IsLocked(11)).IsTrue();
        AssertThat(gate.Evaluate(Hello("4821"), 1, 2, Classes, 11) == RejectReason.Locked).IsTrue();
        AssertThat(gate.Evaluate(Hello("4821"), 1, 2, Classes, 39.9) == RejectReason.Locked).IsTrue();
        AssertThat(gate.Evaluate(Hello("4821"), 1, 2, Classes, 40.1) is null).IsTrue();
    }

    [TestCase]
    public void RightCodeResetsTheWrongAttemptCounter()
    {
        var gate = new JoinGate("4821", maxBadCodes: 3);
        gate.Evaluate(Hello("0000"), 1, 2, Classes, 0);
        gate.Evaluate(Hello("0000"), 1, 2, Classes, 0);
        AssertThat(gate.BadCodes).IsEqual(2);
        gate.Evaluate(Hello("4821"), 1, 2, Classes, 0);
        AssertThat(gate.BadCodes).IsEqual(0);
    }

    [TestCase]
    public void CodeValidationAcceptsOnlyFourToSixDigits()
    {
        AssertThat(NetProtocol.IsValidCode(string.Empty)).IsTrue();
        AssertThat(NetProtocol.IsValidCode("1234")).IsTrue();
        AssertThat(NetProtocol.IsValidCode("123456")).IsTrue();
        AssertThat(NetProtocol.IsValidCode("123")).IsFalse();
        AssertThat(NetProtocol.IsValidCode("1234567")).IsFalse();
        AssertThat(NetProtocol.IsValidCode("12a4")).IsFalse();
        AssertThat(NetProtocol.IsValidCode("١٢٣٤")).IsFalse();
        AssertThat(NetProtocol.IsValidCode(null)).IsFalse();
        AssertThat(RoomCodes.Generate().Length).IsEqual(4);
        AssertThat(NetProtocol.IsValidCode(RoomCodes.Generate())).IsTrue();
    }

    [TestCase]
    public void HelloCarriesTheCodeAndRejectsMalformedOnes()
    {
        var packet = NetProtocol.WriteHello(Hello("4821", "hammer"));
        AssertThat(NetProtocol.TryReadHello(packet, out var back)).IsTrue();
        AssertThat(back.Code).IsEqual("4821");
        AssertThat(back.ClassId).IsEqual("hammer");
        AssertThat(packet.Length).IsLessEqual(NetProtocol.MaxHelloBytes);

        // Código com letra, curto demais e tamanho declarado maior que o pacote.
        var bad = NetProtocol.WriteHello(Hello(string.Empty));
        AssertThat(NetProtocol.TryReadHello([.. bad[..^1], 3, (byte)'1', (byte)'2', (byte)'x'], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadHello([.. bad[..^1], 2, (byte)'1', (byte)'2'], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadHello([.. bad[..^1], 6, (byte)'1'], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadHello([.. bad[..^1], 9, 49, 49, 49, 49, 49, 49, 49, 49, 49], out _)).IsFalse();
    }

    [TestCase]
    public void IdsCannotCarryPathsOrTraversal()
    {
        foreach (var bad in new[] { "..", "../x", "a/b", "a\b", "/etc", ".hidden", "a..b", "a.json", "sala 1", "", "x\0y", "praça" })
        {
            AssertThat(NetProtocol.IsValidId(bad)).IsFalse();
        }

        foreach (var good in new[] { "praca", "esgoto_arena", "cao-de-vigilia", "Lodo2" })
        {
            AssertThat(NetProtocol.IsValidId(good)).IsTrue();
        }

        // Um snapshot com id de sala perigoso é recusado na leitura.
        var packet = new WorldSnapshot(1, "praca", [], []).Write();
        var evil = packet.ToList();
        var index = evil.IndexOf((byte)'p');
        evil[index] = (byte)'.';
        evil[index + 1] = (byte)'.';
        AssertThat(WorldSnapshot.TryRead([.. evil], out _)).IsFalse();
    }

    [TestCase]
    public void PacketSizesAreBoundedPerKind()
    {
        AssertThat(NetProtocol.KindOfBounded(NetProtocol.WriteHello(Hello("4821"))) == NetMessageKind.Hello).IsTrue();
        AssertThat(NetProtocol.KindOfBounded(new byte[NetProtocol.MaxHelloBytes + 1].Select((_, i) => i == 0 ? (byte)NetMessageKind.Hello : (byte)0).ToArray()) is null).IsTrue();
        AssertThat(NetProtocol.KindOfBounded(NetProtocol.WriteCommand(default)) == NetMessageKind.Command).IsTrue();
        AssertThat(NetProtocol.KindOfBounded([.. NetProtocol.WriteCommand(default), 0]) is null).IsTrue();
        AssertThat(NetProtocol.KindOfBounded([(byte)NetMessageKind.Welcome, 1, 0]) is null).IsTrue();
        AssertThat(NetProtocol.KindOfBounded([]) is null).IsTrue();
        AssertThat(NetProtocol.KindOfBounded([99, 1]) is null).IsTrue();
        var huge = new byte[NetProtocol.MaxSnapshotBytes + 1];
        huge[0] = (byte)NetMessageKind.Snapshot;
        AssertThat(NetProtocol.KindOfBounded(huge) is null).IsTrue();
    }

    [TestCase]
    public void RejectionTextsAreInPortuguese()
    {
        foreach (var reason in Enum.GetValues<RejectReason>())
        {
            var text = NetProtocol.Describe(reason);
            AssertThat(text.Length > 10 && text.EndsWith('.')).IsTrue();
        }

        AssertThat(NetProtocol.Describe(RejectReason.BadCode)).Contains("Código da sala");
        AssertThat(NetProtocol.Describe(RejectReason.Full)).Contains("cheio");
    }
}
