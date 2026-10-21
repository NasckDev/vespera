namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

[TestSuite]
public class PropHealthTest
{
    [TestCase]
    public void BreaksWhenTheDamageAddsUp()
    {
        var hp = new PropHealth(30);
        AssertThat(hp.Hit(12)).IsEqual(PropHitResult.Damaged);
        AssertThat(hp.Hit(12)).IsEqual(PropHitResult.Damaged);
        AssertThat(hp.Fraction).IsEqualApprox(0.2f, 0.001f);
        AssertThat(hp.Hit(12)).IsEqual(PropHitResult.Broken);
        AssertThat(hp.IsBroken).IsTrue();
        AssertThat(hp.Hit(12)).IsEqual(PropHitResult.Ignored);
        AssertThat(new PropHealth(10).Hit(0)).IsEqual(PropHitResult.Ignored);
    }

    [TestCase]
    public void HpComesFromTheData()
    {
        var rules = TestDb.Instance.Rules.World.Props;
        AssertThat(PropHealth.HpFor("barrel", rules.Hp, rules.FallbackHp)).IsEqual(30);
        AssertThat(PropHealth.HpFor("altar", rules.Hp, rules.FallbackHp)).IsEqual(rules.FallbackHp);
        // a Lâmina (12 de dano) quebra saco e urna num golpe, caixa em dois, barril em três
        var sword = TestDb.Instance.Weapons["sword"].Damage;
        AssertThat((int)Math.Ceiling(rules.Hp["sack"] / (double)sword)).IsEqual(1);
        AssertThat((int)Math.Ceiling(rules.Hp["crate"] / (double)sword)).IsEqual(2);
        AssertThat((int)Math.Ceiling(rules.Hp["barrel"] / (double)sword)).IsEqual(3);
    }
}
