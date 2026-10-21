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
/// Cenários da frente B4d (interiores: Guilda, Estalagem e as 4 lojas). Um bot anda com input real, a partir do spawn,
/// até cada NPC/balcão do <c>maps.json</c> e cada decoração, baú e prop do <c>layout_&lt;id&gt;.json</c>, conferindo a cada
/// frame que o pé nunca entra em área bloqueada; nos NPCs aperta interagir e confere o pedido no EventBus; no fim sai pela
/// porta. Em cada ponto empurra o jogador nas 8 direções contra os móveis. Com <c>--smoke-shots</c>, tira prints.
/// </summary>
public partial class SmokeTest
{
    private static readonly string[] InteriorMapIds = ["guild", "inn"];

    [SmokeScenario("interiores: bot alcança NPCs, balcões, decoração e baús", order: 30)]
    private async Task InteriorsReachEverything()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=interiores
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var failures = new List<string>();
        foreach (var id in InteriorMapIds)
        {
            var map = World.World.Catalog[id];
            Require(map.Mask is not null && map.MaskCell == 4, $"{id}: maps.json deveria apontar a máscara walk_{id}.png com maskCell 4");
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            Require(Find<MaskCollision>() is { CellSize: 4 }, $"{id}: colisão deveria vir da máscara (MaskCollision, célula 4)");
            world.Camera.Snap();
            await Shot($"interior-{id}-00-entrada");

            var targets = InteriorTargets(world, map);
            Require(targets.Count > 1, $"{id}: layout sem nada para visitar");
            var shot = 1;
            foreach (var target in targets)
            {
                using (new SpeedUp(TesterSpeedUp))
                {
                    var error = await WalkNear(world, target.At, target.Reach);
                    if (error is not null)
                    {
                        failures.Add($"{id} {target.Label}: {error}");
                        continue;
                    }
                }

                if (target.Interactable is { } it)
                {
                    if (player.FocusedInteractable != it)
                    {
                        failures.Add($"{id} {target.Label}: em {player.GlobalPosition} o foco é '{player.FocusedInteractable?.Id}', não '{it.Id}'");
                        continue;
                    }

                    var got = await PressInteract();
                    if (got != (it.Kind, it.Id))
                    {
                        failures.Add($"{id} {target.Label}: interagir emitiu {got}, esperava ({it.Kind}, {it.Id})");
                    }
                }

                if (target.Shot)
                {
                    world.Camera.Snap();
                    await Shot($"interior-{id}-{shot++:00}-{target.Slug}");
                }

                using (new SpeedUp(TesterSpeedUp))
                {
                    if (await PushAround(world) is { } pushError)
                    {
                        failures.Add($"{id} perto de {target.Label}: {pushError}");
                    }
                }
            }

            // Volta pela porta: a saída leva à Rua da Forja.
            var exit = map.Exits[0];
            var loads = world.MapLoads;
            using (new SpeedUp(TesterSpeedUp))
            {
                var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
                var path = WalkPathfinder.FindPathInto(world.WalkArea, start, exit.Area);
                if (path is null)
                {
                    failures.Add($"{id}: sem caminho até a saída");
                    continue;
                }

                var error = await TesterWalkPath(world, path, () => world.MapLoads > loads, timeoutGameSeconds: (path.Count * 4f / 78f * 1.6f) + 3f);
                if (error is not null)
                {
                    failures.Add($"{id} saída: {error}");
                    continue;
                }
            }

            await WaitMapSettled(world);
            if (world.MapId != exit.To)
            {
                failures.Add($"{id}: saída levou a {world.MapId}, esperava {exit.To}");
            }

            Log.Info($"interiores: {id} OK ({targets.Count} pontos visitados)");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s):\n  " + string.Join("\n  ", failures));
    }

    /// <summary>Pontos a visitar: interagíveis do maps.json e decoração/baús/props do layout, na ordem do arquivo.</summary>
    private static List<InteriorTarget> InteriorTargets(World.World world, MapDef map)
    {
        var targets = new List<InteriorTarget>();
        foreach (var def in map.Interactables)
        {
            var it = world.GetInteractable(def.Id) ?? throw new SmokeFailure($"{map.Id}: interagível {def.Id} não foi criado");
            targets.Add(new InteriorTarget(def.Label, new MapPoint(def.X, def.Y), def.Radius - 2f, it, Shot: true));
        }

        var dir = map.Image[..map.Image.LastIndexOf('/')];
        var path = $"{dir}/layout_{map.Id}.json";
        Require(Godot.FileAccess.FileExists(path), $"{map.Id}: {path} não existe");
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        var root = doc.RootElement;
        if (root.TryGetProperty("decor", out var decor))
        {
            var first = true;
            foreach (var d in decor.EnumerateArray())
            {
                var r = d.TryGetProperty("r", out var rv) ? rv.GetSingle() : 24f;
                var label = d.TryGetProperty("label", out var lv) ? lv.GetString()! : d.GetProperty("kind").GetString()!;
                targets.Add(new InteriorTarget(label, Point(d), r - 2f, null, Shot: first));
                first = false;
            }
        }

        if (root.TryGetProperty("chests", out var chests))
        {
            foreach (var c in chests.EnumerateArray())
            {
                targets.Add(new InteriorTarget("baú", Point(c), 18f, null, Shot: true)); // v2: "Abrir baú" r 20
            }
        }

        if (root.TryGetProperty("props", out var props))
        {
            foreach (var p in props.EnumerateArray())
            {
                targets.Add(new InteriorTarget($"prop {p.GetProperty("kind").GetString()}", Point(p), 22f, null, Shot: false)); // alcance de golpe
            }
        }

        return targets;

        static MapPoint Point(JsonElement e) => new(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle());
    }

    /// <summary>Anda com input do ponto atual até ficar a <paramref name="reach"/> u do alvo. Null se chegou.</summary>
    private async Task<string?> WalkNear(World.World world, MapPoint target, float reach)
    {
        var player = FindPlayer()!;
        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, target, reach);
        if (path is null)
        {
            return $"inalcançável a {reach:0} u de ({target.X}, {target.Y}) saindo de ({start.X:0}, {start.Y:0})";
        }

        var goal = new Vector2(target.X, target.Y);
        return await TesterWalkPath(world, path, () => player.GlobalPosition.DistanceTo(goal) <= reach + 1f,
            timeoutGameSeconds: (path.Count * 4f / 78f * 1.6f) + 3f);
    }

    /// <summary>Empurra o jogador nas 8 direções a partir do ponto atual: o pé nunca entra em área bloqueada nem salta.</summary>
    private async Task<string?> PushAround(World.World world)
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

                if (p.DistanceTo(previous) > maxStep)
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

    /// <summary>Aperta interagir (evento de input da ação, como uma tecla ou o botão do controle) e devolve o pedido emitido.</summary>
    private async Task<(string Kind, string Id)?> PressInteract()
    {
        (string, string)? got = null;
        void OnRequest(string kind, string id) => got = (kind, id);
        EventBus.Instance.InteractionRequested += OnRequest;
        try
        {
            // PushInput entrega já pelo caminho _Input → GUI → _UnhandledInput do Viewport, com ou sem foco de janela
            // (Input.ParseInputEvent não chega ao _UnhandledInput na janela sem foco das rodadas com prints).
            GetViewport().PushInput(new InputEventAction { Action = InputActions.Interact, Pressed = true });
            await Frames(2);
            GetViewport().PushInput(new InputEventAction { Action = InputActions.Interact, Pressed = false });
            await NextPhysicsFrame();
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
        }

        return got;
    }

    private sealed record InteriorTarget(string Label, MapPoint At, float Reach, Interactable? Interactable, bool Shot)
    {
        public string Slug
        {
            get
            {
                var chars = new List<char>();
                foreach (var c in Label.ToLowerInvariant())
                {
                    chars.Add(char.IsLetterOrDigit(c) ? c : '-');
                }

                return new string([.. chars]).Trim('-');
            }
        }
    }
}
