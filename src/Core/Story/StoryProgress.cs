namespace Vespera.Core.Story;

/// <summary>
/// Progresso da história persistido no save: flags (fatos que já aconteceram), objetivo atual e a fase em que o
/// jogador está. É só dado; as regras ficam em <see cref="StoryEngine"/>.
/// </summary>
public sealed class StoryProgress
{
    /// <summary>Flags ativas (ex.: "met_ravi", "GuardianDefeated:lodo_mae"). Sem repetição.</summary>
    public List<string> Flags { get; set; } = [];

    /// <summary>Id do objetivo atual em <c>story.json</c>; vazio = ainda sem objetivo.</summary>
    public string ObjectiveId { get; set; } = string.Empty;

    /// <summary>Fase onde o jogador estava no último save (vazio = a fase inicial).</summary>
    public string LevelId { get; set; } = string.Empty;

    public bool Has(string flag) => Flags.Contains(flag);

    /// <summary>Liga a flag; devolve verdadeiro se ela era nova.</summary>
    public bool Add(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || Flags.Contains(flag))
        {
            return false;
        }

        Flags.Add(flag);
        return true;
    }

    /// <summary>Remove repetidos e vazios (saves editados à mão ou migrados).</summary>
    public void Sanitize()
    {
        Flags = [.. (Flags ?? []).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct()];
        ObjectiveId ??= string.Empty;
        LevelId ??= string.Empty;
    }
}
