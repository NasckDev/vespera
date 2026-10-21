namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// B2-D48: rotas dos moradores calculadas em tempo de execução sobre a área carregada (qualquer máscara válida, de 4 ou
/// 8 u), com replanejamento quando travam e desvio de outros corpos.
/// </summary>
[TestSuite]
public class WalkPlannerTest
{
    private const double Dt = 1.0 / 60.0;

    private static WalkerDef Walker(float speed, params Point2[] path) => new()
    {
        Id = "teste",
        Name = "Teste",
        Map = "x",
        HourFrom = 16,
        HourTo = 30,
        Speed = speed,
        Path = path,
        Lines = ["a"],
    };

    /// <summary>Máscara com moldura, "prédios" retangulares e alguns móveis soltos.</summary>
    private static (MaskWalkArea Area, bool[] Cells, int W, int H) RandomTown(int seed, int cell)
    {
        var r = new Random(seed);
        var w = 480 / cell;
        var h = 320 / cell;
        var cells = Enumerable.Repeat(true, w * h).ToArray();
        void Block(int x0, int y0, int bw, int bh)
        {
            for (var y = Math.Max(0, y0); y < Math.Min(h, y0 + bh); y++)
            {
                for (var x = Math.Max(0, x0); x < Math.Min(w, x0 + bw); x++)
                {
                    cells[(y * w) + x] = false;
                }
            }
        }

        Block(0, 0, w, 1);
        Block(0, h - 1, w, 1);
        Block(0, 0, 1, h);
        Block(w - 1, 0, 1, h);
        var buildings = r.Next(3, 8);
        for (var b = 0; b < buildings; b++)
        {
            Block(r.Next(2, w - 4), r.Next(2, h - 4), r.Next(3, w / 4), r.Next(3, h / 4));
        }

        for (var b = 0; b < r.Next(4, 12); b++)
        {
            Block(r.Next(2, w - 2), r.Next(2, h - 2), 1, 1);
        }

        return (new MaskWalkArea(new WalkGrid(w, h, cells), cell), cells, w, h);
    }

    private static (double WorstStuck, HashSet<int> Legs) Walk(IWalkArea area, TownWalker walker, double seconds, IReadOnlyList<MapPoint>? others = null)
    {
        var legs = new HashSet<int>();
        double stuck = 0, worst = 0;
        float lx = walker.X, ly = walker.Y;
        for (var i = 0; i < seconds / Dt; i++)
        {
            var was = walker.IsMoving;
            walker.Update(area, Dt, others);
            AssertThat(area.CanStand(walker.X, walker.Y, WalkMover.NpcRadius))
                .OverrideFailureMessage($"NPC entrou na parede em ({walker.X}; {walker.Y})").IsTrue();
            legs.Add(walker.Leg);
            stuck = was && walker.IsMoving && Math.Abs(walker.X - lx) + Math.Abs(walker.Y - ly) < 0.01f ? stuck + Dt : 0;
            worst = Math.Max(worst, stuck);
            lx = walker.X;
            ly = walker.Y;
        }

        return (worst, legs);
    }

    [TestCase]
    public void LatticeNodesNeverSitOnACellBorder()
    {
        // Raio 4: sondas em x ± 4 e y ± 2,4. Com nós em x ≡ 2 e y ≡ 0 (mod 4), nenhuma sonda cai numa borda de 4 ou 8 u.
        var planner = new WalkPlanner(new MaskWalkArea(new WalkGrid(40, 40, Enumerable.Repeat(true, 1600).ToArray()), 4));
        for (var i = 0; i < 20; i++)
        {
            var p = planner.NodePoint(i, i);
            foreach (var probe in new[] { p.X - 4, p.X + 4 })
            {
                AssertThat(Math.Abs(probe - (MathF.Round(probe / 4) * 4))).IsGreaterEqual(1.9f);
            }

            foreach (var probe in new[] { p.Y - 2.4f, p.Y + 2.4f })
            {
                AssertThat(Math.Abs(probe - (MathF.Round(probe / 4) * 4))).IsGreaterEqual(1.5f);
            }
        }
    }

    [TestCase]
    public void RoutesWorkOnAnyMask()
    {
        // 120 cidades aleatórias (metade com células de 4 u, como a máscara nova da Rua da Forja; metade de 8 u), waypoints
        // em qualquer lugar (inclusive dentro de prédio): ninguém entra em parede nem fica "andando" parado.
        var frozen = new List<string>();
        var incomplete = 0;
        for (var seed = 0; seed < 120; seed++)
        {
            var cell = seed % 2 == 0 ? 4 : 8;
            var (area, _, _, _) = RandomTown(seed, cell);
            var r = new Random(seed + 7);
            var path = Enumerable.Range(0, r.Next(3, 5)).Select(_ => new Point2(r.Next(12, 468), r.Next(12, 308))).ToArray();
            var route = WalkerRoute.Build(area, path);
            foreach (var w in route.Waypoints)
            {
                AssertThat(area.CanStand(w.X, w.Y, WalkMover.NpcRadius)).IsTrue();
            }

            var walker = new TownWalker(Walker(30, path), route, new SeededRandom(seed));
            var (worst, legs) = Walk(area, walker, 150);
            if (worst >= 1.0)
            {
                frozen.Add($"seed {seed} (célula {cell}): {worst:0.0} s");
            }

            if (route.Legs.All(l => l is not null) && legs.Count < route.Waypoints.Count)
            {
                incomplete++;
            }
        }

        AssertThat(frozen.Count).OverrideFailureMessage($"travados: {string.Join(", ", frozen.Take(8))}").IsEqual(0);
        AssertThat(incomplete).OverrideFailureMessage($"{incomplete} rota(s) completas que o NPC não percorreu").IsEqual(0);
    }

    [TestCase]
    public void StaleRouteFromAnOldMaskIsReplanned()
    {
        // A rota foi feita para a máscara antiga; na nova, um muro corta a rua no meio (B4b refazendo a colisão).
        var oldCells = Enumerable.Repeat(true, 60 * 40).ToArray();
        var newCells = (bool[])oldCells.Clone();
        for (var y = 0; y < 30; y++)
        {
            newCells[(y * 60) + 30] = false; // muro em x = 120..124, aberto embaixo (y ≥ 120)
        }

        var oldArea = new MaskWalkArea(new WalkGrid(60, 40, oldCells), 4);
        var newArea = new MaskWalkArea(new WalkGrid(60, 40, newCells), 4);
        var def = Walker(30, new Point2(40, 40), new Point2(200, 40));
        var route = WalkerRoute.Build(oldArea, def.Path);
        AssertThat(route.Legs[0]!.Count).IsEqual(2); // reta na máscara antiga

        var walker = new TownWalker(def, route, new ConstantRandom(0.0));
        var (worst, legs) = Walk(newArea, walker, 60);
        AssertThat(worst).IsLess(1.0);
        AssertThat(legs.Count).IsEqual(2); // chegou ao outro lado e voltou a contar trechos
        AssertThat(walker.Replans).IsGreater(0);
    }

    [TestCase]
    public void WaypointInsideAWallGoesToTheRouteRegion()
    {
        // Rua larga embaixo; um quintal fechado em cima, colado num prédio. O waypoint cai no prédio, mais perto do
        // quintal que da rua: tem que ir para a rua, de onde os outros waypoints chegam.
        var w = 50;
        var h = 40;
        var cells = Enumerable.Repeat(true, w * h).ToArray();
        void Block(int x0, int y0, int bw, int bh)
        {
            for (var y = y0; y < y0 + bh; y++)
            {
                for (var x = x0; x < x0 + bw; x++)
                {
                    cells[(y * w) + x] = false;
                }
            }
        }

        Block(0, 0, w, 1);
        Block(0, h - 1, w, 1);
        Block(0, 0, 1, h);
        Block(w - 1, 0, 1, h);
        Block(1, 10, 48, 1);   // muro do quintal (y = 40..44), fecha o quintal de cima
        Block(10, 11, 20, 12); // prédio em x 40..120, y 44..92
        var area = new MaskWalkArea(new WalkGrid(w, h, cells), 4);
        var def = Walker(30, new Point2(70, 50), new Point2(160, 130), new Point2(40, 130));
        var route = WalkerRoute.Build(area, def.Path);
        AssertThat(route.Legs.All(l => l is not null)).IsTrue();
        var planner = new WalkPlanner(area);
        AssertThat(planner.RegionOf(route.Waypoints[0])).IsEqual(planner.RegionOf(new MapPoint(160, 130)));
        AssertThat(route.Waypoints[0].Y).IsGreater(44f); // não foi para o quintal (y < 40), que está mais perto
    }

    [TestCase]
    public void PlanBetweenSeparateRoomsFailsWithoutSearching()
    {
        var cells = Enumerable.Repeat(true, 40 * 20).ToArray();
        for (var y = 0; y < 20; y++)
        {
            cells[(y * 40) + 20] = false;
            cells[(y * 40) + 21] = false;
        }

        var planner = new WalkPlanner(new MaskWalkArea(new WalkGrid(40, 20, cells), 4));
        var result = new List<MapPoint>();
        AssertThat(planner.TryPlan(new MapPoint(30, 40), new MapPoint(130, 40), result)).IsFalse();
        AssertThat(planner.LastExpansions).IsEqual(0);
        AssertThat(planner.RegionOf(new MapPoint(30, 40))).IsNotEqual(planner.RegionOf(new MapPoint(130, 40)));
        AssertThat(planner.TryPlan(new MapPoint(30, 40), new MapPoint(50, 70), result)).IsTrue();
    }

    [TestCase]
    public void WalkersGoingOppositeWaysStepAside()
    {
        // Rua de 40 u de largura: dois NPCs de frente desviam e não passam um "por dentro" do outro.
        var area = new MaskWalkArea(new WalkGrid(80, 12, Enumerable.Range(0, 80 * 12).Select(i => i / 80 is > 0 and < 11).ToArray()), 4);
        var a = new TownWalker(Walker(30, new Point2(20, 24), new Point2(300, 24)), WalkerRoute.Build(area, [new Point2(20, 24), new Point2(300, 24)]), new ConstantRandom(0.0));
        var b = new TownWalker(Walker(30, new Point2(300, 24), new Point2(20, 24)), WalkerRoute.Build(area, [new Point2(300, 24), new Point2(20, 24)]), new ConstantRandom(0.0));
        var bodies = new List<MapPoint> { default, default };
        var closest = float.MaxValue;
        for (var i = 0; i < 14 / Dt; i++)
        {
            bodies[0] = new MapPoint(a.X, a.Y);
            bodies[1] = new MapPoint(b.X, b.Y);
            a.Update(area, Dt, bodies, 0);
            b.Update(area, Dt, bodies, 1);
            closest = MathF.Min(closest, WalkMover.Hypot(a.X - b.X, a.Y - b.Y));
        }

        AssertThat(closest).IsGreater(7f);
        AssertThat(a.Leg).IsEqual(1); // os dois chegaram
        AssertThat(b.Leg).IsEqual(1);
    }

    [TestCase]
    public void SomeoneStandingInANarrowAlleyDoesNotFreezeTheWalker()
    {
        // Beco de um corpo só, com o jogador parado no meio: o NPC cede a vez, depois passa (como a v2), sem travar.
        var w = 60;
        var cells = new bool[w * 7];
        for (var x = 1; x < w - 1; x++)
        {
            cells[(3 * w) + x] = true; // uma linha de células de 8 u: y 24..32
        }

        var area = new MaskWalkArea(new WalkGrid(w, 7, cells), 8);
        var path = new[] { new Point2(20, 28), new Point2(400, 28) };
        var walker = new TownWalker(Walker(30, path), WalkerRoute.Build(area, path), new ConstantRandom(0.0));
        var player = new List<MapPoint> { new(200, 28) };
        var (worst, legs) = Walk(area, walker, 30, player);
        AssertThat(worst).IsLess(1.0);
        AssertThat(legs.Count).IsEqual(2);
    }
}
