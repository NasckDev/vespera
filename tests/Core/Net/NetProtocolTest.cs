namespace Vespera.Tests.Core.Net;

using GdUnit4;
using Vespera.Core.Coop;
using Vespera.Core.Net;
using static GdUnit4.Assertions;

/// <summary>Protocolo do co-op online: entrada, comando, snapshot, fila de comandos do host e interpolação do convidado.</summary>
[TestSuite]
public class NetProtocolTest
{
    private static readonly string[] Classes = ["sword", "bow", "hammer"];

    [TestCase]
    public void HelloRoundTripsAndHostValidates()
    {
        var packet = NetProtocol.WriteHello(new HelloMessage(NetProtocol.Version, "bow"));
        AssertThat(NetProtocol.KindOf(packet) == NetMessageKind.Hello).IsTrue();
        AssertThat(NetProtocol.TryReadHello(packet, out var hello)).IsTrue();
        AssertThat(hello.ClassId).IsEqual("bow");
        AssertThat(NetProtocol.Validate(hello, 1, 2, Classes) is null).IsTrue();
        AssertThat(NetProtocol.Validate(hello, 2, 2, Classes) == RejectReason.Full).IsTrue();
        AssertThat(NetProtocol.Validate(hello with { Version = 99 }, 1, 2, Classes) == RejectReason.Version).IsTrue();
        AssertThat(NetProtocol.Validate(hello with { ClassId = "lança" }, 1, 2, Classes) == RejectReason.BadClass).IsTrue();
    }

    [TestCase]
    public void MalformedPacketsAreRejectedWithoutThrowing()
    {
        var hello = NetProtocol.WriteHello(new HelloMessage(NetProtocol.Version, "sword"));
        AssertThat(NetProtocol.TryReadHello(hello[..^1], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadHello([.. hello, 7], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadHello([], out _)).IsFalse();
        AssertThat(NetProtocol.KindOf([200]) is null).IsTrue();
        AssertThat(NetProtocol.TryReadWelcome([(byte)NetMessageKind.Welcome, 0], out _)).IsFalse();
        AssertThat(NetProtocol.TryReadWelcome(NetProtocol.WriteWelcome(1), out var slot) && slot == 1).IsTrue();
        AssertThat(NetProtocol.TryReadReject(NetProtocol.WriteReject(RejectReason.Full), out var reason) && reason == RejectReason.Full).IsTrue();
        AssertThat(WorldSnapshot.TryRead([(byte)NetMessageKind.Snapshot, 1, 2], out _)).IsFalse();
    }

    [TestCase]
    public void CommandKeepsTheSenderSlot()
    {
        var packet = NetProtocol.WriteCommand(new PlayerCommand(10, 0, 1f, 1f, CommandButtons.Attack, CommandButtons.Attack));
        AssertThat(NetProtocol.TryReadCommand(packet, 1, out var cmd)).IsTrue();
        AssertThat((int)cmd.Slot).IsEqual(1);
        AssertThat(cmd.WasPressed(CommandButtons.Attack)).IsTrue();
        AssertThat(MathF.Sqrt((cmd.MoveX * cmd.MoveX) + (cmd.MoveY * cmd.MoveY))).IsLessEqual(1.0005f);
        AssertThat(NetProtocol.TryReadCommand(packet[..5], 1, out _)).IsFalse();
    }

    [TestCase]
    public void SnapshotRoundTripsAndRejectsBadValues()
    {
        var snap = new WorldSnapshot(77, "praca", [new NetHunter(0, 10, 20, 2, false, 50, 100), new NetHunter(1, -5, 3, 3, true, 0, 80)],
            [new NetEnemy(5, 1, 2, 30, 45)], "J2 · conversando");
        AssertThat(WorldSnapshot.TryRead(snap.Write(), out var back)).IsTrue();
        AssertThat(back.Tick).IsEqual(77u);
        AssertThat(back.LevelId).IsEqual("praca");
        AssertThat(back.Hunters.Count).IsEqual(2);
        AssertThat(back.Hunter(1)!.Value.Downed).IsTrue();
        AssertThat(back.Enemies[0].Id).IsEqual(5);
        AssertThat(back.Dialogue).IsEqual("J2 · conversando");

        var tooMuchHp = snap with { Enemies = [new NetEnemy(5, 1, 2, 99, 45)] };
        AssertThat(WorldSnapshot.TryRead(tooMuchHp.Write(), out _)).IsFalse();
        var twin = snap with { Hunters = [new NetHunter(0, 0, 0, 0, false, 1, 1), new NetHunter(0, 0, 0, 0, false, 1, 1)] };
        AssertThat(WorldSnapshot.TryRead(twin.Write(), out _)).IsFalse();
        var bytes = snap.Write();
        AssertThat(WorldSnapshot.TryRead(bytes[..^3], out _)).IsFalse();
    }

    [TestCase]
    public void QueueMergesBurstsRepeatsDirectionAndDropsStale()
    {
        var q = new CommandQueue(1);
        AssertThat(q.Push(new PlayerCommand(1, 0, 1f, 0f, CommandButtons.None, CommandButtons.Attack))).IsTrue();
        AssertThat(q.Push(new PlayerCommand(2, 0, 0f, 1f, CommandButtons.None, CommandButtons.None))).IsTrue();
        AssertThat(q.Push(new PlayerCommand(2, 0, -1f, 0f, CommandButtons.None, CommandButtons.Dodge))).IsFalse();
        var first = q.Next(500);
        AssertThat(first.WasPressed(CommandButtons.Attack)).IsTrue();
        AssertThat(first.MoveY).IsEqual(1f);
        AssertThat((int)first.Slot).IsEqual(1);
        AssertThat(first.Tick).IsEqual(500u);
        var repeat = q.Next(501);
        AssertThat(repeat.MoveY).IsEqual(1f);
        AssertThat(repeat.Pressed == CommandButtons.None).IsTrue();
        AssertThat(q.Dropped).IsEqual(1);
    }

    [TestCase]
    public void QueueStaysBoundedWithoutLosingPresses()
    {
        var q = new CommandQueue(1, 4);
        q.Push(new PlayerCommand(1, 1, 0, 0, CommandButtons.None, CommandButtons.Skill));
        for (uint t = 2; t <= 10; t++)
        {
            q.Push(new PlayerCommand(t, 1, 0, 0, CommandButtons.None, CommandButtons.None));
        }

        AssertThat(q.Pending).IsLessEqual(4);
        AssertThat(q.Next(1).WasPressed(CommandButtons.Skill)).IsTrue();
    }

    [TestCase]
    public void InterpolatesBetweenSnapshotsAndResetsOnLevelChange()
    {
        var interp = new SnapshotInterpolator(0.1);
        interp.Add(1.0, Snap(1, "praca", 0f));
        interp.Add(1.05, Snap(2, "praca", 10f));
        AssertThat(interp.Add(1.06, Snap(1, "praca", 99f))).IsFalse();
        var mid = interp.HunterAt(1.125, 1)!.Value;
        AssertThat(mid.X).IsEqualApprox(5f, 0.01f);
        AssertThat(interp.HunterAt(2.0, 1)!.Value.X).IsEqual(10f);
        AssertThat(interp.EnemyAt(1.125, 9)!.Value.X).IsEqualApprox(5f, 0.01f);
        interp.Add(1.1, Snap(3, "esgoto_1", 300f));
        AssertThat(interp.Count).IsEqual(1);
        AssertThat(interp.HunterAt(1.11, 1)!.Value.X).IsEqual(300f);
        AssertThat(interp.EnemyAt(1.11, 42) is null).IsTrue();
    }

    [TestCase]
    public void AddressAndPortValidation()
    {
        AssertThat(NetProtocol.TryParsePort("24680", out var port) && port == 24680).IsTrue();
        AssertThat(NetProtocol.TryParsePort("80", out _)).IsFalse();
        AssertThat(NetProtocol.TryParsePort("abc", out _)).IsFalse();
        AssertThat(NetProtocol.IsValidAddress("127.0.0.1")).IsTrue();
        AssertThat(NetProtocol.IsValidAddress("::1")).IsTrue();
        AssertThat(NetProtocol.IsValidAddress("casa-do-ravi.local")).IsTrue();
        AssertThat(NetProtocol.IsValidAddress("1.2.3.4; rm")).IsFalse();
        AssertThat(NetProtocol.IsValidAddress("")).IsFalse();
    }

    [TestCase]
    public void OnlineDefaultsAndValidation()
    {
        var rules = CoopRules.Defaults;
        AssertThat(rules.Online.Port).IsEqual(24680);
        AssertThat(rules.Online.SnapshotHz).IsEqual(20);
        rules.Online.Port = 80;
        AssertThat(rules.Validate().Count).IsEqual(1);
    }

    private static WorldSnapshot Snap(uint tick, string level, float x) =>
        new(tick, level, [new NetHunter(1, x, 0, 0, false, 10, 10)], [new NetEnemy(9, x, 0, 5, 5)]);
}
