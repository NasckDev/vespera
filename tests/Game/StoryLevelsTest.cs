using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

[TestSuite]
public class StoryLevelsTest
{
    private static readonly string[] Ids = ["praca", "forjas", "forja_interior"];

    private static string LevelsDir => Path.Combine(AppContext.BaseDirectory, "Levels");

    private static LevelData Load(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, $"{id}.json")));

    private static bool FeetFit(LevelData level, float x, float y)
    {
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
        var s = level.ActorScale;
        var feet = CollisionMath.FeetCapsule(x, y, 22f * s, 11f * s);
        return !CollisionMath.CapsuleHitsGrid(grid, feet, 0f);
    }

    [TestCase]
    public void ScaleRegistry_HumanIsOneDoorAndTargetsFollowTable()
    {
        AssertThat(ScaleRegistry.HumanHeight(72f)).IsEqual(72f);
        var human = ScaleRegistry.HumanHeight(100f);
        AssertThat(ScaleRegistry.TargetHeight("benicio", human)).IsEqual(95f);
        AssertThat(ScaleRegistry.TargetHeight("lis", human)).IsEqual(75f);
        AssertThat(ScaleRegistry.TargetHeight("odete", human)).IsEqual(ScaleRegistry.TargetHeight("ravi", human));
        AssertThat(Math.Abs(ScaleRegistry.HunterScale(72f) - 0.576f)).IsLess(0.001f);
    }

    [TestCase]
    [ThrowsException(typeof(KeyNotFoundException))]
    public void ScaleRegistry_UnknownActorIsAnError() => ScaleRegistry.TargetHeight("fantasma", 70f);

    [TestCase]
    public void Levels_DeclareDoorHeightAndDeriveActorScaleFromIt()
    {
        foreach (var id in Ids)
        {
            var level = Load(id);
            AssertThat(level.DoorHeight).IsGreater(0f);
            AssertThat(level.ActorScale).IsEqual(ScaleRegistry.HunterScale(level.DoorHeight));
        }
    }

    [TestCase]
    public void Levels_SpawnAndEveryExitDestinationAreWalkable()
    {
        foreach (var id in Ids)
        {
            var level = Load(id);
            AssertThat(FeetFit(level, level.Spawn[0], level.Spawn[1])).IsTrue();
            foreach (var zone in level.Zones.Where(z => z.Target.Length > 0))
            {
                var target = Load(zone.Target);
                AssertThat(zone.Spawn.Length).IsEqual(2);
                AssertThat(FeetFit(target, zone.Spawn[0], zone.Spawn[1])).IsTrue();
            }
        }
    }

    [TestCase]
    public void Levels_EveryNpcStandsOnWalkableGroundAndHasDialogueFile()
    {
        var dialogueDir = Path.Combine(AppContext.BaseDirectory, "Dialogue");
        foreach (var id in Ids)
        {
            var level = Load(id);
            foreach (var npc in level.Npcs)
            {
                AssertThat(FeetFit(level, npc.At[0], npc.At[1])).IsTrue();
                var dialogue = npc.Dialogue.Length > 0 ? npc.Dialogue : npc.Id;
                AssertThat(File.Exists(Path.Combine(dialogueDir, $"{dialogue}.json"))).IsTrue();
            }

            foreach (var zone in level.Zones.Where(z => z.Dialogue.Length > 0))
            {
                AssertThat(File.Exists(Path.Combine(dialogueDir, $"{zone.Dialogue}.json"))).IsTrue();
            }
        }
    }

    [TestCase]
    public void Levels_LineupAreaIsWalkable()
    {
        foreach (var id in Ids)
        {
            var level = Load(id);
            AssertThat(level.Lineup.Length).IsEqual(2);
            var human = ScaleRegistry.HumanHeight(level.DoorHeight);
            for (var i = 0; i <= 5; i++)
            {
                AssertThat(FeetFit(level, level.Lineup[0] + (i * human), level.Lineup[1])).IsTrue();
            }
        }
    }
}
