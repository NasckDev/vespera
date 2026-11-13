using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Horta;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>Os 6 canteiros da Horta no Pomar Alto: um por canteiro do JSON, atrás da Celeste e ao alcance de quem anda pelo mapa.</summary>
[TestSuite]
public class HortaLevelTest
{
    private const float Step = 8f;
    private static string LevelsDir => Path.Combine(AppContext.BaseDirectory, "Levels");

    private static LevelData Pomar => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "pomar_alto.json")));

    private static HortaDef Def => HortaDef.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Horta", "horta.json")));

    [TestCase]
    public void PomarAlto_HasOnePlotZonePerCanteiroOfTheHorta()
    {
        var zones = Pomar.Zones.Where(z => z.Kind == "canteiro").ToList();

        AssertThat(zones.Count).IsEqual(Def.Plots);
        AssertThat(zones.Select(z => z.Index).OrderBy(i => i).ToList()).IsEqual(Enumerable.Range(0, Def.Plots).ToList());
    }

    [TestCase]
    public void PomarAlto_PlotsOnlyExistAfterMeetingCeleste()
    {
        foreach (var zone in Pomar.Zones.Where(z => z.Kind == "canteiro"))
        {
            AssertThat(zone.Conditions).Contains(HortaRules.UnlockFlag);
        }
    }

    [TestCase]
    public void PomarAlto_EveryPlotIsReachableOnFootFromTheSpawn()
    {
        var level = Pomar;
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
        var reached = Flood(grid, level);

        var unreachable = level.Zones.Where(z => z.Kind == "canteiro" && !reached.Any(p => Distance(p, z.At) <= z.Radius * 0.9f)).Select(z => z.Index).ToList();

        AssertThat(unreachable).IsEmpty();
    }

    [TestCase]
    public void Nilza_OffersTheFeiraOnlyAfterCeleste()
    {
        var file = DialogueFile.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Dialogue", "nilza.json")));
        var choice = file.Nodes["hub"].Choices.Single(c => c.Next == "feira");

        AssertThat(choice.Conditions).Contains("met_celeste");
        AssertThat(file.Nodes["feira"].Events).Contains(StoryFlags.EventOpenFeira);
    }

    private static float Distance((float X, float Y) p, float[] at) => MathF.Sqrt(((p.X - at[0]) * (p.X - at[0])) + ((p.Y - at[1]) * (p.Y - at[1])));

    /// <summary>Posições do pé (passo de 8 px) que o caçador alcança andando a partir do nascimento da fase, com a cápsula real do pé.</summary>
    private static List<(float X, float Y)> Flood(LevelGrid grid, LevelData level)
    {
        var scale = level.ActorScale;
        bool Free(float x, float y) => !CollisionMath.CapsuleHitsGrid(grid, CollisionMath.FeetCapsule(x, y, 22f * scale, 11f * scale), 0f);
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int Cx, int Cy)>();
        var start = ((int)MathF.Round(level.Spawn[0] / Step), (int)MathF.Round(level.Spawn[1] / Step));
        queue.Enqueue(start);
        seen.Add(start);
        var result = new List<(float, float)>();
        (int, int)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            result.Add((cx * Step, cy * Step));
            foreach (var (dx, dy) in dirs)
            {
                var next = (cx + dx, cy + dy);
                if (!seen.Contains(next) && Free(next.Item1 * Step, next.Item2 * Step))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return result;
    }
}
