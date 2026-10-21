using Vespera.Core.Ranks;

namespace Vespera.Core.Data;

/// <summary>Validação de referências cruzadas: todo id citado por um dado aponta para algo que existe.</summary>
public sealed partial class GameDatabase
{
    private void Validate(List<string> errors)
    {
        var check = new RefChecker(errors);
        var rankIds = Ranks.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var slotIds = GearSlots.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var seasonIds = SeasonData.Seasons.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        ValidateProgression(errors);

        foreach (var theme in ThemeList)
        {
            var ctx = $"tema '{theme.Id}'";
            check.Ref(theme.Boss, Monsters, $"{ctx}.boss");
            if (Monsters.TryGetValue(theme.Boss, out var boss) && !boss.Boss)
            {
                errors.Add($"{ctx}.boss '{theme.Boss}' não é chefe");
            }

            foreach (var mob in theme.Mobs)
            {
                check.Ref(mob.Monster, Monsters, $"{ctx}.mobs");
                check.Positive(mob.Weight, $"{ctx}.mobs['{mob.Monster}'].weight");
            }

            check.Ref(theme.Extra, Monsters, $"{ctx}.extra");
            check.Ref(theme.Elite, Monsters, $"{ctx}.elite");
            check.Ref(theme.PropMaterial, Materials, $"{ctx}.propMaterial");
            check.RankIndex(theme.MinRank, Ranks.Count, $"{ctx}.minRank");
            foreach (var season in seasonIds.Where(s => !theme.Titles.ContainsKey(s)))
            {
                errors.Add($"{ctx}.titles sem a estação '{season}'");
            }

            foreach (var key in theme.Titles.Keys.Where(k => !seasonIds.Contains(k)))
            {
                errors.Add($"{ctx}.titles tem estação desconhecida '{key}'");
            }
        }

        foreach (var weapon in WeaponOrder)
        {
            var ctx = $"arma '{weapon.Id}'";
            check.Cost(weapon.Unlock, Materials, $"{ctx}.unlock");
            check.Ref(weapon.UpgradeMaterial, Materials, $"{ctx}.upgradeMaterial");
            check.Refs(weapon.Elements, Elements, $"{ctx}.elements");
            var melee = weapon.Kind is WeaponKind.Melee or WeaponKind.Smash;
            if (melee != (weapon.Arc is not null && weapon.Range is not null))
            {
                errors.Add($"{ctx}: arc/range são obrigatórios em melee/smash e proibidos nas demais");
            }
        }

        foreach (var element in Elements.Values)
        {
            check.Cost(element.Infusion, Materials, $"elemento '{element.Id}'.infusion");
        }

        foreach (var material in Materials.Values)
        {
            check.Ref(material.Rarity, Rarities, $"material '{material.Id}'.rarity");
        }

        foreach (var rarity in Rarities.Values)
        {
            check.In(rarity.MinRank, rankIds, $"raridade '{rarity.Id}'.minRank");
        }

        foreach (var item in Gear.Values)
        {
            var ctx = $"equipamento '{item.Id}'";
            check.In(item.Slot, slotIds, $"{ctx}.slot");
            check.Ref(item.Rarity, Rarities, $"{ctx}.rarity");
            check.Refs(item.Classes ?? [], Classes, $"{ctx}.classes");
            if (item.MinRank is not null)
            {
                check.In(item.MinRank, rankIds, $"{ctx}.minRank");
            }
        }

        foreach (var recipe in Recipes)
        {
            check.Ref(recipe.Gear, Gear, "receita.gear");
            check.Cost(recipe.Cost, Materials, $"receita '{recipe.Gear}'");
        }

        foreach (var offer in Shops.Relics)
        {
            check.Ref(offer.Gear, Gear, "relíquia.gear");
            check.Cost(offer.Cost, Materials, $"relíquia '{offer.Gear}'");
        }

        foreach (var service in Shops.Services)
        {
            var ctx = $"serviço '{service.Id}'";
            check.Cost(service.Cost, Materials, ctx);
            if (service.Gear is not null)
            {
                check.Ref(service.Gear, Gear, $"{ctx}.gear");
            }

            var effects = (service.Potions > 0 ? 1 : 0) + (service.Gear is null ? 0 : 1) + (service.Rest ? 1 : 0) + (service.LanternRescue ? 1 : 0);
            if (effects != 1)
            {
                errors.Add($"{ctx} precisa de exatamente um efeito (potions, gear, rest ou lanternRescue)");
            }
        }

        foreach (var monster in Monsters.Values)
        {
            ValidateMonster(monster, check, errors);
        }

        check.Ref(EcoData.Tuning.Material, Materials, "ecos.tuning.material");
        foreach (var eco in Ecos.Values)
        {
            check.Ref(eco.Source, Monsters, $"Eco '{eco.Id}'.source");
            if (Monsters.TryGetValue(eco.Source, out var source) && source.Eco != eco.Id)
            {
                errors.Add($"Eco '{eco.Id}'.source '{eco.Source}' não deixa este Eco");
            }
        }

        foreach (var ultimate in Ultimates.Values)
        {
            check.Ref(ultimate.Class, Classes, $"Supremo '{ultimate.Name}'.class");
        }

        foreach (var cls in ClassList)
        {
            check.Ref(cls.Id, Weapons, $"classe '{cls.Id}' (arma)");
            if (!Ultimates.ContainsKey(cls.Id))
            {
                errors.Add($"classe '{cls.Id}' sem Golpe Supremo");
            }
        }

        foreach (var walker in Npcs.Walkers)
        {
            if (walker.Path.Count == 0 || walker.Lines.Count == 0 || walker.HourTo <= walker.HourFrom)
            {
                errors.Add($"NPC '{walker.Id}' precisa de caminho, falas e horário válido");
            }
        }

        foreach (var hunter in Social.Hunters)
        {
            check.In(hunter.Rank, rankIds, $"caçador '{hunter.Name}'.rank");
            check.Ref(hunter.Weapon, Weapons, $"caçador '{hunter.Name}'.weapon");
        }

        foreach (var quest in Quests)
        {
            ValidateQuest(quest, check, rankIds, errors);
        }

        ValidateRules(check, slotIds, errors);
    }

    private void ValidateProgression(List<string> errors)
    {
        if (Ranks.Count < 2 || Ranks[0].Xp != 0)
        {
            errors.Add("ranks: precisa de ao menos 2 Selos e o primeiro começa em 0 XP");
        }

        for (var i = 1; i < Ranks.Count; i++)
        {
            if (Ranks[i].Xp <= Ranks[i - 1].Xp)
            {
                errors.Add($"ranks: XP de '{Ranks[i].Id}' precisa ser maior que o de '{Ranks[i - 1].Id}'");
            }
        }

        var enumNames = Enum.GetNames<Rank>();
        if (enumNames.Length != Ranks.Count)
        {
            errors.Add($"ranks: {Ranks.Count} Selos nos dados, mas o enum Rank tem {enumNames.Length}");
        }
        else
        {
            for (var i = 0; i < Ranks.Count; i++)
            {
                if (!string.Equals(enumNames[i], Ranks[i].Id, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"ranks[{i}]: id '{Ranks[i].Id}' não bate com Rank.{enumNames[i]}");
                }
            }
        }

        if (RankData.Level.BaseNeed <= 0 || RankData.Level.Growth < 1)
        {
            errors.Add("ranks.level: baseNeed > 0 e growth >= 1");
        }

        if (SeasonData.Seasons.Count == 0 || SeasonData.DaysPerSeason <= 0 || SeasonData.FirstSeasonIndex < 0)
        {
            errors.Add("seasons: precisa de estações, daysPerSeason > 0 e firstSeasonIndex >= 0");
        }
    }

    private void ValidateMonster(MonsterDef monster, RefChecker check, List<string> errors)
    {
        var ctx = $"monstro '{monster.Id}'";
        if (monster.Parts.Count == 0)
        {
            errors.Add($"{ctx} sem partes");
        }

        foreach (var part in monster.Parts)
        {
            check.Ref(part.Drop, Materials, $"{ctx}.parts['{part.Id}'].drop");
            if (part.Count < 1 || part.Hp <= 0)
            {
                errors.Add($"{ctx}.parts['{part.Id}']: count >= 1 e hp > 0");
            }
        }

        if (monster.Summon is not null)
        {
            check.Ref(monster.Summon, Monsters, $"{ctx}.summon");
        }

        if (monster.Eco is not null)
        {
            check.Ref(monster.Eco, Ecos, $"{ctx}.eco");
        }

        check.Refs(monster.Weak, Elements, $"{ctx}.weak");
        check.Refs(monster.Res, Elements, $"{ctx}.res");
        if (monster.Glow is not null)
        {
            check.Ref(monster.Glow.Light, Palette.Lights, $"{ctx}.glow.light");
        }
    }

    private void ValidateQuest(QuestDef quest, RefChecker check, HashSet<string> rankIds, List<string> errors)
    {
        var ctx = $"missão '{quest.Id}'";
        check.Refs(quest.Reward.Mats.Keys, Materials, $"{ctx}.reward.mats");
        var c = quest.Condition;
        switch (c.Kind)
        {
            case QuestConditionKind.Counter when string.IsNullOrEmpty(c.Key) || c.Target <= 0:
                errors.Add($"{ctx}: condição counter precisa de key e target > 0");
                break;
            case QuestConditionKind.WeaponLevel when c.Target < 1 || c.Target > WeaponData.MaxLevel:
                errors.Add($"{ctx}: weaponLevel fora de 1..{WeaponData.MaxLevel}");
                break;
            case QuestConditionKind.Rank:
                check.In(c.Rank ?? string.Empty, rankIds, $"{ctx}.condition.rank");
                break;
            case QuestConditionKind.ThemeClosed:
                check.Ref(c.Theme ?? string.Empty, Themes, $"{ctx}.condition.theme");
                break;
        }

        if (quest.End is not null && !Npcs.Portraits.ContainsKey(quest.End.Who))
        {
            errors.Add($"{ctx}.end.who '{quest.End.Who}' sem retrato em npcs.json");
        }
    }

    private void ValidateRules(RefChecker check, HashSet<string> slotIds, List<string> errors)
    {
        var start = Rules.Start;
        check.Refs(start.Backpack, Gear, "rules.start.backpack");
        foreach (var (slot, gearId) in start.Equipment)
        {
            check.In(slot, slotIds, "rules.start.equipment (slot)");
            if (check.Ref(gearId, Gear, "rules.start.equipment") && Gear[gearId].Slot != slot)
            {
                errors.Add($"rules.start.equipment: '{gearId}' não é do slot '{slot}'");
            }
        }

        check.Ref(start.DefaultClass, Classes, "rules.start.defaultClass");
        check.Ref(Rules.Economy.ShardMaterial, Materials, "rules.economy.shardMaterial");
        check.Refs(Rules.Stats.GearAtkMult.Keys, Weapons, "rules.stats.gearAtkMult");

        var loot = Rules.Loot;
        check.Refs(loot.GearExclude, Gear, "rules.loot.gearExclude");
        foreach (var rarity in Rarities.Keys)
        {
            if (!loot.GearWeights.ContainsKey(rarity) || !loot.GearWeightsBoss.ContainsKey(rarity))
            {
                errors.Add($"rules.loot: raridade '{rarity}' sem peso em gearWeights/gearWeightsBoss");
            }
        }

        check.Refs(loot.Chest.BasicMaterials, Materials, "rules.loot.chest.basicMaterials");
        check.Ref(loot.Prop.FallbackMaterial, Materials, "rules.loot.prop.fallbackMaterial");

        var brecha = Rules.Brecha;
        check.Ref(brecha.FirstTheme, Themes, "rules.brecha.firstTheme");
        check.Ref(brecha.Invasion.Alpha, Monsters, "rules.brecha.invasion.alpha");
        check.RankIndex(brecha.MaxRank, Ranks.Count, "rules.brecha.maxRank");

        var world = Rules.World;
        check.Ref(world.Flower.Material, Materials, "rules.world.flower.material");
        check.Ref(world.Fishing.Material, Materials, "rules.world.fishing.material");
        if (world.CharacterScale <= 0 || world.PropScale <= 0)
        {
            errors.Add("rules.world: characterScale e propScale precisam ser positivos");
        }
    }

    private sealed class RefChecker(List<string> errors)
    {
        public bool Ref<T>(string id, IReadOnlyDictionary<string, T> table, string context)
        {
            if (table.ContainsKey(id))
            {
                return true;
            }

            errors.Add($"{context}: '{id}' não existe");
            return false;
        }

        public void Refs<T>(IEnumerable<string> ids, IReadOnlyDictionary<string, T> table, string context)
        {
            foreach (var id in ids)
            {
                Ref(id, table, context);
            }
        }

        public void In(string id, HashSet<string> set, string context)
        {
            if (!set.Contains(id))
            {
                errors.Add($"{context}: '{id}' não existe");
            }
        }

        public void Cost(Cost cost, IReadOnlyDictionary<string, MaterialDef> materials, string context)
        {
            if (cost.Lumes < 0)
            {
                errors.Add($"{context}: Lumes negativos");
            }

            foreach (var (id, amount) in cost.Mats)
            {
                Ref(id, materials, $"{context}.mats");
                if (amount <= 0)
                {
                    errors.Add($"{context}.mats['{id}'] precisa ser > 0");
                }
            }
        }

        public void Positive(double value, string context)
        {
            if (value <= 0)
            {
                errors.Add($"{context} precisa ser > 0");
            }
        }

        public void RankIndex(int index, int count, string context)
        {
            if (index < 0 || index >= count)
            {
                errors.Add($"{context}: índice de Selo {index} fora de 0..{count - 1}");
            }
        }
    }
}
