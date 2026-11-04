using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>Bairros dos Atos IV e V (Cemitério das Lanternas e Torre do Sino): régua, grade, saídas em sequência e NPCs.</summary>
[TestSuite]
public class Ato45LevelsTest
{
    private static readonly string[] Ids = ["cemiterio", "torre"];

    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static LevelData Load(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static bool FeetFit(LevelData level, float x, float y)
    {
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
        var s = level.ActorScale;
        var feet = CollisionMath.FeetCapsule(x, y, 22f * s, 11f * s);
        return !CollisionMath.CapsuleHitsGrid(grid, feet, 0f);
    }

    [TestCase]
    public void Hubs_HaveRulerGridAndWalkableSpawnLineupAndNpcs()
    {
        foreach (var id in Ids)
        {
            var level = Load(id);
            AssertThat(level.DoorHeight).IsEqual(80f);
            AssertThat(level.Grid.Count).IsEqual(level.Rows);
            AssertThat(level.Grid.All(r => r.Length == level.Cols)).IsTrue();
            AssertThat(FeetFit(level, level.Spawn[0], level.Spawn[1])).IsTrue();
            var human = ScaleRegistry.HumanHeight(level.DoorHeight);
            for (var i = 0; i <= 5; i++)
            {
                AssertThat(FeetFit(level, level.Lineup[0] + (i * human), level.Lineup[1])).IsTrue();
            }

            foreach (var npc in level.Npcs)
            {
                AssertThat(FeetFit(level, npc.At[0], npc.At[1])).IsTrue();
                AssertThat(File.Exists(Path.Combine(Dir("Dialogue"), $"{npc.Dialogue}.json"))).IsTrue();
            }

            foreach (var zone in level.Zones.Where(z => z.Dialogue.Length > 0))
            {
                AssertThat(File.Exists(Path.Combine(Dir("Dialogue"), $"{zone.Dialogue}.json"))).IsTrue();
            }
        }
    }

    [TestCase]
    public void Hubs_ChainPomarCemiterioTorreAndEveryExitLandsOnFloor()
    {
        foreach (var id in new[] { "pomar_alto", "cemiterio", "torre" })
        {
            foreach (var zone in Load(id).Zones.Where(z => z.Target.Length > 0))
            {
                var target = Load(zone.Target);
                AssertThat(FeetFit(target, zone.Spawn[0], zone.Spawn[1])).IsTrue();
            }
        }

        var toCemiterio = Load("pomar_alto").Zones.Single(z => z.Target == "cemiterio");
        AssertThat(toCemiterio.Requires).ContainsExactly("GuardianDefeated:farol_afogado");
        AssertThat(toCemiterio.LockedText.Length).IsGreater(0);
        var toTorre = Load("cemiterio").Zones.Single(z => z.Target == "torre");
        AssertThat(toTorre.Requires).ContainsExactly("GuardianDefeated:velora");
    }

    [TestCase]
    public void Hubs_BrechaIsVisibleButSealedWithComingSoonText()
    {
        foreach (var id in Ids)
        {
            var brecha = Load(id).Zones.Single(z => z.Kind == "brecha");
            AssertThat(brecha.Brecha.Length).IsGreater(0);
            AssertThat(brecha.LockedText).Contains("Em breve");
        }
    }

    [TestCase]
    public void Vigia_OnlyAtNightOnTheBenchAndInTheScaleContract()
    {
        var vigia = Load("cemiterio").Npcs.Single(n => n.Id == "vigia");
        AssertThat(vigia.Conditions).ContainsExactly("noite");
        AssertThat(vigia.Dialogue).IsEqual("ato4/vigia");
        AssertThat(ScaleRegistry.Get("vigia").Height).IsEqual(1.00f);
    }

    [TestCase]
    public void Guardian_VeloraIdMatchesTheDialogueFlags()
    {
        var guardians = File.ReadAllText(Path.Combine(Dir("Progressao"), "guardians.json"));
        AssertThat(guardians).Contains("\"id\": \"velora\"");
        AssertThat(guardians).NotContains("velorio");
    }
}
