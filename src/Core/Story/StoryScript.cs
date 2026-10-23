using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Story;

/// <summary>Um objetivo do Diário (data/dialogue/story.json). A ordem na lista é a ordem da campanha.</summary>
public sealed class ObjectiveDef
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Objetivo de um ato ainda não jogável: aparece no Diário como "em breve".</summary>
    public bool Locked { get; set; }
}

/// <summary>Regra: quando a flag <see cref="When"/> liga, aplica o objetivo e liga outras flags.</summary>
public sealed class TriggerDef
{
    public string When { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public List<string> Set { get; set; } = [];
}

/// <summary>Quem fala: nome na placa e retrato (modelo com {0} = 1 neutro ou 2 expressão). Sem retrato = só a placa de nome.</summary>
public sealed class SpeakerDef
{
    public string Name { get; set; } = string.Empty;

    public string? Portrait { get; set; }
}

/// <summary>Fala que abre sozinha quando o jogador entra numa fase (a chegada à Praça, por exemplo).</summary>
public sealed class AutoplayDef
{
    public string Level { get; set; } = string.Empty;

    public string Dialogue { get; set; } = string.Empty;

    /// <summary>Id do NPC que anda até o jogador antes da fala (vazio = ninguém; a cena da chegada não tem NPC).</summary>
    public string Npc { get; set; } = string.Empty;

    [JsonPropertyName("if")]
    public List<string> Conditions { get; set; } = [];
}

/// <summary>Roteiro geral: objetivos, regras de avanço, falantes e falas automáticas.</summary>
public sealed class StoryScript
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public List<ObjectiveDef> Objectives { get; set; } = [];

    public List<TriggerDef> Triggers { get; set; } = [];

    public Dictionary<string, SpeakerDef> Speakers { get; set; } = [];

    public List<AutoplayDef> Autoplay { get; set; } = [];

    /// <summary>Objetivo dado ao começar um jogo novo.</summary>
    public string StartObjective { get; set; } = string.Empty;

    public static StoryScript Parse(string json)
    {
        var script = JsonSerializer.Deserialize<StoryScript>(json, Options) ?? throw new FormatException("story.json vazio");
        script.Validate();
        return script;
    }

    public ObjectiveDef? Objective(string id) => Objectives.FirstOrDefault(o => o.Id == id);

    public int ObjectiveIndex(string id) => Objectives.FindIndex(o => o.Id == id);

    private void Validate()
    {
        var ids = new HashSet<string>();
        foreach (var o in Objectives)
        {
            if (string.IsNullOrWhiteSpace(o.Id) || !ids.Add(o.Id))
            {
                throw new FormatException($"objetivo com id vazio ou repetido: '{o.Id}'");
            }
        }

        foreach (var t in Triggers.Where(t => t.Objective.Length > 0 && !ids.Contains(t.Objective)))
        {
            throw new FormatException($"regra '{t.When}' aponta para objetivo inexistente '{t.Objective}'");
        }

        if (StartObjective.Length > 0 && !ids.Contains(StartObjective))
        {
            throw new FormatException($"startObjective inexistente: '{StartObjective}'");
        }
    }
}
