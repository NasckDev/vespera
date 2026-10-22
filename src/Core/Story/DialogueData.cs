using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Story;

/// <summary>Resposta que o jogador escolhe. Some da lista se a condição não vale.</summary>
public sealed class DialogueChoice
{
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("if")]
    public List<string> Conditions { get; set; } = [];

    public List<string> Set { get; set; } = [];

    /// <summary>Id do nó seguinte; vazio = a conversa acaba.</summary>
    public string Next { get; set; } = string.Empty;
}

/// <summary>
/// Uma caixa de fala. Os efeitos (<see cref="Set"/>, <see cref="Objective"/>, <see cref="Events"/>) valem assim que a
/// caixa aparece, mesmo que o jogador pule o texto.
/// </summary>
public sealed class DialogueNode
{
    /// <summary>Chave em <c>story.json</c> (speakers). Vazio = narração sem placa.</summary>
    public string Speaker { get; set; } = string.Empty;

    /// <summary>Troca a placa de nome só nesta caixa (ex.: Nilza na voz do Ravi).</summary>
    public string? Label { get; set; }

    /// <summary>1 = retrato neutro, 2 = expressão.</summary>
    public int Expression { get; set; } = 1;

    /// <summary>Texto em pt-BR; aceita {nome} (nome do caçador).</summary>
    public string Text { get; set; } = string.Empty;

    public List<string> Set { get; set; } = [];

    public string Objective { get; set; } = string.Empty;

    public List<string> Events { get; set; } = [];

    public string Next { get; set; } = string.Empty;

    public List<DialogueChoice> Choices { get; set; } = [];
}

/// <summary>Porta de entrada: a primeira cuja condição vale decide por onde a conversa começa.</summary>
public sealed class DialogueEntry
{
    [JsonPropertyName("if")]
    public List<string> Conditions { get; set; } = [];

    public string Node { get; set; } = string.Empty;
}

/// <summary>Arquivo de falas de um personagem ou objeto (data/dialogue/&lt;id&gt;.json).</summary>
public sealed class DialogueFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public string Id { get; set; } = string.Empty;

    public List<DialogueEntry> Entries { get; set; } = [];

    public Dictionary<string, DialogueNode> Nodes { get; set; } = [];

    public static DialogueFile Parse(string json)
    {
        var file = JsonSerializer.Deserialize<DialogueFile>(json, Options) ?? throw new FormatException("fala vazia");
        file.Validate();
        return file;
    }

    /// <summary>Confere que todo "next" e toda entrada apontam para um nó que existe.</summary>
    public void Validate()
    {
        foreach (var entry in Entries.Where(e => !Nodes.ContainsKey(e.Node)))
        {
            throw new FormatException($"fala '{Id}': entrada aponta para nó inexistente '{entry.Node}'");
        }

        foreach (var (key, node) in Nodes)
        {
            if (node.Next.Length > 0 && !Nodes.ContainsKey(node.Next))
            {
                throw new FormatException($"fala '{Id}', nó '{key}': next inexistente '{node.Next}'");
            }

            foreach (var choice in node.Choices.Where(c => c.Next.Length > 0 && !Nodes.ContainsKey(c.Next)))
            {
                throw new FormatException($"fala '{Id}', nó '{key}': escolha aponta para '{choice.Next}'");
            }

            if (node.Text.Length == 0)
            {
                throw new FormatException($"fala '{Id}', nó '{key}': texto vazio");
            }
        }
    }
}
