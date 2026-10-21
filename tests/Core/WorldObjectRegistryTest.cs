namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class WorldObjectRegistryTest
{
    [TestCase]
    public void SpentObjectsStaySpentUntilTheNextDay()
    {
        var reg = new WorldObjectRegistry();
        var key = WorldObjectRegistry.Key("forge", "prop", 2);
        AssertThat(key).IsEqual("forge:prop:2");
        AssertThat(reg.IsSpent(key, day: 1, time: 0)).IsFalse();
        reg.MarkSpent(key, day: 1, time: 10);
        AssertThat(reg.IsSpent(key, 1, 500)).IsTrue();
        AssertThat(reg.IsSpent(key, 2, 500)).IsFalse(); // outro dia
        reg.NewDay();
        AssertThat(reg.IsSpent(key, 1, 500)).IsFalse();
        AssertThat(reg.SpentCount).IsEqual(0);
    }

    [TestCase]
    public void FlowersRegrowAfterTheirTime()
    {
        var reg = new WorldObjectRegistry();
        var key = WorldObjectRegistry.Key("forge", "flower", 0);
        reg.MarkSpent(key, 3, 100);
        AssertThat(reg.IsSpent(key, 3, 150, regrowSeconds: 90)).IsTrue();
        AssertThat(reg.RemainingSeconds(key, 3, 150, 90)).IsEqual(40.0);
        AssertThat(reg.IsSpent(key, 3, 191, regrowSeconds: 90)).IsFalse();
    }

    [TestCase]
    public void FlagsSurviveTheNewDay()
    {
        var reg = new WorldObjectRegistry();
        var key = WorldObjectRegistry.Key("forge", "lamp", 0);
        AssertThat(reg.GetFlag(key, true)).IsTrue();
        reg.SetFlag(key, false);
        reg.NewDay();
        AssertThat(reg.GetFlag(key, true)).IsFalse();
    }
}
