using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>Custos de forja e afinação. Lumes aqui são o preço base: cobre com <see cref="PriceCalculator"/>.</summary>
public sealed class UpgradeCosts(GameDatabase db)
{
    /// <summary>
    /// Upgrade lv → lv+1 no Brás (<c>world.js:236</c>): <c>40*lv</c> Lumes + <c>UPG[arma] × lv</c>
    /// (cajado <c>× 2*lv</c>). Null no nível máximo (5).
    /// </summary>
    public Cost? WeaponUpgrade(string weaponId, int currentLevel)
    {
        var weapon = db.Weapons[weaponId];
        if (currentLevel >= db.WeaponData.MaxLevel)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(currentLevel, 1);
        var mats = new Dictionary<string, int> { [weapon.UpgradeMaterial] = weapon.UpgradeMaterialPerLevel * currentLevel };
        return Cost.Of(db.WeaponData.UpgradeLumesPerLevel * currentLevel, mats);
    }

    /// <summary>Forjar uma arma nova (<c>WEAP.unlock</c>). A v2 recusa armas de outra classe; quem chama decide.</summary>
    public Cost WeaponUnlock(string weaponId) => db.Weapons[weaponId].Unlock;

    /// <summary>Infusão elemental no Gaspar (<c>ELEM_COST</c>).</summary>
    public Cost Infusion(string elementId) => db.Elements[elementId].Infusion;

    /// <summary>Afinar Eco na Vesna (<c>world.js:295</c>): <c>70+60*lv</c> Lumes + <c>4+2*lv</c> Estilhaços. Null no máximo (2).</summary>
    public Cost? EcoTuning(int currentEcoLevel)
    {
        var t = db.EcoData.Tuning;
        if (currentEcoLevel >= t.MaxLevel)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(currentEcoLevel);
        var mats = new Dictionary<string, int> { [t.Material] = t.MaterialBase + (t.MaterialPerLevel * currentEcoLevel) };
        return Cost.Of(t.LumesBase + (t.LumesPerLevel * currentEcoLevel), mats);
    }

    /// <summary>Essência para usar o Eco (<c>ecoCost</c>, <c>core.js:214</c>): custo base − 10 por afinação.</summary>
    public int EcoCost(string ecoId, int ecoLevel) => db.Ecos[ecoId].Cost - (db.EcoData.Tuning.CostReductionPerLevel * ecoLevel);
}
