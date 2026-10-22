using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.Testing;

/// <summary>
/// QA da frente B4d (interiores: Guilda, Estalagem e as 4 lojas), escrito pelo tester de experiência. Joga como um
/// jogador: entra pela porta, anda com input real até cada dono/balcão, para atrás de cada móvel alto (oclusor do
/// layout) para o print mostrar se ele cobre o jogador, e sai. Os cenários "estresse:" (só com <c>--smoke-stress</c>)
/// rodam um bot aleatório + bot de alvos por 6 min de jogo detectando enrosco (input ativo e menos de 1 u de progresso
/// em 1 s de jogo), 50 trocas de mapa medindo nós/memória, spam de ações e o tempo de frame (média e p95) a 1×.
/// </summary>
public partial class SmokeTest
{
    private static readonly StringName[] QaSpamActions =
    [
        InputActions.Attack, InputActions.AttackHeavy, InputActions.Dodge, InputActions.Interact, InputActions.Lantern, InputActions.Pause,
    ];

    [SmokeScenario("testa interiores (QA): entra pela porta, fala com o dono, para atrás dos móveis altos e sai", order: 40)]
    private async Task QaInteriorsPlayerTour()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=QA
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var size = DisplayServer.WindowGetSize();
        var tag = $"{size.X}x{size.Y}";
        var behindShots = size.Y == 1080; // prints atrás de cada móvel só em 1080p; nas outras resoluções, chegada e dono
        var failures = new List<string>();
        var behind = new List<string>();
        foreach (var id in InteriorMapIds)
        {
            var map = World.World.Catalog[id];

            // 1. Entrada: Guilda e Estalagem pela porta da Rua da Forja, com input. As lojas ainda não têm porta na Rua
            // da Forja desta branch (saídas da cidade são de outra frente): entra com o fade normal da troca de mapa.
            var door = World.World.Catalog[World.World.CityMapId].Exits.FirstOrDefault(e => e.To == id);
            if (door is not null)
            {
                var from = map.Exits[0].At;
                world.GoMap(World.World.CityMapId, from.X, from.Y, instant: true);
                await WaitMapSettled(world);
                var loads = world.MapLoads;
                using (new SpeedUp(TesterSpeedUp))
                {
                    if (await QaWalkInto(world, door.Area, () => world.MapLoads > loads) is { } error)
                    {
                        failures.Add($"{id}: porta na Rua da Forja: {error}");
                        continue;
                    }
                }
            }
            else
            {
                world.GoMap(id, map.Spawn.X, map.Spawn.Y);
            }

            await WaitMapSettled(world);
            if (world.MapId != id)
            {
                failures.Add($"{id}: a porta levou a {world.MapId}");
                continue;
            }

            var p = player.GlobalPosition;
            if (!world.WalkArea.CanStand(p.X, p.Y))
            {
                failures.Add($"{id}: chegada em ({p.X}, {p.Y}) onde não dá para ficar de pé");
            }

            world.Camera.Snap();
            await Shot($"qa-{tag}-{id}-01-chegada");

            // 2. Dono/balcão: anda até o foco do interagível, aperta interagir.
            foreach (var def in map.Interactables)
            {
                var it = world.GetInteractable(def.Id)!;
                using (new SpeedUp(TesterSpeedUp))
                {
                    if (await QaWalkNear(world, new MapPoint(def.X, def.Y), def.Radius - 2f) is { } error)
                    {
                        failures.Add($"{id} {def.Label}: {error}");
                        continue;
                    }
                }

                await NextPhysicsFrame();
                if (player.FocusedInteractable != it)
                {
                    failures.Add($"{id} {def.Label}: foco em '{player.FocusedInteractable?.Id}'");
                    continue;
                }

                var got = await PressInteract();
                if (got != (it.Kind, it.Id))
                {
                    failures.Add($"{id} {def.Label}: interagir emitiu {got}");
                }

                world.Camera.Snap();
                await Shot($"qa-{tag}-{id}-02-{QaSlug(def.Id)}");
            }

            // 3. Atrás de cada móvel alto (oclusor do layout): o pé fica acima de baseY, dentro do recorte.
            if (behindShots)
            {
                var n = 0;
                foreach (var o in QaArray(QaLayout(map), "occluders"))
                {
                    var note = o.TryGetProperty("note", out var nv) ? nv.GetString()! : "oclusor";
                    var spot = QaBehindSpot(world, player, o);
                    if (spot is null)
                    {
                        behind.Add($"{id} '{note}': sem lugar de pé alcançável atrás");
                        continue;
                    }

                    using (new SpeedUp(TesterSpeedUp))
                    {
                        if (await QaWalkNear(world, spot.Value, 2f) is { } error)
                        {
                            failures.Add($"{id} atrás de '{note}': {error}");
                            continue;
                        }
                    }

                    PressDirection(Vector2.Up); // vira de costas para a câmera, como quem olha o móvel
                    await NextPhysicsFrame();
                    PressDirection(Vector2.Zero);
                    world.Camera.Snap();
                    await Shot($"qa-{tag}-{id}-03-{++n:00}-atras-{QaSlug(note)}");
                    behind.Add($"{id} '{note}': pé em ({player.GlobalPosition.X:0}, {player.GlobalPosition.Y:0})");
                }
            }

            // 4. Sai pela porta, com input.
            var exit = map.Exits[0];
            var before = world.MapLoads;
            using (new SpeedUp(TesterSpeedUp))
            {
                if (await QaWalkInto(world, exit.Area, () => world.MapLoads > before) is { } error)
                {
                    failures.Add($"{id} saída: {error}");
                    continue;
                }
            }

            await WaitMapSettled(world);
            p = player.GlobalPosition;
            if (world.MapId != exit.To || !world.WalkArea.CanStand(p.X, p.Y))
            {
                failures.Add($"{id}: saída levou a {world.MapId} ({p.X}, {p.Y})");
            }

            Log.Info($"QA interiores: {id} OK ({tag})");
        }

        foreach (var line in behind)
        {
            Log.Info($"QA interiores atrás: {line}");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: interiores (QA): bot aleatório e bot de alvos por 6 min de jogo sem enroscar", order: 41)]
    private async Task QaInteriorsRandomBot()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var rng = new Random(4242); // semente fixa: o mesmo passeio a cada rodada
        var stuck = new List<string>();
        var unreachable = new HashSet<string>();
        var violations = new List<string>();
        var report = new List<string>();
        var interactions = 0;
        void OnRequest(string kind, string id) => interactions++;
        EventBus.Instance.InteractionRequested += OnRequest;
        try
        {
            using var speed = new SpeedUp(TesterSpeedUp);
            var fps = (int)Math.Round(Engine.PhysicsTicksPerSecond / Engine.TimeScale);
            var maxStep = (Player.Player.BaseSpeed / fps) + 0.6f;
            foreach (var id in InteriorMapIds)
            {
                var map = World.World.Catalog[id];
                world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                await WaitMapSettled(world);
                var targets = InteriorTargets(world, map);
                var fixedIndex = 0;
                int paths = 0, reached = 0, holds = 0, exits = 0;
                var start = Engine.GetPhysicsFrames();
                var budget = (ulong)(60 * fps); // 60 s de jogo por interior = 6 min no total
                while (Engine.GetPhysicsFrames() - start < budget)
                {
                    if (world.MapId != id)
                    {
                        exits++; // o passeio aleatório saiu pela porta: volta, como um jogador que entra de novo
                        await WaitMapSettled(world);
                        world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                        await WaitMapSettled(world);
                    }

                    if (fixedIndex >= targets.Count && rng.NextDouble() < 0.3)
                    {
                        // Bot aleatório: segura uma direção qualquer de 0,3 a 1,5 s, apertando ações no meio.
                        holds++;
                        var angle = (float)(rng.NextDouble() * Math.Tau);
                        var frames = rng.Next(fps * 3 / 10, fps * 3 / 2);
                        var previous = player.GlobalPosition;
                        PressDirection(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                        for (var f = 0; f < frames && world.MapId == id && !world.IsTransitioning; f++)
                        {
                            QaMaybeTapAction(rng);
                            await NextPhysicsFrame();
                            var q = player.GlobalPosition;
                            if (world.IsTransitioning || world.MapId != id)
                            {
                                break;
                            }

                            if (!world.WalkArea.IsWalkable(q.X, q.Y) || !world.WalkArea.CanStand(q.X, q.Y, 3.5f))
                            {
                                violations.Add($"{id}: pé em área bloqueada em ({q.X:0.0}, {q.Y:0.0})");
                            }
                            else if (q.DistanceTo(previous) > maxStep)
                            {
                                violations.Add($"{id}: salto ({previous.X:0.0}, {previous.Y:0.0}) → ({q.X:0.0}, {q.Y:0.0})");
                            }

                            previous = q;
                        }

                        PressDirection(Vector2.Zero);
                        continue;
                    }

                    // Bot de alvos: primeiro cada NPC/decoração/baú/prop do mapa, depois pontos de pé sorteados.
                    MapPoint target;
                    float reach;
                    string label;
                    if (fixedIndex < targets.Count)
                    {
                        var t = targets[fixedIndex++];
                        (target, reach, label) = (t.At, t.Reach, t.Label);
                    }
                    else
                    {
                        if (QaRandomStandable(world, map, rng) is not { } r)
                        {
                            continue;
                        }

                        (target, reach, label) = (r, 4f, "ponto sorteado"); // 4 u > meia diagonal da malha de 4 u do caminho
                    }

                    paths++;
                    if (await QaWalkNear(world, target, reach, () => QaMaybeTapAction(rng)) is { } error)
                    {
                        if (error.StartsWith("inalcançável", StringComparison.Ordinal))
                        {
                            unreachable.Add($"{id} {label}: {error}");
                        }
                        else if (world.MapId == id)
                        {
                            stuck.Add($"{id} {label} ({target.X:0}, {target.Y:0}): {error}");
                        }
                    }
                    else
                    {
                        reached++;
                    }
                }

                report.Add($"{id}: {paths} trajetos ({reached} chegaram), {holds} direções aleatórias, {exits} saídas pela porta");
            }
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
            PressDirection(Vector2.Zero);
        }

        foreach (var line in report)
        {
            Log.Info($"QA bot: {line}");
        }

        Log.Info($"QA bot: {interactions} pedidos de interação no caminho, {stuck.Count} enroscos, {unreachable.Count} inalcançáveis, {violations.Count} violações");
        var problems = stuck.Concat(unreachable.Select(u => $"inalcançável: {u}")).Concat(violations.Distinct()).ToList();
        Require(problems.Count == 0, $"{problems.Count} problema(s):\n  " + string.Join("\n  ", problems.Take(40)));
    }

    [SmokeScenario("estresse: interiores (QA): 50 trocas de mapa seguidas sem vazar nós nem memória", order: 42)]
    private async Task QaInteriorsMapSwaps()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var failures = new List<string>();
        var firstVisit = new Dictionary<string, (double Nodes, double Memory)>();
        var lastVisit = new Dictionary<string, (double Nodes, double Memory)>();
        var visits = new Dictionary<string, int>();
        var samples = new Dictionary<string, List<(double Nodes, double Memory)>>();
        var orphansStart = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        var worstMs = 0.0;
        var next = 0;
        for (var i = 0; i < 50; i++)
        {
            // Rua da Forja ↔ cada interior, em ciclo, com o fade normal da troca de mapa (como pela porta).
            string to;
            Vector2 at;
            if (world.MapId == World.World.CityMapId)
            {
                to = InteriorMapIds[next++ % InteriorMapIds.Length];
                var door = World.World.Catalog[World.World.CityMapId].Exits.FirstOrDefault(e => e.To == to);
                var spot = door?.At ?? World.World.Catalog[to].Spawn;
                at = new Vector2(spot.X, spot.Y);
            }
            else
            {
                var exit = world.Map.Exits[0];
                (to, at) = (exit.To, new Vector2(exit.At.X, exit.At.Y));
            }

            var t0 = Time.GetTicksMsec();
            world.GoMap(to, at);
            await WaitMapSettled(world);
            var ms = Time.GetTicksMsec() - t0;
            worstMs = Math.Max(worstMs, ms);
            await Frames(3);
            var p = player.GlobalPosition;
            if (world.MapId != to || !world.WalkArea.CanStand(p.X, p.Y))
            {
                failures.Add($"troca {i + 1}: em {world.MapId} ({p.X}, {p.Y}), esperava {to} em pé");
            }

            if (GameManager.Instance.IsGameplayBlocked)
            {
                failures.Add($"troca {i + 1}: jogo continua bloqueado depois do fade em {to}");
            }

            var sample = (Performance.GetMonitor(Performance.Monitor.ObjectNodeCount), Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024));
            // a 1ª visita carrega texturas e caches preguiçosos (a arte aprovada trouxe vários): é aquecimento. O vazamento se mede da
            // 2ª visita em diante, quando só sobra o que cresce a cada troca
            visits[to] = visits.GetValueOrDefault(to) + 1;
            if (visits[to] >= 2)
            {
                if (!samples.TryGetValue(to, out var list))
                {
                    samples[to] = list = [];
                }

                list.Add(sample);
                firstVisit.TryAdd(to, sample);
                lastVisit[to] = sample;
            }
            if ((i + 1) % 10 == 0)
            {
                Log.Info($"QA trocas: {i + 1} trocas, nós={sample.Item1} órfãos={Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)} memória={sample.Item2:0.0}MB, pior troca {worstMs} ms");
            }
        }

        foreach (var (map, first) in firstVisit)
        {
            var last = lastVisit[map];
            Log.Info($"QA trocas: {map} nós {first.Nodes} → {last.Nodes}, memória {first.Memory:0.0} → {last.Memory:0.0} MB");
            if (last.Nodes > first.Nodes + 4)
            {
                failures.Add($"{map}: nós cresceram de {first.Nodes} para {last.Nodes}");
            }

            // a memória estática serra com o coletor de lixo: com amostras o bastante, a tendência é a média das 3 últimas contra a das
            // 3 primeiras depois do aquecimento, não dois pontos soltos (que medem o dente da serra)
            var list = samples[map];
            var (head, tail) = list.Count >= 6
                ? (list.Take(3).Average(x => x.Memory), list.Skip(list.Count - 3).Average(x => x.Memory))
                : (first.Memory, last.Memory);
            if (tail > head + 8)
            {
                failures.Add($"{map}: memória cresceu de {head:0.0} para {tail:0.0} MB");
            }
        }

        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        if (orphans > orphansStart)
        {
            failures.Add($"nós órfãos cresceram de {orphansStart} para {orphans}");
        }

        if (worstMs > 3000)
        {
            failures.Add($"troca de mapa mais lenta levou {worstMs} ms");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: interiores (QA): spam de interagir, esquivar, atacar e pausar perto de cada dono", order: 43)]
    private async Task QaInteriorsActionSpam()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var rng = new Random(77);
        var failures = new List<string>();
        var interactions = 0;
        void OnRequest(string kind, string id) => interactions++;
        EventBus.Instance.InteractionRequested += OnRequest;
        try
        {
            foreach (var id in InteriorMapIds)
            {
                var map = World.World.Catalog[id];
                var def = map.Interactables[0];
                world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                await WaitMapSettled(world);
                using (new SpeedUp(TesterSpeedUp))
                {
                    if (await QaWalkNear(world, new MapPoint(def.X, def.Y), def.Radius - 2f) is { } error)
                    {
                        failures.Add($"{id}: {error}");
                        continue;
                    }
                }

                // 5 s a 1×: a cada frame uma ação apertada e solta no seguinte; direção muda a cada 10 frames.
                var before = interactions;
                StringName? held = null;
                for (var f = 0; f < 300 && world.MapId == id; f++)
                {
                    if (held is not null)
                    {
                        GetViewport().PushInput(new InputEventAction { Action = held, Pressed = false });
                    }

                    held = QaSpamActions[rng.Next(QaSpamActions.Length)];
                    GetViewport().PushInput(new InputEventAction { Action = held, Pressed = true });
                    if (f % 10 == 0)
                    {
                        var angle = (float)(rng.NextDouble() * Math.Tau);
                        PressDirection(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.6f);
                    }

                    await NextPhysicsFrame();
                    var q = player.GlobalPosition;
                    if (!world.IsTransitioning && world.MapId == id && (!world.WalkArea.IsWalkable(q.X, q.Y) || !world.WalkArea.CanStand(q.X, q.Y, 3.5f)))
                    {
                        failures.Add($"{id}: pé em área bloqueada durante o spam em ({q.X:0.0}, {q.Y:0.0})");
                        break;
                    }
                }

                if (held is not null)
                {
                    GetViewport().PushInput(new InputEventAction { Action = held, Pressed = false });
                }

                PressDirection(Vector2.Zero);
                await Frames(30);
                // O spam deixa telas abertas (a pausa, o diário e/ou uma fala do dono, empilhadas): um jogador aperta Esc
                // (ui_cancel fecha a tela do topo; a ação "pause" só abre a pausa) até sair delas, uma por vez.
                for (var esc = 0; esc < 6 && GameManager.Instance.IsGameplayBlocked; esc++)
                {
                    GetViewport().PushInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
                    await Frames(2);
                    GetViewport().PushInput(new InputEventAction { Action = "ui_cancel", Pressed = false });
                    await Frames(30);
                }

                if (GameManager.Instance.IsGameplayBlocked)
                {
                    failures.Add($"{id}: o jogo ficou bloqueado depois do spam ({string.Join(",", GameManager.Instance.GameplayBlocks)}, tela {UI.GameUi.Current?.Stack.Top?.GetType().Name})");
                    continue;
                }

                if (world.MapId != id)
                {
                    world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                    await WaitMapSettled(world);
                }

                // Depois do spam o jogador ainda anda e ainda fala com o dono.
                var p0 = player.GlobalPosition;
                var moved = 0f;
                foreach (var d in new[] { Vector2.Down, Vector2.Up, Vector2.Left, Vector2.Right })
                {
                    PressDirection(d);
                    await Frames(20);
                    moved = Math.Max(moved, player.GlobalPosition.DistanceTo(p0));
                }

                PressDirection(Vector2.Zero);
                if (moved < 4f)
                {
                    failures.Add($"{id}: depois do spam o jogador não anda (moveu {moved:0.0} u)");
                }

                using (new SpeedUp(TesterSpeedUp))
                {
                    await QaWalkNear(world, new MapPoint(def.X, def.Y), def.Radius - 2f);
                }

                await NextPhysicsFrame();
                if (await PressInteract() is not { } got || got.Id != def.Id)
                {
                    failures.Add($"{id}: depois do spam interagir não fala com {def.Id}");
                }

                Log.Info($"QA spam: {id} OK ({interactions - before} pedidos de interação durante o spam)");
            }
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
            PressDirection(Vector2.Zero);
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: interiores (QA): tempo de frame e alocação andando em cada interior (1×)", order: 44)]
    private async Task QaInteriorsFrameTime()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var headless = DisplayServer.GetName() == "headless";
        var size = DisplayServer.WindowGetSize();
        var failures = new List<string>();
        var samples = new double[4096];
        foreach (var id in InteriorMapIds)
        {
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            world.Camera.Snap();
            await WaitSeconds(0.5); // aquecimento

            var count = 0;
            var measuring = true;
            async Task Measure()
            {
                var last = Time.GetTicksUsec();
                while (measuring)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var now = Time.GetTicksUsec();
                    if (count < samples.Length)
                    {
                        samples[count++] = (now - last) / 1000.0;
                    }

                    last = now;
                }
            }

            var measure = Measure();
            var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            var physicsBefore = Engine.GetPhysicsFrames();
            var t0 = Time.GetTicksMsec();
            var npc = new MapPoint(map.Interactables[0].X, map.Interactables[0].Y);
            var home = map.Spawn;
            var toNpc = true;
            while (Time.GetTicksMsec() - t0 < 6000)
            {
                var error = await QaWalkNear(world, toNpc ? npc : home, toNpc ? map.Interactables[0].Radius - 2f : 6f);
                if (error is not null)
                {
                    failures.Add($"{id}: andando para medir: {error}");
                    break;
                }

                toNpc = !toNpc;
            }

            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
            var physicsFrames = Math.Max(1UL, Engine.GetPhysicsFrames() - physicsBefore);
            measuring = false;
            await measure;

            var sorted = samples.Take(count).Skip(2).OrderBy(x => x).ToArray(); // descarta o 1º frame (troca)
            var mean = sorted.Average();
            var p95 = sorted[(int)(sorted.Length * 0.95)];
            var max = sorted[^1];
            var hitches = sorted.Count(x => x > 33.4);
            var over20 = sorted.Count(x => x > 20);
            var perFrame = allocated / (long)physicsFrames;
            Log.Info($"QA frame: {id} {size.X}x{size.Y}: {sorted.Length} frames, média {mean:0.00} ms, p95 {p95:0.00} ms, máx {max:0.00} ms, " +
                     $"{over20} frames > 20 ms, {hitches} > 33 ms, fps {Engine.GetFramesPerSecond():0}, {perFrame} B/frame de física (inclui o bot), nós {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
            if (!headless && p95 > 20)
            {
                failures.Add($"{id}: p95 do frame {p95:0.00} ms (> 20 ms)");
            }
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));
    }

    /// <summary>Anda com input até ficar a <paramref name="reach"/> u do ponto (caminho do Core). Null se chegou.</summary>
    private async Task<string?> QaWalkNear(World.World world, MapPoint target, float reach, Action? perFrame = null)
    {
        var player = FindPlayer()!;
        var start = QaPathStart(world, player);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, target, reach);
        if (path is null)
        {
            return $"inalcançável a {reach:0} u de ({target.X:0}, {target.Y:0}) saindo de ({start.X:0}, {start.Y:0})";
        }

        var goal = new Vector2(target.X, target.Y);
        return await QaWalkPath(world, path, () => player.GlobalPosition.DistanceTo(goal) <= reach + 1f, perFrame);
    }

    /// <summary>Anda com input até o pé entrar no retângulo e <paramref name="done"/> valer (ex.: troca de mapa).</summary>
    private async Task<string?> QaWalkInto(World.World world, MapRect area, Func<bool> done)
    {
        var start = QaPathStart(world, FindPlayer()!);
        var path = WalkPathfinder.FindPathInto(world.WalkArea, start, area);
        return path is null ? $"sem caminho até {area} saindo de ({start.X:0}, {start.Y:0})" : await QaWalkPath(world, path, done, null);
    }

    /// <summary>O caminho do Core parte de um ponto onde dá para ficar de pé; colado na parede, usa o mais próximo.</summary>
    private static MapPoint QaPathStart(World.World world, Player.Player player)
    {
        var p = player.GlobalPosition;
        return world.WalkArea.CanStand(p.X, p.Y) ? new MapPoint(p.X, p.Y) : world.WalkArea.NearestWalkable(p.X, p.Y, maxDistance: 24f) ?? new MapPoint(p.X, p.Y);
    }

    /// <summary>
    /// Segue o caminho com input real. Falha se o pé entrar em área bloqueada, se enroscar (input ativo e menos de 1 u de
    /// progresso em 1 s de jogo) ou se passar do tempo. Funciona a 1× e acelerado (conta frames por segundo de jogo).
    /// </summary>
    private async Task<string?> QaWalkPath(World.World world, IReadOnlyList<MapPoint> path, Func<bool> done, Action? perFrame)
    {
        var player = FindPlayer()!;
        var mapId = world.MapId;
        var fps = Math.Max(1, (int)Math.Round(Engine.PhysicsTicksPerSecond / Engine.TimeScale));
        var maxFrames = (int)(((path.Count * 4f / Player.Player.BaseSpeed * 1.6f) + 3f) * fps);
        var history = new Vector2[fps];
        var last = path[^1];
        var beforeLast = path.Count > 1 ? path[^2] : new MapPoint(last.X, last.Y - 4);
        var finalDirection = new Vector2(last.X - beforeLast.X, last.Y - beforeLast.Y).Normalized();
        var next = 0;
        var moving = 0; // frames seguidos andando no mesmo mapa (o anel de 1 s só vale depois de cheio)
        try
        {
            for (var frame = 0; !done(); frame++)
            {
                var p = player.GlobalPosition;
                if (frame > maxFrames)
                {
                    return $"tempo esgotado; parou em ({p.X:0.0}, {p.Y:0.0}), próximo ponto {(next < path.Count ? path[next] : last)}";
                }

                if (GameManager.Instance.GameplayBlocks.Contains("ui"))
                {
                    // o Interact aleatório do passeio abriu uma fala/olhar: a tela trava o jogador de propósito; quem joga fecha e segue
                    UI.GameUi.Current?.CloseAll();
                }

                if (world.MapId == mapId && !world.IsTransitioning && !GameManager.Instance.IsGameplayBlocked)
                {
                    if (!world.WalkArea.IsWalkable(p.X, p.Y))
                    {
                        return $"pé em área bloqueada em ({p.X:0.0}, {p.Y:0.0})";
                    }

                    var slot = moving % fps;
                    if (moving >= fps && p.DistanceTo(history[slot]) < 1f)
                    {
                        return $"enroscado em ({p.X:0.0}, {p.Y:0.0}): menos de 1 u em 1 s com input ativo (próximo ponto {(next < path.Count ? path[next] : last)})";
                    }

                    history[slot] = p;
                    moving++;
                }
                else
                {
                    moving = 0;
                }

                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                PressDirection(next < path.Count ? (new Vector2(path[next].X, path[next].Y) - p).Normalized() : finalDirection);
                perFrame?.Invoke();
                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }

        return null;
    }

    /// <summary>De vez em quando (1 em 90 frames) aperta atacar/esquivar/lanterna no meio do passeio.</summary>
    private void QaMaybeTapAction(Random rng)
    {
        if (rng.Next(90) != 0)
        {
            return;
        }

        var action = QaSpamActions[rng.Next(QaSpamActions.Length - 1)]; // sem pausar: o passeio não para
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = true });
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = false });
    }

    /// <summary>Ponto sorteado onde o jogador cabe, longe das saídas (para o passeio não sair do mapa sem querer).</summary>
    private static MapPoint? QaRandomStandable(World.World world, MapDef map, Random rng)
    {
        for (var tries = 0; tries < 400; tries++)
        {
            var x = (float)(rng.NextDouble() * world.WalkArea.Width);
            var y = (float)(rng.NextDouble() * world.WalkArea.Height);
            if (!world.WalkArea.CanStand(x, y) || map.Exits.Any(e => new MapRect(e.Area.X - 16, e.Area.Y - 16, e.Area.Width + 32, e.Area.Height + 32).Contains(x, y)))
            {
                continue;
            }

            return new MapPoint(x, y);
        }

        return null;
    }

    /// <summary>Ponto de pé atrás do oclusor (acima de baseY, dentro do recorte) alcançável de onde o jogador está.</summary>
    private static MapPoint? QaBehindSpot(World.World world, Player.Player player, JsonElement occluder)
    {
        var points = occluder.GetProperty("poly").EnumerateArray().Select(p => (X: p[0].GetSingle(), Y: p[1].GetSingle())).ToList();
        var baseY = occluder.GetProperty("baseY").GetSingle();
        var (minX, maxX, minY) = (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y));
        var cx = (minX + maxX) / 2;
        var start = QaPathStart(world, player);
        var attempts = 0;
        for (var y = baseY - 6; y >= minY + 8; y -= 2)
        {
            for (var dx = 0f; dx <= (maxX - minX) / 2 - 2; dx += 2)
            {
                foreach (var x in new[] { cx - dx, cx + dx })
                {
                    if (!world.WalkArea.CanStand(x, y) || attempts++ > 40)
                    {
                        continue;
                    }

                    if (WalkPathfinder.FindPathNear(world.WalkArea, start, new MapPoint(x, y), 2f) is not null)
                    {
                        return new MapPoint(x, y);
                    }
                }
            }
        }

        return null;
    }

    private static JsonElement QaLayout(MapDef map)
    {
        var dir = map.Image[..map.Image.LastIndexOf('/')];
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString($"{dir}/layout_{map.Id}.json"));
        return doc.RootElement.Clone();
    }

    private static IEnumerable<JsonElement> QaArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var a) ? a.EnumerateArray().ToList() : [];

    private static string QaSlug(string text)
    {
        var chars = text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
