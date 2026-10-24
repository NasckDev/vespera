namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Final;
using Vespera.Core.Hunt;
using static GdUnit4.Assertions;

/// <summary>
/// O Reflexo (Final, ROTEIRO-V2 §8 e §13): perfil de luta tirado das estatísticas salvas (arma preferida, bênçãos
/// favoritas, ritmo e lado da esquiva) e uma lanterna acesa (buff) por Lanterna perdida no jogo.
/// </summary>
[TestSuite]
public class ReflexoTest
{
    private static readonly Dictionary<string, string> Families = new()
    {
        ["brasa"] = "chama",
        ["faisca"] = "chama",
        ["passo_longo"] = "vigilia",
        ["caldo_forte"] = "sopa",
    };

    private static readonly Dictionary<string, string> FamilyNames = new()
    {
        ["chama"] = "Chama",
        ["vigilia"] = "Vigília",
        ["sopa"] = "Sopa",
    };

    private static CombatStats Stats() => new()
    {
        Weapons = new() { ["sword"] = 2, ["hammer"] = 7, ["bow"] = 1 },
        Blessings = new() { ["brasa"] = 3, ["faisca"] = 2, ["passo_longo"] = 4, ["caldo_forte"] = 1 },
        Dodges = 30,
        HitsTaken = 10,
        DodgesLeft = 21,
        DodgesRight = 9,
        LanternsLost = 7,
    };

    [TestCase]
    public void PreferredWeaponIsTheMostHuntedOne()
    {
        var profile = ReflexoProfile.From(Stats(), Families, FamilyNames);
        AssertThat(profile.PreferredWeapon).IsEqual("hammer");
    }

    [TestCase]
    public void FavoriteFamilySumsEveryBlessingOfTheFamily()
    {
        // Chama 3+2 = 5 vence Vigília 4, mesmo com Passo Longo sendo a bênção mais escolhida sozinha.
        var profile = ReflexoProfile.From(Stats(), Families, FamilyNames);
        AssertThat(profile.FavoriteFamily).IsEqual("chama");
        AssertThat(profile.FavoriteBlessings).ContainsExactly("passo_longo", "brasa", "faisca");
    }

    [TestCase]
    public void DodgeSideAndRhythmComeFromTheStats()
    {
        var profile = ReflexoProfile.From(Stats(), Families, FamilyNames);
        AssertThat(profile.DodgeSide).IsEqual(DodgeSide.Left);
        AssertThat(profile.DodgeRate).IsEqual(0.75);
        // Ritmo de esquiva do Reflexo: a taxa do jogador, presa entre o mínimo e o máximo.
        AssertThat(profile.DodgeChance).IsEqual(ReflexoProfile.MaxDodgeChance);
    }

    [TestCase]
    public void TokensFillTheReflexoLines()
    {
        var tokens = ReflexoProfile.From(Stats(), Families, FamilyNames).Tokens();
        AssertThat(tokens["lado_esquiva"]).IsEqual("esquerda");
        AssertThat(tokens["familia_medo"]).IsEqual("Chama");
        AssertThat(tokens["lanternas_perdidas"]).IsEqual("7");
    }

    [TestCase]
    public void EachLostLanternLightsOneMoreLampOnTheReflexo()
    {
        var none = ReflexoBuff.For(0);
        var seven = ReflexoBuff.For(7);
        AssertThat(none.Lamps).IsEqual(0);
        AssertThat(none.DamageMult).IsEqual(1.0);
        AssertThat(none.HpMult).IsEqual(1.0);
        AssertThat(seven.Lamps).IsEqual(7);
        AssertThat(seven.DamageMult).IsEqual(1 + (7 * ReflexoBuff.DamagePerLamp));
        AssertThat(seven.HpMult).IsEqual(1 + (7 * ReflexoBuff.HpPerLamp));
    }

    [TestCase]
    public void LampsAreCappedSoTheFightStaysWinnable()
    {
        var many = ReflexoBuff.For(100);
        AssertThat(many.Lamps).IsEqual(100);
        AssertThat(many.DamageMult).IsEqual(1 + (ReflexoBuff.MaxLamps * ReflexoBuff.DamagePerLamp));
        AssertThat(ReflexoBuff.For(-3).Lamps).IsEqual(0);
    }

    [TestCase]
    public void EmptyStatsGiveAPlainReflexoWithTheBlade()
    {
        var profile = ReflexoProfile.From(new CombatStats(), Families, FamilyNames);
        AssertThat(profile.PreferredWeapon).IsEqual("sword");
        AssertThat(profile.FavoriteBlessings).IsEmpty();
        AssertThat(profile.DodgeSide).IsEqual(DodgeSide.Right);
        AssertThat(profile.DodgeChance).IsEqual(ReflexoProfile.MinDodgeChance);
        AssertThat(profile.Tokens()["familia_medo"]).IsEqual(ReflexoProfile.NoFamilyName);
    }

    [TestCase]
    public void EachMinuteTheReflexoSwitchesToTheNextMostUsedWeapon()
    {
        var profile = ReflexoProfile.From(Stats(), Families, FamilyNames);
        AssertThat(profile.AttackForMinute(0)).IsEqual(ReflexoAttack.Martelo);
        AssertThat(profile.AttackForMinute(1)).IsEqual(ReflexoAttack.Lamina);
        AssertThat(profile.AttackForMinute(2)).IsEqual(ReflexoAttack.Arco);
        AssertThat(profile.AttackForMinute(3)).IsEqual(ReflexoAttack.Martelo);
    }

    [TestCase]
    public void LineForMinuteFollowsTheScriptFlags()
    {
        AssertThat(ReflexoProfile.MinuteFlag(0)).IsNull();
        AssertThat(ReflexoProfile.MinuteFlag(1)).IsEqual("reflexo_min_2");
        AssertThat(ReflexoProfile.MinuteFlag(3)).IsEqual("reflexo_min_4");
        AssertThat(ReflexoProfile.MinuteFlag(9)).IsEqual("reflexo_min_4");
    }
}
