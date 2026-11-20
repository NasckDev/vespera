using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Vespera.Tests.Game;

/// <summary>
/// Windows e macOS não diferenciam maiúsculas de minúsculas nos nomes de arquivo; o Linux (CI, servidores, Steam Deck) diferencia.
/// Um <c>res://assets/V2/foo.png</c> que "funciona" no PC de quem desenvolve some no Linux. Este teste compara todo
/// caminho <c>res://</c> citado em código, JSON, cenas e recursos com o nome real em disco, byte a byte (ordinal), e
/// também acusa dois arquivos que só diferem pela caixa (um deles sumiria num checkout em Windows).
/// </summary>
[TestSuite]
public class ResourcePathCaseTest
{
    private static readonly string[] SkippedDirectories = [".git", ".godot", ".vs", "tmp", "bin", "obj", "TestResults", "node_modules", "docs", "site", "installer"];
    private static readonly string[] ScannedExtensions = [".cs", ".json", ".tscn", ".tres", ".godot", ".cfg", ".gd", ".import"];
    private static readonly Regex ResPath = new("""res://[^"'\s)\]},;<>|`\\]+""", RegexOptions.Compiled);

    [TestCase]
    public void EveryLiteralResPathMatchesTheNameOnDiskExactly()
    {
        var root = FindRepositoryRoot();
        var files = ListFiles(root);
        var exact = new HashSet<string>(files, StringComparer.Ordinal);
        var directories = new HashSet<string>(Directories(files), StringComparer.Ordinal);
        var byLowerCase = files.Concat(directories).GroupBy(p => p, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();
        foreach (var (file, reference) in LiteralReferences(root, files))
        {
            if (IsDynamicOrIgnored(reference) || exact.Contains(reference) || directories.Contains(reference.TrimEnd('/')))
            {
                continue;
            }

            if (byLowerCase.TryGetValue(reference.TrimEnd('/'), out var real))
            {
                mismatches.Add($"{file}: 'res://{reference}' existe como '{real}' (caixa diferente; quebra no Linux)");
            }
        }

        AssertThat(mismatches).IsEmpty();
    }

    [TestCase]
    public void NoTwoFilesDifferOnlyByCase()
    {
        var files = ListFiles(FindRepositoryRoot());
        var collisions = files.GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => string.Join(" <-> ", g)).ToList();

        AssertThat(collisions).IsEmpty();
    }

    [TestCase]
    public void TheCheckerCatchesAWrongCasePath()
    {
        // Prova o próprio checador: o caminho existe, mas com a caixa trocada, e precisa ser acusado.
        var files = new[] { "assets/v2/fonts/PixelifySans.ttf" };
        var exact = new HashSet<string>(files, StringComparer.Ordinal);
        var lower = files.ToDictionary(p => p, p => p, StringComparer.OrdinalIgnoreCase);

        const string wrong = "assets/v2/fonts/pixelifysans.ttf";

        AssertThat(exact.Contains(wrong)).IsFalse();
        AssertThat(lower.ContainsKey(wrong)).IsTrue();
    }

    /// <summary>Caminhos montados em tempo de execução (<c>{id}</c>, <c>%s</c>, prefixos sem arquivo) não têm como ser conferidos aqui.</summary>
    private static bool IsDynamicOrIgnored(string reference) =>
        reference.Length == 0 || reference.StartsWith(".godot", StringComparison.Ordinal) || reference.StartsWith("..", StringComparison.Ordinal)
        || reference.Contains('{') || reference.Contains('%') || reference.Contains('*') || reference.EndsWith('.');

    private static IEnumerable<(string File, string Reference)> LiteralReferences(string root, IReadOnlyList<string> files)
    {
        foreach (var file in files.Where(f => ScannedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            string text;
            try
            {
                text = File.ReadAllText(Path.Combine(root, file));
            }
            catch (IOException)
            {
                continue;
            }

            foreach (Match match in ResPath.Matches(text))
            {
                yield return (file, match.Value["res://".Length..]);
            }
        }
    }

    /// <summary>Caminhos relativos à raiz (com "/"), com a caixa que o sistema de arquivos realmente guarda.</summary>
    private static List<string> ListFiles(string root)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var dir in Directory.EnumerateDirectories(current))
            {
                if (!SkippedDirectories.Contains(Path.GetFileName(dir), StringComparer.Ordinal))
                {
                    pending.Push(dir);
                }
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                result.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            }
        }

        return result;
    }

    private static IEnumerable<string> Directories(IEnumerable<string> files) =>
        files.SelectMany(f =>
        {
            var parts = f.Split('/');
            return Enumerable.Range(1, parts.Length - 1).Select(n => string.Join('/', parts.Take(n)));
        }).Distinct(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("project.godot não encontrado acima de " + AppContext.BaseDirectory);
    }
}
