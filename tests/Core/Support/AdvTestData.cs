namespace Vespera.Tests.Core.Support;

using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Ranks;

/// <summary>Catálogo real de progressão (data/progressao/*.json copiado para a saída do teste) e atalhos para montar o motor.</summary>
public static class AdvTestData
{
    private static readonly Lazy<AdvancementCatalog> Lazy = new(() => AdvancementCatalog.Load(ReadFile));

    public static AdvancementCatalog Catalog => Lazy.Value;

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Progressao");

    public static string ReadFile(string fileName) => File.ReadAllText(Path.Combine(DataDirectory, fileName));

    public static AdvancementCatalog LoadPatched(string fileName, Func<string, string> patch)
        => AdvancementCatalog.Load(name => name == fileName ? patch(ReadFile(name)) : ReadFile(name));

    public static ProgressionEngine NewEngine(string classId = "sword", int seed = 1, HunterProgress? progress = null)
        => new(Catalog, progress ?? new HunterProgress(), classId, new SeededRandom(seed));

    /// <summary>Caçador pronto no rank pedido: dá XP e conquistas de todos os ranks abaixo.</summary>
    public static ProgressionEngine HunterAt(Rank rank, string classId = "sword", int seed = 1)
    {
        var engine = NewEngine(classId, seed);
        var target = Catalog.RankIndex(rank);
        for (var i = 0; i < target; i++)
        {
            var step = Catalog.Ranks[i];
            engine.Progress.Xp = Math.Max(engine.Progress.Xp, step.Xp);
            engine.RecordGuardianKill(step.Guardian!);
            if (step.Exam)
            {
                engine.TryPassExam(step.Id);
            }
        }

        engine.Progress.Xp = Math.Max(engine.Progress.Xp, Catalog.Ranks[target].Xp);
        return engine;
    }
}
