namespace Vespera.Tests.Core.Coop;

using GdUnit4;
using Vespera.Core.Coop;
using static GdUnit4.Assertions;

/// <summary>Escala do co-op lida de data/coop.json (vida, ondas, dano do Guardião, XP, reanimação, piso da câmera).</summary>
[TestSuite]
public class CoopRulesTest
{
    private static readonly Lazy<CoopRules> Real = new(() =>
        CoopRules.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Coop", "coop.json"))));

    private static CoopRules R => Real.Value;

    [TestCase(1, 1.0)]
    [TestCase(2, 1.5)]
    [TestCase(3, 2.0)]
    [TestCase(4, 2.5)]
    public void EnemyHpScalesWithPlayers(int players, double expected)
        => AssertThat(R.EnemyHp(players)).IsEqualApprox(expected, 1e-9);

    [TestCase]
    public void PlayerCountIsClamped()
    {
        AssertThat(R.EnemyHp(0)).IsEqualApprox(1.0, 1e-9);
        AssertThat(R.EnemyHp(9)).IsEqualApprox(2.5, 1e-9);
    }

    [TestCase(3, 1, 3)]
    [TestCase(3, 2, 4)]
    [TestCase(2, 4, 5)]
    [TestCase(0, 2, 1)]
    public void WaveGetsOneMonsterPerExtraPlayer(int baseCount, int players, int expected)
        => AssertThat(R.WaveSize(baseCount, players)).IsEqual(expected);

    [TestCase]
    public void BossDamageIsNotLinear()
    {
        AssertThat(R.BossDamage(1)).IsEqualApprox(1.0, 1e-9);
        AssertThat(R.BossDamage(2)).IsEqualApprox(1.15, 1e-9);
        AssertThat(R.BossDamage(4) < R.EnemyHp(2)).IsTrue();
    }

    [TestCase]
    public void SharedXpGivesEveryoneTheFullAmount()
    {
        AssertThat(CoopRules.XpPerPlayer(12, 1)).IsEqual(12);
        AssertThat(CoopRules.XpPerPlayer(12, 2)).IsEqual(12);
        AssertThat(CoopRules.XpPerPlayer(12, 4)).IsEqual(12);
        AssertThat(CoopRules.XpPerPlayer(12, 0)).IsEqual(0);
    }

    [TestCase]
    public void ReviveAndCameraComeFromData()
    {
        AssertThat(R.Revive.HoldSeconds).IsEqualApprox(2.0, 1e-9);
        AssertThat(R.RevivedHp(100)).IsEqual(40);
        AssertThat(R.RevivedHp(1)).IsEqual(1);
        AssertThat(R.MinZoomFactor(110)).IsEqualApprox(85.0 / 110.0, 1e-9);
        AssertThat(R.MinZoomFactor(80)).IsEqualApprox(1.0, 1e-9);
    }

    [TestCase]
    public void DefaultsMatchTheDataFile()
    {
        var d = CoopRules.Defaults;
        AssertThat(d.EnemyHpMultiplier).IsEqual(R.EnemyHpMultiplier);
        AssertThat(d.BossDamageMultiplier).IsEqual(R.BossDamageMultiplier);
        AssertThat(d.ExtraEnemiesPerWavePerPlayer).IsEqual(R.ExtraEnemiesPerWavePerPlayer);
    }

    [TestCase]
    public void ValidationRejectsBadTables()
    {
        var bad = """{ "maxPlayers": 4, "enemyHpMultiplier": [1.5, 1.0] }""";
        AssertThrown(() => CoopRules.Parse(bad)).IsInstanceOf<FormatException>();
        var decreasing = """{ "maxPlayers": 4, "bossDamageMultiplier": [1.0, 1.2, 1.1] }""";
        AssertThrown(() => CoopRules.Parse(decreasing)).IsInstanceOf<FormatException>();
        var revive = """{ "revive": { "holdSeconds": 0 } }""";
        AssertThrown(() => CoopRules.Parse(revive)).IsInstanceOf<FormatException>();
    }
}
