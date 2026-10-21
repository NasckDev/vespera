using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// Cenários do tester da frente B2 (cidade viva): escuridão por hora em rua e interiores (com prints de madrugada e
/// interiores), 5 min de rotina dos NPCs andados no mapa real, pausa congelando timers e moradores, portal que já
/// está aberto ao entrar na Rua da Forja, balões de chat (fora da tela e com o autor certo) e custo da luz com a
/// cidade cheia.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("cidade (tester): escuridão por hora na rua e nos interiores", order: 65)]
    private async Task CityTesterDarknessByHour()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 16 * 60 + 30);
        try
        {
            city.TimeScale = 0f; // o relógio só muda pelo teste
            var catalog = World.World.Catalog;

            // spec 01 §4.5 (tabela) + D39 (sem o salto das 22h): base 0,16 até 17h; 0,50 às 19h; 0,62 da 22h às 05h;
            // 0,56 às 05:30. Rua ×0,6; Guilda/Estalagem ×0,55 + 0,14; lojas ×0,45 + 0,12.
            var cases = new (string Map, double Clock, float Expected, string Shot)[]
            {
                ("forge", 19 * 60 + 30, 0.6f * (0.50f + (0.12f * 0.5f / 3f)), "t-forja-1930"),
                ("forge", 29 * 60 + 30, 0.6f * 0.56f, "t-forja-madrugada-0530"),
                ("inn", 16 * 60 + 30, (0.16f * 0.55f) + 0.14f, "t-estalagem-dia"),
                ("inn", 26 * 60, (0.62f * 0.55f) + 0.14f, "t-estalagem-noite"),
                ("shop_arm", 26 * 60, (0.62f * 0.45f) + 0.12f, "t-loja-noite"),
                ("guild", 29 * 60 + 30, (0.56f * 0.55f) + 0.14f, "t-guilda-madrugada"),
            };

            foreach (var (mapId, clock, expected, shot) in cases)
            {
                GameManager.Instance.State.Save.ClockMinutes = clock;
                var map = catalog[mapId];
                var at = mapId == "forge" ? new Vector2(800, 520) : new Vector2(map.Spawn.X, map.Spawn.Y);
                if (world.MapId != mapId)
                {
                    world.GoMap(mapId, at, instant: true);
                    await WaitMapSettled(world);
                }

                await ProcessFrames(3); // com janela, vários frames de física podem passar sem nenhum _Process
                var dark = city.Lighting.CurrentDarkness;
                Require(Math.Abs(dark - expected) < 0.005f, $"{mapId} às {SeasonCalendarFormat(clock)}: escuridão {dark:0.000}, esperado {expected:0.000}");
                Require(city.Lighting.CurrentLightCount >= 2, $"{mapId}: só {city.Lighting.CurrentLightCount} luz(es) na tela (luzes do mapa + lanterna)");
                await Shot(shot);
            }
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): 5 min de rotina dos NPCs na Rua da Forja sem travar nem atravessar parede", order: 66)]
    private async Task CityTesterNpcLongRun()
    {
        var (world, city) = await StartCity("forge", new Vector2(1500, 250), 17 * 60 + 30);
        try
        {
            city.TimeScale = 0f; // relógio parado às 17:30: Lauro, Marta e Faísca ficam no mapa
            var town = city.Townsfolk;
            await WaitUntil(() => town.Npcs.Count == 3 && town.Npcs.All(n => n.Walker is not null), 15, "Lauro, Marta e Faísca prontos");
            var area = world.WalkArea;
            var random = GameManager.Instance.State.Random;
            const double dt = 1.0 / 60.0;
            const int steps = 300 * 60; // 5 min de jogo, passo de 1/60 s

            var problems = new List<string>();
            foreach (var npc in town.Npcs)
            {
                var walker = npc.Walker!;
                var legs = new HashSet<int>();
                var laps = 0;
                var prevLeg = walker.Leg;
                var last = new Vector2(walker.X, walker.Y);
                double stuck = 0, worstStuck = 0, movingTime = 0, distance = 0;
                var stuckAt = Vector2.Zero;
                var stuckLeg = -1;
                for (var i = 0; i < steps; i++)
                {
                    var wasMoving = walker.IsMoving;
                    npc.Step(area, dt, random);
                    var p = new Vector2(walker.X, walker.Y);
                    Require(area.CanStand(p.X, p.Y, WalkMover.NpcRadius), $"{npc.Def.Id} entrou em parede em {p} (t={i * dt:0.0}s)");
                    legs.Add(walker.Leg);
                    if (walker.Leg != prevLeg)
                    {
                        laps += walker.Leg == 0 ? 1 : 0;
                        prevLeg = walker.Leg;
                    }

                    var moved = p.DistanceTo(last);
                    if (wasMoving && walker.IsMoving)
                    {
                        movingTime += dt;
                        distance += moved;
                        stuck = moved < 0.01f ? stuck + dt : 0;
                        if (stuck > worstStuck)
                        {
                            worstStuck = stuck;
                            stuckAt = p;
                            stuckLeg = walker.Leg;
                        }
                    }
                    else
                    {
                        stuck = 0;
                    }

                    last = p;
                }

                var speed = movingTime > 0 ? distance / movingTime : 0;
                Log.Info($"cidade (tester): {npc.Def.Id} em 300 s: {laps} volta(s), trechos {legs.Count}/{walker.Route.Waypoints.Count}, andando {movingTime:0} s a {speed:0.0} u/s (v2: {npc.Def.Speed}), maior trava {worstStuck:0.00} s em ({stuckAt.X:0.000}, {stuckAt.Y:0.000}) no trecho {stuckLeg}");
                if (worstStuck >= 1.0)
                {
                    problems.Add($"{npc.Def.Id} ficou {worstStuck:0} s andando sem sair do lugar em ({stuckAt.X:0.000}, {stuckAt.Y:0.000}), trecho {stuckLeg}");
                }

                if (legs.Count < walker.Route.Waypoints.Count || laps < 2)
                {
                    problems.Add($"{npc.Def.Id}: {laps} volta(s), {legs.Count}/{walker.Route.Waypoints.Count} trechos em 5 min");
                }

                if (speed < npc.Def.Speed * 0.85)
                {
                    problems.Add($"{npc.Def.Id}: velocidade média {speed:0.0} u/s, a v2 anda a {npc.Def.Speed}");
                }
            }

            // Caçadores ambiente: 5 min de passeio pelo mapa real.
            foreach (var hunter in town.Hunters)
            {
                var brain = hunter.Brain;
                var arrivals = 0;
                var waiting = brain.Wait > 0;
                for (var i = 0; i < steps; i++)
                {
                    hunter.Step(area, world.Map.ImageWidth, world.Map.ImageHeight, dt);
                    Require(area.CanStand(brain.X, brain.Y, WalkMover.NpcRadius), $"caçador {brain.Def.Name} entrou em parede em ({brain.X:0}, {brain.Y:0})");
                    if (!waiting && brain.Wait > 0)
                    {
                        arrivals++;
                    }

                    waiting = brain.Wait > 0;
                }

                Log.Info($"cidade (tester): caçador {brain.Def.Name} em 300 s: {arrivals} paradas");
                if (arrivals < 5)
                {
                    problems.Add($"caçador {brain.Def.Name}: só {arrivals} paradas em 5 min");
                }
            }

            // Depois dos 5 min, o jogo de verdade (o diretor da cidade, frame a frame) anda com eles por 3 s.
            var player = FindPlayer()!;
            var before = town.Npcs.ToDictionary(n => n.Def.Id, n => n.Position);
            await WaitSeconds(3.0);
            foreach (var npc in town.Npcs)
            {
                var d = npc.Position.DistanceTo(before[npc.Def.Id]);
                Log.Info($"cidade (tester): {npc.Def.Id} andou {d:0.0} u em 3 s de jogo real (andando={npc.Walker!.IsMoving})");
                if (npc.Walker.IsMoving && d < 0.5f)
                {
                    problems.Add($"{npc.Def.Id} continua travado no jogo real em ({npc.Position.X:0}, {npc.Position.Y:0})");
                }
            }

            var shown = town.Npcs.FirstOrDefault(n => n.Walker!.IsMoving) ?? town.Npcs[0];
            player.Teleport(world.CorrectToWalkable(shown.Position + new Vector2(0, 40)));
            world.Camera.Snap();
            await Frames(3);
            await Shot("t-npc-travado");
            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): pausa congela timers das Brechas, NPCs e caçadores", order: 67)]
    private async Task CityTesterPauseFreezesEverything()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60);
        var bus = EventBus.Instance;
        try
        {
            var town = city.Townsfolk;
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount > 0, 3, "uma Brecha aberta");
            await WaitUntil(() => town.Npcs.Count > 0 && town.Npcs.All(n => n.Walker is not null), 15, "NPCs prontos");
            await WaitSeconds(0.5);
            var b = city.Brechas.Brechas.First(x => x.IsOpen);

            foreach (var freeze in new[] { "pausa", "tela" })
            {
                if (freeze == "pausa")
                {
                    bus.EmitSignal(EventBus.SignalName.PauseChanged, true);
                }
                else
                {
                    GameManager.Instance.BlockGameplay("smoke-dialogo");
                }

                await Frames(2);
                var timeLeft = b.TimeLeft;
                var next = city.Brechas.NextOpenIn;
                var clock = GameManager.Instance.State.Save.ClockMinutes;
                var npcs = town.Npcs.Select(n => n.Position).ToArray();
                var hunters = town.Hunters.Select(h => h.Position).ToArray();
                await WaitSeconds(1.0);
                Require(b.TimeLeft == timeLeft, $"{freeze}: timer da Brecha andou ({timeLeft:0.00} → {b.TimeLeft:0.00})");
                Require(city.Brechas.NextOpenIn == next, $"{freeze}: contagem da próxima Brecha andou");
                Require(GameManager.Instance.State.Save.ClockMinutes == clock, $"{freeze}: relógio andou");
                Require(town.Npcs.Select(n => n.Position).SequenceEqual(npcs), $"{freeze}: NPC andou com o jogo congelado");
                Require(town.Hunters.Select(h => h.Position).SequenceEqual(hunters), $"{freeze}: caçador andou com o jogo congelado");

                if (freeze == "pausa")
                {
                    bus.EmitSignal(EventBus.SignalName.PauseChanged, false);
                }
                else
                {
                    GameManager.Instance.UnblockGameplay("smoke-dialogo");
                }

                await WaitSeconds(0.5);
                Require(b.TimeLeft < timeLeft, $"{freeze}: ao voltar, o timer da Brecha deveria correr de novo");
            }
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): Brecha aberta longe da rua aparece já aberta ao chegar e não duplica", order: 68)]
    private async Task CityTesterPortalSeenLater()
    {
        var catalog = World.World.Catalog;
        var guildSpawn = catalog["guild"].Spawn;
        var (world, city) = await StartCity("guild", new Vector2(guildSpawn.X, guildSpawn.Y), 17 * 60);
        var shakes = new List<float>();
        var toasts = new List<string>();
        void OnShake(float a) => shakes.Add(a);
        void OnToast(string t) => toasts.Add(t);
        var bus = EventBus.Instance;
        bus.CameraShakeRequested += OnShake;
        bus.ToastRequested += OnToast;
        try
        {
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount > 0, 3, "Brecha aberta com o jogador na Guilda");
            var b = city.Brechas.Brechas.First(x => x.IsOpen);
            Require(toasts.Any(t => t.StartsWith("Uma Brecha se abriu", StringComparison.Ordinal)), "o toast da abertura aparece mesmo longe da rua");
            Require(shakes.Count == 0, $"longe da rua a câmera não treme ({string.Join(",", shakes)})");
            Require(FindAll<PortalVisual>().Count == 0, "nenhum portal desenhado na Guilda");

            for (var visit = 0; visit < 2; visit++)
            {
                world.GoMap("forge", 1500, 250, instant: true);
                await WaitMapSettled(world);
                await Frames(3);
                var visuals = FindAll<PortalVisual>().Where(v => !v.IsQueuedForDeletion()).ToList();
                Require(visuals.Count == 1, $"visita {visit + 1}: {visuals.Count} portais na Rua da Forja (esperado 1)");
                Require(!visuals[0].IsOpening && visuals[0].Frame is 3 or 4, $"visita {visit + 1}: portal visto depois deveria já estar no loop (quadro {visuals[0].Frame})");
                var portal = world.GetInteractable($"portal{b.Spot}")!;
                Require(portal.Enabled && portal.Prompt == $"Brecha · {b.Bairro}", $"visita {visit + 1}: portal{b.Spot} ligado={portal.Enabled}, rótulo \"{portal.Prompt}\"");
                world.GoMap("guild", guildSpawn.X, guildSpawn.Y, instant: true);
                await WaitMapSettled(world);
            }
        }
        finally
        {
            bus.CameraShakeRequested -= OnShake;
            bus.ToastRequested -= OnToast;
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): balão de caçador fora da tela não aparece colado na borda", order: 69)]
    private async Task CityTesterOffscreenBubble()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60);
        try
        {
            city.TimeScale = 0f;
            var town = city.Townsfolk;
            await WaitUntil(() => town.Hunters.Count > 0, 5, "caçador ambiente na Rua da Forja");
            var hunter = town.Hunters[0];
            var player = FindPlayer()!;
            var view = GetViewport().GetVisibleRect().Size;

            // Põe o jogador ~450 u ao lado do caçador (mesma altura): o caçador fica fora da tela na horizontal.
            var offscreen = false;
            foreach (var side in new[] { 450f, -450f, 600f, -600f })
            {
                var target = hunter.Position + new Vector2(side, 0);
                if (target.X < 20 || target.X > world.Map.ImageWidth - 20)
                {
                    continue;
                }

                player.Teleport(world.CorrectToWalkable(target));
                world.Camera.Snap();
                await Frames(2);
                var sp = WorldLabels.ToScreen(hunter, hunter.GlobalPosition);
                if ((sp.X < -30 || sp.X > view.X + 30) && sp.Y > 40 && sp.Y < view.Y - 10)
                {
                    offscreen = true;
                    break;
                }
            }

            Require(offscreen, "não consegui deixar o caçador fora da tela na horizontal");
            const string text = "teste: falando fora da tela";
            town.Chat(hunter.Brain.Def.Name, hunter.Brain.Def.Rank, text);
            await Frames(3);
            var bubble = city.Labels.GetChildren().OfType<Label>().FirstOrDefault(l => l.Visible && l.Text == text);
            await Shot("t-balao-fora-da-tela");
            var onScreen = bubble is not null && new Rect2(Vector2.Zero, view).Intersects(new Rect2(bubble.Position, bubble.Size));
            var hunterScreen = WorldLabels.ToScreen(hunter, hunter.GlobalPosition);
            Require(!onScreen, $"balão de {hunter.Brain.Def.Name} aparece na tela em {bubble?.Position} sem o caçador (caçador na tela em {hunterScreen})");
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): fala da Lúcia e da Iara não sai na boca de outro caçador", order: 70)]
    private async Task CityTesterChatSpeaker()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60);
        try
        {
            city.TimeScale = 0f;
            var town = city.Townsfolk;
            await WaitUntil(() => town.Hunters.Count > 0, 5, "caçador ambiente na Rua da Forja");
            await WaitUntil(() => town.Hunters.All(h => h.Bubble is null), 5, "caçadores calados");
            town.Chat("Lúcia", "—", TownChat.RuptureLuciaLine);
            await Frames(2);
            var wrong = town.Hunters.FirstOrDefault(h => h.Bubble == TownChat.RuptureLuciaLine);
            Require(wrong is null, $"a fala da Lúcia (estalajadeira) aparece no balão do caçador {wrong?.Brain.Def.Name} {wrong?.Brain.Def.Rank}");

            var names = town.Hunters.Select(h => h.Brain.Def.Name).ToHashSet();
            if (!names.Contains("Iara"))
            {
                town.Chat("Iara", "B", TownChat.RuptureLine);
                await Frames(2);
                wrong = town.Hunters.FirstOrDefault(h => h.Bubble == TownChat.RuptureLine);
                Require(wrong is null, $"a fala da Iara aparece no balão do caçador {wrong?.Brain.Def.Name}");
            }
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (tester): custo da luz, do clima e dos portais com a cidade cheia", order: 71)]
    private async Task CityTesterLightingCost()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 21 * 60);
        try
        {
            var town = city.Townsfolk;
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount == 1, 3, "1ª Brecha");
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount == 2, 3, "2ª Brecha");
            await WaitUntil(() => town.Npcs.Count > 0 && town.Npcs.All(n => n.Walker is not null), 15, "NPCs prontos");
            city.TimeScale = 0f;
            var player = FindPlayer()!;
            player.Teleport(world.CorrectToWalkable(new Vector2(800, 520)));
            await WaitSeconds(1.2); // portais terminam de abrir

            // Alocação por frame de processo com 2 portais, NPCs, caçadores, luzes e clima.
            var before = GC.GetTotalAllocatedBytes(precise: true);
            var frames0 = Engine.GetProcessFrames();
            await WaitSeconds(2.0);
            var perFrame = (GC.GetTotalAllocatedBytes(precise: true) - before) / (long)Math.Max(1, Engine.GetProcessFrames() - frames0);
            Log.Info($"cidade (tester): {perFrame} B/frame com 2 portais, {town.Npcs.Count} NPCs, {town.Hunters.Count} caçadores, {city.Lighting.CurrentLightCount} luzes");
            Require(perFrame < 2048, $"cidade cheia aloca {perFrame} B por frame");

            if (DisplayServer.GetName() == "headless")
            {
                return; // custo de GPU só com janela
            }

            var darkness = city.Lighting.GetNode<CanvasItem>("Darkness");
            var vsync = DisplayServer.WindowGetVsyncMode();
            var maxFps = Engine.MaxFps;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
            try
            {
                var results = new List<string>();
                double worstExtra = 0;
                foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
                {
                    DisplayServer.WindowSetSize(size);
                    await WaitSeconds(0.5);
                    var on = await MeasureFrameMs(2.0);
                    darkness.Visible = false;
                    await WaitSeconds(0.3);
                    var noDark = await MeasureFrameMs(2.0);
                    city.Lighting.Weather.Visible = false;
                    await WaitSeconds(0.3);
                    var off = await MeasureFrameMs(2.0);
                    darkness.Visible = true;
                    city.Lighting.Weather.Visible = true;
                    worstExtra = Math.Max(worstExtra, on - off);
                    results.Add($"{DisplayServer.WindowGetSize()}: {on:0.00} ms com luz+clima ({1000 / on:0} fps), {noDark:0.00} ms sem a escuridão, {off:0.00} ms sem nada ({1000 / off:0} fps)");
                }

                // Brechas com 60 luzes no lights.json (Catedral, Estufa, Minas), em 1080p.
                var cityLights = city.Lighting.CurrentLightCount;
                DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
                foreach (var dungeon in new[] { "cath", "garden", "mine" })
                {
                    var spawn = World.World.Catalog[dungeon].Spawn;
                    world.GoMap(dungeon, spawn.X, spawn.Y, instant: true);
                    await WaitMapSettled(world);
                    await WaitSeconds(0.5);
                    var on = await MeasureFrameMs(2.0);
                    var lights = city.Lighting.CurrentLightCount;
                    darkness.Visible = false;
                    await WaitSeconds(0.3);
                    var off = await MeasureFrameMs(2.0);
                    darkness.Visible = true;
                    worstExtra = Math.Max(worstExtra, on - off);
                    results.Add($"{dungeon} 1080p com {lights} luzes: {on:0.00} ms com a escuridão, {off:0.00} ms sem");
                }

                DisplayServer.WindowSetSize(new Vector2I(1280, 720));
                Log.Info($"cidade (tester): GPU {RenderingServer.GetVideoAdapterName()}; {cityLights} luzes na rua; {string.Join(" | ", results)}");
                Require(worstExtra < 4.0, $"luz + clima custam {worstExtra:0.00} ms por frame");
            }
            finally
            {
                darkness.Visible = true;
                city.Lighting.Weather.Visible = true;
                DisplayServer.WindowSetVsyncMode(vsync);
                Engine.MaxFps = maxFps;
            }
        }
        finally
        {
            StopCity(city);
        }
    }

    private async Task ProcessFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task<double> MeasureFrameMs(double seconds)
    {
        var frames0 = Engine.GetProcessFrames();
        var t0 = Time.GetTicksUsec();
        await WaitSeconds(seconds);
        var frames = Math.Max(1, Engine.GetProcessFrames() - frames0);
        return (Time.GetTicksUsec() - t0) / 1000.0 / frames;
    }

    private static string SeasonCalendarFormat(double clock) => SeasonCalendar.FormatClock(clock);
}
