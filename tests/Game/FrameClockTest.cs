using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Vespera.Tests.Game;

/// <summary>
/// O jogo e os bots rodam com <c>--fixed-fps</c>: um quadro de física é sempre 1/60 s de jogo, mas quantos quadros cabem em
/// um segundo de relógio depende da máquina (o runner do CI renderiza ~3x mais que um PC). Tempo de jogo medido em
/// milissegundos de relógio (<c>Time.GetTicksMsec</c>, <c>Stopwatch</c>, <c>DateTime</c>) dá resultado diferente por máquina:
/// foi assim que a pausa de impacto virou 3x mais quadros parados no Linux e os bots não venciam os Guardiões. Combate,
/// atores, caçada e mundo contam ticks de física (<c>Engine.GetPhysicsFrames</c>) ou o <c>delta</c>.
/// </summary>
[TestSuite]
public class FrameClockTest
{
    // Linhas com "SeededRandom(" usam o relógio só como semente (pesca), não como duração de jogo.
    private static readonly string[] GameplayFolders = ["src/Game/Combat", "src/Game/Actors", "src/Game/Hunt", "src/Game/World"];
    private static readonly Regex WallClock = new(@"Time\.GetTicks(Msec|Usec)|Stopwatch|DateTime(Offset)?\.(Now|UtcNow)|OS\.GetTicks", RegexOptions.Compiled);

    [TestCase]
    public void GameplayTimingNeverReadsTheWallClock()
    {
        var root = FindRepositoryRoot();
        var offenders = new List<string>();
        foreach (var folder in GameplayFolders.Select(f => Path.Combine(root, f)).Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (WallClock.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && !lines[i].Contains("SeededRandom(", StringComparison.Ordinal))
                    {
                        offenders.Add($"{Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/')}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        AssertThat(offenders).IsEmpty();
    }

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
