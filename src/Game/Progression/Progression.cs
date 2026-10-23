using System;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;

namespace Vespera.Game.Hunter;

/// <summary>
/// Serviço de progressão do jogador (rank, XP, loot, Guardiões, exames, forja, equipamento, Ecos, Lanterna). É o único ponto que o
/// resto do jogo chama: ele liga o <see cref="ProgressionEngine"/> do Core ao save da sessão (<see cref="GameSession.Data"/>),
/// grava depois das mudanças importantes e avisa por eventos. Estático e sem nó: funciona em qualquer cena, com a árvore pausada.
/// A documentação de uso está em <c>docs/v2/api-progressao.md</c>.
/// </summary>
public static class Progression
{
    private const string DataFolder = "res://data/progressao/";

    private static AdvancementCatalog? _catalog;
    private static ProgressionEngine? _engine;
    private static Core.Save.SaveData? _boundData;
    private static int? _seed;

    /// <summary>Subiu de rank (de qualquer jeito: XP, Guardião ou exame). A tela de celebração escuta isto.</summary>
    public static event Action<RankChange>? RankChanged;

    /// <summary>Cruzou uma fronteira de estágio visual (E–D, C–B, A–S, SS–SSS, Alvor). Vem junto com <see cref="RankChanged"/>.</summary>
    public static event Action<RankChange>? StageChanged;

    /// <summary>Um saque foi entregue (monstro, parte, baú ou Guardião).</summary>
    public static event Action<LootGrant>? LootGranted;

    public static event Action<GuardianKillResult>? GuardianDefeated;

    /// <summary>Qualquer mudança no estado (para telas abertas se atualizarem).</summary>
    public static event Action? Changed;

    /// <summary>Grava o save em cada mudança importante (forja, equipar, rank, Guardião, exame, fim de caçada). Desligue em testes.</summary>
    public static bool AutosaveEnabled { get; set; } = true;

    /// <summary>Abre a celebração de rank sozinha quando <see cref="RankChanged"/> dispara. Desligue se a cena controlar isso.</summary>
    public static bool ShowRankUpOverlay { get; set; } = true;

    public static AdvancementCatalog Catalog => _catalog ??= LoadCatalog();

    /// <summary>Há uma sessão (jogo carregado) para a progressão operar.</summary>
    public static bool IsActive => GameSession.Data is not null;

    /// <summary>Motor ligado ao save da sessão. Troca sozinho quando o jogador carrega outro save.</summary>
    public static ProgressionEngine Engine
    {
        get
        {
            var data = GameSession.Data ?? throw new InvalidOperationException("Progression exige uma sessão ativa (GameSession.Begin).");
            if (_engine is null || !ReferenceEquals(_boundData, data))
            {
                _boundData = data;
                var random = new SeededRandom(_seed ?? Environment.TickCount);
                _engine = new ProgressionEngine(Catalog, data.Hunter, data.ClassId, random);
                foreach (var warning in _engine.Warnings)
                {
                    Log.Warn($"progressão: {warning}");
                }
            }

            return _engine;
        }
    }

    public static ForgeApi Forge { get; } = new();

    public static EquipmentApi Equipment { get; } = new();

    public static Rank Rank => Engine.Rank;

    public static int Stage => Engine.Stage;

    public static RankStatus Status => Engine.RankStatus;

    public static HunterProgress State => Engine.Progress;

    /// <summary>Fixa a semente dos sorteios (testes). Vale para o próximo save ligado.</summary>
    public static void UseSeed(int seed)
    {
        _seed = seed;
        _engine = null;
    }

    /// <summary>Esquece o motor ligado (ao encerrar a sessão ou no começo de um teste).</summary>
    public static void Reset()
    {
        _engine = null;
        _boundData = null;
        _seed = null;
    }

    public static XpResult AddXp(int amount) => Commit(Engine.AddXp(amount), r => r.RankChange);

    public static void AddLumes(int amount)
    {
        Engine.AddLumes(amount);
        Notify();
    }

    /// <summary>Sorteia e entrega o saque de uma fonte numa Brecha de <paramref name="breachRank"/>.</summary>
    public static LootGrant GrantLoot(Rank breachRank, LootSource source)
    {
        var grant = Engine.GrantLoot(breachRank, source);
        LootGranted?.Invoke(grant);
        Notify();
        return grant;
    }

    /// <summary>Registra o Guardião derrotado: conquista, saque garantido, Eco e conjunto. Salva e celebra o rank, se subiu.</summary>
    public static GuardianKillResult RecordGuardianKill(string guardianId)
    {
        var result = Engine.RecordGuardianKill(guardianId);
        LootGranted?.Invoke(result.Loot);
        GuardianDefeated?.Invoke(result);
        return Commit(result, r => r.RankChange);
    }

    /// <summary>Registra a aprovação no exame da Ordem do rank (a sala do exame é jogada por fora).</summary>
    public static ExamResult TryPassExam(Rank rank) => Commit(Engine.TryPassExam(rank), r => r.RankChange);

    /// <summary>Entra numa Brecha do bairro; devolve os Fragmentos recolhidos de uma Lanterna perdida ali.</summary>
    public static int BeginRun(string neighborhoodId)
    {
        var recovered = Engine.BeginRun(neighborhoodId);
        Notify();
        return recovered;
    }

    /// <summary>Termina a caçada: vivo leva os Fragmentos para a cidade; morto deixa na Lanterna do bairro. Salva.</summary>
    public static RunEnd? EndRun(bool survived)
    {
        var end = Engine.EndRun(survived);
        Save();
        Notify();
        return end;
    }

    public static void Save()
    {
        if (AutosaveEnabled)
        {
            GameSession.Autosave();
        }
    }

    internal static void Notify() => Changed?.Invoke();

    internal static T Commit<T>(T result, Func<T, RankChange?> rankChange)
    {
        Save();
        Notify();
        if (rankChange(result) is { } change)
        {
            RankChanged?.Invoke(change);
            if (change.StageChanged)
            {
                StageChanged?.Invoke(change);
            }

            if (ShowRankUpOverlay)
            {
                UI.RankUpScreen.Show(change);
            }
        }

        return result;
    }

    private static AdvancementCatalog LoadCatalog() => AdvancementCatalog.Load(name =>
        UserFile.ReadText(DataFolder + name) ?? throw new InvalidOperationException($"Dado de progressão ausente: {DataFolder}{name}"));
}
