namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Os JSON de progressão (data/progressao) carregam, se validam e batem com o plano da v2.</summary>
[TestSuite]
public class AdvancementCatalogTest
{
    private static AdvancementCatalog Catalog => AdvTestData.Catalog;

    [TestCase]
    public void LadderHasNineRanksFromEToAlvorWithoutSsss()
    {
        var ids = Catalog.Ranks.Select(r => r.Id).ToArray();
        AssertThat(ids).ContainsExactly(Rank.E, Rank.D, Rank.C, Rank.B, Rank.A, Rank.S, Rank.SS, Rank.SSS, Rank.Alvor);
        AssertThat(Catalog.Ranks[0].Xp).IsEqual(0);
    }

    [TestCase]
    public void ExamsExistExactlyAtCAndSAndASSS()
    {
        var withExam = Catalog.Ranks.Where(r => r.Exam).Select(r => r.Id).ToArray();
        AssertThat(withExam).ContainsExactly(Rank.C, Rank.A, Rank.S, Rank.SSS);
        AssertThat(Catalog.ExamList.Count).IsEqual(4);
        foreach (var exam in Catalog.ExamList)
        {
            AssertThat(exam.Objectives.Count).IsGreater(0);
            AssertThat(exam.RewardXp).IsGreater(0);
        }
    }

    [TestCase]
    public void EveryRankHasAGuardianExceptTheLast()
    {
        for (var i = 0; i < Catalog.Ranks.Count - 1; i++)
        {
            AssertThat(Catalog.Ranks[i].Guardian).IsNotNull();
            AssertThat(Catalog.Guardians[Catalog.Ranks[i].Guardian!].Rank).IsEqual(Catalog.Ranks[i].Id);
        }

        AssertThat(Catalog.Ranks[^1].Guardian).IsNull();
    }

    [TestCase]
    public void ActOneMaterialsFromTheGddExist()
    {
        AssertThat(Catalog.Materials["gel_de_lodo"].Name).IsEqual("Gel de Lodo");
        AssertThat(Catalog.Materials["veu_de_espectro"].Name).IsEqual("Véu de Espectro");
        AssertThat(Catalog.Materials["nucleo_lodo_mae"].Name).IsEqual("Núcleo da Lodo-Mãe");
        AssertThat(Catalog.Materials["nucleo_lodo_mae"].Kind).IsEqual(MaterialKind.Guardian);
        AssertThat(Catalog.Materials["gel_de_lodo"].Origin).IsEqual("gdd");
    }

    [TestCase]
    public void EveryRankHasAtLeastTwoCommonMaterialsAndEachGuardianOne()
    {
        foreach (var step in Catalog.Ranks)
        {
            AssertThat(Catalog.CommonMaterials(step.Id).Count).IsGreaterEqual(2);
        }

        foreach (var guardian in Catalog.Guardians.Values)
        {
            AssertThat(Catalog.Materials[guardian.Material].Kind).IsEqual(MaterialKind.Guardian);
        }
    }

    [TestCase]
    public void ThreeClassesHaveNineTiersEachWithGrowingDamage()
    {
        AssertThat(Catalog.Weapons.Keys.OrderBy(k => k).ToArray()).ContainsExactly("bow", "hammer", "sword");
        foreach (var weapon in Catalog.Weapons.Values)
        {
            AssertThat(weapon.Tiers.Count).IsEqual(9);
            for (var i = 1; i < 9; i++)
            {
                AssertThat(weapon.Tiers[i].Damage).IsGreater(weapon.Tiers[i - 1].Damage);
            }
        }
    }

    [TestCase]
    public void LodoMaeSetHasFourPiecesAndTwoAndFourPieceBonuses()
    {
        var set = Catalog.Sets["conjunto_lodo_mae"];
        AssertThat(set.Name).IsEqual("Conjunto da Lodo-Mãe");
        AssertThat(set.Pieces.Count).IsEqual(4);
        AssertThat(set.Bonuses.Select(b => b.Pieces).OrderBy(n => n).ToArray()).ContainsExactly(2, 4);
        AssertThat(Catalog.Guardians["lodo_mae"].ArmorSet).IsEqual("conjunto_lodo_mae");
    }

    [TestCase]
    public void LodoMaeGivesTheViscousEcho()
    {
        AssertThat(Catalog.Guardians["lodo_mae"].Echo).IsEqual("eco_viscoso");
        AssertThat(Catalog.Echoes["eco_viscoso"].Name).IsEqual("Eco Viscoso");
        AssertThat(Catalog.EchoData.Slots).IsEqual(2);
    }

    [TestCase]
    public void RankStageMappingFollowsThePlan()
    {
        var ladder = new RankLadder(Catalog);
        AssertThat(new[] { Rank.E, Rank.D }.Select(ladder.Stage).ToArray()).ContainsExactly(0, 0);
        AssertThat(new[] { Rank.C, Rank.B }.Select(ladder.Stage).ToArray()).ContainsExactly(1, 1);
        AssertThat(new[] { Rank.A, Rank.S }.Select(ladder.Stage).ToArray()).ContainsExactly(2, 2);
        AssertThat(new[] { Rank.SS, Rank.SSS }.Select(ladder.Stage).ToArray()).ContainsExactly(3, 3);
        AssertThat(ladder.Stage(Rank.Alvor)).IsEqual(4);
        AssertThat(Catalog.RankData.Stages.Select(s => s.Name).ToArray()).ContainsExactly("Aprendiz", "Caçador", "Veterano", "Elite", "Radiante");
    }

    [TestCase]
    public void DuplicateMaterialIsRejected()
    {
        var ex = CatchData(() => AdvTestData.LoadPatched(
            "materials.json",
            json => json.Replace("\"id\": \"veu_de_espectro\"", "\"id\": \"gel_de_lodo\"", StringComparison.Ordinal)));
        AssertThat(ex.Message).Contains("duplicado");
    }

    [TestCase]
    public void RankWithExamButNoExamDefinitionIsRejected()
    {
        var ex = CatchData(() => AdvTestData.LoadPatched(
            "exams.json",
            json => json.Replace("\"rank\": \"C\"", "\"rank\": \"B\"", StringComparison.Ordinal)));
        AssertThat(ex.Message).Contains("exame");
    }

    [TestCase]
    public void LootWeightsThatDoNotSumToOneAreRejected()
    {
        var ex = CatchData(() => AdvTestData.LoadPatched(
            "loot.json",
            json => json.Replace("\"same\": 0.80", "\"same\": 0.70", StringComparison.Ordinal)));
        AssertThat(ex.Message).Contains("somar 1");
    }

    [TestCase]
    public void GuardianPointingToMissingMaterialIsRejected()
    {
        var ex = CatchData(() => AdvTestData.LoadPatched(
            "guardians.json",
            json => json.Replace("\"material\": \"nucleo_lodo_mae\"", "\"material\": \"nao_existe\"", StringComparison.Ordinal)));
        AssertThat(ex.Message).Contains("nao_existe");
    }

    [TestCase]
    public void MissingFileIsReportedAsDataError()
    {
        var ex = CatchData(() => AdvancementCatalog.Load(_ => throw new FileNotFoundException("x")));
        AssertThat(ex.Message).Contains("Não foi possível ler");
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
