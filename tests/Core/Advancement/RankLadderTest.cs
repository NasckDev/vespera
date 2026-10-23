namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Rank sobe por XP e por conquista (Guardião e, em C/A/S/SSS, exame).</summary>
[TestSuite]
public class RankLadderTest
{
    [TestCase]
    public void NewHunterIsRankEWithZeroXp()
    {
        var engine = AdvTestData.NewEngine();
        AssertThat(engine.Rank).IsEqual(Rank.E);
        AssertThat(engine.Stage).IsEqual(0);
        AssertThat(engine.RankStatus.Progress).IsEqual(0f);
    }

    [TestCase]
    public void XpAloneDoesNotPromoteWithoutTheGuardian()
    {
        var engine = AdvTestData.NewEngine();
        var result = engine.AddXp(5000);
        AssertThat(result.RankChange).IsNull();
        AssertThat(engine.Rank).IsEqual(Rank.E);
        AssertThat(engine.RankStatus.Progress).IsEqual(1f);
    }

    [TestCase]
    public void GuardianAloneDoesNotPromoteWithoutXp()
    {
        var engine = AdvTestData.NewEngine();
        var kill = engine.RecordGuardianKill("lodo_mae");
        AssertThat(kill.RankChange).IsNull();
        AssertThat(engine.Rank).IsEqual(Rank.E);
    }

    [TestCase]
    public void XpThenGuardianPromotesAtTheKill()
    {
        var engine = AdvTestData.NewEngine();
        engine.AddXp(150);
        var kill = engine.RecordGuardianKill("lodo_mae");
        AssertThat(kill.RankChange).IsNotNull();
        AssertThat(kill.RankChange!.From).IsEqual(Rank.E);
        AssertThat(kill.RankChange.To).IsEqual(Rank.D);
        AssertThat(kill.RankChange.StageChanged).IsFalse();
        AssertThat(engine.Rank).IsEqual(Rank.D);
    }

    [TestCase]
    public void GuardianThenXpPromotesAtTheXp()
    {
        var engine = AdvTestData.NewEngine();
        engine.RecordGuardianKill("lodo_mae");
        var result = engine.AddXp(120);
        AssertThat(result.RankChange!.To).IsEqual(Rank.D);
    }

    [TestCase]
    public void RankCNeedsTheExamToo()
    {
        var engine = AdvTestData.HunterAt(Rank.C);
        AssertThat(engine.Rank).IsEqual(Rank.C);
        engine.AddXp(10000);
        engine.RecordGuardianKill("farol_afogado");
        AssertThat(engine.Rank).IsEqual(Rank.C);
        var exam = engine.TryPassExam(Rank.C);
        AssertThat(exam.Passed).IsTrue();
        AssertThat(exam.RankChange!.To).IsEqual(Rank.B);
        AssertThat(exam.RankChange.StageChanged).IsFalse();
        AssertThat(engine.Stage).IsEqual(1);
    }

    [TestCase]
    public void CrossingToStageOneIsAStageChange()
    {
        var engine = AdvTestData.HunterAt(Rank.C);
        engine.AddXp(10000);
        engine.TryPassExam(Rank.C);
        var kill = engine.RecordGuardianKill("farol_afogado");
        AssertThat(kill.RankChange!.From).IsEqual(Rank.C);
        AssertThat(kill.RankChange.To).IsEqual(Rank.B);
        // C e B são o mesmo estágio; o cruzamento D -> C é que troca (Aprendiz -> Caçador).
        var d = AdvTestData.HunterAt(Rank.D);
        d.AddXp(10000);
        var toC = d.RecordGuardianKill("macieira_oca");
        AssertThat(toC.RankChange!.To).IsEqual(Rank.C);
        AssertThat(toC.RankChange.StageChanged).IsTrue();
        AssertThat(toC.RankChange.FromStage).IsEqual(0);
        AssertThat(toC.RankChange.ToStage).IsEqual(1);
    }

    [TestCase]
    public void ExamCannotBeTakenBeforeReachingItsRank()
    {
        var engine = AdvTestData.HunterAt(Rank.D);
        var result = engine.TryPassExam(Rank.C);
        AssertThat(result.Status).IsEqual(ExamStatus.RankTooLow);
        AssertThat(engine.Progress.ExamsPassed).IsEmpty();
    }

    [TestCase]
    public void RanksWithoutExamReportNoExam()
    {
        var engine = AdvTestData.NewEngine();
        AssertThat(engine.TryPassExam(Rank.E).Status).IsEqual(ExamStatus.NoExamForRank);
        AssertThat(engine.TryPassExam(Rank.B).Status).IsEqual(ExamStatus.NoExamForRank);
    }

    [TestCase]
    public void ExamPassesOnlyOnceAndPaysXpOnce()
    {
        var engine = AdvTestData.HunterAt(Rank.C);
        var before = engine.Progress.Xp;
        var first = engine.TryPassExam(Rank.C);
        var second = engine.TryPassExam(Rank.C);
        AssertThat(first.XpAwarded).IsGreater(0);
        AssertThat(engine.Progress.Xp).IsEqual(before + first.XpAwarded);
        AssertThat(second.Status).IsEqual(ExamStatus.AlreadyPassed);
    }

    [TestCase]
    public void RepeatingAGuardianDoesNotChangeTheAchievement()
    {
        var engine = AdvTestData.NewEngine();
        AssertThat(engine.RecordGuardianKill("lodo_mae").FirstKill).IsTrue();
        AssertThat(engine.RecordGuardianKill("lodo_mae").FirstKill).IsFalse();
        AssertThat(engine.Progress.GuardiansDefeated.Count(g => g == "lodo_mae")).IsEqual(1);
    }

    [TestCase]
    public void HugeXpAndAllAchievementsReachAlvorInOneStep()
    {
        var engine = AdvTestData.NewEngine();
        engine.Progress.Xp = 999999;
        foreach (var step in AdvTestData.Catalog.Ranks.Where(r => r.Guardian is not null))
        {
            engine.Progress.GuardiansDefeated.Add(step.Guardian!);
            if (step.Exam)
            {
                engine.Progress.ExamsPassed.Add(AdvTestData.Catalog.ExamFor(step.Id)!.Id);
            }
        }

        AssertThat(engine.Rank).IsEqual(Rank.Alvor);
        AssertThat(engine.Stage).IsEqual(4);
        AssertThat(engine.RankStatus.AtTop).IsTrue();
        AssertThat(engine.RankStatus.Requirements).IsEmpty();
    }

    [TestCase]
    public void StatusListsMissingAchievementsInPortuguese()
    {
        var engine = AdvTestData.NewEngine();
        engine.AddXp(50);
        var status = engine.RankStatus;
        AssertThat(status.Next).IsEqual(Rank.D);
        AssertThat(status.Requirements.Count).IsEqual(2);
        AssertThat(status.Requirements[0].Text).IsEqual("Alcance 120 XP (faltam 70)");
        AssertThat(status.Requirements[1].Text).IsEqual("Derrote o Guardião do rank E: Lodo-Mãe");
        AssertThat(status.Missing.Count).IsEqual(2);

        engine.RecordGuardianKill("lodo_mae");
        AssertThat(engine.RankStatus.Missing.Count).IsEqual(1);
    }

    [TestCase]
    public void StatusAtRankCIncludesTheExam()
    {
        var engine = AdvTestData.HunterAt(Rank.C);
        var kinds = engine.RankStatus.Requirements.Select(r => r.Kind).ToArray();
        AssertThat(kinds).ContainsExactly(RequirementKind.Xp, RequirementKind.Guardian, RequirementKind.Exam);
        AssertThat(engine.RankStatus.Requirements[2].Text).IsEqual("Passe no Exame da Ordem do rank C");
    }

    [TestCase]
    public void MaxHpBonusGrowsTwentyPerRank()
    {
        var ladder = new RankLadder(AdvTestData.Catalog);
        AssertThat(ladder.MaxHpBonus(Rank.E)).IsEqual(0);
        AssertThat(ladder.MaxHpBonus(Rank.D)).IsEqual(20);
        AssertThat(ladder.MaxHpBonus(Rank.Alvor)).IsEqual(160);
    }

    [TestCase]
    public void EveryRankIsReachableInOrderUsingTheHelper()
    {
        foreach (var step in AdvTestData.Catalog.Ranks)
        {
            AssertThat(AdvTestData.HunterAt(step.Id).Rank).IsEqual(step.Id);
        }
    }
}
