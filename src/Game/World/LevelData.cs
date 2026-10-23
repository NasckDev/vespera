using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Game.World;

/// <summary>
/// Dados de uma fase (data/levels/&lt;id&gt;.json): chão pintado, grade de colisão de 48 px autorada à mão, objetos
/// posicionados pelo pé e zonas de interação sem sprite (portas). Lógica pura, sem Godot: testada fora da engine.
/// </summary>
public sealed class LevelData
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public GroundData Ground { get; set; } = new();

    public int Cell { get; set; }

    public int Cols { get; set; }

    public int Rows { get; set; }

    public float[] Spawn { get; set; } = [0, 0];

    /// <summary>
    /// Régua do mapa (PLANO-V2 §2.1): altura em px de mundo de uma porta pintada no chão (ou de um arco/grade
    /// equivalente). A altura humana do mapa é 0,75 × isto; tudo o mais deriva dela.
    /// </summary>
    public float DoorHeight { get; set; }

    /// <summary>Altura humana do mapa em px de mundo (<see cref="ScaleRules.HumanPerDoor"/> × porta).</summary>
    public float HumanHeight => ScaleRules.HumanHeight(DoorHeight);

    /// <summary>
    /// Escala dos atores derivada da porta (velocidades, pés e alcances são dados na escala 1 = humano de
    /// <see cref="ScaleRules.ReferenceHuman"/> px). Nunca é escolhida à mão por mapa.
    /// </summary>
    public float ActorScale => ScaleRegistry.HunterScale(DoorHeight);

    /// <summary>Multiplica a escala de todos os objetos do catálogo nesta fase (mantém a proporção com o caçador).</summary>
    public float PropScale { get; set; } = 1f;

    /// <summary>Zoom da câmera (mapa "afastado" pintado pequeno: ~2x deixa o caçador com 70-90 px na tela).</summary>
    public float CameraZoom { get; set; } = 1f;

    public List<PropPlacement> Props { get; set; } = [];

    public List<ZoneData> Zones { get; set; } = [];

    /// <summary>Pé do primeiro ator da "fila de escala" (captura de revisão: caçador e NPCs lado a lado junto a uma porta).</summary>
    public float[] Lineup { get; set; } = [];

    /// <summary>NPCs da fase (posição pelo pé, rotina e fala). "if" filtra por flags da história ao carregar.</summary>
    public List<NpcPlacement> Npcs { get; set; } = [];

    public List<string> Grid { get; set; } = [];

    /// <summary>Pé da porta (ou arco) usada como régua, para a captura "fila de escala".</summary>
    public float[]? DoorAt { get; set; }

    /// <summary>Sala de Brecha: ondas, saída selada, baú, Guardião (null na cidade).</summary>
    public RiftRoomData? Rift { get; set; }
}

/// <summary>Conteúdo de uma sala de Brecha (camada 3): ondas de monstros, saída que só abre com a sala limpa.</summary>
public sealed class RiftRoomData
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Saída por passagem: barreira [x, y, w, h] que sela e o gatilho [x, y, w, h] além dela.</summary>
    public RiftExitData? Exit { get; set; }

    /// <summary>Ondas em ordem; a próxima entra quando a anterior morre toda.</summary>
    public List<List<SpawnData>> Waves { get; set; } = [];

    /// <summary>Pé do baú [x, y], se houver.</summary>
    public float[]? Chest { get; set; }

    public SpawnData? Boss { get; set; }

    /// <summary>Fenda de saída [x, y] que abre quando a sala acaba (sem passagem pintada).</summary>
    public float[]? ExitPortal { get; set; }

    /// <summary>Passagens seladas para sempre nesta caçada [x, y, w, h] (entrada às costas, saídas que não levam a nada).</summary>
    public List<float[]> Sealed { get; set; } = [];
}

public sealed class RiftExitData
{
    public float[] Barrier { get; set; } = [0, 0, 0, 0];

    public float[] Trigger { get; set; } = [0, 0, 0, 0];
}

public sealed class SpawnData
{
    public string Type { get; set; } = string.Empty;

    public float[] At { get; set; } = [0, 0];
}

public sealed class GroundData
{
    public string Texture { get; set; } = string.Empty;

    public float Scale { get; set; } = 1f;

    /// <summary>"linear" ou "nearest": filtro do chão ampliado pela câmera.</summary>
    public string Filter { get; set; } = "linear";
}

public sealed class PropPlacement
{
    public string Type { get; set; } = string.Empty;

    public float[] At { get; set; } = [0, 0];

    public bool Flip { get; set; }
}

/// <summary>Zona de interação sem objeto desenhado (porta pintada no chão, por exemplo).</summary>
public sealed class ZoneData
{
    public string Kind { get; set; } = string.Empty;

    public float[] At { get; set; } = [0, 0];

    public float Radius { get; set; } = 64f;

    public string Prompt { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Fase de destino (saída com transição); vazio = trancada, mostra <see cref="Text"/>.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Onde o jogador nasce na fase de destino (pé, em px do mundo dela).</summary>
    public float[] Spawn { get; set; } = [];

    /// <summary>Id de uma fala em data/dialogue (zona do tipo "talk").</summary>
    public string Dialogue { get; set; } = string.Empty;
}

/// <summary>NPC posicionado numa fase. <see cref="Routine"/>: "idle" fica parado; "patrol" anda entre os pontos de <see cref="Path"/>.</summary>
public sealed class NpcPlacement
{
    public string Id { get; set; } = string.Empty;

    public float[] At { get; set; } = [0, 0];

    /// <summary>"down", "up", "left" ou "right".</summary>
    public string Facing { get; set; } = "down";

    public string Routine { get; set; } = "idle";

    public List<float[]> Path { get; set; } = [];

    public string Dialogue { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    /// <summary>Condições de história (mesma sintaxe das falas) para o NPC existir nesta fase.</summary>
    [JsonPropertyName("if")]
    public List<string> Conditions { get; set; } = [];
}

/// <summary>
/// Definição de um objeto de cenário (data/levels/props.json). Medidas em pixels da textura: <see cref="Anchor"/> é o
/// pé (origem do y-sort); <see cref="Footprint"/> = [x, y, largura, altura] relativo ao pé; <see cref="Scale"/> na cena.
/// </summary>
public sealed class PropDef
{
    public string Texture { get; set; } = string.Empty;

    public float Scale { get; set; } = 1f;

    public float[] Anchor { get; set; } = [0, 0];

    public float[] Footprint { get; set; } = [0, 0, 0, 0];

    public bool Occludes { get; set; }

    public LightDef? Light { get; set; }

    public InteractDef? Interact { get; set; }

    /// <summary>Cópia com a escala multiplicada por <paramref name="factor"/> (luz e alcance também), para a escala da fase.</summary>
    public PropDef Scaled(float factor) => new()
    {
        Texture = Texture,
        Scale = Scale * factor,
        Anchor = Anchor,
        Footprint = Footprint,
        Occludes = Occludes,
        Light = Light is null ? null : new LightDef { Offset = Light.Offset, Color = Light.Color, Energy = Light.Energy, Radius = Light.Radius * factor },
        Interact = Interact is null ? null : new InteractDef
        {
            Kind = Interact.Kind, Prompt = Interact.Prompt, PromptOff = Interact.PromptOff, Title = Interact.Title,
            Text = Interact.Text, Dialogue = Interact.Dialogue, Radius = Interact.Radius * factor,
        },
    };

    /// <summary>Pegada em pixels do mundo, relativa ao pé (já escalada).</summary>
    public RectF WorldFootprint => new(Footprint[0] * Scale, Footprint[1] * Scale, Footprint[2] * Scale, Footprint[3] * Scale);
}

public sealed class LightDef
{
    public float[] Offset { get; set; } = [0, 0];

    public string Color { get; set; } = "#ffb45a";

    public float Energy { get; set; } = 1f;

    public float Radius { get; set; } = 200f;
}

public sealed class InteractDef
{
    public string Kind { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string PromptOff { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Id de uma fala em data/dialogue; se preenchido, interagir abre a conversa em vez do painel de texto.</summary>
    public string Dialogue { get; set; } = string.Empty;

    /// <summary>Alcance da interação em px do mundo; 0 = automático (meia pegada + folga).</summary>
    public float Radius { get; set; }
}

/// <summary>Contas do contrato de escala (PLANO-V2 §2.1), puras para valer igual no jogo e nos testes.</summary>
public static class ScaleRules
{
    /// <summary>Altura humana = 0,75 × porta.</summary>
    public const float HumanPerDoor = 0.75f;

    /// <summary>Humano de referência da escala 1 dos atores (px); o caçador da Praça (porta 72) mede 54 = 0,432 × 125.</summary>
    public const float ReferenceHuman = 125f;

    /// <summary>Tolerância do teste de altura renderizada (±8%).</summary>
    public const float Tolerance = 0.08f;

    public static float HumanHeight(float doorHeight) => doorHeight * HumanPerDoor;
}

/// <summary>Leitura e validação dos JSON de fase e de objetos.</summary>
public static class LevelJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static LevelData ParseLevel(string json)
    {
        var level = JsonSerializer.Deserialize<LevelData>(json, Options) ?? throw new FormatException("fase vazia");
        if (level.Cell <= 0 || level.Cols <= 0 || level.Rows <= 0)
        {
            throw new FormatException($"fase '{level.Id}': cell/cols/rows inválidos");
        }

        if (string.IsNullOrEmpty(level.Ground.Texture))
        {
            throw new FormatException($"fase '{level.Id}': sem textura de chão");
        }

        if (level.DoorHeight <= 0)
        {
            throw new FormatException($"fase '{level.Id}': sem doorHeight (régua do contrato de escala, PLANO-V2 §2.1)");
        }

        return level;
    }

    /// <summary>Lê o catálogo; chaves que começam com "_" são comentários e ficam de fora.</summary>
    public static Dictionary<string, PropDef> ParseProps(string json)
    {
        var result = new Dictionary<string, PropDef>();
        using var doc = JsonDocument.Parse(json);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Name.StartsWith('_') || entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var def = entry.Value.Deserialize<PropDef>(Options) ?? throw new FormatException($"objeto '{entry.Name}' vazio");
            if (def.Anchor.Length != 2 || def.Footprint.Length != 4 || def.Scale <= 0)
            {
                throw new FormatException($"objeto '{entry.Name}': anchor/footprint/scale inválidos");
            }

            result[entry.Name] = def;
        }

        return result;
    }

    /// <summary>Confere que todo objeto posicionado existe no catálogo; devolve a lista de problemas.</summary>
    public static List<string> Validate(LevelData level, IReadOnlyDictionary<string, PropDef> props)
    {
        var problems = new List<string>();
        foreach (var placement in level.Props)
        {
            if (!props.ContainsKey(placement.Type))
            {
                problems.Add($"objeto '{placement.Type}' não está no catálogo");
            }

            if (placement.At.Length != 2)
            {
                problems.Add($"objeto '{placement.Type}' sem posição [x, y]");
            }
        }

        return problems;
    }
}
