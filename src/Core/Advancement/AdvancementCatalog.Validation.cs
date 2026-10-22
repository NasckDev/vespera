using Vespera.Core.Ranks;

namespace Vespera.Core.Advancement;

public sealed partial class AdvancementCatalog
{
    private static readonly string[] AllowedAffixStats = ["hp", "defense", "critPct", "speedPct", "fragmentosPct"];
    private static readonly string[] AllowedObjectives = ["survive", "kill_count", "destroy_marked", "reach_exit"];

    private void Validate(List<string> errors)
    {
        ValidateRanks(errors);
        ValidateGuardiansAndMaterials(errors);
        ValidateExams(errors);
        ValidateWeapons(errors);
        ValidateGear(errors);
        ValidateEchoesAndLoot(errors);
    }

    private void ValidateRanks(List<string> errors)
    {
        if (Ranks.Count == 0)
        {
            errors.Add("ranks.json: nenhum rank");
            return;
        }

        var stageIds = RankData.Stages.Select(s => s.Id).ToHashSet();
        for (var i = 0; i < Ranks.Count; i++)
        {
            var step = Ranks[i];
            if (i > 0 && step.Xp <= Ranks[i - 1].Xp)
            {
                errors.Add($"ranks.json: XP de {step.Id} ({step.Xp}) precisa ser maior que o de {Ranks[i - 1].Id}");
            }

            if (i > 0 && step.Id <= Ranks[i - 1].Id)
            {
                errors.Add($"ranks.json: {step.Id} fora de ordem depois de {Ranks[i - 1].Id}");
            }

            if (i > 0 && step.Stage < Ranks[i - 1].Stage)
            {
                errors.Add($"ranks.json: estágio de {step.Id} não pode voltar");
            }

            if (!stageIds.Contains(step.Stage))
            {
                errors.Add($"ranks.json: {step.Id} usa estágio inexistente {step.Stage}");
            }

            var last = i == Ranks.Count - 1;
            if (last == (step.Guardian is not null))
            {
                errors.Add(last ? $"ranks.json: o último rank ({step.Id}) não tem Guardião" : $"ranks.json: {step.Id} precisa de um Guardião para sair dele");
            }

            if (step.Guardian is not null && !Guardians.ContainsKey(step.Guardian))
            {
                errors.Add($"ranks.json: {step.Id} aponta para o Guardião inexistente '{step.Guardian}'");
            }
        }

        if (Ranks[0].Xp != 0)
        {
            errors.Add("ranks.json: o primeiro rank precisa começar com 0 XP");
        }
    }

    private void ValidateGuardiansAndMaterials(List<string> errors)
    {
        var ladder = Ranks.Select(r => r.Id).ToHashSet();
        foreach (var guardian in Guardians.Values)
        {
            if (!ladder.Contains(guardian.Rank))
            {
                errors.Add($"Guardião '{guardian.Id}': rank {guardian.Rank} fora da escada");
            }

            if (!Materials.TryGetValue(guardian.Material, out var mat) || mat.Kind != MaterialKind.Guardian)
            {
                errors.Add($"Guardião '{guardian.Id}': material '{guardian.Material}' não existe ou não é de Guardião");
            }
            else if (mat.Rank != guardian.Rank)
            {
                errors.Add($"Guardião '{guardian.Id}': material '{guardian.Material}' é de outro rank");
            }

            if (guardian.ArmorSet is not null && !Sets.ContainsKey(guardian.ArmorSet))
            {
                errors.Add($"Guardião '{guardian.Id}': conjunto '{guardian.ArmorSet}' não existe");
            }

            if (guardian.Echo is not null && !Echoes.ContainsKey(guardian.Echo))
            {
                errors.Add($"Guardião '{guardian.Id}': Eco '{guardian.Echo}' não existe");
            }
        }

        foreach (var step in Ranks)
        {
            if (CommonMaterials(step.Id).Count < 2)
            {
                errors.Add($"materiais: o rank {step.Id} precisa de ao menos 2 materiais comuns (principal e secundário da forja)");
            }

            if (step.Guardian is not null && Guardians.TryGetValue(step.Guardian, out var g) && g.Rank != step.Id)
            {
                errors.Add($"ranks.json: o Guardião de {step.Id} ('{g.Id}') é de {g.Rank}");
            }
        }

        foreach (var material in Materials.Values)
        {
            if (material.Kind == MaterialKind.Common && material.Weight <= 0)
            {
                errors.Add($"material '{material.Id}': comum precisa de peso > 0");
            }

            if (material.Origin is not ("gdd" or "proposta"))
            {
                errors.Add($"material '{material.Id}': origem deve ser 'gdd' ou 'proposta'");
            }
        }
    }

    private void ValidateExams(List<string> errors)
    {
        foreach (var step in Ranks)
        {
            var count = ExamList.Count(e => e.Rank == step.Id);
            if (step.Exam && count != 1)
            {
                errors.Add($"exames: o rank {step.Id} exige exatamente 1 exame (tem {count})");
            }

            if (!step.Exam && count > 0)
            {
                errors.Add($"exames: o rank {step.Id} não marca exame, mas há {count} definido(s)");
            }
        }

        foreach (var exam in ExamList)
        {
            if (exam.Objectives.Count == 0)
            {
                errors.Add($"exame '{exam.Id}': sem objetivos");
            }

            foreach (var objective in exam.Objectives.Where(o => !AllowedObjectives.Contains(o.Type)))
            {
                errors.Add($"exame '{exam.Id}': objetivo '{objective.Type}' desconhecido");
            }
        }
    }

    private void ValidateWeapons(List<string> errors)
    {
        var forge = WeaponData.Forge;
        if (forge.LevelsPerTier < 1 || forge.FragmentosBase < 1 || forge.FragmentosGrowth < 1 || forge.SecondaryEveryLevels < 1)
        {
            errors.Add("weapons.json: parâmetros de forja inválidos");
        }

        foreach (var weapon in Weapons.Values)
        {
            if (weapon.Tiers.Count != Ranks.Count)
            {
                errors.Add($"arma '{weapon.Id}': precisa de {Ranks.Count} degraus (um por rank), tem {weapon.Tiers.Count}");
                continue;
            }

            for (var i = 0; i < Ranks.Count; i++)
            {
                if (weapon.Tiers[i].Rank != Ranks[i].Id)
                {
                    errors.Add($"arma '{weapon.Id}': degrau {i} deveria ser {Ranks[i].Id}");
                }

                if (i > 0 && weapon.Tiers[i].Damage <= weapon.Tiers[i - 1].Damage)
                {
                    errors.Add($"arma '{weapon.Id}': o dano precisa crescer a cada rank ({weapon.Tiers[i].Rank})");
                }
            }
        }
    }

    private void ValidateGear(List<string> errors)
    {
        var slots = GearData.Slots.Select(s => s.Id).ToHashSet();
        foreach (var set in Sets.Values)
        {
            if (set.Pieces.Count != 4 || set.Pieces.Select(p => p.Slot).Distinct().Count() != 4)
            {
                errors.Add($"conjunto '{set.Id}': precisa de 4 peças em 4 slots diferentes");
            }

            foreach (var piece in set.Pieces.Where(p => !slots.Contains(p.Slot)))
            {
                errors.Add($"conjunto '{set.Id}': peça '{piece.Id}' usa slot inexistente '{piece.Slot}'");
            }

            var thresholds = set.Bonuses.Select(b => b.Pieces).OrderBy(n => n).ToArray();
            if (!thresholds.SequenceEqual([2, 4]))
            {
                errors.Add($"conjunto '{set.Id}': precisa de bônus de 2 e de 4 peças");
            }

            if (!Guardians.TryGetValue(set.Guardian, out var guardian) || guardian.ArmorSet != set.Id)
            {
                errors.Add($"conjunto '{set.Id}': o Guardião '{set.Guardian}' não aponta de volta para ele");
            }
        }

        foreach (var template in GearData.RareTemplates.Where(t => !slots.Contains(t.Slot)))
        {
            errors.Add($"peça rara '{template.Name}': slot inexistente '{template.Slot}'");
        }

        foreach (var slot in slots.Where(s => GearData.RareTemplates.All(t => t.Slot != s)))
        {
            errors.Add($"slot '{slot}' sem molde de peça rara");
        }

        foreach (var affix in GearData.Affixes.Where(a => !AllowedAffixStats.Contains(a.Stat)))
        {
            errors.Add($"afixo '{affix.Id}': atributo '{affix.Stat}' desconhecido");
        }

        if (GearData.Affixes.Count == 0)
        {
            errors.Add("gear.json: nenhum afixo");
        }
    }

    private void ValidateEchoesAndLoot(List<string> errors)
    {
        foreach (var echo in Echoes.Values)
        {
            if (!Guardians.TryGetValue(echo.Guardian, out var guardian) || guardian.Echo != echo.Id)
            {
                errors.Add($"Eco '{echo.Id}': o Guardião '{echo.Guardian}' não aponta de volta para ele");
            }
        }

        if (EchoData.Slots < 1)
        {
            errors.Add("echoes.json: precisa de ao menos 1 slot");
        }

        var weights = Loot.RankWeights;
        if (Math.Abs(weights.Same + weights.Next + weights.Rare - 1.0) > 1e-9)
        {
            errors.Add("loot.json: os pesos de rank (mesmo/próximo/raro) precisam somar 1");
        }

        foreach (var kind in new[] { "monster", "part", "chest", "guardian" }.Where(k => !Loot.Sources.ContainsKey(k)))
        {
            errors.Add($"loot.json: fonte '{kind}' ausente");
        }
    }
}
