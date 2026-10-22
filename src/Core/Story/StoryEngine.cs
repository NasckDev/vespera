namespace Vespera.Core.Story;

/// <summary>
/// Estado vivo da história: flags e objetivo (um <see cref="StoryProgress"/> que vai para o save), as regras de
/// <see cref="StoryScript"/> e o aviso de eventos. Lógica pura, sem Godot.
/// </summary>
public sealed class StoryEngine : IStoryContext
{
    private readonly Func<int> _rankIndex;

    public StoryEngine(StoryProgress progress, StoryScript script, Func<int>? rankIndex = null)
    {
        Progress = progress;
        Script = script;
        _rankIndex = rankIndex ?? (() => 0);
    }

    public StoryProgress Progress { get; }

    public StoryScript Script { get; }

    public string ObjectiveId => Progress.ObjectiveId;

    public int RankIndex => _rankIndex();

    /// <summary>Qualquer mudança de flag ou objetivo (o Diário e o aviso de objetivo escutam).</summary>
    public event Action? Changed;

    /// <summary>Evento de uma só vez (não persistido), ex.: <see cref="StoryFlags.EventOpenForge"/>.</summary>
    public event Action<string>? EventRaised;

    public bool HasFlag(string flag) => Progress.Has(flag);

    public ObjectiveDef? CurrentObjective => Script.Objective(Progress.ObjectiveId);

    /// <summary>Liga a flag e aplica as regras que dependem dela. Devolve verdadeiro se era nova.</summary>
    public bool SetFlag(string flag)
    {
        var isNew = Progress.Add(flag);
        if (!isNew)
        {
            return false;
        }

        foreach (var trigger in Script.Triggers.Where(t => t.When == flag).ToList())
        {
            foreach (var extra in trigger.Set)
            {
                Progress.Add(extra);
            }

            if (trigger.Objective.Length > 0)
            {
                Progress.ObjectiveId = trigger.Objective;
            }
        }

        Changed?.Invoke();
        return true;
    }

    public void SetObjective(string id)
    {
        if (id.Length == 0 || id == Progress.ObjectiveId)
        {
            return;
        }

        if (Script.Objective(id) is null)
        {
            throw new ArgumentException($"objetivo inexistente: '{id}'", nameof(id));
        }

        Progress.ObjectiveId = id;
        Changed?.Invoke();
    }

    public void RaiseEvent(string name) => EventRaised?.Invoke(name);

    /// <summary>Começa a campanha num jogo novo (objetivo inicial); não mexe num save que já tem objetivo.</summary>
    public void BeginCampaignIfNew()
    {
        if (Progress.ObjectiveId.Length == 0 && Progress.Flags.Count == 0 && Script.StartObjective.Length > 0)
        {
            Progress.ObjectiveId = Script.StartObjective;
            Changed?.Invoke();
        }
    }

    /// <summary>Primeira fala automática da fase cujas condições valem, ou null.</summary>
    public AutoplayDef? AutoplayFor(string levelId) =>
        Script.Autoplay.FirstOrDefault(a => a.Level == levelId && StoryConditions.Evaluate(a.Conditions, this));
}
