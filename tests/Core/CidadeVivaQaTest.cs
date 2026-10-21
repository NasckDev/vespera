namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// QA de experiência da frente B2 (rodada 1): moradores andando juntos (NPCs com rotina + caçadores ambiente + um
/// jogador parado ou andando) em máscaras de 8 e de 4 u. Travar aqui é "andar e não sair de 1 u em 1 s" (pega também o
/// vai-e-volta numa quina, que o "parado no lugar" não pega), e "um dentro do outro" é dois corpos a menos de 6 u.
/// </summary>
[TestSuite]
public class CidadeVivaQaTest
{
    private const double Dt = 1.0 / 60.0;

    private static WalkerDef Def(string id, float speed, params Point2[] path) => new()
    {
        Id = id,
        Name = id,
        Map = "x",
        HourFrom = 16,
        HourTo = 30,
        Speed = speed,
        Path = path,
        Lines = ["a"],
    };

    private static MaskWalkArea Parse(int cell, params string[] rows)
    {
        var cells = rows.SelectMany(r => r.Select(c => c == '.')).ToArray();
        return new MaskWalkArea(new WalkGrid(rows[0].Length, rows.Length, cells), cell);
    }

    /// <summary>Cidade aleatória com moldura, prédios e móveis, em células de <paramref name="cell"/> u (mesma forma em u para 4 e 8).</summary>
    private static MaskWalkArea Town(int seed, int cell)
    {
        var r = new Random(seed);
        const int w8 = 80, h8 = 50; // 640 × 400 u
        var coarse = Enumerable.Repeat(true, w8 * h8).ToArray();
        void Block(int x0, int y0, int bw, int bh)
        {
            for (var y = Math.Max(0, y0); y < Math.Min(h8, y0 + bh); y++)
            {
                for (var x = Math.Max(0, x0); x < Math.Min(w8, x0 + bw); x++)
                {
                    coarse[(y * w8) + x] = false;
                }
            }
        }

        Block(0, 0, w8, 1);
        Block(0, h8 - 1, w8, 1);
        Block(0, 0, 1, h8);
        Block(w8 - 1, 0, 1, h8);
        for (var b = r.Next(5, 10); b > 0; b--)
        {
            Block(r.Next(2, w8 - 6), r.Next(2, h8 - 6), r.Next(3, 14), r.Next(3, 10));
        }

        for (var b = r.Next(6, 16); b > 0; b--)
        {
            Block(r.Next(2, w8 - 2), r.Next(2, h8 - 2), 1, 1);
        }

        if (cell == 8)
        {
            return new MaskWalkArea(new WalkGrid(w8, h8, coarse), 8);
        }

        // Mesma cidade em células de 4 u, com as bordas dos prédios serrilhadas meia célula (como uma máscara redesenhada).
        var w4 = w8 * 2;
        var h4 = h8 * 2;
        var fine = new bool[w4 * h4];
        for (var y = 0; y < h4; y++)
        {
            for (var x = 0; x < w4; x++)
            {
                fine[(y * w4) + x] = coarse[((y / 2) * w8) + (x / 2)];
            }
        }

        for (var y = 2; y < h4 - 2; y++)
        {
            for (var x = 2; x < w4 - 2; x++)
            {
                if (!fine[(y * w4) + x] && fine[(y * w4) + x - 1] && r.NextDouble() < 0.3)
                {
                    fine[(y * w4) + x - 1] = false; // engorda a borda esquerda de vez em quando
                }
            }
        }

        return new MaskWalkArea(new WalkGrid(w4, h4, fine), 4);
    }

    private sealed class Watch
    {
        public float AnchorX;
        public float AnchorY;
        public double AnchorT;
        public double Worst;
        public float WorstX;
        public float WorstY;
        public TownFacing Facing;
        public TownFacing Previous;
        public double ChangedAt = -10;
        public int Flicks;

        public void Face(TownFacing facing, double t)
        {
            if (facing == Facing)
            {
                return;
            }

            if (facing == Previous && t - ChangedAt < 0.15)
            {
                Flicks++;
            }

            Previous = Facing;
            Facing = facing;
            ChangedAt = t;
        }

        public void Sample(bool moving, float x, float y, double t)
        {
            if (!moving || MathF.Abs(x - AnchorX) + MathF.Abs(y - AnchorY) >= 1f)
            {
                AnchorX = x;
                AnchorY = y;
                AnchorT = t;
                return;
            }

            if (t - AnchorT > Worst)
            {
                Worst = t - AnchorT;
                WorstX = x;
                WorstY = y;
            }
        }
    }

    /// <summary>
    /// 3 NPCs e 2 caçadores juntos por <paramref name="seconds"/> s, cada um vendo os outros e um jogador que anda de um
    /// lado para o outro. Devolve os problemas encontrados.
    /// </summary>
    private static List<string> Simulate(IWalkArea area, int seed, double seconds, bool playerStands)
    {
        var rng = new SeededRandom(seed);
        var planner = new WalkPlanner(area);
        planner.Prepare();
        var problems = new List<string>();
        var walkers = new List<TownWalker>();
        for (var k = 0; k < 3; k++)
        {
            var pts = new List<Point2>();
            for (var p = 0; p < 3; p++)
            {
                var q = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
                pts.Add(new Point2(q.X, q.Y));
            }

            var route = WalkerRoute.Build(planner, pts);
            walkers.Add(new TownWalker(Def($"npc{k}", 22 + (7 * k), [.. pts]), route, rng, planner));
        }

        var social = TestDb.Instance.Social;
        var hunters = new List<AmbientHunter>();
        for (var k = 0; k < 2; k++)
        {
            // Nascer onde o corpo não cabe é outro teste (HunterSpawnedWhereItsBodyDoesNotFitStillWalks).
            var at = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
            while (!area.CanStand(at.X, at.Y, WalkMover.NpcRadius))
            {
                at = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
            }

            hunters.Add(new AmbientHunter(new HunterDef { Name = $"h{k}", Rank = "C", Weapon = "x" }, at.X, at.Y, social, rng, planner));
        }

        // Jogador: anda entre dois pontos (ou fica parado no meio da rua).
        var a = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
        var b = WalkMover.RandomPoint(area, area.Width, area.Height, rng, a.X, a.Y, minDistance: 150f, radius: 8f);
        var player = new WalkFollower(a.X, a.Y);
        var playerTo = b;
        player.Plan(planner, playerTo);

        var n = walkers.Count + hunters.Count + 1;
        var bodies = new List<MapPoint>(n);
        var watches = Enumerable.Range(0, n - 1).Select(_ => new Watch()).ToArray();
        var overlapRun = new double[n * n];
        var overlapWorst = new double[n * n];
        var t = 0.0;
        for (var i = 0; i < seconds / Dt; i++, t += Dt)
        {
            bodies.Clear();
            bodies.Add(new MapPoint(player.X, player.Y));
            bodies.AddRange(walkers.Select(w => new MapPoint(w.X, w.Y)));
            bodies.AddRange(hunters.Select(h => new MapPoint(h.X, h.Y)));
            if (!playerStands && player.Advance(area, 78f, Dt) == WalkStatus.Arrived)
            {
                playerTo = playerTo.Equals(a) ? b : a;
                player.Plan(planner, playerTo);
            }

            for (var k = 0; k < walkers.Count; k++)
            {
                walkers[k].Update(area, Dt, bodies, 1 + k);
            }

            for (var k = 0; k < hunters.Count; k++)
            {
                hunters[k].Update(area, area.Width, area.Height, Dt, bodies, 1 + walkers.Count + k);
            }

            for (var k = 0; k < n - 1; k++)
            {
                var (x, y, moving, name) = k < walkers.Count
                    ? (walkers[k].X, walkers[k].Y, walkers[k].IsMoving, $"npc{k}")
                    : (hunters[k - walkers.Count].X, hunters[k - walkers.Count].Y, hunters[k - walkers.Count].IsMoving, $"caçador{k - walkers.Count}");
                if (!area.CanStand(x, y, WalkMover.NpcRadius))
                {
                    problems.Add($"[parede] {name} na parede em ({x:0.0}; {y:0.0}) t={t:0.0}");
                    return problems;
                }

                watches[k].Sample(moving, x, y, t);
                watches[k].Face(k < walkers.Count ? walkers[k].Facing : hunters[k - walkers.Count].Facing, t);
            }

            // Pares de moradores (sem o jogador) um dentro do outro.
            for (var p = 1; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    var px = p <= walkers.Count ? walkers[p - 1].X : hunters[p - 1 - walkers.Count].X;
                    var py = p <= walkers.Count ? walkers[p - 1].Y : hunters[p - 1 - walkers.Count].Y;
                    var qx = q <= walkers.Count ? walkers[q - 1].X : hunters[q - 1 - walkers.Count].X;
                    var qy = q <= walkers.Count ? walkers[q - 1].Y : hunters[q - 1 - walkers.Count].Y;
                    var k = (p * n) + q;
                    overlapRun[k] = WalkMover.Hypot(px - qx, py - qy) < 6f ? overlapRun[k] + Dt : 0;
                    overlapWorst[k] = Math.Max(overlapWorst[k], overlapRun[k]);
                }
            }
        }

        for (var k = 0; k < n - 1; k++)
        {
            if (watches[k].Flicks / (seconds / 60.0) > 2)
            {
                problems.Add($"[tremida] {(k < walkers.Count ? $"npc{k}" : $"caçador{k - walkers.Count}")}: sprite virou e voltou em < 0,15 s {watches[k].Flicks}x em {seconds:0} s (desviando dos outros)");
            }

            if (watches[k].Worst >= 1.0)
            {
                problems.Add($"[trava] {(k < walkers.Count ? $"npc{k}" : $"caçador{k - walkers.Count}")} andou {watches[k].Worst:0.0} s sem sair de 1 u em ({watches[k].WorstX:0.0}; {watches[k].WorstY:0.0})");
            }
        }

        for (var k = 0; k < overlapWorst.Length; k++)
        {
            if (overlapWorst[k] >= 1.5)
            {
                problems.Add($"[sobreposto] corpos {k / n} e {k % n} ficaram {overlapWorst[k]:0.0} s um dentro do outro");
            }
        }

        return problems;
    }

    private static readonly Dictionary<int, List<string>> Runs = [];

    /// <summary>30 cidades de 8 u e 30 de 4 u, 90 s cada (simuladas uma vez e reaproveitadas pelos testes abaixo).</summary>
    private static List<string> Problems(int cell, string kind)
    {
        lock (Runs)
        {
            if (!Runs.TryGetValue(cell, out var all))
            {
                all = [];
                for (var seed = 0; seed < 30; seed++)
                {
                    all.AddRange(Simulate(Town(seed, cell), seed, 90, playerStands: seed % 3 == 0).Select(p => $"seed {seed}: {p}"));
                }

                Runs[cell] = all;
            }

            return all.Where(p => p.Contains($"[{kind}]", StringComparison.Ordinal)).ToList();
        }
    }

    private static void AssertNone(List<string> problems) =>
        AssertThat(problems.Count).OverrideFailureMessage($"{problems.Count} problema(s): {string.Join(" | ", problems.Take(8))}").IsEqual(0);

    [TestCase]
    public void TownfolkTogetherNeverFreezeNorEnterWalls()
    {
        foreach (var cell in new[] { 8, 4 })
        {
            AssertNone([.. Problems(cell, "trava"), .. Problems(cell, "parede")]);
        }
    }

    [TestCase]
    public void TownfolkSpriteDoesNotFlickWhileAvoidingOthers()
    {
        // Sozinho não treme (WalkerSpriteDoesNotFlickBetweenDirections); desviando dos outros, o desvio entorta a direção
        // frame a frame e o sprite pisca entre frente/costas e lado.
        AssertNone([.. Problems(8, "tremida"), .. Problems(4, "tremida")]);
    }

    [TestCase]
    public void TownfolkDoNotStandInsideEachOther()
    {
        AssertNone([.. Problems(8, "sobreposto"), .. Problems(4, "sobreposto")]);
    }

    [TestCase]
    public void TwoWalkersMeetingInANarrowCorridorBothGetThrough()
    {
        // Corredor de 3 células (24 u) com salas nas pontas: um vai, o outro volta, de frente.
        var rows = new List<string> { new('#', 40) };
        for (var y = 0; y < 6; y++)
        {
            rows.Add("#" + new string('.', 6) + new string('#', 26) + new string('.', 6) + "#");
        }

        rows[3] = rows[4] = rows[5] = "#" + new string('.', 38) + "#";
        rows.Add(new string('#', 40));
        var area = Parse(8, [.. rows]);
        var left = Def("a", 30, new Point2(28, 36), new Point2(292, 36));
        var right = Def("b", 30, new Point2(292, 36), new Point2(28, 36));
        var rng = new SeededRandom(5);
        var wa = new TownWalker(left, WalkerRoute.Build(area, left.Path), new ConstantRandomStart());
        var wb = new TownWalker(right, WalkerRoute.Build(area, right.Path), new ConstantRandomStart());
        var bodies = new List<MapPoint>(2);
        double doneA = -1, doneB = -1;
        for (var i = 0; i < 30 / Dt; i++)
        {
            bodies.Clear();
            bodies.Add(new MapPoint(wa.X, wa.Y));
            bodies.Add(new MapPoint(wb.X, wb.Y));
            wa.Update(area, Dt, bodies, 0);
            wb.Update(area, Dt, bodies, 1);
            AssertThat(area.CanStand(wa.X, wa.Y) && area.CanStand(wb.X, wb.Y)).IsTrue();
            if (doneA < 0 && wa.Leg == 1)
            {
                doneA = i * Dt;
            }

            if (doneB < 0 && wb.Leg == 1)
            {
                doneB = i * Dt;
            }
        }

        // 264 u a 30 u/s = 8,8 s + espera inicial; cruzar de frente pode custar um desvio, não 10 s de espera.
        AssertThat(doneA).OverrideFailureMessage($"quem ia para a direita chegou em {doneA:0.0} s").IsBetween(0, 14);
        AssertThat(doneB).OverrideFailureMessage($"quem ia para a esquerda chegou em {doneB:0.0} s").IsBetween(0, 14);
        _ = rng;
    }

    [TestCase]
    public void PlannerRegionsCoverEverythingAPlayerCanReach()
    {
        // B2-D48: "todo vão por onde um corpo de raio 4 passa tem um nó dentro". O que a BFS do jogador alcança a partir
        // de um ponto (WalkPathfinder, raio 4) tem que ser a mesma região para o A* dos moradores.
        var splits = new List<string>();
        foreach (var cell in new[] { 8, 4 })
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var area = Town(seed, cell);
                var planner = new WalkPlanner(area);
                var rng = new SeededRandom(seed);
                var start = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
                if (!planner.CanStandFirmly(start))
                {
                    continue;
                }

                var home = planner.RegionOf(start);
                var reached = new List<MapPoint>();
                WalkPathfinder.FindPath(area, start, p =>
                {
                    reached.Add(p);
                    return false;
                });
                var cut = reached.Where(p => planner.CanStandFirmly(p) && planner.RegionOf(p) != home).ToList();
                if (cut.Count > 10)
                {
                    splits.Add($"{cell} u seed {seed}: {cut.Count} de {reached.Count} pontos alcançáveis a pé ficam em outra região do A*, ex. ({cut[0].X:0}; {cut[0].Y:0})");
                }
            }
        }

        AssertThat(splits.Count).OverrideFailureMessage($"{splits.Count} cidade(s) com área alcançável que o A* separa: {string.Join(" | ", splits.Take(6))}").IsEqual(0);
    }

    [TestCase]
    public void HunterSpawnedWhereItsBodyDoesNotFitStillWalks()
    {
        // O diretor sorteia o ponto do caçador com RandomPoint(raio 8) (sondas a ±8 u), mas o corpo dele tem raio 4 (sondas
        // a ±4 u): ao lado de um poste/móvel de uma célula, o ponto vale para 8 e não para 4. Ali o TryPlan recusa a
        // partida (CanStand(from) falso) e o caçador fica esperando 0,5 s e sorteando de novo, parado para sempre.
        var frozen = new List<string>();
        var found = 0;
        for (var seed = 0; seed < 60 && found < 10; seed++)
        {
            var area = Town(seed, 8);
            var rng = new SeededRandom(seed);
            for (var tries = 0; tries < 300 && found < 10; tries++)
            {
                var at = WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f);
                if (area.CanStand(at.X, at.Y, WalkMover.NpcRadius))
                {
                    continue;
                }

                found++;
                var hunter = new AmbientHunter(new HunterDef { Name = "h", Rank = "C", Weapon = "x" }, at.X, at.Y, TestDb.Instance.Social, rng, new WalkPlanner(area));
                for (var i = 0; i < 30 / Dt; i++)
                {
                    hunter.Update(area, area.Width, area.Height, Dt);
                }

                var moved = WalkMover.Hypot(hunter.X - at.X, hunter.Y - at.Y);
                if (moved < 10f)
                {
                    frozen.Add($"seed {seed}: nasceu em ({at.X:0.0}; {at.Y:0.0}) e andou {moved:0.0} u em 30 s");
                }
            }
        }

        AssertThat(found).OverrideFailureMessage("nenhum ponto de nascimento problemático encontrado; o teste não exercitou o caso").IsGreater(0);
        AssertThat(frozen.Count).OverrideFailureMessage($"{frozen.Count} de {found} caçadores ficaram parados para sempre: {string.Join(" | ", frozen.Take(5))}").IsEqual(0);
    }

    [TestCase]
    public void WalkerSpriteDoesNotFlickBetweenDirections()
    {
        // Sozinho na cidade (ninguém para desviar): o sprite não pode virar para um lado e voltar em menos de 0,15 s
        // (de frente → de lado → de frente por 1–8 frames é uma "tremida" visível).
        var report = new List<string>();
        var total = 0;
        var minutes = 0.0;
        foreach (var cell in new[] { 8, 4 })
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var area = Town(seed, cell);
                var rng = new SeededRandom(seed);
                var planner = new WalkPlanner(area);
                var pts = Enumerable.Range(0, 4).Select(_ => WalkMover.RandomPoint(area, area.Width, area.Height, rng, 0, 0, radius: 8f)).Select(q => new Point2(q.X, q.Y)).ToArray();
                var walker = new TownWalker(Def("w", 30, pts), WalkerRoute.Build(planner, pts), rng, planner);
                var facing = walker.Facing;
                var previous = facing;
                var changedAt = -10.0;
                var flicks = 0;
                string? first = null;
                for (var i = 0; i < 120 / Dt; i++)
                {
                    walker.Update(area, Dt);
                    var t = i * Dt;
                    if (walker.Facing != facing)
                    {
                        if (walker.Facing == previous && t - changedAt < 0.15)
                        {
                            flicks++;
                            first ??= $"({walker.X:0.0}; {walker.Y:0.0}) {previous}→{facing}→{walker.Facing}";
                        }

                        previous = facing;
                        facing = walker.Facing;
                        changedAt = t;
                    }
                }

                total += flicks;
                minutes += 2;
                if (flicks > 0)
                {
                    report.Add($"{cell} u seed {seed}: {flicks}x, ex. {first}");
                }
            }
        }

        // Tolerância: uma tremida a cada 2 min de caminhada.
        AssertThat(total / minutes).OverrideFailureMessage($"{total} tremidas em {minutes:0} min de caminhada sem ninguém por perto: {string.Join(" | ", report.Take(6))}").IsLessEqual(0.5);
    }

    /// <summary>Sorteios sempre 0: o NPC nasce no primeiro waypoint e não espera para sair.</summary>
    private sealed class ConstantRandomStart : IRandom
    {
        public double NextDouble() => 0;
    }
}
