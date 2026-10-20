using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Core.Rules;

/// <summary>Atributos derivados do jogador (<c>stats()</c>, <c>core.js:225-232</c>).</summary>
/// <param name="Hp">HP máximo (Selo + Nível + equipamento; ×0.6 sem Lanterna).</param>
/// <param name="Atk">Dano da arma empunhada já com nível e bônus de equipamento.</param>
/// <param name="AtkBonus">Soma do ataque dos equipamentos (antes do ×0.5 das adagas).</param>
/// <param name="Def">Defesa: redução percentual do dano recebido (teto 60%).</param>
/// <param name="Crit">Chance de crítico em pontos percentuais.</param>
/// <param name="Speed">Multiplicador de velocidade (1 = 100%).</param>
/// <param name="Light">Multiplicador de luz (×0.45 sem Lanterna).</param>
/// <param name="Essence">Multiplicador de ganho de Essência.</param>
/// <param name="Regen">HP por segundo (só depois de 4 s sem dano).</param>
/// <param name="Element">Infusão ativa na arma empunhada, ou null.</param>
public readonly record struct PlayerStats(int Hp, int Atk, int AtkBonus, int Def, int Crit, double Speed, double Light, double Essence, double Regen, string? Element);

/// <summary>Calcula <see cref="PlayerStats"/> a partir do save. Sem estado: recalcule quando XP, equipamento, arma ou Lanterna mudarem.</summary>
public sealed class PlayerStatsCalculator(GameDatabase db)
{
    public PlayerStats Compute(SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var rules = db.Rules.Stats;
        var hp = db.Progression.BaseMaxHp(save.Xp);
        int atk = 0, def = 0, crit = rules.BaseCrit;
        double speed = rules.BaseSpeed, light = rules.BaseLight, essence = rules.BaseEssence, regen = 0;

        // Ordem dos slots do protótipo; "light" substitui (o último encontrado vale, só a lanterna tem luz).
        foreach (var slot in db.GearSlots)
        {
            if (!save.Equipment.TryGetValue(slot.Id, out var gearId) || !db.Gear.TryGetValue(gearId, out var gear))
            {
                continue;
            }

            hp += gear.Hp;
            atk += gear.Atk;
            def += gear.Def;
            crit += gear.Crit;
            speed += gear.Spd;
            regen += gear.Regen;
            essence += gear.Ess;
            if (gear.Light is { } l)
            {
                light = l;
            }
        }

        if (save.LostLantern is not null)
        {
            hp = JsMath.Round(hp * db.Rules.Lantern.HpMult);
            light *= db.Rules.Lantern.LightMult;
        }

        var weaponId = save.CurrentWeapon;
        var level = Math.Max(1, save.WeaponLevel(weaponId));
        var gearAtkMult = rules.GearAtkMult.TryGetValue(weaponId, out var m) ? m : 1;
        var weaponAtk = JsMath.Round(BaseWeaponDamage(weaponId, level) + (atk * gearAtkMult));
        var element = save.ActiveInfusion.TryGetValue(weaponId, out var el) ? el : null;
        return new PlayerStats(hp, weaponAtk, atk, def, crit, speed, light, essence, regen, element);
    }

    /// <summary>Dano da arma sem arredondar: <c>dmg * (1 + 0.22 * (nível - 1))</c>.</summary>
    public double BaseWeaponDamage(string weaponId, int level) =>
        db.Weapons[weaponId].Damage * (1 + (db.WeaponData.DamageGrowthPerLevel * (level - 1)));

    /// <summary>Dano da arma no nível, arredondado como no HUD da forja (<c>world.js:235</c>).</summary>
    public int WeaponDamage(string weaponId, int level) => JsMath.Round(BaseWeaponDamage(weaponId, level));
}
