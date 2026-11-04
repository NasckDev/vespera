namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Final;
using Vespera.Core.Story;
using static GdUnit4.Assertions;

/// <summary>Quem toca o sino no Final (ROTEIRO-V2 §13 FINAL, fala 5): Benício, Lis ou o jogador, pelas flags da história.</summary>
[TestSuite]
public class BellChoiceTest
{
    private static Func<string, bool> Flags(params string[] on) => flag => on.Contains(flag);

    [TestCase]
    public void ThePlayerCanAlwaysRing()
    {
        AssertThat(BellChoice.Available(Flags())).ContainsExactly(BellRinger.Jogador);
    }

    [TestCase]
    public void BenicioRingsOnlyIfThePlayerBroughtHimToTheDecision()
    {
        AssertThat(BellChoice.Available(Flags(BellChoice.BenicioFlag)))
            .ContainsExactly(BellRinger.Benicio, BellRinger.Jogador);
    }

    [TestCase]
    public void LisRingsOnlyIfHiddenFromTheOrderAndInsideTheBreach()
    {
        AssertThat(BellChoice.Available(Flags(BellChoice.LisHiddenFlag))).ContainsExactly(BellRinger.Jogador);
        AssertThat(BellChoice.Available(Flags(BellChoice.LisHiddenFlag, BellChoice.LisInsideFlag)))
            .ContainsExactly(BellRinger.Lis, BellRinger.Jogador);
    }

    [TestCase]
    public void EachRingerHasItsOwnEnding()
    {
        var benicio = BellChoice.Outcome(BellRinger.Benicio);
        AssertThat(benicio.Flag).IsEqual("final_benicio");
        AssertThat(benicio.BenicioReturns).IsFalse();
        AssertThat(benicio.FirstBreachClosed).IsTrue();
        AssertThat(benicio.Epilogue).IsEqual("final/epilogo_sem_benicio");

        var lis = BellChoice.Outcome(BellRinger.Lis);
        AssertThat(lis.Flag).IsEqual("final_lis");
        AssertThat(lis.FirstBreachClosed).IsFalse();
        AssertThat(lis.TownsLinked).IsTrue();
        AssertThat(lis.EchoesStay).IsTrue();
        AssertThat(lis.Epilogue).IsEqual("final/epilogo");

        var player = BellChoice.Outcome(BellRinger.Jogador);
        AssertThat(player.Flag).IsEqual("final_jogador");
        AssertThat(player.FirstBreachClosed).IsTrue();
        AssertThat(player.EchoesStay).IsTrue();
        AssertThat(player.BenicioReturns).IsTrue();
    }

    [TestCase]
    public void RingBellEventsMapToRingers()
    {
        AssertThat(BellChoice.FromEvent("RingBell:benicio")).IsEqual(BellRinger.Benicio);
        AssertThat(BellChoice.FromEvent("RingBell:lis")).IsEqual(BellRinger.Lis);
        AssertThat(BellChoice.FromEvent("RingBell:jogador")).IsEqual(BellRinger.Jogador);
        AssertThat(BellChoice.FromEvent("BurnSeal:Alvor")).IsNull();
    }

    [TestCase]
    public void TheSinoDialogueOffersExactlyTheAvailableRingers()
    {
        var file = DialogueFile.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Dialogue", "final", "sino.json")));
        var cases = new[]
        {
            Array.Empty<string>(),
            [BellChoice.BenicioFlag],
            [BellChoice.LisHiddenFlag, BellChoice.LisInsideFlag],
            [BellChoice.BenicioFlag, BellChoice.LisHiddenFlag, BellChoice.LisInsideFlag],
        };
        foreach (var flags in cases)
        {
            var engine = new StoryEngine(new StoryProgress(), new StoryScript(), () => 0);
            foreach (var flag in flags)
            {
                engine.SetFlag(flag);
            }

            var runner = new DialogueRunner(file, engine, "Rui");
            AssertThat(runner.Start()).IsTrue();
            var offered = runner.Choices.Select(c => BellChoice.FromEvent(file.Nodes[c.Next!].Events[0])).ToList();
            AssertThat(offered).ContainsExactly(BellChoice.Available(engine.HasFlag).Cast<BellRinger?>().ToArray());
        }
    }
}
