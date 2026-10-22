using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

[TestSuite]
public class LevelDataTest
{
    private static string LevelsDir => Path.Combine(AppContext.BaseDirectory, "Levels");

    [TestCase]
    public void Praca_ParsesWithGridMatchingDeclaredSize()
    {
        var level = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "praca.json")));

        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

        AssertThat(level.Cell).IsGreater(0);
        AssertThat(grid.Width).IsEqual(level.Cols * (float)level.Cell);
        AssertThat(grid.WalkableCount()).IsGreater(300);
    }

    [TestCase]
    public void Praca_EveryPlacedPropExistsInCatalog()
    {
        var level = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "praca.json")));
        var props = LevelJson.ParseProps(File.ReadAllText(Path.Combine(LevelsDir, "props.json")));

        AssertThat(LevelJson.Validate(level, props)).IsEmpty();
    }

    [TestCase]
    public void Praca_SpawnIsOnWalkableCellAndOutsideFootprints()
    {
        var level = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "praca.json")));
        var props = LevelJson.ParseProps(File.ReadAllText(Path.Combine(LevelsDir, "props.json")));
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
        var feet = CollisionMath.FeetCapsule(level.Spawn[0], level.Spawn[1], 22f * level.ActorScale, 11f * level.ActorScale);

        AssertThat(CollisionMath.CapsuleHitsGrid(grid, feet, 0f)).IsFalse();
        foreach (var placement in level.Props)
        {
            var f = props[placement.Type].Scaled(level.PropScale).WorldFootprint.Offset(placement.At[0], placement.At[1]);
            AssertThat(CollisionMath.CapsuleHitsRect(feet.X0, feet.X1, feet.Y, feet.R, f, 0f)).IsFalse();
        }
    }

    [TestCase]
    public void Praca_PropFootprintsSitOnWalkableGround()
    {
        var level = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "praca.json")));
        var props = LevelJson.ParseProps(File.ReadAllText(Path.Combine(LevelsDir, "props.json")));
        var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

        foreach (var placement in level.Props)
        {
            AssertThat(grid.IsBlockedAt(placement.At[0], placement.At[1] - 1f))
                .OverrideFailureMessage($"o pé de '{placement.Type}' em {placement.At[0]},{placement.At[1]} caiu em célula bloqueada")
                .IsFalse();
        }
    }

    [TestCase]
    public void ParseProps_SkipsCommentKeysAndScalesFootprint()
    {
        const string json = """
            { "_doc": "comentário", "banco": { "texture": "res://x.png", "scale": 2, "anchor": [10, 20], "footprint": [-5, -4, 10, 4] } }
            """;

        var props = LevelJson.ParseProps(json);

        AssertThat(props.Count).IsEqual(1);
        AssertThat(props["banco"].WorldFootprint).IsEqual(new RectF(-10, -8, 20, 8));
    }

    [TestCase]
    public void ParseLevel_RejectsMissingGround()
    {
        AssertThrown(() => LevelJson.ParseLevel("""{ "id": "x", "cell": 48, "cols": 1, "rows": 1, "grid": ["."] }"""))
            .IsInstanceOf<FormatException>();
    }
}
