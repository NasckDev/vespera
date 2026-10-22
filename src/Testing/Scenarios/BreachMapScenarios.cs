using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Cenários da frente B4c (mapas das 5 Brechas e da Câmara do Núcleo). Para cada arena, um bot anda com input real a
/// partir do spawn por cada baú, decoração e prop do <c>layout_&lt;id&gt;.json</c> (e, no cenário de estresse, por uma
/// varredura do mapa inteiro: pontos abertos a cada <see cref="BreachSweepStep"/> u), sempre do ponto onde está até o
/// mais próximo ainda não visitado,
/// conferindo a cada frame que o pé nunca entra em área bloqueada nem trava. Em vários pontos empurra o jogador nas
/// 8 direções. No fim chega na porta, no chefe/saída e na fuga. Com <c>--smoke-shots</c>, tira prints pelo caminho.
/// </summary>
public partial class SmokeTest
{
    private const float BreachSweepStep = 112f;

    private static readonly string[] BreachMapIds = ["fish2", "crypt2", "mine", "garden", "cath", "core"];

    [SmokeScenario("brechas: bot visita baús, decoração e props e chega em porta, chefe e fuga", order: 31)]
    private async Task BreachMapsWalkObjects() => await BreachMapsWalk(sweep: false);

    [SmokeScenario(StressPrefix + " brechas: bot varre cada arena inteira com input real", order: 32)]
    private async Task BreachMapsWalkSweep() => await BreachMapsWalk(sweep: true);

    private async Task BreachMapsWalk(bool sweep)
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=brechas
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var failures = new List<string>();
        foreach (var id in BreachMapIds)
        {
            var map = World.World.Catalog[id];
            Require(map.Mask == $"res://assets/maps/{id}/walk_{id}.png" && map.MaskCell == 4,
                $"{id}: maps.json deveria apontar a máscara walk_{id}.png com maskCell 4");
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            Require(Find<MaskCollision>() is { CellSize: 4 }, $"{id}: colisão deveria vir da máscara (MaskCollision, célula 4)");
            world.Camera.Snap();
            await Shot($"brecha-{id}-00-spawn");

            var pending = BreachTargets(world, map, sweep);
            var visited = 0;
            var walked = 0f;
            var shot = 1;
            while (pending.Count > 0)
            {
                var target = TakeNearest(pending, player.GlobalPosition);
                using (new SpeedUp(TesterSpeedUp))
                {
                    var (error, length) = await BreachWalkNear(world, target.At, target.Reach);
                    walked += length;
                    if (error is not null)
                    {
                        failures.Add($"{id} {target.Label} ({target.At.X:0}, {target.At.Y:0}): {error}");
                        // Volta ao spawn para o resto da varredura não herdar o problema.
                        player.Teleport(new Vector2(map.Spawn.X, map.Spawn.Y));
                        await NextPhysicsFrame();
                        continue;
                    }

                    visited++;
                    if (target.Push && await BreachPushAround(world) is { } pushError)
                    {
                        failures.Add($"{id} perto de {target.Label} ({target.At.X:0}, {target.At.Y:0}): {pushError}");
                    }
                }

                if (target.Shot)
                {
                    world.Camera.Snap();
                    await Shot($"brecha-{id}-{shot++:00}-{target.Label}");
                }
            }

            // Pontos de jogo: porta (fechada: só chegar dentro dela), chefe e portal de saída, e por último a fuga.
            var goals = new List<(string Label, Func<IReadOnlyList<MapPoint>?> Path, Func<bool> Done)>();
            if (map.Door is { } door)
            {
                goals.Add(("porta", () => WalkPathfinder.FindPathInto(world.WalkArea, PlayerPoint(), door),
                    () => door.Contains(player.GlobalPosition.X, player.GlobalPosition.Y)));
            }

            foreach (var (label, point) in new[] { ("chefe", map.BossHere ?? map.BossDefault), ("saída", map.ExitAt ?? map.ExitDefault) })
            {
                if (point is { } p)
                {
                    var goal = new Vector2(p.X, p.Y);
                    goals.Add((label, () => WalkPathfinder.FindPathNear(world.WalkArea, PlayerPoint(), p, 20f),
                        () => player.GlobalPosition.DistanceTo(goal) <= 21f));
                }
            }

            var escapes = 0;
            void OnEscape(string _) => escapes++;
            if (map.Back is { } back)
            {
                goals.Add(("fuga", () => WalkPathfinder.FindPathInto(world.WalkArea, PlayerPoint(), back), () => escapes > 0));
            }

            EventBus.Instance.DungeonEscapeRequested += OnEscape;
            try
            {
                foreach (var (label, pathOf, done) in goals)
                {
                    using (new SpeedUp(TesterSpeedUp))
                    {
                        var path = pathOf();
                        if (path is null)
                        {
                            failures.Add($"{id} {label}: sem caminho de {player.GlobalPosition}");
                            continue;
                        }

                        walked += path.Count * 4f;
                        var error = await TesterWalkPath(world, path, done, timeoutGameSeconds: (path.Count * 4f / 78f * 1.6f) + 3f);
                        if (error is not null)
                        {
                            failures.Add($"{id} {label}: {error}");
                            continue;
                        }
                    }

                    if (world.MapId != id)
                    {
                        failures.Add($"{id} {label}: saiu do mapa para {world.MapId}");
                        break;
                    }

                    world.Camera.Snap();
                    await Shot($"brecha-{id}-{shot++:00}-{label}");
                }
            }
            finally
            {
                EventBus.Instance.DungeonEscapeRequested -= OnEscape;
            }

            Log.Info($"brechas: {id} — {visited} pontos visitados, {walked:0} u andados, {goals.Count} pontos de jogo");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));

        MapPoint PlayerPoint() => new(player.GlobalPosition.X, player.GlobalPosition.Y);
    }

    /// <summary>Varredura (pontos abertos em grade) + baús, decoração e props do layout.</summary>
    private static List<BreachTarget> BreachTargets(World.World world, MapDef map, bool sweep)
    {
        var targets = new List<BreachTarget>();
        var area = world.WalkArea;
        var n = 0;
        for (var y = BreachSweepStep / 2; sweep && y < map.ImageHeight; y += BreachSweepStep)
        {
            for (var x = BreachSweepStep / 2; x < map.ImageWidth; x += BreachSweepStep)
            {
                // Procura, perto do nó da grade, um ponto com folga (centro de área aberta, não colado na parede).
                if (OpenSpotNear(area, x, y) is { } p)
                {
                    targets.Add(new BreachTarget($"varredura{n}", p, 6f, Push: n % 3 == 0, Shot: n % 6 == 0));
                    n++;
                }
            }
        }

        var dir = map.Image[..map.Image.LastIndexOf('/')];
        var path = $"{dir}/layout_{map.Id}.json";
        Require(Godot.FileAccess.FileExists(path), $"{map.Id}: {path} não existe");
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        var root = doc.RootElement;
        var chests = root.TryGetProperty("chests", out var cv) ? cv.GetArrayLength() : 0;
        var props = root.TryGetProperty("props", out var pv) ? pv.GetArrayLength() : 0;
        if (map.Id != World.World.CoreMapId)
        {
            Require(chests is >= 2 and <= 4, $"{map.Id}: {chests} baús (esperado 2–4)");
            Require(props is >= 12 and <= 25, $"{map.Id}: {props} props (esperado 12–25)");
        }

        if (chests > 0)
        {
            foreach (var c in cv.EnumerateArray())
            {
                targets.Add(new BreachTarget("bau", Point(c), 18f, Push: true, Shot: true)); // v2: "Abrir baú" r 20
            }
        }

        if (root.TryGetProperty("decor", out var decor))
        {
            foreach (var d in decor.EnumerateArray())
            {
                var r = d.TryGetProperty("r", out var rv) ? rv.GetSingle() : 24f;
                targets.Add(new BreachTarget(d.GetProperty("kind").GetString()!, Point(d), r - 2f, Push: true, Shot: true));
            }
        }

        if (props > 0)
        {
            foreach (var p in pv.EnumerateArray())
            {
                targets.Add(new BreachTarget($"prop-{p.GetProperty("kind").GetString()}", Point(p), 22f, Push: false, Shot: false)); // alcance de golpe
            }
        }

        return targets;

        static MapPoint Point(JsonElement e) => new(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle());
    }

    private static MapPoint? OpenSpotNear(IWalkArea area, float x, float y)
    {
        for (var r = 0f; r <= BreachSweepStep / 3; r += 8f)
        {
            for (var a = 0; a < (r == 0 ? 1 : 8); a++)
            {
                var px = x + (r * Mathf.Cos(a * Mathf.Pi / 4));
                var py = y + (r * Mathf.Sin(a * Mathf.Pi / 4));
                if (area.CanStand(px, py, 10f))
                {
                    return new MapPoint(px, py);
                }
            }
        }

        return null;
    }

    private static BreachTarget TakeNearest(List<BreachTarget> pending, Vector2 from)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < pending.Count; i++)
        {
            var d = from.DistanceSquaredTo(new Vector2(pending[i].At.X, pending[i].At.Y));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }

        var target = pending[best];
        pending.RemoveAt(best);
        return target;
    }

    /// <summary>Anda com input do ponto atual até ficar a <paramref name="reach"/> u do alvo. Erro null se chegou.</summary>
    private async Task<(string? Error, float Length)> BreachWalkNear(World.World world, MapPoint target, float reach)
    {
        var player = FindPlayer()!;
        var goal = new Vector2(target.X, target.Y);
        if (player.GlobalPosition.DistanceTo(goal) <= reach)
        {
            return (null, 0f);
        }

        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, target, reach);
        if (path is null)
        {
            return ($"inalcançável a {reach:0} u saindo de ({start.X:0}, {start.Y:0})", 0f);
        }

        var error = await TesterWalkPath(world, path, () => player.GlobalPosition.DistanceTo(goal) <= reach + 1f,
            timeoutGameSeconds: (path.Count * 4f / 78f * 1.6f) + 3f);
        return (error, path.Count * 4f);
    }

    /// <summary>Empurra o jogador nas 8 direções a partir do ponto atual: o pé nunca entra em área bloqueada nem salta.</summary>
    private async Task<string?> BreachPushAround(World.World world)
    {
        var player = FindPlayer()!;
        var origin = player.GlobalPosition;
        var mapId = world.MapId;
        var maxStep = (78f / Engine.PhysicsTicksPerSecond * TesterSpeedUp) + 0.6f;
        string? error = null;
        for (var i = 0; i < 8 && error is null; i++)
        {
            var angle = i * Mathf.Pi / 4f;
            player.Teleport(origin);
            await NextPhysicsFrame();
            PressDirection(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            var previous = player.GlobalPosition;
            for (var frame = 0; frame < 40; frame++)
            {
                await NextPhysicsFrame();
                if (world.IsTransitioning || world.MapId != mapId)
                {
                    error = "empurrão saiu do mapa";
                    break;
                }

                var p = player.GlobalPosition;
                if (!world.WalkArea.IsWalkable(p.X, p.Y) || !world.WalkArea.CanStand(p.X, p.Y, 3.5f))
                {
                    error = $"pé em área bloqueada em {p} (direção {i * 45}°)";
                    break;
                }

                // A fuga empurra 14 u para cima ao andar dentro de "back" (world.js:115): é mecânica, não atravessar parede.
                var pushedBack = world.Map.Back is { } back && back.Contains(previous.X, previous.Y) && p.Y < previous.Y;
                if (!pushedBack && p.DistanceTo(previous) > maxStep)
                {
                    error = $"salto {previous} → {p} (direção {i * 45}°)";
                    break;
                }

                previous = p;
            }

            PressDirection(Vector2.Zero);
        }

        player.Teleport(origin);
        await NextPhysicsFrame();
        return error;
    }

    private sealed record BreachTarget(string Label, MapPoint At, float Reach, bool Push, bool Shot);
}
