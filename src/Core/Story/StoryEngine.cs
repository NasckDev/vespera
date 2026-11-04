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

    /// <summary>Flag gravada ou calculada (<see cref="NightFlags"/>: noite, pergunta do Vigia, Ravi no Avesso).</summary>
    public bool HasFlag(string flag) => NightFlags.Computed(flag, Progress) ?? Progress.Has(flag);

    public ObjectiveDef? CurrentObjective => Script.Objective(Progress.ObjectiveId);

    /// <summary>Liga a flag e aplica as regras que dependem dela. Devolve verdadeiro se era nova.</summary>
    public bool SetFlag(string flag)
    {
        if (NightFlags.IsComputed(flag))
        {
            return false;
        }

        var isNew = Progress.Add(flag);
        if (!isNew)
        {
            return false;
        }

        foreach (var trigger in Script.Triggers.Where(t => t.When == flag).ToList())
        {
            if (!StoryConditions.Evaluate(trigger.Conditions, this))
            {
                continue;
            }

            foreach (var extra in trigger.Set)
            {
                Progress.Add(extra);
            }

            if (trigger.Objective.Length > 0)
            {
                Progress.ObjectiveId = trigger.Objective;
            }
        }

        ObserveNight(flag);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// A véspera acabou: cai uma noite nova (o Vigia volta ao banco e pode perguntar de novo) e a Brecha B das Ruas
    /// Baixas avança um passo (<see cref="RaviRescue"/>). <paramref name="ruasCanRupture"/> = a Brecha B já é jogável.
    /// </summary>
    public RuasStage NightFalls(bool ruasCanRupture)
    {
        Progress.Night.Fall();
        var stage = RaviRescue.Advance(this, ruasCanRupture);
        Changed?.Invoke();
        return stage;
    }

    /// <summary>A noite passou (cena da manhã).</summary>
    public void Dawn()
    {
        if (Progress.Night.IsNight)
        {
            Progress.Night.Dawn();
            Changed?.Invoke();
        }
    }

    /// <summary>Respostas ao Vigia marcam a pergunta da noite; conhecer o Vigia abre a Brecha B das Ruas Baixas.</summary>
    private void ObserveNight(string flag)
    {
        if (NightFlags.IsVigiaAnswer(flag))
        {
            Progress.Night.VigiaAskedOn = Progress.Night.Night;
        }
        else if (flag == NightFlags.MetVigia)
        {
            RaviRescue.Open(this);
        }
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
