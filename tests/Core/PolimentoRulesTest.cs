namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Regras da frente C1 (polimento da v0.1): Brechas do novato.</summary>
[TestSuite]
public class PolimentoRulesTest
{
    private static BrechaRules Brecha => new(TestDb.Instance);

    [TestCase]
    public void NoviceRankNeverPassesWhatTheSealCanEnter()
    {
        var db = TestDb.Instance;

        // Selo E (0): a Cripta (minRank 1) com o bônus de 25% daria C (2), que o Selo E não pode entrar.
        var rolled = Brecha.RollRank(db.Themes["crypt"], 0, first: false, new ConstantRandom(0.1));
        AssertThat(rolled).IsEqual(2);
        AssertThat(Brecha.CanEnter(rolled, 0)).IsFalse();

        var novice = Brecha.RollRank(db.Themes["crypt"], 0, first: false, novice: true, new ConstantRandom(0.1));
        AssertThat(novice).IsEqual(1);
        AssertThat(Brecha.CanEnter(novice, 0)).IsTrue();

        // Fora da proteção o sorteio da v2 continua igual.
        AssertThat(Brecha.RollRank(db.Themes["crypt"], 0, first: false, novice: false, new ConstantRandom(0.1))).IsEqual(2);
    }

    [TestCase]
    public void NoviceLastsForTheFirstThreeClosedBrechas()
    {
        AssertThat(Brecha.Def.NoviceBrechas).IsEqual(3);
        AssertThat(Brecha.IsNovice(0)).IsTrue();
        AssertThat(Brecha.IsNovice(2)).IsTrue();
        AssertThat(Brecha.IsNovice(3)).IsFalse();
    }

    [TestCase]
    public void NoviceScheduleOnlyOpensEnterableBrechas()
    {
        // Muitas aberturas com o bônus sempre ligado (0,1 < 0,25): o novato de Selo E nunca vê uma Brecha que não entra.
        var events = new List<BrechaEvent>();
        var s = new BrechaSchedule(TestDb.Instance, new ConstantRandom(0.1), firstDone: true) { Novice = true };
        for (var i = 0; i < 2000; i++)
        {
            s.Tick(1, 0, 1, events);
        }

        var opened = events.Where(e => e.Kind == BrechaEventKind.Opened).Select(e => e.Brecha).ToList();
        AssertThat(opened.Count).IsGreater(3);
        foreach (var b in opened)
        {
            AssertThat(Brecha.CanEnter(b.Rank, 0)).OverrideFailureMessage($"Brecha {b.ThemeId} rank {b.Rank} aberta para o Selo E").IsTrue();
        }

        // Sem a proteção, o mesmo sorteio abre Brechas acima do Selo (o comportamento da v2 depois das primeiras).
        var later = new List<BrechaEvent>();
        var veteran = new BrechaSchedule(TestDb.Instance, new ConstantRandom(0.1), firstDone: true);
        for (var i = 0; i < 2000; i++)
        {
            veteran.Tick(1, 0, 1, later);
        }

        AssertThat(later.Any(e => e.Kind == BrechaEventKind.Opened && !Brecha.CanEnter(e.Brecha.Rank, 0))).IsTrue();
    }
}
