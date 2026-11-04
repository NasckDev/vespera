namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Final;
using Vespera.Core.Hunt;
using static GdUnit4.Assertions;

/// <summary>
/// Luta do Reflexo (ROTEIRO-V2 §13 FINAL): três fases de intensidade, golpes que alternam na ordem das armas mais usadas
/// pelo jogador (a do minuto lidera), aviso âmbar que encurta, esquiva para o lado do jogador; e as regras de história do
/// Final (Brecha aberta, sino, ilustração de quem tocou, fim da Parte 1).
/// </summary>
[TestSuite]
public class ReflexoPatternTest
{
    private static readonly double[] Phases = [0.66, 0.33];

    private static ReflexoProfile Profile(int sword, int bow, int hammer, int left = 0, int right = 0, int dodges = 0, int hits = 0) =>
        ReflexoProfile.From(
            new CombatStats
            {
                Weapons = new() { ["sword"] = sword, ["bow"] = bow, ["hammer"] = hammer },
                DodgesLeft = left,
                DodgesRight = right,
                Dodges = dodges,
                HitsTaken = hits,
            },
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

    private static ReflexoPattern Pattern(ReflexoProfile profile) => new(profile, new ReflexoTuning(), Phases);

    private static Func<string, bool> Flags(params string[] on) => flag => on.Contains(flag);

    [TestCase]
    public void PhaseFollowsTheHpThresholds()
    {
        var pattern = Pattern(Profile(1, 0, 0));
        AssertThat(pattern.Phase(1.0)).IsEqual(1);
        AssertThat(pattern.Phase(0.66)).IsEqual(2);
        AssertThat(pattern.Phase(0.4)).IsEqual(2);
        AssertThat(pattern.Phase(0.33)).IsEqual(3);
        AssertThat(pattern.Phase(0.0)).IsEqual(3);
    }

    [TestCase]
    public void PhaseOneOnlyUsesThePreferredWeapon()
    {
        var pattern = Pattern(Profile(sword: 1, bow: 9, hammer: 3));
        for (var i = 0; i < 4; i++)
        {
            AssertThat(pattern.Next(1, 0)).IsEqual(ReflexoAttack.Arco);
        }
    }

    [TestCase]
    public void EachPhaseAddsTheNextMostUsedWeapon()
    {
        var pattern = Pattern(Profile(sword: 1, bow: 9, hammer: 3));
        AssertThat(pattern.Pool(2, 0)).ContainsExactly(ReflexoAttack.Arco, ReflexoAttack.Martelo);
        AssertThat(pattern.Pool(3, 0)).ContainsExactly(ReflexoAttack.Arco, ReflexoAttack.Martelo, ReflexoAttack.Lamina);
        var third = new[] { pattern.Next(3, 0), pattern.Next(3, 0), pattern.Next(3, 0), pattern.Next(3, 0) };
        AssertThat(third).ContainsExactly(ReflexoAttack.Arco, ReflexoAttack.Martelo, ReflexoAttack.Lamina, ReflexoAttack.Arco);
    }

    [TestCase]
    public void TheMinuteWeaponLeadsBecauseHeLearns()
    {
        var pattern = Pattern(Profile(sword: 1, bow: 9, hammer: 3));
        AssertThat(pattern.Pool(1, 1)).ContainsExactly(ReflexoAttack.Martelo);
        AssertThat(pattern.Pool(2, 2)).ContainsExactly(ReflexoAttack.Lamina, ReflexoAttack.Arco);
    }

    [TestCase]
    public void PeekDoesNotSpendTheTurn()
    {
        var pattern = Pattern(Profile(sword: 5, bow: 4, hammer: 3));
        AssertThat(pattern.Peek(2, 0)).IsEqual(ReflexoAttack.Lamina);
        AssertThat(pattern.Peek(2, 0)).IsEqual(ReflexoAttack.Lamina);
        AssertThat(pattern.Next(2, 0)).IsEqual(ReflexoAttack.Lamina);
        AssertThat(pattern.Peek(2, 0)).IsEqual(ReflexoAttack.Arco);
    }

    [TestCase]
    public void TheAmberWarningGetsShorterEachPhaseButNeverDisappears()
    {
        var pattern = Pattern(Profile(1, 0, 0));
        AssertThat(pattern.Telegraph(1)).IsGreater(pattern.Telegraph(2));
        AssertThat(pattern.Telegraph(2)).IsGreater(pattern.Telegraph(3));
        AssertThat(pattern.Telegraph(3)).IsGreaterEqual(0.5);
        AssertThat(pattern.Cooldown(1)).IsGreater(pattern.Cooldown(3));
        AssertThat(pattern.Arrows(1)).IsEqual(1);
        AssertThat(pattern.Arrows(3)).IsEqual(3);
    }

    [TestCase]
    public void HeDodgesToThePlayersFavoriteSide()
    {
        AssertThat(Pattern(Profile(1, 0, 0, left: 10, right: 2)).DodgeDirection).IsEqual(-1);
        AssertThat(Pattern(Profile(1, 0, 0, left: 1, right: 9)).DodgeDirection).IsEqual(1);
    }

    [TestCase]
    public void DodgeChanceComesFromThePlayersRhythmAndGrowsWithThePhase()
    {
        // Jogador que esquiva 3 de 4 avisos: chance do perfil 0,6 (máximo); fase 1 = 0,6 × 0,6.
        var pattern = Pattern(Profile(1, 0, 0, dodges: 30, hits: 10));
        AssertThat(pattern.DodgeChance(1)).IsEqual(ReflexoProfile.MaxDodgeChance * 0.6);
        AssertThat(pattern.DodgeChance(3)).IsEqual(ReflexoProfile.MaxDodgeChance);
        AssertThat(pattern.ShouldDodge(3, 0.59)).IsTrue();
        AssertThat(pattern.ShouldDodge(3, 0.61)).IsFalse();
        var careless = Pattern(Profile(1, 0, 0, dodges: 0, hits: 10));
        AssertThat(careless.DodgeChance(1)).IsEqual(ReflexoProfile.MinDodgeChance * 0.6);
    }

    [TestCase]
    public void OneLinePerMinuteOfFight()
    {
        var pattern = Pattern(Profile(1, 0, 0));
        AssertThat(pattern.MinuteAt(0)).IsEqual(0);
        AssertThat(pattern.MinuteAt(59.9)).IsEqual(0);
        AssertThat(pattern.MinuteAt(60)).IsEqual(1);
        AssertThat(pattern.MinuteAt(185)).IsEqual(3);
    }

    [TestCase]
    public void FinalBrechaDataHasTheReflexoRhythm()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "brecha_final.json"));
        var tuning = ReflexoTuning.FromBrechaJson(json);
        AssertThat(tuning.Validate()).IsEmpty();
        AssertThat(tuning.Telegraph).HasSize(3);
        AssertThat(tuning.LineSeconds).IsEqual(60.0);
    }

    [TestCase]
    public void TuningRejectsListsWithoutThreePhases()
    {
        var tuning = new ReflexoTuning { Telegraph = [0.8, 0.6] };
        AssertThat(tuning.Validate()).IsNotEmpty();
        AssertThrown(() => ReflexoTuning.FromBrechaJson("{\"id\": \"x\"}")).IsInstanceOf<FormatException>();
    }

    [TestCase]
    public void TheFirstBreachOpensAfterActSevenAndClosesWithTheBell()
    {
        AssertThat(FinalStory.BreachOpen(Flags())).IsFalse();
        AssertThat(FinalStory.BreachOpen(Flags(FinalStory.UnlockFlag))).IsTrue();
        AssertThat(FinalStory.BreachOpen(Flags(FinalStory.UnlockFlag, FinalStory.AlvorFlag))).IsFalse();
    }

    [TestCase]
    public void TheBellWaitsForTheReflexoToFall()
    {
        AssertThat(FinalStory.BellReady(Flags())).IsFalse();
        AssertThat(FinalStory.BellReady(Flags(FinalStory.ReflexoFellFlag))).IsTrue();
        AssertThat(FinalStory.BellReady(Flags(FinalStory.ReflexoFellFlag, FinalStory.AlvorFlag))).IsFalse();
    }

    [TestCase]
    public void EachRingerHasItsIllustration()
    {
        AssertThat(FinalStory.SceneFor(BellRinger.Benicio)).EndsWith("cena_sino_benicio.png");
        AssertThat(FinalStory.SceneFor(BellRinger.Lis)).EndsWith("cena_sino_lis.png");
        AssertThat(FinalStory.SceneFor(BellRinger.Jogador)).EndsWith("cena_sino_cacador.png");
    }

    [TestCase]
    public void EnterBreachEventCarriesTheBrechaId()
    {
        AssertThat(FinalStory.BreachFromEvent("EnterBreach:primeira_brecha")).IsEqual(FinalStory.BrechaId);
        AssertThat(FinalStory.BreachFromEvent("EnterBreach:")).IsNull();
        AssertThat(FinalStory.BreachFromEvent("RingBell:lis")).IsNull();
        AssertThat(FinalStory.PartOneDone(Flags(FinalStory.PartOneDoneFlag))).IsTrue();
    }
}
