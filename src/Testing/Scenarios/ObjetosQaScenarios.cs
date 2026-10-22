using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.Debug;

namespace Vespera.Testing;

/// <summary>
/// Cenários do tester da frente B5 (biblioteca de objetos). Conferem os recortes pixel a pixel contra o objects.json
/// (pé, footprint, luz, borda), põem objetos do catálogo no mundo do jeito que a apiNotes manda (Sprite2D no y-sort +
/// corpo do footprint) e andam com o jogador de verdade em volta deles, e estressam a galeria e o mundo.
/// Os de nome "estresse:" só rodam com <c>--smoke-stress</c>.
/// </summary>
public partial class SmokeTest
{
    private const int QaObjSpeedUp = 4;
    private const byte QaOpaque = 128;
    private const int QaJoyDevice = 7;

    private static readonly string[] QaAllMaps =
        ["guild", "forge", "inn", "fish2", "crypt2", "mine", "garden", "cath", "core", "shop_arm", "shop_mag", "shop_alq", "shop_rel"];

    /// <summary>Amostra jogável: um de cada tipo que um mapa usaria, mais os suspeitos da auditoria de pixels.</summary>
    private static readonly string[][] QaSandboxBatches =
    [
        ["barrel_fish", "crate_fish", "lamp_post_common", "stall_striped", "bench_stone_flowers"],
        ["statue_angel", "gravestone_cross_flowers", "chest_closed", "tree_dead", "fence_iron"],
        ["mausoleum_small", "well_stone", "pew", "dock_lamp"],
        ["crane_mine", "pool_square", "greenhouse_gothic", "banner_stand"],
    ];

    [SmokeScenario("objetos (tester): pé, footprint, luz e borda de cada recorte batem com o desenho", order: 910)]
    private async Task QaObjectsMatchTheDrawing()
    {
        var catalog = ObjectGallery.Catalog;
        var failures = new List<string>();
        var count = 0;
        foreach (var o in catalog.Objects)
        {
            if (++count % 25 == 0)
            {
                await NextPhysicsFrame(); // não congela a janela por segundos
            }

            var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(o.File));
            if (image is null || image.IsEmpty())
            {
                failures.Add($"{o.Id}: não abriu {o.File}");
                continue;
            }

            image.Convert(Image.Format.Rgba8);
            var w = image.GetWidth();
            var h = image.GetHeight();
            var data = image.GetData();
            int Alpha(int x, int y) => data[(((y * w) + x) * 4) + 3];
            var k = o.Density;

            if (w != o.PixelWidth || h != o.PixelHeight)
            {
                failures.Add($"{o.Id}: textura {w}×{h}, catálogo {o.PixelWidth}×{o.PixelHeight}");
            }

            // Borda da textura: opaco encostado na borda = objeto cortado (o mipmap/filtro deixa uma linha dura).
            var border = 0;
            for (var x = 0; x < w; x++)
            {
                border = Math.Max(border, Math.Max(Alpha(x, 0), Alpha(x, h - 1)));
            }

            for (var y = 0; y < h; y++)
            {
                border = Math.Max(border, Math.Max(Alpha(0, y), Alpha(w - 1, y)));
            }

            if (border > 16)
            {
                failures.Add($"{o.Id}: alpha {border} na borda da textura (recorte cortado)");
            }

            var opaqueTotal = 0;
            for (var i = 3; i < data.Length; i += 4)
            {
                opaqueTotal += data[i] > QaOpaque ? 1 : 0;
            }

            if (o.Layer == ObjectLayer.Entity)
            {
                // Colunas que tocam o chão: as do footprint (o que bloqueia) ou o recorte inteiro (sem corpo).
                var (cx0, cx1) = o.Footprint is { } fc
                    ? (Math.Clamp((int)MathF.Floor(o.AnchorX + (fc.X * k)), 0, w), Math.Clamp((int)MathF.Ceiling(o.AnchorX + (fc.Right * k)), 0, w))
                    : (0, w);
                var lowY = -1;
                var lowX = -1;
                for (var y = h - 1; y >= 0 && lowY < 0; y--)
                {
                    for (var x = cx0; x < cx1; x++)
                    {
                        if (Alpha(x, y) > QaOpaque)
                        {
                            lowY = y;
                            lowX = x;
                            break;
                        }
                    }
                }

                if (lowY < 0)
                {
                    failures.Add($"{o.Id}: nada desenhado nas colunas do footprint");
                }
                else
                {
                    var gap = (o.AnchorY - lowY) / k;
                    if (gap > 4f)
                    {
                        failures.Add($"{o.Id}: pé {gap:0.0} u abaixo da base desenhada (o corpo fica fora do desenho)");
                    }

                    // Pé apoiado numa peça solta pequena (outro objeto fundido no recorte). Ilhas e cristais flutuam de propósito.
                    if (o.Kind is not ("island" or "crystal"))
                    {
                        var piece = QaComponentSize(data, w, h, lowX, lowY);
                        if (piece < 0.08f * opaqueTotal)
                        {
                            failures.Add($"{o.Id}: pé apoiado numa peça solta de {piece} px (de {opaqueTotal}); recorte fundiu outro objeto?");
                        }
                    }
                }
            }

            if (o.Footprint is { } f)
            {
                var fx0 = Math.Clamp((int)MathF.Floor(o.AnchorX + (f.X * k)), 0, w);
                var fx1 = Math.Clamp((int)MathF.Ceiling(o.AnchorX + (f.Right * k)), 0, w);
                var fy0 = Math.Clamp((int)MathF.Floor(o.AnchorY + (f.Y * k)), 0, h);
                var fy1 = Math.Clamp((int)MathF.Ceiling(o.AnchorY + (f.Bottom * k)), 0, h);
                var area = Math.Max(1, (fx1 - fx0) * (fy1 - fy0));
                var solid = 0;
                for (var y = fy0; y < fy1; y++)
                {
                    for (var x = fx0; x < fx1; x++)
                    {
                        solid += Alpha(x, y) > QaOpaque ? 1 : 0;
                    }
                }

                if (solid < 0.18f * area)
                {
                    failures.Add($"{o.Id}: footprint sobre área quase vazia ({100f * solid / area:0}% desenhado): bloqueia o nada e deixa passar pelo objeto");
                }
            }

            if (o.Light is { } l)
            {
                var lx = o.AnchorX + (l.Offset.X * k);
                var ly = o.AnchorY + (l.Offset.Y * k);
                var r = 3f * k;
                var hit = false;
                for (var y = Math.Max(0, (int)(ly - r)); y < Math.Min(h, (int)(ly + r)) && !hit; y++)
                {
                    for (var x = Math.Max(0, (int)(lx - r)); x < Math.Min(w, (int)(lx + r)); x++)
                    {
                        if (Alpha(x, y) > QaOpaque)
                        {
                            hit = true;
                            break;
                        }
                    }
                }

                if (!hit)
                {
                    failures.Add($"{o.Id}: luz em ({l.Offset.X}, {l.Offset.Y}) u fora do desenho");
                }
            }
        }

        Log.Info($"objetos (tester): {count} recortes conferidos, {failures.Count} problema(s)");
        Require(failures.Count == 0, $"{failures.Count} recorte(s) com pé/footprint/luz/borda errados:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("objetos (tester): jogador anda em volta de objetos do catálogo no mundo", order: 911)]
    private async Task QaWalkAroundCatalogObjects()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var catalog = ObjectGallery.Catalog;
        var player = FindPlayer()!;
        var failures = new List<string>();
        var notes = new List<string>();

        // Área livre para a "praça de testes" numa grade de 8 u: largura da fileira mais larga, 150 u de fundo.
        // Integração v0.1: com o caçador em 56,7 u os objetos ficaram 1,75× maiores e os mapas redesenhados não têm
        // praça vazia desse tamanho; sem área natural, a Rua da Forja vira um chão liso (fixture) só para este cenário.
        var rowWidth = QaSandboxBatches.Max(batch => batch.Sum(id => catalog[id].WorldWidth + 26f)) + 32f;
        (string Map, Vector2 Origin)? site = null;
        foreach (var id in new[] { "forge", "guild", "garden", "core", "fish2", "crypt2", "cath", "mine" })
        {
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            if (QaFindFreeArea(world.WalkArea, map, rowWidth, 150f) is { } origin)
            {
                site = (id, origin);
                break;
            }
        }

        if (site is null)
        {
            World.World.LayoutOverrides["forge"] = "res://src/Testing/Fixtures/sandbox_forge.json";
            var forge = World.World.Catalog["forge"];
            world.GoMap("forge", forge.Spawn.X, forge.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            if (QaFindFreeArea(world.WalkArea, forge, rowWidth, 150f) is { } origin)
            {
                site = ("forge", origin);
            }
        }

        Require(site is not null, $"nenhum mapa tem {rowWidth:0} × 150 u livres para a praça de testes");
        var (siteMap, siteOrigin) = site!.Value;
        Log.Info($"objetos (tester): praça de testes em {siteMap} ({siteOrigin.X:0}, {siteOrigin.Y:0})");

        // O jogador do mundo mede o mesmo que a régua do catálogo (c_sword_front: o caçador da Lâmina; o cenário anterior
        // pode ter deixado outra classe no save).
        player.ClassId = "sword";
        await Frames(2);
        var playerSprite = player.Sprite;
        var playerHeight = ObjectGallery.WorldHeightOf(playerSprite);
        Require(Math.Abs(playerHeight - catalog.PlayerHeight) < 1.5f, $"jogador no mundo mede {playerHeight:0.0} u, catálogo calibrado em {catalog.PlayerHeight} u");

        using var speed = new SpeedUp(QaObjSpeedUp);
        var spawned = new List<Node2D>();
        try
        {
            for (var b = 0; b < QaSandboxBatches.Length; b++)
            {
                foreach (var n in spawned)
                {
                    n.QueueFree();
                }

                spawned.Clear();
                var baseline = siteOrigin.Y + 120f;
                var x = siteOrigin.X + 16f;
                var placed = new List<(ObjectDef Def, Vector2 Foot, Sprite2D Sprite)>();
                foreach (var id in QaSandboxBatches[b])
                {
                    var o = catalog[id];
                    var foot = new Vector2(x + (o.WorldWidth / 2f), baseline);
                    var (node, sprite) = QaSpawnObject(world, o, foot);
                    spawned.Add(node);
                    placed.Add((o, foot, sprite));
                    x += o.WorldWidth + 26f;
                }

                await Frames(2);
                player.Teleport(new Vector2(siteOrigin.X + 8f, baseline + 22f));
                await Frames(2);
                await Shot($"objetos-mundo-lote{b + 1}");

                foreach (var (o, foot, sprite) in placed)
                {
                    var h = ObjectGallery.WorldHeightOf(sprite);
                    if (Math.Abs(h - o.WorldHeight) > 1.5f)
                    {
                        failures.Add($"{o.Id}: no mundo mede {h:0.0} u, catálogo {o.WorldHeight} u");
                    }

                    if (o.Footprint is not { } f)
                    {
                        notes.Add($"{o.Id}: sem footprint (passa por dentro)");
                        continue;
                    }

                    var body = new Rect2(foot.X + f.X, foot.Y + f.Y, f.Width, f.Height);

                    // 1) Empurrar contra o objeto pelos 4 lados: o pé (círculo de 3,5 u) nunca entra no retângulo sólido.
                    var sides = new (string Name, Vector2 Start, Vector2 Dir)[]
                    {
                        ("sul", new Vector2(body.GetCenter().X, body.End.Y + 14f), Vector2.Up),
                        ("norte", new Vector2(body.GetCenter().X, body.Position.Y - 14f), Vector2.Down),
                        ("oeste", new Vector2(body.Position.X - 14f, body.GetCenter().Y), Vector2.Right),
                        ("leste", new Vector2(body.End.X + 14f, body.GetCenter().Y), Vector2.Left),
                    };
                    foreach (var (name, start, dir) in sides)
                    {
                        player.Teleport(start);
                        await NextPhysicsFrame();
                        PressDirection(dir);
                        var inside = false;
                        for (var frame = 0; frame < 60; frame++)
                        {
                            await NextPhysicsFrame();
                            if (QaCircleInRect(player.GlobalPosition, 3.5f - 0.75f, body))
                            {
                                failures.Add($"{o.Id}: entrou no corpo pelo {name} em {player.GlobalPosition}");
                                inside = true;
                                break;
                            }
                        }

                        PressDirection(Vector2.Zero);
                        if (name == "sul" && !inside)
                        {
                            notes.Add($"{o.Id}: parou de frente a {player.GlobalPosition.Y - foot.Y:0.0} u do pé");
                        }
                    }

                    // 2) Dar a volta no objeto rente ao corpo (8 u de folga): não pode travar nem atravessar.
                    var m = 8f;
                    var loop = new[]
                    {
                        new Vector2(body.Position.X - m, body.End.Y + m),
                        new Vector2(body.Position.X - m, body.Position.Y - m),
                        new Vector2(body.GetCenter().X, body.Position.Y - m),
                        new Vector2(body.End.X + m, body.Position.Y - m),
                        new Vector2(body.End.X + m, body.End.Y + m),
                        new Vector2(body.GetCenter().X, body.End.Y + m),
                    };
                    player.Teleport(new Vector2(body.GetCenter().X, body.End.Y + m));
                    await NextPhysicsFrame();
                    for (var i = 0; i < loop.Length; i++)
                    {
                        var error = await QaWalkTo(player, loop[i], body, 4f);
                        if (error is not null)
                        {
                            failures.Add($"{o.Id}: volta em torno, trecho {i + 1}: {error}");
                            break;
                        }

                        if (i == 2)
                        {
                            Require(player.GlobalPosition.Y < sprite.GlobalPosition.Y, $"{o.Id}: atrás do objeto o pé deveria estar acima do pé do objeto");
                            await Shot($"objetos-mundo-{o.Id}-atras");
                        }
                    }

                    await Shot($"objetos-mundo-{o.Id}-frente");
                }
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
            foreach (var n in spawned)
            {
                n.QueueFree();
            }

            if (World.World.LayoutOverrides.Remove("forge"))
            {
                world.GoMap("forge", 1500, 250, instant: true);
                await WaitMapSettled(world);
            }
        }

        foreach (var note in notes)
        {
            Log.Info($"objetos (tester): {note}");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s) andando em volta dos objetos:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("objetos (tester): galeria navega pelo analógico do gamepad", order: 912)]
    private async Task QaGalleryGamepad()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        GameManager.Instance.ChangeScene(ObjectGallery.ScenePath);
        await WaitForScene("ObjectGallery", timeoutSeconds: 10);
        var gallery = Find<ObjectGallery>() ?? throw new SmokeFailure("sem ObjectGallery");
        var themes = ObjectGallery.Catalog.Themes.Keys.ToList();
        try
        {
            gallery.ShowTheme(themes[0]);
            await Frames(2);
            // Analógico segurado 0,5 s anda um tema só (não dispara a cada quadro).
            await QaJoyAxis(JoyAxis.LeftX, 1f, frames: 30);
            Require(gallery.Theme == themes[1], $"analógico → segurado deveria andar 1 tema, está em {gallery.Theme}");
            await QaJoyAxis(JoyAxis.LeftX, -1f, frames: 30);
            Require(gallery.Theme == themes[0], $"analógico ← deveria voltar a {themes[0]}, está em {gallery.Theme}");

            var paged = themes.First(t => { gallery.ShowTheme(t); return gallery.PageCount > 1; });
            gallery.ShowTheme(paged);
            await Frames(2);
            await QaJoyAxis(JoyAxis.LeftY, 1f, frames: 10);
            Require(gallery.Page == 1, $"analógico ↓ em {paged} deveria ir à página 2, está na {gallery.Page + 1}");
            await QaJoyAxis(JoyAxis.LeftY, -1f, frames: 10);
            Require(gallery.Page == 0, $"analógico ↑ em {paged} deveria voltar à página 1, está na {gallery.Page + 1}");
            await Shot("objetos-galeria-gamepad");
        }
        finally
        {
            GameManager.Instance.ChangeScene(WorldScenePath);
            await WaitForScene("World", timeoutSeconds: 10);
        }
    }

    private async Task QaJoyAxis(JoyAxis axis, float value, int frames)
    {
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = QaJoyDevice });
        await Frames(frames);
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = axis, AxisValue = 0f, Device = QaJoyDevice });
        await Frames(2);
    }

    [SmokeScenario("estresse: galeria — 600 trocas de tema e página por input sem vazar nós nem engasgar", order: 950)]
    private async Task QaStressGallery()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        GameManager.Instance.ChangeScene(ObjectGallery.ScenePath);
        await WaitForScene("ObjectGallery", timeoutSeconds: 10);
        var gallery = Find<ObjectGallery>() ?? throw new SmokeFailure("sem ObjectGallery");
        var themes = ObjectGallery.Catalog.Themes.Keys.ToList();

        // Aquece: visita todas as páginas uma vez (texturas no cache), volta ao começo e mede a base.
        foreach (var t in themes)
        {
            gallery.ShowTheme(t);
            for (var p = 0; p < gallery.PageCount; p++)
            {
                gallery.ShowPage(p);
                await Frames(1);
            }
        }

        gallery.ShowTheme(themes[0]);
        await Frames(10);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var objects0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);

        var frames = new List<double>(4096);
        var measuring = true;
        var meter = QaMeasureFrames(frames, () => measuring);
        var rng = new Random(5);
        StringName[] actions = [InputActions.MoveRight, InputActions.MoveLeft, InputActions.MoveDown, InputActions.MoveUp];
        for (var i = 0; i < 600; i++)
        {
            await Tap(actions[rng.Next(actions.Length)]);
        }

        measuring = false;
        await meter;

        // Parado na página mais cheia: tempo de quadro sem troca.
        var busiest = themes.OrderByDescending(t => ObjectGallery.Catalog.InTheme(t).Count).First();
        gallery.ShowTheme(busiest);
        await Frames(10);
        var idle = new List<double>(512);
        measuring = true;
        meter = QaMeasureFrames(idle, () => measuring);
        await WaitSeconds(3);
        measuring = false;
        await meter;

        gallery.ShowTheme(themes[0]);
        await Frames(10);
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var objects1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var (avg, p95, max) = QaFrameStats(frames);
        var (iavg, ip95, imax) = QaFrameStats(idle);
        Log.Info($"estresse galeria: trocando {frames.Count} quadros média {avg:0.0} ms p95 {p95:0.0} ms máx {max:0.0} ms · " +
                 $"parado em {busiest} média {iavg:0.0} ms p95 {ip95:0.0} ms máx {imax:0.0} ms · nós {nodes0}→{nodes1} objetos {objects0}→{objects1} " +
                 $"órfãos {orphans} memória {mem0 / 1048576:0.0}→{mem1 / 1048576:0.0} MB");

        GameManager.Instance.ChangeScene(WorldScenePath);
        await WaitForScene("World", timeoutSeconds: 10);

        Require(Math.Abs(nodes1 - nodes0) <= 2, $"nós cresceram {nodes0} → {nodes1} depois de 600 trocas");
        Require(objects1 - objects0 < 200, $"objetos cresceram {objects0} → {objects1} depois de 600 trocas");
        Require(orphans == 0, $"{orphans} nós órfãos");
        Require(mem1 - mem0 < 32 * 1048576, $"memória cresceu {(mem1 - mem0) / 1048576:0.0} MB");
        Require(ip95 <= 20, $"p95 parado na galeria {ip95:0.0} ms (> 20 ms)");
        // Troca de página é ação de depuração: tolera 2 quadros, não 4 (cada troca recria ~100 nós e rótulos).
        Require(p95 <= 33, $"p95 trocando de página {p95:0.0} ms (> 33 ms): cada troca engasga");
    }

    [SmokeScenario("estresse: bot anda 5 min pelos 13 mapas e tenta alcançar cada ponto de interesse", order: 951)]
    private async Task QaStressBotReachEverything()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var unreachable = new List<string>();
        var stuck = new List<string>();
        var reached = 0;
        var rng = new Random(77);
        var physicsStart = Engine.GetPhysicsFrames();
        var gameSecondsPerMap = 26.0;
        using var speed = new SpeedUp(QaObjSpeedUp);
        foreach (var id in QaAllMaps)
        {
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            var mapStart = Engine.GetPhysicsFrames();

            var pois = new List<(string Label, MapRect Target)>();
            foreach (var it in map.Interactables)
            {
                var r = Math.Max(4f, it.Radius - 4f);
                pois.Add(($"interagível {it.Id}", new MapRect(it.X - r, it.Y - r, 2 * r, 2 * r)));
            }

            foreach (var e in map.Exits)
            {
                pois.Add(($"saída→{e.To}", e.Area));
            }

            if (map.Door is { } door)
            {
                pois.Add(("porta", door));
            }

            if (map.Back is { } back)
            {
                pois.Add(("fuga", back));
            }

            foreach (var (label, target) in pois)
            {
                var from = player.GlobalPosition;
                var path = WalkPathfinder.FindPathInto(world.WalkArea, new MapPoint(from.X, from.Y), target);
                if (path is null)
                {
                    // Tenta da chegada padrão antes de declarar inalcançável.
                    player.Teleport(new Vector2(map.Spawn.X, map.Spawn.Y));
                    await NextPhysicsFrame();
                    path = WalkPathfinder.FindPathInto(world.WalkArea, map.Spawn, target);
                }

                if (path is null)
                {
                    unreachable.Add($"{id} {label} [{target.X},{target.Y},{target.Width},{target.Height}]");
                    continue;
                }

                var loads = world.MapLoads;
                var escapes = 0;
                void OnEscape(string _) => escapes++;
                EventBus.Instance.DungeonEscapeRequested += OnEscape;
                try
                {
                    var result = await QaFollowPath(world, player, path,
                        () => world.MapLoads > loads || escapes > 0 || target.Contains(player.GlobalPosition.X, player.GlobalPosition.Y), path.Count * 4f / 78f * 2f + 3f);
                    if (result is not null)
                    {
                        stuck.Add($"{id} {label}: {result}");
                    }
                    else
                    {
                        reached++;
                    }
                }
                finally
                {
                    EventBus.Instance.DungeonEscapeRequested -= OnEscape;
                }

                if (world.MapId != id || world.IsTransitioning)
                {
                    await WaitMapSettled(world);
                    world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                    await WaitMapSettled(world);
                }
            }

            // Passeio aleatório até completar o tempo de jogo do mapa.
            var walks = 0;
            while ((Engine.GetPhysicsFrames() - mapStart) / 60.0 < gameSecondsPerMap && walks < 60)
            {
                walks++;
                var bounds = new Vector2(map.ImageWidth, map.ImageHeight);
                Vector2 goal = default;
                var found = false;
                for (var tries = 0; tries < 50 && !found; tries++)
                {
                    goal = new Vector2((float)rng.NextDouble() * bounds.X, (float)rng.NextDouble() * bounds.Y);
                    found = world.WalkArea.CanStand(goal.X, goal.Y) && map.FindExit(goal.X, goal.Y) is null
                        && !(map.Back is { } bk && bk.Contains(goal.X, goal.Y));
                }

                if (!found)
                {
                    break;
                }

                var from = player.GlobalPosition;
                var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(from.X, from.Y), new MapPoint(goal.X, goal.Y), 4f);
                if (path is null)
                {
                    continue; // ilha sem caminho (área separada): não é ponto de interesse
                }

                var loads = world.MapLoads;
                var result = await QaFollowPath(world, player, path, () => player.GlobalPosition.DistanceTo(goal) < 6f || world.MapLoads > loads, path.Count * 4f / 78f * 2f + 3f);
                if (result is not null)
                {
                    stuck.Add($"{id} passeio até ({goal.X:0},{goal.Y:0}): {result}");
                }

                if (world.MapId != id || world.IsTransitioning)
                {
                    await WaitMapSettled(world);
                    world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                    await WaitMapSettled(world);
                }
            }

            Log.Info($"estresse bot: {id} {(Engine.GetPhysicsFrames() - mapStart) / 60.0:0} s de jogo, {pois.Count} pontos, {walks} passeios");
        }

        var gameSeconds = (Engine.GetPhysicsFrames() - physicsStart) / 60.0;
        Log.Info($"estresse bot: {gameSeconds:0} s de jogo, {reached} pontos alcançados, {unreachable.Count} inalcançáveis, {stuck.Count} travamentos");
        foreach (var u in unreachable)
        {
            Log.Info($"estresse bot: inalcançável {u}");
        }

        foreach (var s in stuck)
        {
            Log.Info($"estresse bot: travou {s}");
        }

        Require(gameSeconds >= 300, $"bot andou só {gameSeconds:0} s de jogo");
        Require(stuck.Count == 0 && unreachable.Count == 0, $"{stuck.Count} travamento(s) e {unreachable.Count} ponto(s) inalcançável(is):\n  " + string.Join("\n  ", stuck.Concat(unreachable)));
    }

    [SmokeScenario("estresse: 50 trocas de mapa seguidas e spam de interact/dodge/attack/pause", order: 952)]
    private async Task QaStressMapChangesAndSpam()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        world.GoMap("guild", 250, 560, instant: true);
        await WaitMapSettled(world);
        await Frames(10);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var loads0 = world.MapLoads;
        var frames = new List<double>(8192);
        var measuring = true;
        var meter = QaMeasureFrames(frames, () => measuring);
        var rng = new Random(9);
        for (var i = 0; i < 50; i++)
        {
            var id = QaAllMaps[rng.Next(QaAllMaps.Length)];
            if (id == world.MapId)
            {
                id = QaAllMaps[(Array.IndexOf(QaAllMaps, id) + 1) % QaAllMaps.Length];
            }

            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y);
            await WaitUntil(() => world.MapId == id, 5, $"troca {i + 1} para {id}");
            await WaitMapSettled(world);
            Require(world.Entities.GetChildCount() == 1, $"troca {i + 1}: {world.Entities.GetChildCount()} entidades (só o jogador esperado)");
            Require(world.CollisionRoot.GetChildCount() == 1, $"troca {i + 1}: {world.CollisionRoot.GetChildCount()} corpos de colisão");
        }

        measuring = false;
        await meter;
        world.GoMap("guild", 250, 560, instant: true);
        await WaitMapSettled(world);
        await Frames(10);
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var (avg, p95, max) = QaFrameStats(frames);
        Log.Info($"estresse trocas: {world.MapLoads - loads0} cargas, {frames.Count} quadros média {avg:0.0} ms p95 {p95:0.0} ms máx {max:0.0} ms · " +
                 $"nós {nodes0}→{nodes1} órfãos {orphans} memória {mem0 / 1048576:0.0}→{mem1 / 1048576:0.0} MB");
        Require(world.MapLoads - loads0 >= 51, $"só {world.MapLoads - loads0} cargas de mapa");
        Require(Math.Abs(nodes1 - nodes0) <= 2, $"nós cresceram {nodes0} → {nodes1} em 50 trocas");
        Require(orphans == 0, $"{orphans} nós órfãos depois das trocas");
        Require(mem1 - mem0 < 64 * 1048576, $"memória cresceu {(mem1 - mem0) / 1048576:0.0} MB em 50 trocas");

        // Regime: em cada mapa, 1 s parado + 2 s andando, sem troca no meio (o engasgo da carga fica de fora).
        var steady = new List<string>();
        var worstSteady = 0.0;
        foreach (var id in QaAllMaps)
        {
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            await Frames(20);
            var mapFrames = new List<double>(256);
            measuring = true;
            meter = QaMeasureFrames(mapFrames, () => measuring);
            await WaitSeconds(1);
            await QaMovesSomewhere(player);
            await QaMovesSomewhere(player);
            measuring = false;
            await meter;
            var (mavg, mp95, mmax) = QaFrameStats(mapFrames);
            worstSteady = Math.Max(worstSteady, mp95);
            steady.Add($"{id} média {mavg:0.0} p95 {mp95:0.0} máx {mmax:0.0} ms");
        }

        Log.Info("estresse regime por mapa: " + string.Join(" · ", steady));
        world.GoMap("guild", 250, 560, instant: true);
        await WaitMapSettled(world);

        // Spam de botões no meio da Guilda, perto de um interagível: nada trava, o jogador volta a andar.
        var it = world.Map.Interactables.FirstOrDefault();
        if (it is not null)
        {
            var near = world.CorrectToWalkable(new Vector2(it.X, it.Y + 8));
            player.Teleport(near);
            await Frames(2);
        }

        var requests = 0;
        void OnRequest(string kind, string id) => requests++;
        EventBus.Instance.InteractionRequested += OnRequest;
        StringName[] spam = [InputActions.Interact, InputActions.Dodge, InputActions.Attack, InputActions.AttackHeavy, InputActions.Pause];
        try
        {
            for (var i = 0; i < 400; i++)
            {
                var action = spam[rng.Next(spam.Length)];
                var pressed = rng.Next(2) == 0;
                Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
                if (pressed)
                {
                    Input.ActionPress(action);
                }
                else
                {
                    Input.ActionRelease(action);
                }

                if (i % 4 == 0)
                {
                    PressDirection(new Vector2(rng.Next(3) - 1, rng.Next(3) - 1).Normalized());
                }

                await NextPhysicsFrame();
            }
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
            ReleaseAllActions();
            PressDirection(Vector2.Zero);
        }

        await Frames(10);
        Log.Info($"estresse spam: {requests} pedidos de interação, pausado={GetTree().Paused}, bloqueado={GameManager.Instance.IsGameplayBlocked}");
        Require(!GetTree().Paused, "a árvore ficou pausada depois do spam");
        Require(!GameManager.Instance.IsGameplayBlocked, "o gameplay ficou bloqueado depois do spam");
        var moved = await QaMovesSomewhere(player);
        Require(moved, "depois do spam o jogador não anda para nenhum lado");
        Require(max < 1000, $"engasgo de {max:0} ms numa troca de mapa");
        Require(worstSteady <= 20, $"p95 em regime {worstSteady:0.0} ms (> 20 ms): " + string.Join(" · ", steady));
    }

    // --- utilidades do tester B5 ---

    /// <summary>Põe um objeto do catálogo no mundo como a apiNotes da B5 manda: nó no pé (y-sort), Sprite2D com offset = -âncora e escala 1/densidade, corpo do footprint na camada "world".</summary>
    private static (Node2D Node, Sprite2D Sprite) QaSpawnObject(World.World world, ObjectDef o, Vector2 foot)
    {
        var node = new Node2D { Name = $"Qa_{o.Id}", Position = foot };
        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(o.File),
            Centered = false,
            Offset = new Vector2(-o.AnchorX, -o.AnchorY),
            Scale = Vector2.One / o.Density,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        node.AddChild(sprite);
        if (o.Footprint is { } f)
        {
            var body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
            body.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(f.Width, f.Height) },
                Position = new Vector2(f.X + (f.Width / 2f), f.Y + (f.Height / 2f)),
            });
            node.AddChild(body);
        }

        world.Entities.AddChild(node);
        return (node, sprite);
    }

    private static Vector2? QaFindFreeArea(IWalkArea area, MapDef map, float width, float height)
    {
        for (var y = 16f; y + height < map.ImageHeight - 16; y += 16)
        {
            for (var x = 16f; x + width < map.ImageWidth - 16; x += 16)
            {
                var ok = true;
                for (var dy = 0f; dy <= height && ok; dy += 8)
                {
                    for (var dx = 0f; dx <= width && ok; dx += 8)
                    {
                        ok = area.CanStand(x + dx, y + dy) && map.FindExit(x + dx, y + dy) is null && !(map.Back is { } b && b.Contains(x + dx, y + dy));
                    }
                }

                if (ok)
                {
                    return new Vector2(x, y);
                }
            }
        }

        return null;
    }

    private static bool QaCircleInRect(Vector2 c, float r, Rect2 rect)
    {
        var nx = Math.Clamp(c.X, rect.Position.X, rect.End.X);
        var ny = Math.Clamp(c.Y, rect.Position.Y, rect.End.Y);
        return ((c.X - nx) * (c.X - nx)) + ((c.Y - ny) * (c.Y - ny)) < r * r;
    }

    /// <summary>Anda em linha reta até o ponto com input; erro se entrar no corpo ou se ficar 1 s de jogo sem andar 1 u.</summary>
    private async Task<string?> QaWalkTo(Player.Player player, Vector2 target, Rect2 body, float reach)
    {
        var window = new Queue<Vector2>();
        for (var frame = 0; frame < 300; frame++)
        {
            var p = player.GlobalPosition;
            if (p.DistanceTo(target) < reach)
            {
                PressDirection(Vector2.Zero);
                return null;
            }

            if (QaCircleInRect(p, 3.5f - 0.75f, body))
            {
                PressDirection(Vector2.Zero);
                return $"entrou no corpo em {p}";
            }

            window.Enqueue(p);
            if (window.Count > 60)
            {
                var old = window.Dequeue();
                if (old.DistanceTo(p) < 1f)
                {
                    PressDirection(Vector2.Zero);
                    return $"travado em {p} indo para {target}";
                }
            }

            PressDirection((target - p).Normalized());
            await NextPhysicsFrame();
        }

        PressDirection(Vector2.Zero);
        return $"não chegou em {target} em 5 s (parou em {player.GlobalPosition})";
    }

    /// <summary>Segue um caminho do Core com input. Erro se travar (input ativo e &lt; 1 u em 1 s de jogo) ou estourar o tempo.</summary>
    private async Task<string?> QaFollowPath(World.World world, Player.Player player, IReadOnlyList<MapPoint> path, Func<bool> done, float timeoutGameSeconds)
    {
        var mapId = world.MapId;
        var next = 1;
        var window = new Queue<Vector2>();
        var maxFrames = (int)(timeoutGameSeconds * 60);
        try
        {
            for (var frame = 0; !done(); frame++)
            {
                if (world.MapId != mapId || world.IsTransitioning)
                {
                    return null;
                }

                var p = player.GlobalPosition;
                if (frame > maxFrames)
                {
                    return $"tempo esgotado em ({p.X:0},{p.Y:0})";
                }

                window.Enqueue(p);
                if (window.Count > 60 && window.Dequeue().DistanceTo(p) < 1f)
                {
                    return $"travado em ({p.X:0},{p.Y:0}) com input ativo (próximo ponto {(next < path.Count ? path[next] : path[^1])})";
                }

                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                var aim = next < path.Count ? path[next] : path[^1];
                var d = new Vector2(aim.X, aim.Y) - p;
                PressDirection(d.Length() > 0.01f ? d.Normalized() : Vector2.Down);
                await NextPhysicsFrame();
            }

            return null;
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }
    }

    private async Task<bool> QaMovesSomewhere(Player.Player player)
    {
        foreach (var d in new[] { Vector2.Down, Vector2.Up, Vector2.Left, Vector2.Right })
        {
            var before = player.GlobalPosition;
            PressDirection(d);
            await Frames(30);
            PressDirection(Vector2.Zero);
            if (player.GlobalPosition.DistanceTo(before) > 5f)
            {
                return true;
            }
        }

        return false;
    }

    private async Task QaMeasureFrames(List<double> into, Func<bool> keepGoing)
    {
        var last = Time.GetTicksUsec();
        while (keepGoing())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            into.Add((now - last) / 1000.0);
            last = now;
        }
    }

    private static (double Avg, double P95, double Max) QaFrameStats(List<double> frames)
    {
        if (frames.Count < 3)
        {
            return (0, 0, 0);
        }

        var sorted = frames.Skip(1).OrderBy(v => v).ToList();
        return (sorted.Average(), sorted[(int)(sorted.Count * 0.95)], sorted[^1]);
    }

    private static int QaComponentSize(byte[] data, int w, int h, int sx, int sy)
    {
        var seen = new bool[w * h];
        var stack = new Stack<int>();
        stack.Push((sy * w) + sx);
        seen[(sy * w) + sx] = true;
        var size = 0;
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            size++;
            var x = i % w;
            var y = i / w;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    {
                        continue;
                    }

                    var j = (ny * w) + nx;
                    if (!seen[j] && data[(j * 4) + 3] > QaOpaque)
                    {
                        seen[j] = true;
                        stack.Push(j);
                    }
                }
            }
        }

        return size;
    }
}
