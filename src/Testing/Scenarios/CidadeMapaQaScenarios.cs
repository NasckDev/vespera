using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.Testing;

/// <summary>
/// Testes do tester de experiência da frente B4b (Rua da Forja). Um bot anda com input real até cada área andável,
/// escada, cais, baú, objeto e decoração do <c>layout_forge.json</c>, entra e sai de cada porta da rua e mede
/// travamentos (input ativo e &lt; 1 u de progresso em 1 s de jogo), paredes invisíveis (bloqueado onde a máscara diz
/// chão) e saltos. Os cenários "estresse:" fazem 5 min de caminhada aleatória, 50 trocas de mapa e spam de botões,
/// com tempo de frame (média/p95) e crescimento de nós/memória.
/// </summary>
public partial class SmokeTest
{
    private const string QaForgeLayoutPath = "res://assets/maps/city/layout_forge.json";

    /// <summary>Ticks de física por segundo de jogo (o <see cref="SpeedUp"/> multiplica ticks e TimeScale juntos).</summary>
    private const int QaTicksPerGameSecond = 60;

    /// <summary>Um ponto que o jogador deveria conseguir alcançar a pé.</summary>
    private sealed record QaPoi(string Kind, string Label, float X, float Y, float Near, bool BoatOnly);

    /// <summary>Resultado de uma caminhada do bot.</summary>
    private sealed record QaWalkResult(bool Reached, string? Error, List<string> Stucks, float Length);

    private static string QaAt(Vector2 p) => $"({p.X:0},{p.Y:0})";

    /// <summary>Áreas walk "só de balsa" do layout (rect), para separar o que é inalcançável a pé por projeto.</summary>
    private static List<MapRect> QaBoatOnlyAreas(JsonElement root)
    {
        var list = new List<MapRect>();
        foreach (var w in root.GetProperty("walk").EnumerateArray())
        {
            if (w.TryGetProperty("note", out var note) && (note.GetString() ?? string.Empty).Contains("balsa", StringComparison.OrdinalIgnoreCase)
                && w.TryGetProperty("rect", out var r))
            {
                list.Add(new MapRect(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle()));
            }
        }

        return list;
    }

    /// <summary>Todos os pontos de interesse da Rua da Forja: áreas andáveis (centro), baús, quebráveis, decor e interagíveis.</summary>
    private static List<QaPoi> QaForgePois(IWalkArea area, MapDef map)
    {
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(QaForgeLayoutPath));
        var root = doc.RootElement;
        var boatOnly = QaBoatOnlyAreas(root);
        bool OnBoatDock(float x, float y)
        {
            foreach (var r in boatOnly)
            {
                if (x >= r.X - 8 && x <= r.Right + 8 && y >= r.Y - 8 && y <= r.Bottom + 8)
                {
                    return true;
                }
            }

            return false;
        }

        var pois = new List<QaPoi>();
        foreach (var w in root.GetProperty("walk").EnumerateArray())
        {
            float cx, cy;
            if (w.TryGetProperty("rect", out var r))
            {
                cx = r[0].GetSingle() + (r[2].GetSingle() / 2);
                cy = r[1].GetSingle() + (r[3].GetSingle() / 2);
            }
            else if (w.TryGetProperty("poly", out var poly))
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var pt in poly.EnumerateArray())
                {
                    minX = MathF.Min(minX, pt[0].GetSingle());
                    maxX = MathF.Max(maxX, pt[0].GetSingle());
                    minY = MathF.Min(minY, pt[1].GetSingle());
                    maxY = MathF.Max(maxY, pt[1].GetSingle());
                }

                cx = (minX + maxX) / 2;
                cy = (minY + maxY) / 2;
            }
            else
            {
                continue;
            }

            var note = w.TryGetProperty("note", out var n) ? n.GetString() ?? "?" : "?";
            var stand = area.NearestWalkable(cx, cy, maxDistance: 64f);
            if (stand is null)
            {
                pois.Add(new QaPoi("área", $"{note} (sem chão perto do centro ({cx:0},{cy:0}))", cx, cy, 8, OnBoatDock(cx, cy)));
                continue;
            }

            pois.Add(new QaPoi("área", note, stand.Value.X, stand.Value.Y, 8, OnBoatDock(stand.Value.X, stand.Value.Y)));
        }

        foreach (var c in root.GetProperty("chests").EnumerateArray())
        {
            var x = c.GetProperty("x").GetSingle();
            var y = c.GetProperty("y").GetSingle();
            var note = c.TryGetProperty("note", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            pois.Add(new QaPoi("baú", note, x, y, 16, OnBoatDock(x, y)));
        }

        foreach (var p in root.GetProperty("props").EnumerateArray())
        {
            var x = p.GetProperty("x").GetSingle();
            var y = p.GetProperty("y").GetSingle();
            pois.Add(new QaPoi("quebrável", p.GetProperty("kind").GetString() ?? "?", x, y, 16, OnBoatDock(x, y)));
        }

        foreach (var d in root.GetProperty("decor").EnumerateArray())
        {
            var x = d.GetProperty("x").GetSingle();
            var y = d.GetProperty("y").GetSingle();
            var r = d.TryGetProperty("r", out var rr) ? rr.GetSingle() : 24f;
            var label = d.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            pois.Add(new QaPoi($"decor {d.GetProperty("kind").GetString()}", label, x, y, MathF.Max(8f, r - 4f), OnBoatDock(x, y)));
        }

        foreach (var it in map.Interactables)
        {
            pois.Add(new QaPoi("interagível", it.Id, it.X, it.Y, MathF.Max(8f, it.Radius - 4f), false));
        }

        for (var i = 0; i < map.PortalSpots.Count; i++)
        {
            var s = map.PortalSpots[i];
            pois.Add(new QaPoi("portal", $"spot {i}", s.X, s.Y, 12, false));
        }

        return pois;
    }

    /// <summary>
    /// Anda com input pelo caminho do Core até perto do alvo. Registra travamentos (input ativo e &lt; 1 u em 1 s de jogo),
    /// pé em área bloqueada e saltos; num travamento, desencaixa o jogador no próximo ponto do caminho e segue.
    /// </summary>
    private async Task<QaWalkResult> QaWalkTo(World.World world, MapPoint target, float near)
    {
        var player = FindPlayer()!;
        var stucks = new List<string>();
        // o motor anda com aceleração/frenagem (B4a-D42): para antes de planejar, senão o embalo do trecho anterior
        // (que pode ter terminado na soleira de uma porta) leva o pé para dentro da saída
        PressDirection(Vector2.Zero);
        for (var i = 0; i < 8; i++)
        {
            await NextPhysicsFrame();
        }

        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        // o corpo (r=3,5) encosta mais na parede que o CanStand do Core (r=4): parte do ponto de pé mais próximo
        start = world.WalkArea.NearestWalkable(start.X, start.Y, maxDistance: 8f) ?? start;
        // Um jogador contorna a soleira das portas que não quer usar: planeja sem as saídas (e com elas, se for o único jeito).
        var path = WalkPathfinder.FindPathNear(new QaExitAvoidingArea(world.WalkArea, world.Map.Exits, target, start), start, target, near)
                   ?? WalkPathfinder.FindPathNear(world.WalkArea, start, target, near);
        if (path is null)
        {
            return new QaWalkResult(false, $"sem caminho a pé de {QaAt(player.GlobalPosition)}", stucks, 0);
        }

        var length = path.Count * 4f;
        var mapId = world.MapId;
        var loads = world.MapLoads;
        var maxFrames = (int)(((length / Player.Player.BaseSpeed * 1.8f) + 4f) * QaTicksPerGameSecond);
        var maxStep = (Player.Player.BaseSpeed / QaTicksPerGameSecond) + 0.6f;
        var next = 1;
        var history = new Vector2[QaTicksPerGameSecond];
        var previous = player.GlobalPosition;
        var end = new Vector2(path[^1].X, path[^1].Y);
        try
        {
            for (var frame = 0; ; frame++)
            {
                var p = player.GlobalPosition;
                if (world.MapLoads > loads || world.MapId != mapId)
                {
                    return new QaWalkResult(false, $"o caminho passou por uma saída e levou a {world.MapId}", stucks, length);
                }

                if (p.DistanceTo(end) < 3f || p.DistanceTo(new Vector2(target.X, target.Y)) <= near)
                {
                    return new QaWalkResult(true, null, stucks, length);
                }

                if (frame > maxFrames)
                {
                    return new QaWalkResult(false, $"timeout em {QaAt(p)}", stucks, length);
                }

                if (!world.WalkArea.IsWalkable(p.X, p.Y))
                {
                    return new QaWalkResult(false, $"pé em área bloqueada em {QaAt(p)}", stucks, length);
                }

                if (frame > 0 && p.DistanceTo(previous) > maxStep)
                {
                    stucks.Add($"salto {QaAt(previous)} → {QaAt(p)}");
                }

                history[frame % history.Length] = p;
                if (frame >= history.Length && p.DistanceTo(history[(frame + 1) % history.Length]) < 1f)
                {
                    var stuckAt = next < path.Count ? path[next] : path[^1];
                    stucks.Add($"travado em {QaAt(p)} indo para ({stuckAt.X:0},{stuckAt.Y:0}) CanStand={world.WalkArea.CanStand(stuckAt.X, stuckAt.Y)}");
                    // desencaixa no ponto seguinte do caminho para medir o resto do trajeto
                    var unstuck = path[Math.Min(next + 2, path.Count - 1)];
                    player.Teleport(new Vector2(unstuck.X, unstuck.Y));
                    Array.Clear(history);
                    frame = -1;
                    maxFrames = Math.Max(0, maxFrames - QaTicksPerGameSecond);
                    if (stucks.Count > 6)
                    {
                        return new QaWalkResult(false, "travou demais", stucks, length);
                    }

                    previous = player.GlobalPosition;
                    await NextPhysicsFrame();
                    continue;
                }

                previous = p;
                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                var aim = next < path.Count ? new Vector2(path[next].X, path[next].Y) : end;
                PressDirection((aim - p).Normalized());
                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }
    }

    [SmokeScenario("testa rua da forja (qa): bot alcança a pé cada área, escada, cais, baú, objeto e decoração", order: 40)]
    private async Task QaForgeReachEveryPoi()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=qa
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        world.GoMap("forge", 1500, 250, instant: true);
        await WaitMapSettled(world);
        var pois = QaForgePois(world.WalkArea, world.Map);

        var unreachable = new List<string>();
        var boatOnly = new List<string>();
        var stuckReport = new List<string>();
        var reached = 0;
        var walked = 0f;
        using (new SpeedUp(TesterSpeedUp))
        {
            // vizinho mais próximo a partir da posição atual: um passeio contínuo pela rua inteira
            var pending = new List<QaPoi>(pois);
            while (pending.Count > 0)
            {
                var here = player.GlobalPosition;
                var bestIndex = 0;
                for (var i = 1; i < pending.Count; i++)
                {
                    if (here.DistanceSquaredTo(new Vector2(pending[i].X, pending[i].Y)) < here.DistanceSquaredTo(new Vector2(pending[bestIndex].X, pending[bestIndex].Y)))
                    {
                        bestIndex = i;
                    }
                }

                var poi = pending[bestIndex];
                pending.RemoveAt(bestIndex);
                var name = $"{poi.Kind} '{poi.Label}' ({poi.X:0},{poi.Y:0})";
                var result = await QaWalkTo(world, new MapPoint(poi.X, poi.Y), poi.Near);
                walked += result.Length;
                foreach (var s in result.Stucks)
                {
                    stuckReport.Add($"{name}: {s}");
                }

                if (result.Reached)
                {
                    reached++;
                    continue;
                }

                var byBoat = poi.BoatOnly || QaReachableByBoat(world.WalkArea, poi);
                (byBoat ? boatOnly : unreachable).Add($"{name}: {result.Error}");
                if (world.MapId != "forge")
                {
                    world.GoMap("forge", 1500, 250, instant: true);
                    await WaitMapSettled(world);
                }
            }
        }

        Log.Info($"qa forja: {reached}/{pois.Count} pontos alcançados a pé, ~{walked:0} u andados, {stuckReport.Count} travamento(s)");
        foreach (var b in boatOnly)
        {
            Log.Info($"qa forja: só de balsa (a balsa não existe no jogo, inalcançável hoje): {b}");
        }

        var problems = new List<string>();
        problems.AddRange(unreachable);
        problems.AddRange(stuckReport);
        Require(problems.Count == 0, $"{unreachable.Count} ponto(s) inalcançável(is) a pé e {stuckReport.Count} travamento(s):\n  " + string.Join("\n  ", problems));
    }

    /// <summary>Alcançável partindo do destino de alguma balsa do layout (o que o <c>layout.py check</c> conta como alcançável).</summary>
    private static bool QaReachableByBoat(IWalkArea area, QaPoi poi)
    {
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(QaForgeLayoutPath));
        foreach (var d in doc.RootElement.GetProperty("decor").EnumerateArray())
        {
            if (d.GetProperty("kind").GetString() == "boat" && d.TryGetProperty("to", out var to)
                && WalkPathfinder.FindPathNear(area, new MapPoint(to[0].GetSingle(), to[1].GetSingle()), new MapPoint(poi.X, poi.Y), poi.Near) is not null)
            {
                return true;
            }
        }

        return false;
    }

    [SmokeScenario("testa rua da forja (qa): cais, baús e pesca pintados dá para alcançar no jogo de hoje", order: 44)]
    private async Task QaForgeNothingOnlyByMissingBoat()
    {
        // A balsa (decor "boat") ainda não existe no jogo. Tudo que o layout põe "só de balsa" fica inalcançável para o
        // jogador: cais pintados, baús, pesca, quebráveis. Passa quando houver acesso a pé ou a balsa funcionar no jogo.
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        world.GoMap("forge", 1500, 250, instant: true);
        await WaitMapSettled(world);
        var spawn = new MapPoint(1500, 250);
        var missing = new List<string>();
        foreach (var poi in QaForgePois(world.WalkArea, world.Map))
        {
            if (WalkPathfinder.FindPathNear(world.WalkArea, spawn, new MapPoint(poi.X, poi.Y), poi.Near) is null)
            {
                missing.Add($"{poi.Kind} '{poi.Label}' ({poi.X:0},{poi.Y:0})");
            }
        }

        var ferries = 0;
        foreach (var node in world.InteractablesRoot.GetChildren())
        {
            var nodeName = node.Name.ToString();
            if (nodeName.Contains("boat", StringComparison.OrdinalIgnoreCase) || nodeName.Contains("balsa", StringComparison.OrdinalIgnoreCase))
            {
                ferries++;
            }
        }

        // Integração v0.1: a balsa do motor (B4a-D46) é decoração do WorldDressing, não um nó de InteractablesRoot.
        foreach (var spot in world.Dressing.Decor)
        {
            if (spot.Def.Kind == "boat" && spot.Def.To is not null)
            {
                ferries++;
            }
        }

        Log.Info($"qa forja: {missing.Count} ponto(s) só alcançáveis de balsa; balsas interagíveis no jogo: {ferries}");
        Require(missing.Count == 0 || ferries > 0, $"{missing.Count} ponto(s) da Rua da Forja só se alcançam de balsa, e a balsa não existe no jogo:\n  " + string.Join("\n  ", missing));
    }

    [SmokeScenario("testa rua da forja (qa): entra e sai de cada porta da rua andando o caminho inteiro", order: 41)]
    private async Task QaForgeEveryDoor()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var failures = new List<string>();
        using var speed = new SpeedUp(TesterSpeedUp);
        foreach (var door in World.World.Catalog["forge"].Exits)
        {
            // parte sempre da praça central para andar um trajeto longo e realista até a porta
            world.GoMap("forge", 1125, 470, instant: true);
            await WaitMapSettled(world);
            var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
            var path = WalkPathfinder.FindPathInto(world.WalkArea, start, door.Area);
            if (path is null)
            {
                failures.Add($"porta→{door.To}: sem caminho a pé da praça");
                continue;
            }

            var loads = world.MapLoads;
            var error = await TesterWalkPath(world, path, () => world.MapLoads > loads, timeoutGameSeconds: (path.Count * 4f / 78f * 1.6f) + 3f);
            if (error is not null)
            {
                failures.Add($"porta→{door.To}: {error}");
                continue;
            }

            await WaitMapSettled(world);
            if (world.MapId != door.To)
            {
                failures.Add($"porta→{door.To}: levou a {world.MapId}");
                continue;
            }

            await Shot($"qa-porta-{door.To}-dentro");
            var back = world.Map.Exits.Count > 0 ? world.Map.Exits[0] : null;
            foreach (var e in world.Map.Exits)
            {
                if (e.To == "forge")
                {
                    back = e;
                }
            }

            if (back is null || back.To != "forge")
            {
                failures.Add($"{door.To}: sem saída para a forge");
                continue;
            }

            // sai andando desde a chegada, caminho inteiro
            var inside = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
            var outPath = WalkPathfinder.FindPathInto(world.WalkArea, inside, back.Area);
            if (outPath is null)
            {
                failures.Add($"{door.To}: sem caminho da chegada até a saída");
                continue;
            }

            loads = world.MapLoads;
            error = await TesterWalkPath(world, outPath, () => world.MapLoads > loads, timeoutGameSeconds: (outPath.Count * 4f / 78f * 1.6f) + 3f);
            if (error is not null)
            {
                failures.Add($"{door.To}→forge: {error}");
                continue;
            }

            await WaitMapSettled(world);
            var p = player.GlobalPosition;
            var doorCenter = new Vector2(door.Area.Center.X, door.Area.Center.Y);
            if (world.MapId != "forge")
            {
                failures.Add($"{door.To}→forge: levou a {world.MapId}");
                continue;
            }

            // lojas (B4b-D61): na frente da porta; estalagem e guilda mantêm a chegada da v2 (D6), só precisa de chão
            var maxDistance = door.To.StartsWith("shop_", StringComparison.Ordinal) ? 40f : 120f;
            if (!world.WalkArea.CanStand(p.X, p.Y) || p.DistanceTo(doorCenter) > maxDistance)
            {
                failures.Add($"{door.To}→forge: chegou em {QaAt(p)} (CanStand={world.WalkArea.CanStand(p.X, p.Y)}), porta em {QaAt(doorCenter)}");
            }

            if (door.Area.Contains(p.X, p.Y))
            {
                failures.Add($"{door.To}→forge: chegou DENTRO da porta {QaAt(p)} (volta a entrar sozinho)");
            }

            await Shot($"qa-porta-{door.To}-fora");
            // parado 1 s na chegada: não pode reentrar sozinho
            loads = world.MapLoads;
            await Frames(QaTicksPerGameSecond * TesterSpeedUp);
            if (world.MapLoads > loads)
            {
                failures.Add($"{door.To}→forge: parado na chegada, entrou de novo em {world.MapId}");
                continue;
            }

            // dá para sair andando da frente da porta (para longe dela)
            var away = (p - doorCenter).Normalized();
            if (away == Vector2.Zero)
            {
                away = Vector2.Down;
            }

            var before = player.GlobalPosition;
            PressDirection(away);
            await Frames(QaTicksPerGameSecond * TesterSpeedUp / 2);
            PressDirection(Vector2.Zero);
            var moved = player.GlobalPosition.DistanceTo(before);
            if (moved < 15f)
            {
                failures.Add($"{door.To}→forge: preso na frente da porta (andou {moved:0.0} u em 0,5 s para longe dela, de {QaAt(before)})");
            }

            Log.Info($"qa forja: porta {door.To} ida e volta OK (chegada {QaAt(p)})");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s) nas portas:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("testa rua da forja (qa): prints atrás de oclusores e de proporção", order: 42)]
    private async Task QaForgeOccluderShots()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        world.GoMap("forge", 1500, 250, instant: true);
        await WaitMapSettled(world);
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(QaForgeLayoutPath));
        var behind = new List<string>();
        var stucks = new List<string>();
        var idx = 0;
        foreach (var oc in doc.RootElement.GetProperty("occluders").EnumerateArray())
        {
            var note = oc.TryGetProperty("note", out var n) ? n.GetString() ?? "?" : "?";
            var baseY = oc.GetProperty("baseY").GetSingle();
            var pts = new List<Vector2>();
            foreach (var pt in oc.GetProperty("poly").EnumerateArray())
            {
                pts.Add(new Vector2(pt[0].GetSingle(), pt[1].GetSingle()));
            }

            var poly = pts.ToArray();
            // pontos onde o jogador fica de pé DENTRO do recorte e acima da base (o objeto deveria cobri-lo)
            Vector2? spot = null;
            var cells = 0;
            for (var y = 0f; y < baseY - 2; y += 4)
            {
                for (var x = 0f; x < world.WalkArea.Width; x += 4)
                {
                    var q = new Vector2(x + 2, y + 2);
                    if (Geometry2D.IsPointInPolygon(q, poly) && world.WalkArea.CanStand(q.X, q.Y))
                    {
                        cells++;
                        if (spot is null || q.Y > spot.Value.Y)
                        {
                            spot = q;
                        }
                    }
                }
            }

            if (spot is null)
            {
                idx++;
                continue;
            }

            behind.Add($"{note}: {cells} célula(s) de pé atrás (ex. {QaAt(spot.Value)})");
            // chega andando (o trecho final com input) e tira o print
            var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), new MapPoint(spot.Value.X, spot.Value.Y), 4);
            if (path is not null)
            {
                // começa 12 passos antes e anda o resto com input (detector de travamento do bot)
                var from = path[Math.Max(0, path.Count - 13)];
                player.Teleport(new Vector2(from.X, from.Y));
                await NextPhysicsFrame();
                var walk = await QaWalkTo(world, new MapPoint(spot.Value.X, spot.Value.Y), 3);
                foreach (var s in walk.Stucks)
                {
                    stucks.Add($"{note}: {s}");
                }

                if (!walk.Reached)
                {
                    stucks.Add($"{note}: não chegou atrás do oclusor em {QaAt(spot.Value)}: {walk.Error}");
                }

                await WaitSeconds(0.4);
                await Shot($"qa-oclusor-{idx:00}");
            }

            idx++;
        }

        Log.Info($"qa forja: {behind.Count} oclusor(es) com chão andável atrás (o jogo precisa desenhá-los na frente do jogador):");
        foreach (var b in behind)
        {
            Log.Info($"qa forja:   {b}");
        }

        // print de proporção: jogador ao lado de lampião, vaso, barraca e porta
        foreach (var (name, x, y) in new[] { ("qa-proporcao-lampiao", 590f, 520f), ("qa-proporcao-porta-arm", 1118f, 208f), ("qa-proporcao-mercado", 420f, 640f) })
        {
            var at = world.WalkArea.NearestWalkable(x, y) ?? new MapPoint(x, y);
            player.Teleport(new Vector2(at.X, at.Y));
            await WaitSeconds(0.6);
            await Shot(name);
        }

        Require(stucks.Count == 0, $"{stucks.Count} travamento(s) chegando atrás de oclusores:\n  " + string.Join("\n  ", stucks));
    }

    [SmokeScenario("estresse: rua da forja (qa): 5 min de caminhada aleatória com detector de travamento e parede invisível", order: 90)]
    private async Task QaStressForgeRandomWalk()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        world.GoMap("forge", 1500, 250, instant: true);
        await WaitMapSettled(world);
        var rng = new Random(4242);
        var problems = new List<string>();
        var invisible = new List<string>();
        var trips = 0;
        var arrived = 0;
        var noPath = 0;
        var viaExit = 0;
        var noPathSamples = new List<string>();
        var pushes = 0;
        var totalFrames = 300 * QaTicksPerGameSecond; // 5 min de jogo
        var framesDone = 0;
        var maxStep = (Player.Player.BaseSpeed / QaTicksPerGameSecond) + 0.6f;
        using (new SpeedUp(TesterSpeedUp))
        {
            while (framesDone < totalFrames && problems.Count < 30)
            {
                if (world.MapId != "forge")
                {
                    world.GoMap("forge", 1125, 470, instant: true);
                    await WaitMapSettled(world);
                }

                if (rng.Next(3) == 0)
                {
                    // modo "jogador apertando direção": segura uma direção 0,3–1,5 s. Se não anda e o chão à frente é
                    // andável e cabe o jogador, é parede invisível (colisor sem desenho).
                    pushes++;
                    var angle = rng.NextSingle() * MathF.Tau;
                    var d = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    var frames = QaTicksPerGameSecond * (3 + rng.Next(13)) / 10;
                    PressDirection(d);
                    var history = new Vector2[QaTicksPerGameSecond / 2];
                    var previous = player.GlobalPosition;
                    var mapId = world.MapId;
                    for (var f = 0; f < frames; f++)
                    {
                        await NextPhysicsFrame();
                        framesDone++;
                        if (world.MapId != mapId || world.IsTransitioning)
                        {
                            break;
                        }

                        var p = player.GlobalPosition;
                        if (!world.WalkArea.IsWalkable(p.X, p.Y))
                        {
                            problems.Add($"pé em área bloqueada em {QaAt(p)}");
                            break;
                        }

                        if (p.DistanceTo(previous) > maxStep)
                        {
                            problems.Add($"salto {QaAt(previous)} → {QaAt(p)}");
                        }

                        previous = p;
                        history[f % history.Length] = p;
                        if (f >= history.Length && p.DistanceTo(history[(f + 1) % history.Length]) < 0.5f)
                        {
                            var ahead = p + (d * 6f);
                            if (world.WalkArea.CanStand(ahead.X, ahead.Y) && world.WalkArea.CanStand(p.X + (d.X * 3f), p.Y + (d.Y * 3f)))
                            {
                                invisible.Add($"parado em {QaAt(p)} empurrando {d.X:0.00},{d.Y:0.00}, máscara diz chão em {QaAt(ahead)}");
                            }

                            break;
                        }
                    }

                    PressDirection(Vector2.Zero);
                    continue;
                }

                // modo "vai até um lugar": ponto aleatório onde dá para ficar de pé
                MapPoint? target = null;
                for (var tries = 0; tries < 200 && target is null; tries++)
                {
                    var x = rng.NextSingle() * world.WalkArea.Width;
                    var y = rng.NextSingle() * world.WalkArea.Height;
                    if (world.WalkArea.CanStand(x, y))
                    {
                        target = new MapPoint(x, y);
                    }
                }

                if (target is null)
                {
                    continue;
                }

                trips++;
                var startFrame = Engine.GetPhysicsFrames();
                var result = await QaWalkTo(world, target.Value, 6);
                framesDone += (int)(Engine.GetPhysicsFrames() - startFrame);
                foreach (var s in result.Stucks)
                {
                    problems.Add($"indo para ({target.Value.X:0},{target.Value.Y:0}): {s}");
                }

                if (result.Reached)
                {
                    arrived++;
                }
                else if (result.Error?.StartsWith("sem caminho", StringComparison.Ordinal) == true)
                {
                    noPath++;
                    if (noPathSamples.Count < 8)
                    {
                        noPathSamples.Add($"({target.Value.X:0},{target.Value.Y:0}) {result.Error}");
                    }
                }
                else if (result.Error?.StartsWith("o caminho passou", StringComparison.Ordinal) == true)
                {
                    viaExit++;
                }
                else if (result.Error is { } err && !err.StartsWith("sem caminho", StringComparison.Ordinal) && !err.StartsWith("o caminho passou", StringComparison.Ordinal))
                {
                    problems.Add($"indo para ({target.Value.X:0},{target.Value.Y:0}): {err}");
                }
            }
        }

        Log.Info($"qa forja estresse: {framesDone / QaTicksPerGameSecond} s de jogo, {trips} viagens ({arrived} chegaram, {noPath} sem caminho a pé, {viaExit} cortadas por uma porta), {pushes} empurrões, {problems.Count} problema(s), {invisible.Count} parede(s) invisível(is)");
        foreach (var i in invisible)
        {
            Log.Info($"qa forja estresse: {i}");
        }

        foreach (var n in noPathSamples)
        {
            Log.Info($"qa forja estresse: sem caminho até {n}");
        }

        problems.AddRange(invisible);
        Require(problems.Count == 0, $"{problems.Count} problema(s) na caminhada aleatória:\n  " + string.Join("\n  ", problems));
    }

    [SmokeScenario("estresse: rua da forja (qa): 50 trocas de mapa, spam de botões, tempo de frame e nós", order: 91)]
    private async Task QaStressForgeMapSwapsAndSpam()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var problems = new List<string>();
        string[] targets = ["shop_arm", "shop_mag", "shop_alq", "shop_rel", "inn", "guild"];
        var report = new StringBuilder();
        double nodesAt10 = 0, memAt10 = 0;
        for (var i = 0; i < 50; i++)
        {
            // metade pela porta (troca com fade), metade direta: forge ↔ outro mapa
            var to = targets[i % targets.Length];
            world.GoMap(to, World.World.Catalog[to].Spawn.X, World.World.Catalog[to].Spawn.Y, instant: i % 2 == 0);
            await WaitMapSettled(world);
            MapExit? back = null;
            foreach (var e in world.Map.Exits)
            {
                if (e.To == "forge")
                {
                    back = e;
                }
            }

            var at = back?.At ?? World.World.Catalog["forge"].Spawn;
            world.GoMap("forge", at.X, at.Y, instant: i % 2 == 1);
            await WaitMapSettled(world);
            var p = player.GlobalPosition;
            if (!world.WalkArea.CanStand(p.X, p.Y))
            {
                problems.Add($"troca {i}: chegou na forge em {QaAt(p)} sem chão");
            }

            if (i == 9)
            {
                nodesAt10 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                memAt10 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
            }
        }

        var nodes50 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem50 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        report.Append($"nós {nodesAt10:0}→{nodes50:0} (troca 10→50), memória {memAt10 / 1048576:0.0}→{mem50 / 1048576:0.0} MB, órfãos {orphans:0}");
        if (nodes50 - nodesAt10 > 20)
        {
            problems.Add($"nós crescem com as trocas: {nodesAt10:0} → {nodes50:0}");
        }

        if (mem50 - memAt10 > 16 * 1048576)
        {
            problems.Add($"memória cresce com as trocas: {memAt10 / 1048576:0.0} → {mem50 / 1048576:0.0} MB");
        }

        if (orphans > 0)
        {
            problems.Add($"{orphans:0} nó(s) órfão(s)");
        }

        // linha de base: mesmo andar aleatório na Guilda (mapa de formas, sem a arte grande), sem spam
        var guild = await QaMeasureFrames(world, "guild", 250, 560, 10, spam: false, problems);
        var forgeWalk = await QaMeasureFrames(world, "forge", 1120, 300, 10, spam: false, problems);
        // spam de interagir/esquivar/atacar/pausar/lanterna andando pela rua (tempo real, sem aceleração)
        var forgeSpam = await QaMeasureFrames(world, "forge", 1120, 300, 20, spam: true, problems);
        if (GetTree().Paused)
        {
            problems.Add("spam: o jogo ficou pausado depois de soltar tudo");
            GetTree().Paused = false;
        }

        report.Append($"; frame (média/p95/pior ms) guild andando {guild}; forge andando {forgeWalk}; forge com spam {forgeSpam}");
        Log.Info($"qa forja estresse: {report}");
        if (DisplayServer.GetName() != "headless" && forgeSpam.P95 > 20.0)
        {
            problems.Add($"p95 do frame na forge com spam {forgeSpam.P95:0.0} ms > 20 ms (guild na mesma máquina: {guild.P95:0.0} ms)");
        }

        Require(problems.Count == 0, $"{problems.Count} problema(s):\n  " + string.Join("\n  ", problems) + $"\n  ({report})");
    }

    private readonly record struct QaForgeFrameStats(double Avg, double P95, double Worst, int Frames)
    {
        public override string ToString() => $"{Avg:0.00}/{P95:0.00}/{Worst:0.0} em {Frames} frames";
    }

    /// <summary>Anda em direções aleatórias por <paramref name="seconds"/> s reais medindo o tempo de cada frame de processo.</summary>
    private async Task<QaForgeFrameStats> QaMeasureFrames(World.World world, string mapId, float x, float y, double seconds, bool spam, List<string> problems)
    {
        var player = FindPlayer()!;
        world.GoMap(mapId, x, y, instant: true);
        await WaitMapSettled(world);
        var rng = new Random(77);
        StringName[] buttons = [InputActions.Interact, InputActions.Dodge, InputActions.Attack, InputActions.AttackHeavy, InputActions.Pause, InputActions.Lantern];
        var frameUs = new List<double>(4096);
        var last = Time.GetTicksUsec();
        var frames = 0;
        var deadline = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < deadline)
        {
            if (spam && rng.Next(8) == 0)
            {
                var b = buttons[rng.Next(buttons.Length)];
                Input.ParseInputEvent(new InputEventAction { Action = b, Pressed = true });
                Input.ParseInputEvent(new InputEventAction { Action = b, Pressed = false });
            }

            if (frames % 40 == 0)
            {
                var a = rng.NextSingle() * MathF.Tau;
                PressDirection(new Vector2(MathF.Cos(a), MathF.Sin(a)));
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            frameUs.Add(now - last);
            last = now;
            frames++;
            if (world.MapId != mapId && !world.IsTransitioning)
            {
                world.GoMap(mapId, x, y, instant: true);
            }

            var p = player.GlobalPosition;
            if (world.MapId == mapId && !world.IsTransitioning && !world.WalkArea.IsWalkable(p.X, p.Y))
            {
                problems.Add($"{mapId}: pé em área bloqueada em {QaAt(p)}");
                break;
            }
        }

        PressDirection(Vector2.Zero);
        ReleaseAllActions();
        frameUs.RemoveRange(0, Math.Min(10, frameUs.Count)); // aquecimento
        frameUs.Sort();
        var sum = 0.0;
        foreach (var f in frameUs)
        {
            sum += f;
        }

        return frameUs.Count == 0
            ? default
            : new QaForgeFrameStats(sum / frameUs.Count / 1000, frameUs[(int)(frameUs.Count * 0.95)] / 1000, frameUs[^1] / 1000, frameUs.Count);
    }

    [SmokeScenario("testa rua da forja (qa): prints de resolução", order: 43)]
    private async Task QaForgeResolutionShots()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var size = DisplayServer.WindowGetSize();
        foreach (var (name, x, y) in new[] { ("praca", 1125f, 470f), ("ponte", 850f, 505f), ("mercado", 520f, 700f) })
        {
            world.GoMap("forge", x, y, instant: true);
            await WaitMapSettled(world);
            await Hold(InputActions.MoveRight, 20);
            await WaitSeconds(0.5);
            await Shot($"qa-res-{size.X}x{size.Y}-{name}");
        }

        Log.Info($"qa forja: janela {size}, viewport {GetViewport().GetVisibleRect().Size}, zoom {world.Camera.Zoom}");
        _ = player;
    }
}

/// <summary>Área andável sem as saídas do mapa (com 6 u de folga), menos as que contêm o alvo ou a partida.</summary>
internal sealed class QaExitAvoidingArea(IWalkArea inner, IReadOnlyList<MapExit> exits, MapPoint target, MapPoint start) : IWalkArea
{
    private const float Margin = 6f;

    public float Width => inner.Width;

    public float Height => inner.Height;

    public bool IsWalkable(float x, float y)
    {
        if (!inner.IsWalkable(x, y))
        {
            return false;
        }

        foreach (var e in exits)
        {
            var r = new MapRect(e.Area.X - Margin, e.Area.Y - Margin, e.Area.Width + (2 * Margin), e.Area.Height + (2 * Margin));
            if (r.Contains(target.X, target.Y))
            {
                continue;
            }

            // a folga só vale se a partida não está nela (senão a partida seria "bloqueada"); a soleira sempre vale
            if (r.Contains(start.X, start.Y) ? e.Area.Contains(x, y) : r.Contains(x, y))
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<MapRect> BlockedRects() => inner.BlockedRects();
}
