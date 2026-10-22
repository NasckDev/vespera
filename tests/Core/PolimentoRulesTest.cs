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

    [TestCase]
    public void EscapeNeedsHalfASecondOfInsisting()
    {
        var intent = new EscapeIntent();
        // Um passo (0,25 s) na borda e soltar: nada.
        for (var i = 0; i < 15; i++)
        {
            AssertThat(intent.Update(inside: true, pushing: true, 1 / 60.0)).IsFalse();
        }

        AssertThat(intent.Update(inside: true, pushing: false, 1 / 60.0)).IsFalse();
        AssertThat(intent.Progress).IsEqual(0.0);

        // Insistir 0,5 s pergunta uma vez só, até o pé sair da borda.
        var fired = 0;
        for (var i = 0; i < 90; i++)
        {
            fired += intent.Update(inside: true, pushing: true, 1 / 60.0) ? 1 : 0;
        }

        AssertThat(fired).IsEqual(1);
        intent.Update(inside: false, pushing: false, 1 / 60.0);
        for (var i = 0; i < 31; i++)
        {
            fired += intent.Update(inside: true, pushing: true, 1 / 60.0) ? 1 : 0;
        }

        AssertThat(fired).IsEqual(2);
    }

    [TestCase]
    public void ArrivalsKeepAwayFromTheFleeEdge()
    {
        var back = new MapRect(790, 890, 90, 30); // Mercado de Peixes (maps.json)
        AssertThat(EscapeIntent.KeepAway(new MapPoint(830, 870), back)).IsEqual(new MapPoint(830, 890 - EscapeIntent.SpawnGap));
        AssertThat(EscapeIntent.KeepAway(new MapPoint(830, 700), back)).IsEqual(new MapPoint(830, 700));
        AssertThat(EscapeIntent.KeepAway(new MapPoint(300, 880), back)).IsEqual(new MapPoint(300, 880));
        AssertThat(EscapeIntent.InZone(back, 835, 905)).IsTrue();
        AssertThat(EscapeIntent.InZone(back, 835, 930)).IsTrue(); // passou do retângulo andando para baixo
        AssertThat(EscapeIntent.InZone(back, 835, 880)).IsFalse();
        AssertThat(EscapeIntent.IsPushingOut(1f)).IsTrue();
        AssertThat(EscapeIntent.IsPushingOut(0.1f)).IsFalse();
    }
}
