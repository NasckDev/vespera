using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Cenários do tester da frente A2: andam com input real o caminho INTEIRO de cada chegada até cada saída/porta/fuga
/// (sem teleportar para perto do fim), empurram o jogador contra paredes/água/móveis em todas as direções e medem
/// colisores e alocação ao andar. Rodam com o tempo acelerado (mesmo passo de física, mais passos por segundo real).
/// </summary>
public partial class SmokeTest
{
    private const int TesterSpeedUp = 4;

    [SmokeScenario("mundo (tester): caminho completo de cada chegada até cada saída", order: 20)]
    private async Task TesterFullPathsToExits()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=tester
        var world = FindWorld();
        var catalog = World.World.Catalog;

        // (mapa, ponto de partida, alvo, destino esperado ou null para porta/fuga)
        var legs = new List<(string Map, MapPoint From, string Label, MapRect Target, string? To)>();
        void AddExits(string map, MapPoint from)
        {
            foreach (var e in catalog[map].Exits)
            {
                legs.Add((map, from, $"saída→{e.To}", e.Area, e.To));
            }
        }

        AddExits("guild", catalog["guild"].Spawn); // novo jogo (250, 560)
        AddExits("guild", new MapPoint(250, 330)); // continuar
        AddExits("guild", new MapPoint(250, 840)); // da Rua da Forja
        AddExits("forge", new MapPoint(1500, 250)); // da Guilda
        AddExits("forge", new MapPoint(268, 325)); // da Estalagem
        AddExits("forge", new MapPoint(305, 790)); // das lojas
        AddExits("inn", new MapPoint(250, 760));
        AddExits("inn", new MapPoint(250, 520));
        foreach (var shop in new[] { "shop_arm", "shop_mag", "shop_alq", "shop_rel" })
        {
            AddExits(shop, catalog[shop].Spawn);
        }

        foreach (var id in new[] { "fish2", "crypt2", "mine", "garden", "cath", "core" })
        {
            var map = catalog[id];
            if (map.Door is { } door)
            {
                legs.Add((id, map.Spawn, "porta", door, null));
            }

            if (map.Back is { } back)
            {
                legs.Add((id, map.Spawn, "fuga", back, null));
            }
        }

        var failures = new List<string>();
        using var speed = new SpeedUp(TesterSpeedUp);
        foreach (var leg in legs)
        {
            world.GoMap(leg.Map, leg.From.X, leg.From.Y, instant: true);
            await WaitMapSettled(world);
            if (leg.Label == "porta")
            {
                world.DoorOpen = true;
            }

            var player = FindPlayer()!;
            var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
            var path = WalkPathfinder.FindPathInto(world.WalkArea, start, leg.Target);
            if (path is null)
            {
                failures.Add($"{leg.Map} ({leg.From.X},{leg.From.Y}) {leg.Label}: sem caminho no Core");
                continue;
            }

            var loads = world.MapLoads;
            var escapes = 0;
            void OnEscape(string _) => escapes++;
            EventBus.Instance.DungeonEscapeRequested += OnEscape;
            Func<bool> done = leg.Label == "fuga" ? () => escapes > 0 : () => world.MapLoads > loads;
            var length = path.Count * 4f;
            try
            {
                var error = await TesterWalkPath(world, path, done, timeoutGameSeconds: (length / 78f * 1.6f) + 3f);
                if (error is not null)
                {
                    failures.Add($"{leg.Map} ({leg.From.X},{leg.From.Y}) {leg.Label} [{length:0} u]: {error}");
                    continue;
                }
            }
            finally
            {
                EventBus.Instance.DungeonEscapeRequested -= OnEscape;
            }

            if (leg.To is { } to)
            {
                await WaitMapSettled(world);
                if (world.MapId != to)
                {
                    failures.Add($"{leg.Map} {leg.Label}: levou a {world.MapId}");
                }
            }

            Log.Info($"tester: {leg.Map} ({leg.From.X},{leg.From.Y}) {leg.Label} OK ({length:0} u andados)");
        }

        Require(failures.Count == 0, $"{failures.Count} trecho(s) falharam:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("mundo (tester): empurrar paredes, água e móveis não atravessa", order: 21)]
    private async Task TesterPushWalls()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=tester
        var world = FindWorld();
        var catalog = World.World.Catalog;
        var starts = new List<(string Map, MapPoint At)>
        {
            ("guild", new MapPoint(155, 430)), // em cima do boneco de treino [149,446,12,14]
            ("guild", new MapPoint(250, 560)),
            ("guild", new MapPoint(247, 250)), // balcão da Odete
            ("inn", new MapPoint(385, 446)), // balcão da Lúcia
            ("inn", new MapPoint(250, 520)),
            ("core", new MapPoint(250, 505)), // arena (círculo)
            ("core", new MapPoint(250, 640)),
            ("forge", new MapPoint(1500, 250)),
            ("forge", new MapPoint(1150, 265)), // forja do Brás
            ("forge", new MapPoint(305, 790)),
            ("forge", new MapPoint(800, 520)),
        };
        foreach (var id in new[] { "fish2", "crypt2", "mine", "garden", "cath", "shop_arm", "shop_mag", "shop_alq", "shop_rel" })
        {
            starts.Add((id, catalog[id].Spawn));
            foreach (var it in catalog[id].Interactables)
            {
                starts.Add((id, new MapPoint(it.X, it.Y)));
            }
        }

        Vector2[] directions =
        [
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new Vector2(1, 1).Normalized(), new Vector2(-1, 1).Normalized(), new Vector2(1, -1).Normalized(), new Vector2(-1, -1).Normalized(),
        ];

        var failures = new List<string>();
        using var speed = new SpeedUp(TesterSpeedUp);
        var player = FindPlayer()!;
        foreach (var (mapId, at) in starts)
        {
            world.GoMap(mapId, at.X, at.Y, instant: true);
            await WaitMapSettled(world);
            var origin = player.GlobalPosition;
            foreach (var d in directions)
            {
                player.Teleport(origin);
                await NextPhysicsFrame();
                PressDirection(d);
                var previous = player.GlobalPosition;
                var maxStep = (78f / Engine.PhysicsTicksPerSecond * TesterSpeedUp) + 0.6f;
                for (var frame = 0; frame < 150 && failures.Count < 20; frame++)
                {
                    await NextPhysicsFrame();
                    if (world.IsTransitioning || world.MapId != mapId)
                    {
                        break; // entrou numa saída: tudo bem, não é parede
                    }

                    var p = player.GlobalPosition;
                    if (!world.WalkArea.IsWalkable(p.X, p.Y) || !world.WalkArea.CanStand(p.X, p.Y, 3.5f))
                    {
                        failures.Add($"{mapId} de ({at.X},{at.Y}) dir {d}: pé em área bloqueada {p}");
                        break;
                    }

                    // Tentar sair da Brecha pelo "back" empurra 14 u para cima (world.js:115): é mecânica, não atravessar parede.
                    // C1-D3: a zona de fuga inclui o trecho andável abaixo do retângulo.
                    var pushedBackFromEscape = world.Map.Back is { } back && EscapeIntent.InZone(back, previous.X, previous.Y)
                        && p.Y < previous.Y && previous.Y - p.Y <= 14f + maxStep;
                    var mid = (p + previous) / 2;
                    if (!pushedBackFromEscape && (p.DistanceTo(previous) > maxStep || !world.WalkArea.IsWalkable(mid.X, mid.Y)))
                    {
                        failures.Add($"{mapId} de ({at.X},{at.Y}) dir {d}: salto {previous} → {p}");
                        break;
                    }

                    previous = p;
                }

                PressDirection(Vector2.Zero);
                if (world.MapId != mapId)
                {
                    break;
                }
            }
        }

        Require(failures.Count == 0, $"{failures.Count} falha(s):\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("mundo (tester): colisores por mapa e alocação ao andar", order: 22)]
    private async Task TesterCollidersAndAllocation()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=tester
        var world = FindWorld();
        var catalog = World.World.Catalog;
        var player = FindPlayer()!;
        var report = new List<string>();
        var worst = 0L;
        foreach (var id in new[] { "guild", "forge", "inn", "fish2", "crypt2", "mine", "garden", "cath", "core", "shop_arm", "shop_mag", "shop_alq", "shop_rel" })
        {
            var map = catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            var shapes = 0;
            foreach (var body in world.CollisionRoot.GetChildren())
            {
                shapes += body.GetChildCount();
            }

            // Anda 1 s (aquecimento), depois mede 2 s de caminhada contra a parede/deslizando.
            Input.ActionPress(InputActions.MoveUp);
            Input.ActionPress(InputActions.MoveLeft);
            await WaitSeconds(0.5);
            var before = GC.GetTotalAllocatedBytes(precise: true);
            var gcBefore = GC.CollectionCount(0);
            var t0 = Time.GetTicksUsec();
            var physicsFrames = Engine.GetPhysicsFrames();
            await WaitSeconds(2.0);
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
            var frames = Math.Max(1, Engine.GetPhysicsFrames() - physicsFrames);
            var realMs = (Time.GetTicksUsec() - t0) / 1000.0;
            Input.ActionRelease(InputActions.MoveUp);
            Input.ActionRelease(InputActions.MoveLeft);
            var perFrame = allocated / (long)frames;
            worst = Math.Max(worst, perFrame);
            var physicsMs = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
            report.Add($"{id}: {shapes} colisores, {allocated} B em {frames} frames de física ({perFrame} B/frame, GC0 +{GC.CollectionCount(0) - gcBefore}), fps {Engine.GetFramesPerSecond():0}, física {physicsMs:0.00} ms, {realMs:0} ms reais");
        }

        foreach (var line in report)
        {
            Log.Info($"tester: {line}");
        }

        Require(worst < 2048, $"andar aloca {worst} B por frame de física (pior mapa); esperado ~0:\n  " + string.Join("\n  ", report));
    }

    /// <summary>Anda o caminho inteiro (desde o primeiro ponto) com input. Devolve null se chegou ou a mensagem do problema.</summary>
    private async Task<string?> TesterWalkPath(World.World world, IReadOnlyList<MapPoint> path, Func<bool> done, float timeoutGameSeconds)
    {
        var player = FindPlayer()!;
        var mapId = world.MapId;
        var last = path[^1];
        var beforeLast = path.Count > 1 ? path[^2] : new MapPoint(last.X, last.Y - 4);
        var finalDirection = new Vector2(last.X - beforeLast.X, last.Y - beforeLast.Y).Normalized();
        var next = 1;
        var maxFrames = (int)(timeoutGameSeconds * Engine.PhysicsTicksPerSecond / TesterSpeedUp);
        var stuckFrames = 0;
        var previous = player.GlobalPosition;
        try
        {
            for (var frame = 0; !done(); frame++)
            {
                var p = player.GlobalPosition;
                if (frame > maxFrames)
                {
                    return $"timeout; parou em {p}, próximo ponto do caminho {(next < path.Count ? path[next] : last)}";
                }

                if (world.MapId == mapId && !world.IsTransitioning)
                {
                    if (!world.WalkArea.IsWalkable(p.X, p.Y))
                    {
                        return $"pé em área bloqueada em {p}";
                    }

                    stuckFrames = p.DistanceTo(previous) < 0.05f ? stuckFrames + 1 : 0;
                    if (stuckFrames > 30)
                    {
                        return $"travado em {p} (caminho do Core passa por {(next < path.Count ? path[next] : last)}, onde CanStand={world.WalkArea.CanStand(path[Math.Min(next, path.Count - 1)].X, path[Math.Min(next, path.Count - 1)].Y)})";
                    }
                }

                previous = p;
                // 4 px: a correção de quina do Player pode manter o corpo até CornerNudgeMax px ao lado da linha do caminho.
                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                var direction = next < path.Count ? (new Vector2(path[next].X, path[next].Y) - p).Normalized() : finalDirection;
                PressDirection(direction);
                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }

        return null;
    }

    /// <summary>Acelera o jogo mantendo o passo de física (mais ticks por segundo real) e restaura no Dispose.</summary>
    private sealed class SpeedUp : IDisposable
    {
        private readonly double _timeScale;
        private readonly int _ticks;
        private readonly int _maxSteps;

        public SpeedUp(int factor)
        {
            _timeScale = Engine.TimeScale;
            _ticks = Engine.PhysicsTicksPerSecond;
            _maxSteps = Engine.MaxPhysicsStepsPerFrame;
            Engine.PhysicsTicksPerSecond = _ticks * factor;
            Engine.MaxPhysicsStepsPerFrame = Math.Max(_maxSteps, 8 * factor);
            Engine.TimeScale = factor;
        }

        public void Dispose()
        {
            Engine.TimeScale = _timeScale;
            Engine.PhysicsTicksPerSecond = _ticks;
            Engine.MaxPhysicsStepsPerFrame = _maxSteps;
        }
    }
}
