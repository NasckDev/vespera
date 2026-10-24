using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Ato III (Cais Afogado): caçadas só com chãos e monstros do cais, a sala alagada (lanterna "M.") em toda caçada,
/// Covil do Farol, portão do Guardião pela Odete, arena que inunda e o fio da história (§13).
/// </summary>
[TestSuite]
public class CaisHuntTest
{
    private static readonly string[] CaisMonsters = ["afogado", "ancoreiro", "lanterna_afogada"];

    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_cais.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vespera.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Vespera.sln não encontrado");
    }

    private static string DialogueDir => Path.Combine(RepoRoot(), "data", "dialogue");

    private static StoryEngine Engine() => new(new StoryProgress(), StoryScript.Parse(File.ReadAllText(Path.Combine(DialogueDir, "story.json"))));

    [TestCase]
    public void CaisRunsUseOnlyHarbourGroundsAndMonsters()
    {
        var planner = new RunPlanner(Config);
        var third = 0;
        for (var seed = 1; seed <= 100; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            AssertThat(plan.Rooms[^1].Ground).IsEqual("cais_arena");
            AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == "afogado")).IsTrue();
            foreach (var room in plan.Rooms)
            {
                AssertThat(room.Ground.StartsWith("cais_", StringComparison.Ordinal)).IsTrue();
                foreach (var spawn in room.Waves.SelectMany(w => w))
                {
                    AssertThat(CaisMonsters).Contains(spawn.Monster);
                    third += spawn.Monster == "lanterna_afogada" ? 1 : 0;
                }
            }
        }

        AssertThat(third).IsGreater(0);
    }

    [TestCase]
    public void FloodedMarketIsInEveryRunAndNeverRepeats()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 200; seed++)
        {
            foreach (var plan in new[] { planner.Plan(seed, 0, false), planner.Plan(seed, 2, false), planner.Plan(seed, 2, true) })
            {
                var grounds = plan.Rooms.Select(r => r.Ground).ToList();
                AssertThat(grounds).Contains("cais_mercado");
                for (var i = 1; i < grounds.Count; i++)
                {
                    AssertThat(grounds[i]).OverrideFailureMessage($"semente {seed}: sala repetida {grounds[i]}").IsNotEqual(grounds[i - 1]);
                }
            }
        }
    }

    [TestCase]
    public void OtherBreachesIgnoreTheNewFields()
    {
        var pomar = HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_pomar.json")));
        AssertThat(pomar.ThirdMonster).IsEqual(string.Empty);
        AssertThat(pomar.RequiredGround).IsEqual(string.Empty);
        AssertThat(pomar.GuardianFlag).IsEqual(string.Empty);
        AssertThat(Config.GuardianFlag).IsEqual("odete_lanterna_m");
    }

    [TestCase]
    public void GuardianRunEndsInTheLighthouseLair()
    {
        var plan = new RunPlanner(Config).Plan(7, 2, true);
        AssertThat(plan.HasGuardian).IsTrue();
        AssertThat(plan.Rooms[^1].Ground).IsEqual("cais_arena");
        AssertThat(plan.Rooms[^1].Kind).IsEqual(RoomKind.Guardian);
    }

    [TestCase]
    public void EverySpawnPointIsOnWalkableFloor()
    {
        foreach (var (id, ground) in Config.Grounds)
        {
            var level = Level(id);
            var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
            foreach (var p in ground.Spawns.Concat(ground.Breakables))
            {
                AssertThat(grid.IsBlockedAt(p[0], p[1])).OverrideFailureMessage($"{id}: ponto {p[0]},{p[1]} em parede").IsFalse();
            }
        }
    }

    [TestCase]
    public void LanternStoneAndArenaCenterAreWalkable()
    {
        var market = Level("cais_mercado");
        var grid = LevelGrid.Parse(market.Cell, market.Cols, market.Rows, market.Grid);
        AssertThat(grid.IsBlockedAt(837, 437)).IsFalse();
        var arena = Level("cais_arena");
        var arenaGrid = LevelGrid.Parse(arena.Cell, arena.Cols, arena.Rows, arena.Grid);
        var plan = new FloodPlan(836, 455, 325, 210, 500, 245, 150);
        for (var i = 0; i < 5; i++)
        {
            var (x, y) = plan.Island(i);
            AssertThat(arenaGrid.IsBlockedAt((float)x, (float)y)).OverrideFailureMessage($"ilha {i} ({x},{y}) em parede").IsFalse();
        }
    }

    [TestCase]
    public void ArenaFloodsByPhase()
    {
        var plan = new FloodPlan(836, 455, 325, 210, 500, 245, 150);
        var ring = (X: 836.0 + 420, Y: 455.0);
        AssertThat(plan.Flooded(ring.X, ring.Y, 1, 0)).IsFalse();
        AssertThat(plan.Flooded(ring.X, ring.Y, 2, 0)).IsTrue();
        AssertThat(plan.Flooded(836, 455, 2, 0)).IsFalse();
        AssertThat(plan.Flooded(836, 455, 3, 0)).IsFalse();
        AssertThat(plan.Flooded(836, 455, 3, 16)).IsTrue();
        var (ix, iy) = plan.Island(1);
        AssertThat(plan.Flooded(ix, iy, 3, 16)).IsFalse();
        AssertThat(plan.Flooded(5000, 5000, 3, 16)).IsFalse();
    }

    [TestCase]
    public void DryIslandMovesEveryFifteenSecondsWithWarning()
    {
        AssertThat(FloodPlan.SpotIndex(0)).IsEqual(0);
        AssertThat(FloodPlan.SpotIndex(14.9)).IsEqual(0);
        AssertThat(FloodPlan.SpotIndex(15)).IsEqual(1);
        AssertThat(FloodPlan.SpotIndex(75)).IsEqual(0);
        AssertThat(FloodPlan.WarnNow(13)).IsFalse();
        AssertThat(FloodPlan.WarnNow(13.6)).IsTrue();
        AssertThat(FloodPlan.WarnNow(15.2)).IsFalse();
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentException))]
    public void FloodPlanRejectsRingSmallerThanDryCenter() => _ = new FloodPlan(0, 0, 300, 200, 100, 100, 50);

    [TestCase]
    public void ActThreeStoryRunsFromSeveroToTheCemeteryHook()
    {
        var engine = Engine();
        engine.SetObjective("ato3");
        engine.SetFlag("met_severo");
        AssertThat(engine.ObjectiveId).IsEqual("ato3_pesca");
        engine.SetFlag("pescou");
        AssertThat(engine.ObjectiveId).IsEqual("ato3_exame");
        engine.SetObjective("ato3_brecha");
        engine.SetFlag("cais_afogado:hunt_1");
        AssertThat(engine.ObjectiveId).IsEqual("ato3_lanterna");
        engine.SetFlag("lanterna_m");
        AssertThat(engine.ObjectiveId).IsEqual("ato3_severo");
        engine.SetFlag("GuardianDefeated:farol_afogado");
        AssertThat(engine.ObjectiveId).IsEqual("ato3_fecho");
        AssertThat(engine.HasFlag("act3_done")).IsTrue();
        AssertThat(engine.Script.Objective("ato4")?.Locked == true).IsTrue();
    }

    [TestCase]
    public void EveryActThreeObjectiveAndDialogueExists()
    {
        var script = StoryScript.Parse(File.ReadAllText(Path.Combine(DialogueDir, "story.json")));
        foreach (var file in Directory.GetFiles(Path.Combine(DialogueDir, "ato3"), "*.json"))
        {
            var dialogue = DialogueFile.Parse(File.ReadAllText(file));
            foreach (var node in dialogue.Nodes.Values)
            {
                AssertThat(node.Text.Length).OverrideFailureMessage($"{file}: fala vazia").IsGreater(0);
                if (node.Objective is { Length: > 0 } objective)
                {
                    AssertThat(script.Objective(objective)).OverrideFailureMessage($"{file}: objetivo '{objective}' não existe").IsNotNull();
                }

                AssertThat(script.Speakers.ContainsKey(node.Speaker)).OverrideFailureMessage($"{file}: falante '{node.Speaker}' sem cadastro").IsTrue();
            }
        }

        foreach (var auto in script.Autoplay.Where(a => a.Dialogue.StartsWith("ato3/", StringComparison.Ordinal)))
        {
            AssertThat(File.Exists(Path.Combine(DialogueDir, auto.Dialogue + ".json"))).OverrideFailureMessage($"autoplay sem fala: {auto.Dialogue}").IsTrue();
        }
    }

    [TestCase]
    public void HubLevelsPointToTheActThreeDialogues()
    {
        var mares = Level("mares");
        var dialogues = mares.Zones.Select(z => z.Dialogue).Concat(mares.Npcs.Select(n => n.Dialogue)).Where(d => d.Length > 0);
        foreach (var id in dialogues)
        {
            AssertThat(File.Exists(Path.Combine(DialogueDir, id + ".json"))).OverrideFailureMessage($"mares: fala '{id}' não existe").IsTrue();
        }

        AssertThat(mares.Zones.Any(z => z.Kind == "brecha" && z.Brecha == "cais_afogado")).IsTrue();
        var praca = Level("praca");
        var bridge = praca.Zones.Single(z => z.Target == "mares");
        AssertThat(bridge.Requires).Contains("GuardianDefeated:macieira_oca");
    }
}
