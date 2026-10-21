namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// Tester da frente B2. Brechas ignoradas (jogador na Guilda/Estalagem enquanto elas rompem) e NPC com rotina andando numa máscara de células de 8 u não pode ficar "andando" parado para
/// sempre. Os pontos do caminho caem em bordas de célula (múltiplos de 4 u) e, com float, o NPC chega a um fio da
/// borda (ex.: x = 27,999996): a sonda lateral do <c>CanStand</c> cai na célula bloqueada vizinha, o passo no outro
/// eixo é pequeno demais para sair dali e ele fica preso. Na Rua da Forja, Lauro, Marta e Faísca travam assim.
/// </summary>
[TestSuite]
public class CityLifeTesterTest
{
    private const double Dt = 1.0 / 60.0;

    private static MaskWalkArea Parse(params string[] rows)
    {
        var cells = rows.SelectMany(r => r.Select(c => c == '.')).ToArray();
        return new MaskWalkArea(new WalkGrid(rows[0].Length, rows.Length, cells));
    }

    private static WalkerDef Walker(params Point2[] path) => new()
    {
        Id = "teste",
        Name = "Teste",
        Map = "x",
        HourFrom = 16,
        HourTo = 30,
        Speed = 30,
        Path = path,
        Lines = ["a"],
    };

    /// <summary>Anda <paramref name="seconds"/> s e devolve o maior tempo "andando" sem sair do lugar e os trechos visitados.</summary>
    private static (double WorstStuck, int Legs, float X, float Y) Run(IWalkArea area, TownWalker walker, double seconds)
    {
        var legs = new HashSet<int>();
        double stuck = 0, worst = 0;
        float lx = walker.X, ly = walker.Y, sx = 0, sy = 0;
        for (var i = 0; i < seconds / Dt; i++)
        {
            var was = walker.IsMoving;
            walker.Update(area, Dt);
            legs.Add(walker.Leg);
            if (was && walker.IsMoving)
            {
                stuck = Math.Abs(walker.X - lx) + Math.Abs(walker.Y - ly) < 0.01f ? stuck + Dt : 0;
                if (stuck > worst)
                {
                    worst = stuck;
                    sx = walker.X;
                    sy = walker.Y;
                }
            }
            else
            {
                stuck = 0;
            }

            lx = walker.X;
            ly = walker.Y;
        }

        return (worst, legs.Count, sx, sy);
    }

    [TestCase]
    public void WalkerDoesNotFreezeOnACellBorderNextToABlockedCell()
    {
        var area = Parse(
            "############",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#......#...#",
            "#.#........#",
            "#.....#....#",
            "#..........#",
            "#..........#",
            "############");
        var def = Walker(new Point2(46, 80), new Point2(20, 82), new Point2(26, 21));
        var route = WalkerRoute.Build(area, def.Path);
        AssertThat(route.Legs.All(l => l is not null)).IsTrue();

        var walker = new TownWalker(def, route, new SeededRandom(24));
        var (worst, legs, x, y) = Run(area, walker, 120);

        // Hoje: preso ~113 s em (27,999996; 64,18), indo de (24, 66) para (28, 62).
        AssertThat(worst).OverrideFailureMessage($"NPC ficou {worst:0.0} s andando sem sair do lugar em ({x:R}; {y:R})").IsLess(1.0);
        AssertThat(legs).IsEqual(3);
    }

    [TestCase]
    public void WalkersNeverFreezeOnSmallMasks()
    {
        // 200 máscaras 12×12 com moldura e 2–6 células bloqueadas, rota de 3 waypoints: ninguém pode travar.
        var frozen = new List<string>();
        for (var seed = 0; seed < 200; seed++)
        {
            var r = new Random(seed);
            const int w = 12, h = 12;
            var cells = Enumerable.Repeat(true, w * h).ToArray();
            for (var x = 0; x < w; x++)
            {
                cells[x] = false;
                cells[((h - 1) * w) + x] = false;
            }

            for (var y = 0; y < h; y++)
            {
                cells[y * w] = false;
                cells[(y * w) + w - 1] = false;
            }

            var blocks = r.Next(2, 7);
            for (var b = 0; b < blocks; b++)
            {
                cells[(r.Next(2, h - 2) * w) + r.Next(2, w - 2)] = false;
            }

            var area = new MaskWalkArea(new WalkGrid(w, h, cells));
            var def = Walker(new Point2(r.Next(12, 84), r.Next(12, 84)), new Point2(r.Next(12, 84), r.Next(12, 84)), new Point2(r.Next(12, 84), r.Next(12, 84)));
            var route = WalkerRoute.Build(area, def.Path);
            if (route.Legs.Any(l => l is null))
            {
                continue;
            }

            var (worst, _, sx, sy) = Run(area, new TownWalker(def, route, new SeededRandom(seed)), 120);
            if (worst >= 1.0)
            {
                frozen.Add($"seed {seed}: {worst:0} s em ({sx:0.000}; {sy:0.000})");
            }
        }

        AssertThat(frozen.Count).OverrideFailureMessage($"{frozen.Count} NPC(s) travados: {string.Join(", ", frozen.Take(8))}").IsEqual(0);
    }

    // --- Brechas ignoradas em sequência ---

    /// <summary>Jogador longe da rua por ~33 min: as Brechas abrem e rompem uma atrás da outra, sem Casa fechando (0,9 &gt; 0,55).</summary>
    private static BrechaSchedule IgnoredForAWhile(List<BrechaEvent> events)
    {
        var s = new BrechaSchedule(TestDb.Instance, new ConstantRandom(0.9), firstDone: true);
        for (var i = 0; i < 2000; i++)
        {
            s.Tick(1, 0, 1, events);
        }

        return s;
    }

    [TestCase]
    public void BrechaListNeverGrowsPastFive()
    {
        var events = new List<BrechaEvent>();
        var s = IgnoredForAWhile(events);
        var ruptures = events.Count(e => e.Kind == BrechaEventKind.Ruptured);

        // BrechaSchedule.MaxListed / api-cidade.md: "no máximo 5, mais antiga primeiro" (slice(-5) da v2).
        AssertThat(s.Brechas.Count)
            .OverrideFailureMessage($"{s.Brechas.Count} Brechas na lista depois de {ruptures} rupturas: {string.Join(", ", s.Brechas.Select(b => $"#{b.Id} {b.Status}"))}")
            .IsLessEqual(BrechaSchedule.MaxListed);
    }

    [TestCase]
    public void ResolvedInvasionLeavesNoRupturedBrechaBehind()
    {
        var events = new List<BrechaEvent>();
        var s = IgnoredForAWhile(events);
        AssertThat(events.Count(e => e.Kind == BrechaEventKind.Ruptured)).IsGreater(1);

        // InvasionStateChanged(false) = a Rua da Forja ficou limpa: nenhuma Brecha deveria continuar "ROMPEU" no Mural.
        s.ResolveInvasion();
        var left = s.Brechas.Where(b => b.Status == BrechaStatus.Ruptured).Select(b => $"#{b.Id}").ToList();
        AssertThat(left.Count)
            .OverrideFailureMessage($"invasão resolvida, mas {left.Count} Brecha(s) continuam rompidas: {string.Join(", ", left)}")
            .IsEqual(0);
    }
}
