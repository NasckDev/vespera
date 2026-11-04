using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;

namespace Vespera.Tests.Game;

/// <summary>
/// Brecha B das Ruas Baixas (ROTEIRO-V2 §8.1, Ravi em perigo): jogável no Ato IV, caçada curta cuja sala 3 é a Cozinha
/// Velha (onde fica a Lanterna do Ravi se a Brecha rompe); fechar antes de romper é a rota A.
/// </summary>
[TestSuite]
public class RavisBreachTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_ruas_b.json")));

    private static StoryEngine Engine() => new(new StoryProgress(), StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json"))));

    [TestCase]
    public void BrechaBIsRankBInTheStreetsHub()
    {
        var def = BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), "brecha_ruas_b.json")));
        AssertThat(def.Id).IsEqual(RaviRescue.BrechaId);
        AssertThat(def.Rank).IsEqual("B");
        AssertThat(def.Hub).IsEqual("ruas_baixas");
        AssertThat(def.NeighborhoodId).IsNotEqual("ruas_baixas");
    }

    [TestCase]
    public void EveryRunHasTheKitchenAsRoomThreeAndNoGuardian()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 50; seed++)
        {
            foreach (var plan in new[] { planner.Plan(seed, 0, false), planner.Plan(seed, 3, false) })
            {
                AssertThat(plan.Rooms.Count).IsEqual(4);
                AssertThat(plan.Rooms[2].Ground).IsEqual(RaviRescue.LanternRoom);
                AssertThat(plan.Rooms[2].Kind).IsEqual(RoomKind.Rest);
                AssertThat(plan.HasGuardian).IsFalse();
            }
        }
    }

    [TestCase]
    public void ClosingBeforeTheRuptureIsRouteA()
    {
        var engine = Engine();
        engine.SetFlag("ruas_baixas_b:hunt_1");
        AssertThat(engine.HasFlag(RaviRescue.ClosedInTime)).IsTrue();
        AssertThat(RaviRescue.Route(engine) == 'A').IsTrue();
    }

    [TestCase]
    public void AfterTheRuptureOnlyTheLanternDecides()
    {
        var engine = Engine();
        engine.SetFlag(RaviRescue.Ruptured);
        engine.SetFlag("ruas_baixas_b:hunt_1");
        AssertThat(engine.HasFlag(RaviRescue.ClosedInTime)).IsFalse();
        AssertThat(RaviRescue.LanternWaiting(RaviRescue.BrechaId, RaviRescue.LanternRoom, engine)).IsTrue();

        var runner = new DialogueRunner(DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "ato4", "lanterna_ravi.json"))), engine, "Rui");
        AssertThat(runner.Start()).IsTrue();
        while (runner.Advance())
        {
        }

        AssertThat(engine.HasFlag(RaviRescue.LanternRecovered)).IsTrue();
        AssertThat(RaviRescue.Route(engine) == 'B').IsTrue();
        AssertThat(RaviRescue.LanternWaiting(RaviRescue.BrechaId, RaviRescue.LanternRoom, engine)).IsFalse();
    }

    [TestCase]
    public void AlternativesInConditionsNeedOnlyOneSide()
    {
        var engine = Engine();
        AssertThat(StoryConditions.Evaluate(["GuardianDefeated:sineiro_mudo|ruas_brecha_aberta"], engine)).IsFalse();
        engine.SetFlag("ruas_brecha_aberta");
        AssertThat(StoryConditions.Evaluate(["GuardianDefeated:sineiro_mudo|ruas_brecha_aberta"], engine)).IsTrue();
        AssertThat(StoryConditions.Evaluate(["nada|!ruas_brecha_aberta"], engine)).IsFalse();
    }
}
