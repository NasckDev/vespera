namespace Vespera.Tests.Core.Net;

using GdUnit4;
using Vespera.Core.Coop;
using Vespera.Core.Net;
using static GdUnit4.Assertions;

/// <summary>Peças da sessão online sem rede: queda por silêncio, ritmo dos snapshots, ids dos monstros e comando remoto.</summary>
[TestSuite]
public class NetLinkTest
{
    [TestCase]
    public void LinkWatchTimesOutOnlyAfterSilence()
    {
        var watch = new LinkWatch(5);
        AssertThat(watch.TimedOut(100)).IsFalse();
        watch.Touch(10);
        AssertThat(watch.TimedOut(14.9)).IsFalse();
        AssertThat(watch.TimedOut(15.1)).IsTrue();
        watch.Touch(15.2);
        AssertThat(watch.TimedOut(16)).IsFalse();
    }

    [TestCase]
    public void PacerSendsAboutHzPerSecondWithoutBursts()
    {
        var pacer = new SnapshotPacer(20);
        var sent = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            if (pacer.Due(frame / 60.0))
            {
                sent++;
            }
        }

        AssertThat(sent).IsGreaterEqual(19);
        AssertThat(sent).IsLessEqual(21);

        // Engasgo de 1 s: manda um e volta ao ritmo, sem rajada.
        AssertThat(pacer.Due(3.0)).IsTrue();
        AssertThat(pacer.Due(3.001)).IsFalse();
        AssertThat(pacer.Due(3.06)).IsTrue();
    }

    [TestCase]
    public void IdMapIsStableAndForgetsTheDead()
    {
        var map = new NetIdMap();
        var a = map.IdOf(1000);
        var b = map.IdOf(2000);
        AssertThat(a).IsNotEqual(b);
        AssertThat(map.IdOf(1000)).IsEqual(a);
        map.Retain([2000]);
        AssertThat(map.Count).IsEqual(1);
        AssertThat(map.IdOf(1000)).IsNotEqual(a);
        AssertThat(map.IdOf(2000)).IsEqual(b);
    }

    [TestCase]
    public void RemoteCommandCannotPauseOrOpenMenusOnTheHost()
    {
        var all = CommandButtons.Attack | CommandButtons.Pause | CommandButtons.Inventory | CommandButtons.Journal | CommandButtons.Interact;
        var clean = RemoteCommands.Sanitize(new PlayerCommand(1, 1, 0.5f, 0f, all, all));
        AssertThat(clean.IsHeld(CommandButtons.Attack) && clean.WasPressed(CommandButtons.Interact)).IsTrue();
        AssertThat(clean.IsHeld(CommandButtons.Pause) || clean.WasPressed(CommandButtons.Pause)).IsFalse();
        AssertThat(clean.WasPressed(CommandButtons.Inventory) || clean.WasPressed(CommandButtons.Journal)).IsFalse();
        AssertThat(clean.MoveX).IsEqual(0.5f);
    }

    [TestCase]
    public void SnapshotCarriesHunterClassAndMonsterType()
    {
        var snap = new WorldSnapshot(3, "brecha_e_1", [new NetHunter(1, 1, 2, 0, false, 5, 10, 2)],
            [new NetEnemy(4, 1, 1, 3, 3, "lodo"), new NetEnemy(5, 2, 2, 1, 1)]);
        AssertThat(WorldSnapshot.TryRead(snap.Write(), out var back)).IsTrue();
        AssertThat((int)back.Hunters[0].Kind).IsEqual(2);
        AssertThat(back.Enemies[0].Type).IsEqual("lodo");
        AssertThat(back.Enemies[1].Type).IsEqual(string.Empty);

        var badKind = snap with { Hunters = [new NetHunter(1, 1, 2, 0, false, 5, 10, 9)] };
        AssertThat(WorldSnapshot.TryRead(badKind.Write(), out _)).IsFalse();
    }
}
