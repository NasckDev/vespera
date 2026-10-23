using System.Collections.Generic;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.Save;


namespace Vespera.Game.Combat;

/// <summary>
/// Atributos de combate do caçador. Com sessão, vêm da <see cref="Hunter.Progression"/> (fonte única da v2): vida base + bônus de
/// rank e peças, dano da arma no nível forjado, defesa e crítico do equipamento. Sem sessão (fase aberta direto num teste),
/// cai no cálculo antigo do save. A Lanterna perdida não reduz a vida: a regra da v2 só deixa os Fragmentos na Brecha.
/// </summary>
public static class HunterStats
{
    public static PlayerStats Compute(GameDatabase db, SaveData save, string weaponId)
    {
        if (!Hunter.Progression.IsActive)
        {
            return new PlayerStatsCalculator(db).Compute(save);
        }

        var engine = Hunter.Progression.Engine;
        var rules = db.Rules.Stats;
        var gear = engine.Equipment().Total;
        var level = engine.Progress.WeaponLevels.GetValueOrDefault(weaponId, 1);
        var weapon = engine.Forge.Stats(engine.Catalog.Weapons.ContainsKey(weaponId) ? weaponId : engine.ClassId, level);
        var hp = db.Progression.BaseMaxHp(0) + engine.MaxHpBonus;
        var crit = (int)System.Math.Round(weapon.CritPct + gear.CritPct);
        var speed = rules.BaseSpeed * (1 + (gear.SpeedPct / 100.0));
        return new PlayerStats(hp, weapon.Damage, 0, gear.Defense, crit, speed, rules.BaseLight, rules.BaseEssence, 0, null);
    }
}
