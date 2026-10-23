using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>Contrato de escala (PLANO-V2 §2.1): régua por mapa, tabela de alturas e standing_height das folhas.</summary>
[TestSuite]
public class ScaleContractTest
{
    private static readonly string[] Rooms = ["praca", "esgoto_corredor", "esgoto_cruzamento", "esgoto_tesouro", "esgoto_arena"];

    private static string LevelsDir => Path.Combine(AppContext.BaseDirectory, "Levels");

    private static string SpritesDir => Path.Combine(AppContext.BaseDirectory, "Sprites");

    [Before]
    public void UseDiskSheets()
    {
        ScaleRegistry.ClearCache();
        ScaleRegistry.ReadText = path =>
        {
            var file = Path.Combine(SpritesDir, path.Replace("res://assets/v2/sprites/", string.Empty));
            return File.Exists(file) ? File.ReadAllText(file) : null;
        };
    }

    [TestCase]
    public void EveryMapDeclaresDoorHeightAndDerivesActorScale()
    {
        foreach (var id in Rooms)
        {
            var level = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, $"{id}.json")));
            AssertThat(level.DoorHeight).IsGreater(0f);
            AssertThat(level.HumanHeight).IsEqualApprox(0.75f * level.DoorHeight, 0.001f);
            AssertThat(level.ActorScale).IsEqualApprox(level.HumanHeight / 125f, 0.0001f);
            AssertThat(level.DoorAt).IsNotNull();
        }
    }

    [TestCase]
    public void PracaKeepsTheApprovedHunterHeight()
    {
        var praca = LevelJson.ParseLevel(File.ReadAllText(Path.Combine(LevelsDir, "praca.json")));
        AssertThat(praca.HumanHeight).IsEqualApprox(54f, 0.01f);
        AssertThat(praca.ActorScale).IsEqualApprox(0.432f, 0.0001f);
    }

    [TestCase]
    public void MapWithoutDoorHeightIsRejected()
    {
        AssertThrown(() => LevelJson.ParseLevel("""{ "id": "x", "cell": 16, "cols": 1, "rows": 1, "ground": { "texture": "a.png" }, "grid": ["."] }"""))
            .IsInstanceOf<FormatException>();
    }

    [TestCase]
    public void TableMatchesTheContract()
    {
        AssertThat(ScaleRegistry.Get("lamina").Height).IsEqual(1.00f);
        AssertThat(ScaleRegistry.Get("lodo").Height).IsEqual(0.35f);
        AssertThat(ScaleRegistry.Get("espectro").Height).IsEqual(1.00f);
        AssertThat(ScaleRegistry.Get("espectro").Hover).IsEqual(0.15f);
        AssertThat(ScaleRegistry.Get("lodo_mae").Height).IsEqual(3.00f);
        AssertThat(ScaleRegistry.Get("benicio").Height).IsEqual(0.95f);
        AssertThat(ScaleRegistry.Get("lis").Height).IsEqual(0.75f);
    }

    [TestCase]
    public void EverySheetHasStandingHeightAndScaleHitsTheTarget()
    {
        foreach (var (id, entry) in ScaleRegistry.Actors)
        {
            if (entry.Sheet.Length == 0)
            {
                continue;
            }

            var standing = ScaleRegistry.StandingHeight(id);
            AssertThat(standing).IsGreater(10f);
            var human = 78.75f;
            var rendered = standing * ScaleRegistry.SpriteScale(id, human);
            AssertThat(ScaleRegistry.WithinTolerance(rendered, entry.Height * human)).IsTrue();
        }
    }

    [TestCase]
    public void ToleranceIsEightPercent()
    {
        AssertThat(ScaleRegistry.WithinTolerance(108f, 100f)).IsTrue();
        AssertThat(ScaleRegistry.WithinTolerance(109f, 100f)).IsFalse();
        AssertThat(ScaleRegistry.WithinTolerance(91f, 100f)).IsFalse();
    }
}
