using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Toda sala de caçada precisa poder acabar: cada ponto de nascimento dos monstros fica no chão alcançável a pé desde a
/// entrada, com a barreira da saída e os selos fechados (um monstro atrás da barreira deixava a sala presa).
/// </summary>
[TestSuite]
public class RoomReachTest
{
    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    /// <summary>Barreira da saída (só quando a sala não tem fenda, como em HuntService.RoomRift) e selos.</summary>
    private static List<RectF> Closed(LevelData level)
    {
        var rects = new List<RectF>();
        if (level.Rift is not { } rift)
        {
            return rects;
        }

        if (rift.ExitPortal is null && rift.Exit is { } exit)
        {
            rects.Add(new RectF(exit.Barrier[0], exit.Barrier[1], exit.Barrier[2], exit.Barrier[3]));
        }

        rects.AddRange(rift.Sealed.Select(r => new RectF(r[0], r[1], r[2], r[3])));
        return rects;
    }

    [TestCase]
    public void EveryHuntSpawnIsReachableFromTheEntrance()
    {
        var problems = new List<string>();
        var files = Directory.GetFiles(Dir("Cacada"), "cacada*.json");
        AssertThat(files.Length).IsGreater(5);
        foreach (var file in files)
        {
            var config = HuntConfig.Parse(File.ReadAllText(file));
            foreach (var (ground, def) in config.Grounds)
            {
                var level = Level(ground);
                var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
                var reach = grid.Reach(level.Spawn[0], level.Spawn[1], Closed(level));
                if (reach.Count == 0)
                {
                    problems.Add($"{Path.GetFileName(file)}:{ground}: a entrada ({level.Spawn[0]},{level.Spawn[1]}) não está no chão");
                    continue;
                }

                foreach (var s in def.Spawns)
                {
                    var col = (int)MathF.Floor(s[0] / grid.Cell);
                    var row = (int)MathF.Floor(s[1] / grid.Cell);
                    if (!reach.IsReachable(col, row))
                    {
                        problems.Add($"{Path.GetFileName(file)}:{ground}: nascimento ({s[0]},{s[1]}) fora do chão alcançável");
                    }
                }
            }
        }

        AssertThat(string.Join("\n", problems)).IsEqual(string.Empty);
    }

    [TestCase]
    public void ExitBarrierCutsTheCorridorOff()
    {
        // Sala 6×3 com a barreira cobrindo a coluna 4 inteira: a coluna 5 (corredor da saída) fica fora.
        var grid = LevelGrid.Parse(10, 6, 3, ["......", "......", "......"]);
        var reach = grid.Reach(5f, 15f, [new RectF(42, 0, 6, 30)]);

        AssertThat(reach.IsReachable(3, 1)).IsTrue();
        AssertThat(reach.IsReachable(4, 1)).IsFalse();
        AssertThat(reach.IsReachable(5, 1)).IsFalse();
        AssertThat(reach.Nearest(5, 1)).IsEqual((3, 1));
        AssertThat(grid.Reach(5f, 15f, []).IsReachable(5, 1)).IsTrue();
    }
}
