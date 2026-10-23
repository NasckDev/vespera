namespace Vespera.Core.Story;

/// <summary>
/// Percorre um <see cref="DialogueFile"/>: escolhe a entrada pelas condições, entrega uma caixa por vez e aplica os
/// efeitos de cada caixa na história. Sem Godot; a caixa de fala só desenha o que o runner entrega.
/// </summary>
public sealed class DialogueRunner
{
    private readonly DialogueFile _file;
    private readonly StoryEngine _story;
    private readonly string _playerName;
    private readonly IReadOnlyDictionary<string, string> _tokens;

    public DialogueRunner(DialogueFile file, StoryEngine story, string playerName, IReadOnlyDictionary<string, string>? tokens = null)
    {
        _tokens = tokens ?? new Dictionary<string, string>();
        _file = file;
        _story = story;
        _playerName = playerName;
    }

    public DialogueNode? Current { get; private set; }

    public string? CurrentId { get; private set; }

    public bool Finished => Current is null;

    /// <summary>Respostas que valem agora (condição satisfeita).</summary>
    public IReadOnlyList<DialogueChoice> Choices { get; private set; } = [];

    /// <summary>Texto da caixa atual com {nome} resolvido.</summary>
    public string Text => Current is null ? string.Empty : Resolve(Current.Text);

    /// <summary>Abre a conversa. Falso se nenhuma entrada vale (o personagem não tem nada a dizer).</summary>
    public bool Start()
    {
        var entry = _file.Entries.FirstOrDefault(e => StoryConditions.Evaluate(e.Conditions, _story));
        if (entry is null)
        {
            return false;
        }

        Enter(entry.Node);
        return true;
    }

    /// <summary>
    /// Avança. Com respostas visíveis, <paramref name="choiceIndex"/> escolhe uma (índice em <see cref="Choices"/>);
    /// sem respostas, segue o "next" da caixa. Devolve falso quando a conversa acabou.
    /// </summary>
    public bool Advance(int choiceIndex = 0)
    {
        if (Current is null)
        {
            return false;
        }

        string next;
        if (Choices.Count > 0)
        {
            if (choiceIndex < 0 || choiceIndex >= Choices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(choiceIndex));
            }

            var choice = Choices[choiceIndex];
            foreach (var flag in choice.Set)
            {
                _story.SetFlag(flag);
            }

            next = choice.Next;
        }
        else
        {
            next = Current.Next;
        }

        if (next.Length == 0)
        {
            Current = null;
            CurrentId = null;
            Choices = [];
            return false;
        }

        Enter(next);
        return true;
    }

    /// <summary>Pula o texto: aplica os efeitos das caixas seguintes até uma escolha ou o fim. Falso se a conversa acabou.</summary>
    public bool SkipToEnd()
    {
        while (Current is not null && Choices.Count == 0)
        {
            Advance();
        }

        return Current is not null;
    }

    /// <summary>Texto com {nome} e as fichas extras (ex.: {arma}) resolvidas.</summary>
    public string Resolve(string text)
    {
        var result = text.Replace("{nome}", _playerName, StringComparison.Ordinal);
        foreach (var (key, value) in _tokens)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return result;
    }

    private void Enter(string id)
    {
        var node = _file.Nodes[id];
        Current = node;
        CurrentId = id;
        foreach (var flag in node.Set)
        {
            _story.SetFlag(flag);
        }

        if (node.Objective.Length > 0)
        {
            _story.SetObjective(node.Objective);
        }

        Choices = [.. node.Choices.Where(c => StoryConditions.Evaluate(c.Conditions, _story))];
        foreach (var name in node.Events)
        {
            _story.RaiseEvent(name);
        }
    }
}
