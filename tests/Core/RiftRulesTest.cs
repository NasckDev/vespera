namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat;
using Vespera.Core.Rift;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Brecha E (Esgotos do Mercado): dados, escala por rank, Guardião, Fragmentos, Lanterna; combo e fôlego.</summary>
[TestSuite]
public class RiftRulesTest
{
    private static readonly Lazy<BrechaDef> Brecha = new(() => BrechaDef.Parse(TestDb.ReadFile("brecha_esgotos.json")));

    private static RiftRules Rules => new(TestDb.Instance, Brecha.Value);

    private static SaveData NewSave() => SaveData.NewGame(TestDb.Instance, "Rui", "sword");

    [TestCase]
    public void BrechaFileLoadsWithRoomsMonstersAndMoves()
    {
        var b = Brecha.Value;
        AssertThat(b.Rooms).ContainsExactly("esgoto_corredor", "esgoto_cruzamento", "esgoto_tesouro", "esgoto_arena");
        AssertThat(b.Monsters.Keys).Contains("lodo", "espectro", "lodo_mae");
        AssertThat(b.Monsters["lodo"].Period).IsEqual(1.2);
        AssertThat(b.Monsters["lodo_mae"].Telegraph).IsEqual(0.6);
        AssertThat(b.Moves["sword"].Light.Count).IsEqual(3);
        AssertThat(b.Moves["hammer"].PartMult).IsEqual(2.0);
        AssertThat(b.NeighborhoodId).IsEqual("bairro_das_forjas");
        AssertThat(b.GuardianId).IsEqual("lodo_mae");
    }

    [TestCase]
    public void InvalidBrechaIsRejected()
    {
        var json = TestDb.ReadFile("brecha_esgotos.json").Replace("\"phases\": [0.66, 0.33]", "\"phases\": [0.2, 0.5]");
        AssertThrown(() => BrechaDef.Parse(json)).IsInstanceOf<FormatException>();
    }

    [TestCase]
    public void RankEDoesNotScaleStats()
    {
        var lodo = Rules.Stats("lodo");
        AssertThat(Rules.RankIndex).IsEqual(0);
        AssertThat(lodo.Hp).IsEqual(28.0);
        AssertThat(lodo.Damage).IsEqual(9.0);
        AssertThrown(() => Rules.Stats("dragao")).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void BossPhasesFollowHpThresholds()
    {
        AssertThat(Rules.BossPhase(480, 480)).IsEqual(1);
        AssertThat(Rules.BossPhase(300, 480)).IsEqual(2);
        AssertThat(Rules.BossPhase(150, 480)).IsEqual(3);
        AssertThat(Rules.BossPhase(0, 480)).IsEqual(3);
    }

    [TestCase]
    public void EachBrokenCoreRemovesOneSplit()
    {
        AssertThat(Rules.SplitCount(0)).IsEqual(3);
        AssertThat(Rules.SplitCount(1)).IsEqual(2);
        AssertThat(Rules.SplitCount(3)).IsEqual(0);
        AssertThat(Rules.SplitCount(7)).IsEqual(0);
    }

    [TestCase]
    public void KillsGiveXpFromTheBrechaData()
    {
        AssertThat(Rules.Xp("lodo")).IsEqual(5);
        AssertThat(Rules.Xp("espectro")).IsEqual(8);
        AssertThat(Rules.Xp("lodo_mae")).IsEqual(50);
        AssertThat(Rules.Xp("dragao")).IsEqual(0);
    }

    [TestCase]
    public void AFullFirstRunReachesTheXpOfRankD()
    {
        // As ondas reais das 4 salas (data/levels) + a Lodo-Mãe: a primeira caçada completa precisa bastar para E -> D
        // (o Guardião é a outra condição, e ele é o fim da caçada).
        var monsters = new List<string>();
        foreach (var room in Brecha.Value.Rooms)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Levels", $"{room}.json")));
            if (!doc.RootElement.TryGetProperty("rift", out var rift) || !rift.TryGetProperty("waves", out var waves))
            {
                continue;
            }

            foreach (var wave in waves.EnumerateArray())
            {
                monsters.AddRange(wave.EnumerateArray().Select(s => s.GetProperty("type").GetString()!));
            }
        }

        var catalog = Vespera.Core.Advancement.AdvancementCatalog.Load(name => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Progressao", name)));
        var xpForD = catalog.RankData.Ranks.First(r => r.Id == Vespera.Core.Ranks.Rank.D).Xp;
        AssertThat(Rules.FullRunXp(monsters)).IsGreaterEqual(xpForD);
    }

    [TestCase]
    public void ClosingTheBrechaCountsIt()
    {
        var save = NewSave();
        RiftRules.MarkClosed(save);
        AssertThat(save.BrechasClosed).IsEqual(1);
        AssertThat(save.FirstBrechaDone).IsTrue();
    }

    [TestCase]
    public void HitStopStaysBetween40And120Ms()
    {
        AssertThat(RiftRules.HitStop(0.01, false)).IsEqual(0.04);
        AssertThat(RiftRules.HitStop(0.05, true)).IsEqualApprox(0.08, 1e-9);
        AssertThat(RiftRules.HitStop(0.12, true)).IsEqual(0.12);
    }

    [TestCase]
    public void ComboAdvancesInsideTheWindowAndResetsAfter()
    {
        var combo = new ComboChain(3, 0.25, 0.4);
        combo.Press(0);
        AssertThat(combo.TryStart(0, true, out var s0)).IsTrue();
        AssertThat(s0).IsEqual(0);
        combo.Press(0.1);
        AssertThat(combo.TryStart(0.15, false, out _)).IsFalse();
        combo.Finish(0.3);
        AssertThat(combo.TryStart(0.3, true, out var s1)).IsTrue();
        AssertThat(s1).IsEqual(1);
        combo.Finish(0.6);
        combo.Press(0.7);
        AssertThat(combo.TryStart(0.7, true, out var s2)).IsTrue();
        AssertThat(s2).IsEqual(2);
        combo.Finish(1.0);
        combo.Press(1.1);
        AssertThat(combo.TryStart(1.1, true, out var s3)).IsTrue();
        AssertThat(s3).IsEqual(0);
    }

    [TestCase]
    public void ComboBufferExpiresAndLateChainRestarts()
    {
        var combo = new ComboChain(3, 0.25, 0.4);
        combo.Press(0);
        AssertThat(combo.TryStart(0.5, true, out _)).IsFalse();
        AssertThat(combo.HasBufferedPress).IsFalse();
        combo.Press(1);
        combo.TryStart(1, true, out _);
        combo.Finish(1.2);
        combo.Press(2);
        AssertThat(combo.TryStart(2, true, out var step)).IsTrue();
        AssertThat(step).IsEqual(0);
    }

    [TestCase]
    public void StaminaSpendsAndRegeneratesAfterDelay()
    {
        var stamina = new StaminaMeter(100, 50, 0.5);
        AssertThat(stamina.TrySpend(30)).IsTrue();
        AssertThat(stamina.TrySpend(30)).IsTrue();
        AssertThat(stamina.TrySpend(30)).IsTrue();
        AssertThat(stamina.TrySpend(30)).IsFalse();
        AssertThat(stamina.Value).IsEqual(10.0);
        stamina.Tick(0.4);
        AssertThat(stamina.Value).IsEqual(10.0);
        stamina.Tick(0.2);
        AssertThat(stamina.Value).IsEqualApprox(20.0, 1e-9);
        stamina.Tick(10);
        AssertThat(stamina.Value).IsEqual(100.0);
    }
}
