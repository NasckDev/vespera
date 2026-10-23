using System.Collections.Generic;
using Godot;
using Vespera.Core.Save;
using Vespera.Core.Story;
using Vespera.Game.Boot;

namespace Vespera.Game.Story;

/// <summary>
/// Ponte entre o Core da história e o jogo: guarda o <see cref="StoryEngine"/> da sessão (ligado ao
/// <see cref="SaveData.Story"/> do save em andamento), lê o roteiro e as falas de <c>res://data/dialogue</c> e
/// dá a quem precisa uma API curta. Para avisar a história de algo do mundo (ex.: Guardião derrotado):
/// <c>StoryService.SetFlag(StoryFlags.GuardianDefeatedLodoMae)</c>.
/// </summary>
public static class StoryService
{
    public const string DialogueFolder = "res://data/dialogue";
    public const string ScriptFile = "story";

    private static readonly Dictionary<string, DialogueFile> Dialogues = [];
    private static StoryScript? _script;
    private static StoryEngine? _engine;
    private static SaveData? _boundTo;

    /// <summary>Roteiro geral (objetivos, regras, falantes). Lido uma vez.</summary>
    public static StoryScript Script => _script ??= StoryScript.Parse(ReadText(ScriptFile));

    /// <summary>Motor da sessão atual; refeito quando o save em andamento muda (novo jogo, continuar).</summary>
    public static StoryEngine Engine
    {
        get
        {
            var data = GameSession.Data;
            if (_engine is null || !ReferenceEquals(_boundTo, data))
            {
                Bind(data);
            }

            return _engine!;
        }
    }

    /// <summary>Quando verdadeiro a fala automática de chegada não abre (testes e capturas que precisam do jogador livre).</summary>
    public static bool SuppressAutoplay { get; set; }

    public static string PlayerName => GameSession.Data?.Name ?? SaveService.DefaultName;

    /// <summary>Liga o motor ao save (ou a um progresso solto, sem sessão: capturas de revisão e testes).</summary>
    public static void Bind(SaveData? data)
    {
        _boundTo = data;
        StoryProgress progress;
        if (data is not null)
        {
            data.Story ??= new StoryProgress();
            progress = data.Story;
        }
        else
        {
            progress = new StoryProgress();
        }

        _engine = new StoryEngine(progress, Script, RankIndexOf);
        if (data is not null)
        {
            _engine.BeginCampaignIfNew();
        }
    }

    /// <summary>Atalho para o resto do jogo avisar a história de uma flag nova (e gravar no save).</summary>
    public static bool SetFlag(string flag)
    {
        var isNew = Engine.SetFlag(flag);
        if (isNew)
        {
            GameSession.Autosave();
        }

        return isNew;
    }

    public static DialogueFile LoadDialogue(string id)
    {
        if (!Dialogues.TryGetValue(id, out var file))
        {
            file = DialogueFile.Parse(ReadText(id));
            Dialogues[id] = file;
        }

        return file;
    }

    public static DialogueRunner? StartDialogue(string id)
    {
        var runner = new DialogueRunner(LoadDialogue(id), Engine, PlayerName, WeaponTokens());
        return runner.Start() ? runner : null;
    }

    /// <summary>Fichas {arma} (artigo de gênero certo) e {este} para as falas da arma da classe do caçador.</summary>
    public static Dictionary<string, string> WeaponTokens() => (GameSession.Data?.ClassId ?? SaveService.DefaultClassId) switch
    {
        "bow" => new() { ["arma"] = "arco", ["este"] = "este", ["armaNome"] = "Arco" },
        "hammer" => new() { ["arma"] = "martelo", ["este"] = "este", ["armaNome"] = "Martelo" },
        _ => new() { ["arma"] = "lâmina", ["este"] = "esta", ["armaNome"] = "Lâmina" },
    };

    public static void ClearCache()
    {
        Dialogues.Clear();
        _script = null;
        _engine = null;
        _boundTo = null;
    }

    /// <summary>Rank do caçador para as condições da história: o da progressão da v2 (fonte única).</summary>
    private static int RankIndexOf() => Hunter.Progression.IsActive ? (int)Hunter.Progression.Rank : 0;

    private static string ReadText(string name)
    {
        var path = $"{DialogueFolder}/{name}.json";
        var text = Godot.FileAccess.GetFileAsString(path);
        if (string.IsNullOrEmpty(text))
        {
            throw new System.IO.FileNotFoundException($"não consegui ler {path}: {Godot.FileAccess.GetOpenError()}");
        }

        return text;
    }
}
