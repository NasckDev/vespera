namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>stats() da v2 (spec 02 §2.4; §6.3 itens 3-4).</summary>
[TestSuite]
public class PlayerStatsCalculatorTest
{
    private static PlayerStatsCalculator Calc => new(TestDb.Instance);

    private static SaveData NewSave(string cls = "sword") => SaveData.NewGame(TestDb.Instance, "Rui", cls);

    [TestCase]
    public void StartingStatsMatchPrototype()
    {
        var st = Calc.Compute(NewSave());
        AssertThat(st.Hp).IsEqual(100);
        AssertThat(st.Atk).IsEqual(12);
        AssertThat(st.Def).IsEqual(6);
        AssertThat(st.Crit).IsEqual(5);
        AssertThat(st.Speed).IsEqual(1.0);
        AssertThat(st.Light).IsEqual(1.0);
        AssertThat(st.Essence).IsEqual(1.0);
        AssertThat(st.Regen).IsEqual(0.0);
        AssertThat(st.Element).IsNull();
    }

    [TestCase(0, 100)]
    [TestCase(100, 124)]
    [TestCase(7500, 334)]
    public void HpComesFromSealAndLevel(int xp, int hp)
    {
        var s = NewSave();
        s.Xp = xp;
        AssertThat(Calc.Compute(s).Hp).IsEqual(hp);
    }

    [TestCase(0, 60)]
    [TestCase(100, 74)]
    public void LostLanternCutsHpAndLight(int xp, int hp)
    {
        var s = NewSave();
        s.Xp = xp;
        s.LostLantern = new LostLantern();
        var st = Calc.Compute(s);
        AssertThat(st.Hp).IsEqual(hp);
        AssertThat(st.Light).IsEqualApprox(0.45, 1e-9);
    }

    // Tabela da spec 02 §1.6 (sem bônus de equipamento).
    [TestCase("sword", 1, 12)]
    [TestCase("sword", 2, 15)]
    [TestCase("sword", 3, 17)]
    [TestCase("sword", 4, 20)]
    [TestCase("sword", 5, 23)]
    [TestCase("bow", 1, 10)]
    [TestCase("bow", 2, 12)]
    [TestCase("bow", 3, 14)]
    [TestCase("bow", 4, 17)]
    [TestCase("bow", 5, 19)]
    [TestCase("staff", 1, 16)]
    [TestCase("staff", 2, 20)]
    [TestCase("staff", 3, 23)]
    [TestCase("staff", 4, 27)]
    [TestCase("staff", 5, 30)]
    [TestCase("hammer", 1, 30)]
    [TestCase("hammer", 2, 37)]
    [TestCase("hammer", 3, 43)]
    [TestCase("hammer", 4, 50)]
    [TestCase("hammer", 5, 56)]
    [TestCase("dag", 1, 6)]
    [TestCase("dag", 2, 7)]
    [TestCase("dag", 3, 9)]
    [TestCase("dag", 4, 10)]
    [TestCase("dag", 5, 11)]
    public void WeaponDamageByLevel(string weapon, int level, int expected)
    {
        AssertThat(Calc.WeaponDamage(weapon, level)).IsEqual(expected);
        var s = NewSave(weapon);
        s.Weapons[weapon] = level;
        AssertThat(Calc.Compute(s).Atk).IsEqual(expected);
    }

    [TestCase]
    public void DaggersUseHalfOfGearAttack()
    {
        var s = NewSave("dag");
        s.Equipment["maos"] = "luva"; // atk 3
        var st = Calc.Compute(s);
        AssertThat(st.AtkBonus).IsEqual(3);
        AssertThat(st.Atk).IsEqual(8); // round(6 + 1.5)

        var sword = NewSave();
        sword.Equipment["maos"] = "luva";
        AssertThat(Calc.Compute(sword).Atk).IsEqual(15);
    }

    [TestCase]
    public void GearStatsAddUpAndLightReplaces()
    {
        var s = NewSave("bow");
        s.Equipment["cabeca"] = "capuz";    // hp 10, def 2
        s.Equipment["capa"] = "capa2";      // crit 6, spd .06
        s.Equipment["lanterna"] = "ln_lunar"; // light 1.3, ess .3
        s.Equipment["reliquia"] = "rl_astro"; // ess .4, spd .04
        var st = Calc.Compute(s);
        AssertThat(st.Hp).IsEqual(110);
        AssertThat(st.Def).IsEqual(8);
        AssertThat(st.Crit).IsEqual(11);
        AssertThat(st.Speed).IsEqualApprox(1.10, 1e-9);
        AssertThat(st.Light).IsEqual(1.3);
        AssertThat(st.Essence).IsEqualApprox(1.7, 1e-9);
    }

    [TestCase]
    public void RegenAndElementComeFromSave()
    {
        var s = NewSave();
        s.Equipment["reliquia"] = "rl_concha";
        s.Infusions["sword"] = ["solar"];
        s.ActiveInfusion["sword"] = "solar";
        var st = Calc.Compute(s);
        AssertThat(st.Regen).IsEqual(1.5);
        AssertThat(st.Hp).IsEqual(125);
        AssertThat(st.Element).IsEqual("solar");
    }
}
