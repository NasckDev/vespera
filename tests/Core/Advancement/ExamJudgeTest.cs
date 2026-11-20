namespace Vespera.Tests.Core.Advancement;

using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Os desafios do exame da Ordem (golpes, tempo, objetivos de exams.json) valem de verdade (issue #79).</summary>
[TestSuite]
public class ExamJudgeTest
{
    private static ExamDef Exam(Rank rank) => AdvTestData.Catalog.ExamFor(rank)!;

    private static ExamRunStats Run(double seconds = 60, int hits = 0, int kills = 20, bool guardian = true) => new(seconds, hits, kills, guardian);

    [TestCase]
    public void SealSAllowsAtMostThreeHits()
    {
        var exam = Exam(Rank.S);
        AssertThat(ExamJudge.Judge(exam, Run(hits: 3)).Passed).IsTrue();

        var verdict = ExamJudge.Judge(exam, Run(hits: 4));
        AssertThat(verdict.Passed).IsFalse();
        AssertThat(verdict.Failures.Any(f => f.Contains("golpes"))).IsTrue();
    }

    [TestCase]
    public void SealSssAllowsAtMostSixHits()
    {
        var exam = Exam(Rank.SSS);
        AssertThat(ExamJudge.Judge(exam, Run(hits: 6)).Passed).IsTrue();
        AssertThat(ExamJudge.Judge(exam, Run(hits: 7)).Passed).IsFalse();
    }

    [TestCase]
    public void OverTheTimeLimitFails()
    {
        var exam = Exam(Rank.C);
        AssertThat(ExamJudge.Judge(exam, Run(seconds: exam.TimeLimitSeconds)).Passed).IsTrue();
        AssertThat(ExamJudge.Judge(exam, Run(seconds: exam.TimeLimitSeconds + 1)).Passed).IsFalse();
    }

    [TestCase]
    public void SealCNeedsToSurviveTheTide()
    {
        var exam = Exam(Rank.C);
        var seconds = exam.Objectives.Single(o => o.Type == "survive").Seconds!.Value;
        AssertThat(ExamJudge.Judge(exam, Run(seconds: seconds - 1)).Passed).IsFalse();
        AssertThat(ExamJudge.Judge(exam, Run(seconds: seconds)).Passed).IsTrue();
    }

    [TestCase]
    public void ReachingTheExitMeansTheGuardianFell()
    {
        AssertThat(ExamJudge.Judge(Exam(Rank.B), Run(guardian: false)).Passed).IsFalse();
        AssertThat(ExamJudge.Judge(Exam(Rank.B), Run()).Passed).IsTrue();
    }

    [TestCase]
    public void KillCountDoesNotCountTheGuardian()
    {
        var exam = Exam(Rank.SSS);
        var withKills = exam with { Objectives = [new ExamObjectiveDef { Type = "kill_count", Count = 10 }], MaxHits = null };
        AssertThat(ExamJudge.Judge(withKills, Run(kills: 10)).Passed).IsFalse(); // 9 ecos + o Guardião
        AssertThat(ExamJudge.Judge(withKills, Run(kills: 11)).Passed).IsTrue();
    }

    [TestCase]
    public void UnknownObjectiveFailsInsteadOfPassingSilently()
    {
        var exam = Exam(Rank.B) with { Objectives = [new ExamObjectiveDef { Type = "destroy_marked" }] };
        AssertThat(ExamJudge.Judge(exam, Run()).Passed).IsFalse();
    }

    [TestCase]
    public void EngineRefusesTheSealWhenChallengesFail()
    {
        var engine = AdvTestData.HunterAt(Rank.S);
        var xp = engine.Progress.Xp;

        var failed = engine.TryPassExam(Rank.S, Run(hits: 9));

        AssertThat(failed.Status).IsEqual(ExamStatus.Failed);
        AssertThat(failed.Failures!.Count).IsGreater(0);
        AssertThat(engine.Progress.ExamsPassed.Contains(Exam(Rank.S).Id)).IsFalse();
        AssertThat(engine.Progress.Xp).IsEqual(xp);

        var passed = engine.TryPassExam(Rank.S, Run(hits: 2));
        AssertThat(passed.Passed).IsTrue();
        AssertThat(engine.Progress.ExamsPassed.Contains(Exam(Rank.S).Id)).IsTrue();
    }

    [TestCase]
    public void EngineWithoutRunStatsKeepsTheRankOnlyRule()
    {
        var engine = AdvTestData.HunterAt(Rank.S);
        AssertThat(engine.TryPassExam(Rank.S).Passed).IsTrue();
    }

    [TestCase]
    public void RulesTextMentionsTheRealLimits()
    {
        var text = ExamJudge.Rules(Exam(Rank.S));
        AssertThat(text.Contains("3 golpes")).IsTrue();
        AssertThat(text.Contains(ExamJudge.Clock(Exam(Rank.S).TimeLimitSeconds))).IsTrue();
    }

    [TestCase]
    public void EveryRealExamIsPassableByAPerfectRun()
    {
        foreach (var exam in AdvTestData.Catalog.ExamList)
        {
            var kills = exam.Objectives.Where(o => o.Type == "kill_count").Select(o => o.Count ?? 0).DefaultIfEmpty(0).Max() + 1;
            var survive = exam.Objectives.Where(o => o.Type == "survive").Select(o => o.Seconds ?? 0).DefaultIfEmpty(0).Max();
            var verdict = ExamJudge.Judge(exam, new ExamRunStats(Math.Max(survive, 1), 0, kills, true));
            AssertThat(verdict.Passed).IsTrue();
        }
    }
}
