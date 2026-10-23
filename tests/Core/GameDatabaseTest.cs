namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Data;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class GameDatabaseTest
{
    [TestCase]
    public void LoadsAllPrototypeTables()
    {
        var db = TestDb.Instance;
        AssertThat(db.Ranks.Count).IsEqual(10);
        AssertThat(db.SeasonData.Seasons.Count).IsEqual(4);
        AssertThat(db.ThemeList.Count).IsEqual(5);
        AssertThat(db.WeaponOrder.Count).IsEqual(5);
        AssertThat(db.Elements.Count).IsEqual(6);
        AssertThat(db.Materials.Count).IsEqual(50); // 48 da v2 + peixe (pesca, DECISIONS D46) + gel_lodo (Guardião da Brecha E)
        AssertThat(db.Gear.Count).IsEqual(25);
        AssertThat(db.GearSlots.Count).IsEqual(8);
        AssertThat(db.Recipes.Count).IsEqual(5);
        AssertThat(db.Shops.Relics.Count).IsEqual(10);
        AssertThat(db.Monsters.Count).IsEqual(24);
        AssertThat(db.Monsters.Values.Count(m => m.Boss)).IsEqual(5);
        AssertThat(db.Ecos.Count).IsEqual(4);
        AssertThat(db.Ultimates.Count).IsEqual(5);
        AssertThat(db.ClassList.Count).IsEqual(5);
        AssertThat(db.Npcs.Walkers.Count).IsEqual(4);
        AssertThat(db.Oficios.Count).IsEqual(4);
        AssertThat(db.Social.Hunters.Count).IsEqual(8);
        AssertThat(db.Social.Chat.Count).IsEqual(14);
        AssertThat(db.Social.Rumors.Count).IsEqual(5);
        AssertThat(db.Quests.Count).IsEqual(13);
    }

    [TestCase]
    public void KeepsSssSealAndV2Thresholds()
    {
        var db = TestDb.Instance;
        AssertThat(db.Ranks.Select(r => r.Id)).ContainsExactly("E", "D", "C", "B", "A", "S", "SS", "SSS", "SSSS", "ALVOR");
        AssertThat(db.Ranks.Select(r => r.Xp)).ContainsExactly(0, 100, 260, 520, 900, 1500, 2400, 3600, 5200, 7500);
        AssertThat(db.Ranks[8].Title).IsEqual("Véspero");
        AssertThat(db.RankIndexOf("ALVOR")).IsEqual(9);
        AssertThat(db.RankIndexOf("Z")).IsEqual(-1);
    }

    [TestCase]
    public void ReadsTypedFieldsAndDefaults()
    {
        var db = TestDb.Instance;
        var wolfE = db.Monsters["wolfE"];
        AssertThat(wolfE.Elite).IsTrue();
        AssertThat(wolfE.Scale).IsEqual(1.4);
        AssertThat(wolfE.Eco).IsEqual("uivo");
        AssertThat(wolfE.Parts[0].Side).IsEqual(PartSide.Front);
        AssertThat(wolfE.Parts[0].Count).IsEqual(2);
        AssertThat(db.Monsters["cnev"].HitHeight).IsNull();
        AssertThat(db.Monsters["knight"].Ai).IsEqual(AiKind.Knight);

        AssertThat(db.Weapons["bow"].Arc).IsNull();
        AssertThat(db.Weapons["dag"].Backstab!.Value).IsEqual(2.0);
        AssertThat(db.Weapons["staff"].UpgradeMaterialPerLevel).IsEqual(2);

        AssertThat(db.Gear["capuz"].Classes).IsNull();
        AssertThat(db.Gear["luva2"].Classes!).ContainsExactly("sword", "hammer", "dag");
        AssertThat(db.Gear["lant3"].Light!.Value).IsEqual(1.35);
        AssertThat(db.Gear["couraca"].Light).IsNull();

        AssertThat(db.Materials["estilhaco"].Rarity).IsEqual("comum");
        AssertThat(db.Materials["lamento"].Value).IsEqual(25);
        AssertThat(db.Rules.Brecha.Timer.Min).IsEqual(210.0);
        AssertThat(db.Rules.Loot.CoinPiles.Max).IsEqual(3);
        AssertThat(db.Npcs.Walkers[0].Path[1].X).IsEqual(1100.0);
    }

    [TestCase]
    public void KeepsVerbatimTexts()
    {
        var db = TestDb.Instance;
        AssertThat(db.Npcs.Walkers.Single(w => w.Id == "lauro").Lines[0]).IsEqual("A Guarda segura a rua. As Brechas, quem segura são vocês.");
        AssertThat(db.Npcs.Dialogue["bras"]["invasion"][0]).IsEqual("Agora não! Tem monstro na minha rua. Limpa isso e eu reabro a forja.");
        AssertThat(db.Quests[^1].Name).IsEqual("Rumo ao Alvor");
        AssertThat(db.SeasonData.Seasons[1].ModText).IsEqual("Monstros ardentes: +15% de dano.");
    }

    [TestCase]
    public void RejectsDropOfUnknownMaterial()
    {
        var ex = CatchData(() => TestDb.LoadPatched("monsters.json", json => json.Replace("\"drop\": \"presa\"", "\"drop\": \"presa_inexistente\"")));
        AssertThat(ex.Message).Contains("'presa_inexistente' não existe");
    }

    [TestCase]
    public void RejectsThemeWithUnknownBoss()
    {
        var ex = CatchData(() => TestDb.LoadPatched("themes.json", json => json.Replace("\"boss\": \"angler\"", "\"boss\": \"kraken\"")));
        AssertThat(ex.Message).Contains("tema 'fish'.boss: 'kraken' não existe");
    }

    [TestCase]
    public void RejectsMisspelledField()
    {
        var ex = CatchData(() => TestDb.LoadPatched("weapons.json", json => json.Replace("\"cooldown\"", "\"coldown\"")));
        AssertThat(ex.Message).Contains("weapons.json inválido");
    }

    [TestCase]
    public void RejectsNonIncreasingRankThresholds()
    {
        var ex = CatchData(() => TestDb.LoadPatched("ranks.json", json => json.Replace("\"xp\": 260", "\"xp\": 100")));
        AssertThat(ex.Message).Contains("ranks: XP de 'C'");
    }

    [TestCase]
    public void ReportsAllProblemsAtOnce()
    {
        var ex = CatchData(() => TestDb.LoadPatched("recipes.json", json => json.Replace("\"minerio\"", "\"x1\"").Replace("\"luva2\"", "\"x2\"")));
        AssertThat(ex.Message).Contains("'x1' não existe");
        AssertThat(ex.Message).Contains("'x2' não existe");
    }

    private static GameDataException CatchData(Action action)
    {
        try
        {
            action();
        }
        catch (GameDataException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("esperava GameDataException");
    }
}
