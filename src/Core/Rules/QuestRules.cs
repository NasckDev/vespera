using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Core.Rules;

/// <summary>Chaves dos contadores de missão (<c>qc</c>, <c>extra.js:115-128</c>).</summary>
public static class QuestCounters
{
    /// <summary>Falou com a Odete no balcão.</summary>
    public const string Odete = "odete";

    /// <summary>Acertos nos bonecos da Guilda.</summary>
    public const string Dummy = "dummy";

    /// <summary>Entradas em Brecha.</summary>
    public const string Entered = "entered";

    /// <summary>Golpes Supremos usados dentro de Brecha.</summary>
    public const string Ults = "ults";

    /// <summary>Chefes derrotados.</summary>
    public const string Bosses = "bosses";

    /// <summary>Partes de monstro quebradas.</summary>
    public const string Broke = "broke";
}

/// <summary>Missões do Diário (<c>QUESTS</c>/<c>updQuests</c>, <c>extra.js:100-121</c>): condição, progresso e recompensa.</summary>
public sealed class QuestRules(GameDatabase db)
{
    /// <summary>Missão atual, ou null se todas foram cumpridas.</summary>
    public QuestDef? Current(SaveData save) => save.Quests.Index < db.Quests.Count ? db.Quests[save.Quests.Index] : null;

    public bool IsComplete(QuestDef quest, SaveData save)
    {
        var c = quest.Condition;
        return c.Kind switch
        {
            QuestConditionKind.Counter => save.Quests.Counter(c.Key!) >= c.Target,
            QuestConditionKind.WeaponLevel => Math.Max(1, save.WeaponLevel(save.ClassId)) >= c.Target,
            QuestConditionKind.Rank => db.Progression.RankIndex(save.Xp) >= db.RankIndexOf(c.Rank!),
            QuestConditionKind.ThemeClosed => save.Quests.ClosedThemes.Contains(c.Theme!),
            _ => false,
        };
    }

    /// <summary>Progresso "n/alvo" do HUD (só missões com <c>showProgress</c>), ou null.</summary>
    public string? ProgressText(QuestDef quest, SaveData save) =>
        quest.ShowProgress ? $"{Math.Min(quest.Condition.Target, save.Quests.Counter(quest.Condition.Key!))}/{quest.Condition.Target}" : null;

    /// <summary>Subtítulo do toast: "+20 Lumes · +1 Poção · +2 Estilhaço de Brecha".</summary>
    public string RewardText(QuestDef quest)
    {
        var parts = new List<string>();
        if (quest.Reward.Lumes > 0)
        {
            parts.Add($"+{quest.Reward.Lumes} Lumes");
        }

        if (quest.Reward.Potions > 0)
        {
            parts.Add($"+{quest.Reward.Potions} Poção");
        }

        foreach (var (id, amount) in quest.Reward.Mats)
        {
            parts.Add($"+{amount} {db.Materials[id].Name}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Dá a recompensa e avança para a próxima missão.</summary>
    public void Complete(QuestDef quest, SaveData save)
    {
        save.Lumes += quest.Reward.Lumes;
        save.Potions += quest.Reward.Potions;
        foreach (var (id, amount) in quest.Reward.Mats)
        {
            save.Materials[id] = save.MaterialCount(id) + amount;
        }

        save.Quests.Index++;
    }
}
