using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// QA de experiência da frente B2 (cidade viva), rodada 1: o jogador anda de verdade (input) entre os moradores no
/// jogo real (diretor da cidade frame a frame), fala com cada NPC andando até ele, alcança os dois portais, olha a
/// cidade em várias horas e confere que nomes e balões nunca aparecem soltos. Os cenários "estresse:" (5 min
/// acelerados, 50 trocas de mapa, spam de input) só rodam com <c>--smoke-stress</c>.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("cidade (qa): nome de morador nunca aparece solto quando ele chega ao mapa", order: 72)]
    private async Task QaNamesNeverFloat()
    {
        // 16:30: Lauro (17h–27h) ainda não chegou. Às 17:00 ele entra no mapa com o jogador olhando (sem fade).
        var (world, city) = await StartCity("forge", new Vector2(600, 520), 16 * 60 + 30);
        try
        {
            var town = city.Townsfolk;
            await WaitUntil(() => town.Npcs.Count == 2 && town.Npcs.All(n => n.Walker is not null), 15, "Marta e Faísca prontas");
            await WaitUntil(() => world.FadeAlpha <= 0f, 3, "fade terminado");
            var problems = new List<string>();
            GameManager.Instance.State.Save.ClockMinutes = (17 * 60) - 0.2;
            var lauroSeen = false;
            for (var frame = 0; frame < 90; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // estado do último frame desenhado
                lauroSeen |= town.Npcs.Any(n => n.Def.Id == "lauro");
                CheckFloatingLabels(world, city, problems, $"frame {frame} depois das 17:00");
            }

            Require(lauroSeen, "Lauro deveria entrar no mapa às 17:00");

            // Troca de mapa sem fade instantâneo: na volta, nomes dos NPCs e dos caçadores também não podem piscar soltos.
            foreach (var (mapId, at) in new[] { ("guild", new Vector2(250, 330)), ("forge", new Vector2(800, 520)) })
            {
                world.GoMap(mapId, at);
                for (var frame = 0; frame < 120; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (world.FadeAlpha < 0.5f)
                    {
                        CheckFloatingLabels(world, city, problems, $"{mapId}, frame {frame} da troca (fade {world.FadeAlpha:0.00})");
                    }
                }
            }

            foreach (var p in problems.Take(10))
            {
                Log.Info($"cidade (qa): {p}");
            }

            Require(problems.Count == 0, $"{problems.Count} rótulo(s) soltos na tela; primeiro: {problems.FirstOrDefault()}");
        }
        finally
        {
            StopCity(city);
        }
    }

    /// <summary>Todo nome visível na tela tem dono visível e fica a poucos px da cabeça dele.</summary>
    private static void CheckFloatingLabels(World.World world, CityLife city, List<string> problems, string when)
    {
        var view = world.GetViewportRect().Size;
        foreach (var node in city.Labels.GetChildren())
        {
            if (node is not Label { Visible: true } label || label.Modulate.A < 0.05f || label.Text.Length == 0)
            {
                continue;
            }

            if (!new Rect2(Vector2.Zero, view).Intersects(new Rect2(label.Position, label.Size)))
            {
                continue;
            }

            Node2D? owner = null;
            var head = Vector2.Zero;
            foreach (var npc in city.Townsfolk.Npcs)
            {
                if (GodotObject.IsInstanceValid(npc) && npc.Def.Name == label.Text)
                {
                    owner = npc;
                    head = WorldLabels.ToScreen(npc, npc.GlobalPosition + new Vector2(0, -40));
                }
            }

            foreach (var hunter in city.Townsfolk.Hunters)
            {
                if (GodotObject.IsInstanceValid(hunter) && hunter.Brain.Def.Name == label.Text)
                {
                    owner = hunter;
                    head = WorldLabels.ToScreen(hunter, hunter.GlobalPosition + new Vector2(0, -38));
                }
            }

            if (owner is null)
            {
                continue; // Selo, balão ou título de portal: conferidos em outros cenários
            }

            var bottom = label.Position + new Vector2(label.Size.X / 2f, label.Size.Y);
            var visible = owner.Visible && owner.Modulate.A > 0.01f;
            if (!visible || bottom.DistanceTo(head) > 40f)
            {
                problems.Add($"{when}: \"{label.Text}\" visível (alpha {label.Modulate.A:0.00}) em {label.Position} com o dono {(visible ? "visível" : "INVISÍVEL")} e a cabeça em {head}");
            }
        }
    }

    [SmokeScenario("cidade (qa): jogador anda entre os moradores e ninguém trava, treme ou entra na parede", order: 73)]
    private async Task QaTownWalkShort()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 17 * 60 + 30);
        try
        {
            city.TimeScale = 0f; // 17:30 fixo: Lauro, Marta e Faísca no mapa
            var problems = await RunTownWalk(world, city, gameSeconds: 45, speedUp: 1, tag: "45 s");
            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("estresse: cidade (qa): 5 min acelerados com jogador, moradores e caçadores juntos", order: 200)]
    private async Task QaTownWalkLong()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 17 * 60 + 30);
        try
        {
            city.TimeScale = 0f;
            var problems = await RunTownWalk(world, city, gameSeconds: 300, speedUp: 4, tag: "5 min");
            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            StopCity(city);
        }
    }

    private sealed class BodyTrack
    {
        public string Name = string.Empty;
        public double Speed;
        public Vector2 Last;
        public Vector2 Anchor;
        public double AnchorT;
        public bool Moving;
        public bool InStuck;
        public double MovingTime;
        public double Distance;
        public double WorstStuck;
        public Vector2 WorstStuckAt;
        public int StuckEvents;
        public int WallHits;
        public string? FirstWall;
        public Vector2 PrevStep;
        public int Reversals;
        public string? FirstReversal;
        public TownFacing Facing;
        public TownFacing PrevFacing;
        public double FacingChangedT = -10;
        public int Flickers;
        public string? FirstFlicker;
        public double OverlapPlayer;
        public int Laps;
        public int LastLeg = -1;

        public void Sample(IWalkArea area, Vector2 p, bool moving, TownFacing facing, double t, double dt, Vector2 player)
        {
            if (!area.CanStand(p.X, p.Y, WalkMover.NpcRadius))
            {
                WallHits++;
                FirstWall ??= $"({p.X:0.0}, {p.Y:0.0}) em t={t:0.0}s";
            }

            var step = p - Last;
            if (moving && Moving)
            {
                MovingTime += dt;
                Distance += step.Length();
                if (step.Length() > 0.05f && PrevStep.Length() > 0.05f && step.Normalized().Dot(PrevStep.Normalized()) < -0.7f)
                {
                    Reversals++;
                    FirstReversal ??= $"({p.X:0.0}, {p.Y:0.0}) em t={t:0.0}s";
                }

                if (p.DistanceTo(Anchor) >= 1f)
                {
                    Anchor = p;
                    AnchorT = t;
                    InStuck = false;
                }
                else if (t - AnchorT >= 1.0)
                {
                    if (!InStuck)
                    {
                        StuckEvents++;
                        InStuck = true;
                    }

                    if (t - AnchorT > WorstStuck)
                    {
                        WorstStuck = t - AnchorT;
                        WorstStuckAt = p;
                    }
                }
            }
            else
            {
                Anchor = p;
                AnchorT = t;
                InStuck = false;
            }

            PrevStep = moving && step.Length() > 0.05f ? step : (moving ? PrevStep : Vector2.Zero);
            if (facing != Facing)
            {
                if (facing == PrevFacing && t - FacingChangedT < 0.15)
                {
                    Flickers++;
                    FirstFlicker ??= $"({p.X:0.0}, {p.Y:0.0}) em t={t:0.0}s {PrevFacing}→{Facing}→{facing}";
                }

                PrevFacing = Facing;
                Facing = facing;
                FacingChangedT = t;
            }

            if (p.DistanceTo(player) < 5f)
            {
                OverlapPlayer += dt;
            }

            Moving = moving;
            Last = p;
        }

        public string Report() =>
            $"{Name}: andando {MovingTime:0} s a {(MovingTime > 0 ? Distance / MovingTime : 0):0.0} u/s (v2 {Speed}), {Laps} volta(s), " +
            $"maior trava {WorstStuck:0.00} s ({StuckEvents}x, pior em ({WorstStuckAt.X:0}, {WorstStuckAt.Y:0})), parede {WallHits}x {FirstWall}, " +
            $"vai-e-volta {Reversals}x {FirstReversal}, pisca direção {Flickers}x {FirstFlicker}, dentro do jogador {OverlapPlayer:0.0} s";
    }

    /// <summary>
    /// Joga <paramref name="gameSeconds"/> s na Rua da Forja com o diretor real: um bot anda com input até pontos
    /// sorteados e, a cada dois alvos, vai se plantar no caminho de um NPC. Mede travas, paredes, vai-e-volta,
    /// direção piscando, o jogador travado e o tempo de frame.
    /// </summary>
    private async Task<List<string>> RunTownWalk(World.World world, CityLife city, double gameSeconds, int speedUp, string tag)
    {
        var town = city.Townsfolk;
        await WaitUntil(() => town.Npcs.Count == 3 && town.Npcs.All(n => n.Walker is not null), 15, "Lauro, Marta e Faísca prontos");
        var area = world.WalkArea;
        var player = FindPlayer()!;
        var rng = new SeededRandom(4242);
        var planner = new WalkPlanner(area, WalkAreaExtensions.PlayerRadius);
        var path = new List<MapPoint>();
        var npcTracks = town.Npcs.Select(n => new BodyTrack { Name = n.Def.Id, Speed = n.Def.Speed, Last = n.Position, Anchor = n.Position, LastLeg = n.Walker!.Leg }).ToArray();
        var hunterTracks = town.Hunters.Select(h => new BodyTrack { Name = $"caçador {h.Brain.Def.Name}", Speed = AmbientHunter.Speed, Last = h.Position, Anchor = h.Position }).ToArray();
        var npcs = town.Npcs.ToArray();
        var hunters = town.Hunters.ToArray();
        var replans0 = npcs.Select(n => n.Walker!.Replans).ToArray();
        var bodies = npcs.Cast<Node2D>().Concat(hunters).ToArray();
        var bodyNames = npcTracks.Select(b => b.Name).Concat(hunterTracks.Select(b => b.Name)).ToArray();
        var overlapRun = new double[bodies.Length * bodies.Length];
        var overlapTotal = new double[bodies.Length * bodies.Length];
        var overlapWorst = new double[bodies.Length * bodies.Length];
        var overlapAt = new Vector2[bodies.Length * bodies.Length];
        var frameMs = new List<double>(20000);
        var playerStucks = new List<string>();
        var playerPlanFails = 0;
        var targets = 0;
        var blocks = 0;
        double t = 0, hold = 0, legT = 0;
        var next = 0;
        var anchor = player.GlobalPosition;
        var anchorT = 0.0;
        var lastDt = 0.0;

        void NewTarget()
        {
            targets++;
            var p = player.GlobalPosition;
            var from = new MapPoint(p.X, p.Y);
            if (!planner.CanStand(from.X, from.Y) && planner.Settle(from, 12f) is { } settled)
            {
                from = settled;
            }

            MapPoint to;
            var moving = npcs.Where(n => GodotObject.IsInstanceValid(n) && n.Walker!.IsMoving && n.Walker.CurrentPath.Count > 0).ToArray();
            if (targets % 2 == 0 && moving.Length > 0)
            {
                // Planta-se no caminho de um NPC (o fim do trecho que ele está andando): ele tem que desviar ou passar.
                var victim = moving[rng.RangeInt(0, moving.Length - 1)];
                to = victim.Walker!.CurrentPath[^1];
                hold = 2.5;
                blocks++;
            }
            else
            {
                to = WalkMover.RandomPoint(area, world.Map.ImageWidth, world.Map.ImageHeight, rng, p.X, p.Y, minDistance: 120f);
                hold = 0.3;
            }

            if (!planner.TryPlan(from, to, path))
            {
                playerPlanFails++;
                path.Clear();
            }

            next = path.Count > 1 ? 1 : path.Count;
            legT = 0;
            anchor = p;
            anchorT = t;
        }

        var speed = speedUp > 1 ? new SpeedUp(speedUp) : null;
        try
        {
            NewTarget();
            while (t < gameSeconds)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var raw = GetProcessDeltaTime();
                frameMs.Add(raw / Engine.TimeScale * 1000.0);
                var dt = lastDt; // as posições vistas agora vieram do passo anterior
                lastDt = Math.Min(0.05, raw);
                t += dt;
                legT += dt;
                var pp = player.GlobalPosition;
                for (var i = 0; i < npcs.Length; i++)
                {
                    if (!GodotObject.IsInstanceValid(npcs[i]))
                    {
                        continue;
                    }

                    var w = npcs[i].Walker!;
                    npcTracks[i].Sample(area, npcs[i].Position, w.IsMoving, w.Facing, t, dt, pp);
                    if (w.Leg != npcTracks[i].LastLeg)
                    {
                        npcTracks[i].Laps += w.Leg == 0 ? 1 : 0;
                        npcTracks[i].LastLeg = w.Leg;
                    }
                }

                for (var i = 0; i < hunters.Length; i++)
                {
                    if (GodotObject.IsInstanceValid(hunters[i]))
                    {
                        hunterTracks[i].Sample(area, hunters[i].Position, hunters[i].Brain.IsMoving, hunters[i].Brain.Facing, t, dt, pp);
                    }
                }

                // Dois moradores um dentro do outro (< 6 u entre os pés): "uma imagem em cima da outra".
                for (var a = 0; a < bodies.Length; a++)
                {
                    for (var b = a + 1; b < bodies.Length; b++)
                    {
                        var k = (a * bodies.Length) + b;
                        if (GodotObject.IsInstanceValid(bodies[a]) && GodotObject.IsInstanceValid(bodies[b]) && bodies[a].Position.DistanceTo(bodies[b].Position) < 6f)
                        {
                            overlapRun[k] += dt;
                            overlapTotal[k] += dt;
                            if (overlapRun[k] > overlapWorst[k])
                            {
                                overlapWorst[k] = overlapRun[k];
                                overlapAt[k] = bodies[a].Position;
                            }
                        }
                        else
                        {
                            overlapRun[k] = 0;
                        }
                    }
                }

                // Bot do jogador: segue o caminho com input; chegou, espera um pouco e sorteia outro alvo.
                while (next < path.Count && pp.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                if (next >= path.Count)
                {
                    PressDirection(Vector2.Zero);
                    hold -= dt;
                    anchor = pp;
                    anchorT = t;
                    if (hold <= 0)
                    {
                        NewTarget();
                    }

                    continue;
                }

                PressDirection((new Vector2(path[next].X, path[next].Y) - pp).Normalized());
                if (pp.DistanceTo(anchor) >= 1f)
                {
                    anchor = pp;
                    anchorT = t;
                }
                else if (t - anchorT >= 1.0)
                {
                    playerStucks.Add($"({pp.X:0}, {pp.Y:0}) indo para ({path[next].X:0}, {path[next].Y:0})");
                    NewTarget();
                }
                else if (legT > 40)
                {
                    playerStucks.Add($"timeout em ({pp.X:0}, {pp.Y:0})");
                    NewTarget();
                }
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
            speed?.Dispose();
        }

        frameMs.Sort();
        var avg = frameMs.Count > 0 ? frameMs.Average() : 0;
        var p95 = frameMs.Count > 0 ? frameMs[(int)(frameMs.Count * 0.95)] : 0;
        var max = frameMs.Count > 0 ? frameMs[^1] : 0;
        var p99 = frameMs.Count > 0 ? frameMs[(int)(frameMs.Count * 0.99)] : 0;
        var over25 = frameMs.Count(f => f > 25.0);
        var problems = new List<string>();
        Log.Info($"cidade (qa) {tag}: {t:0} s de jogo, {frameMs.Count} frames, frame médio {avg:0.00} ms, p95 {p95:0.00} ms, p99 {p99:0.00} ms, pior {max:0.00} ms, {over25} frames > 25 ms (reais; {DisplayServer.GetName()}); jogador: {targets} alvos, {blocks} vezes no caminho de um NPC, {playerPlanFails} sem caminho, {playerStucks.Count} travas [{string.Join("; ", playerStucks.Take(8))}]");
        for (var i = 0; i < npcTracks.Length; i++)
        {
            var b = npcTracks[i];
            var replans = GodotObject.IsInstanceValid(npcs[i]) ? npcs[i].Walker!.Replans - replans0[i] : -1;
            Log.Info($"cidade (qa) {tag}: {b.Report()}, replanejou {replans}x");
            var minutes = Math.Max(1.0, t / 60.0);
            if (b.WallHits > 0)
            {
                problems.Add($"{b.Name} entrou na parede {b.WallHits}x, a primeira em {b.FirstWall}");
            }

            if (b.WorstStuck >= 1.0)
            {
                problems.Add($"{b.Name} ficou {b.WorstStuck:0.0} s andando sem sair do lugar em ({b.WorstStuckAt.X:0}, {b.WorstStuckAt.Y:0})");
            }

            if (b.MovingTime > 5 && b.Distance / b.MovingTime < b.Speed * 0.85)
            {
                problems.Add($"{b.Name}: {b.Distance / b.MovingTime:0.0} u/s andando, a v2 anda a {b.Speed}");
            }

            if (b.Reversals / minutes > 6)
            {
                problems.Add($"{b.Name}: vai-e-volta {b.Reversals}x em {t:0} s (tremida), o primeiro em {b.FirstReversal}");
            }

            if (b.Flickers / minutes > 4)
            {
                problems.Add($"{b.Name}: sprite troca de direção e volta {b.Flickers}x em {t:0} s, a primeira em {b.FirstFlicker}");
            }
        }

        foreach (var b in hunterTracks)
        {
            Log.Info($"cidade (qa) {tag}: {b.Report()}");
            var minutes = Math.Max(1.0, t / 60.0);
            if (b.WallHits > 0)
            {
                problems.Add($"{b.Name} entrou na parede {b.WallHits}x, a primeira em {b.FirstWall}");
            }

            if (b.WorstStuck >= 1.0)
            {
                problems.Add($"{b.Name} ficou {b.WorstStuck:0.0} s andando sem sair do lugar em ({b.WorstStuckAt.X:0}, {b.WorstStuckAt.Y:0})");
            }

            if (b.Reversals / minutes > 6)
            {
                problems.Add($"{b.Name}: vai-e-volta {b.Reversals}x em {t:0} s, o primeiro em {b.FirstReversal}");
            }

            if (b.Flickers / minutes > 4)
            {
                problems.Add($"{b.Name}: sprite troca de direção e volta {b.Flickers}x em {t:0} s, a primeira em {b.FirstFlicker}");
            }
        }

        for (var a = 0; a < bodies.Length; a++)
        {
            for (var b = a + 1; b < bodies.Length; b++)
            {
                var k = (a * bodies.Length) + b;
                if (overlapTotal[k] <= 0)
                {
                    continue;
                }

                Log.Info($"cidade (qa) {tag}: {bodyNames[a]} e {bodyNames[b]} um dentro do outro por {overlapTotal[k]:0.0} s (seguidos: até {overlapWorst[k]:0.0} s em ({overlapAt[k].X:0}, {overlapAt[k].Y:0}))");
                if (overlapWorst[k] >= 1.5)
                {
                    problems.Add($"{bodyNames[a]} e {bodyNames[b]} ficaram {overlapWorst[k]:0.0} s um dentro do outro em ({overlapAt[k].X:0}, {overlapAt[k].Y:0})");
                }
            }
        }

        if (DisplayServer.GetName() != "headless" && p95 > 20)
        {
            problems.Add($"frame p95 {p95:0.0} ms com a cidade cheia");
        }

        return problems;
    }

    [SmokeScenario("cidade (qa): falar com cada morador andando até ele com input", order: 74)]
    private async Task QaTalkToEveryone()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60 + 30);
        var dialogues = new List<(string Speaker, string[] Lines)>();
        void OnDialogue(string s, string[] l) => dialogues.Add((s, l));
        var bus = EventBus.Instance;
        bus.DialogueRequested += OnDialogue;
        try
        {
            city.TimeScale = 0f;
            var town = city.Townsfolk;
            var problems = new List<string>();
            foreach (var mapId in new[] { "forge", "guild" })
            {
                if (world.MapId != mapId)
                {
                    world.GoMap(mapId, new Vector2(250, 560), instant: true);
                    await WaitMapSettled(world);
                }

                await WaitUntil(() => town.Npcs.Count > 0 && town.Npcs.All(n => n.Walker is not null), 15, $"moradores de {mapId}");

                // Todo waypoint do percurso tem que ser alcançável a pé pelo jogador desde a chegada principal.
                var map = World.World.Catalog[mapId];
                var entry = mapId == "forge" ? new MapPoint(1500, 250) : new MapPoint(map.Spawn.X, map.Spawn.Y);
                foreach (var npc in town.Npcs)
                {
                    foreach (var wp in npc.Walker!.Route.Waypoints)
                    {
                        if (WalkPathfinder.FindPathNear(world.WalkArea, entry, wp, TownNpc.TalkRadius - 4) is null)
                        {
                            problems.Add($"{npc.Def.Id}: o jogador não chega a pé ao waypoint ({wp.X:0}, {wp.Y:0}) desde ({entry.X:0}, {entry.Y:0})");
                        }
                    }
                }

                foreach (var npc in town.Npcs.ToArray())
                {
                    var before = dialogues.Count;
                    var started = Time.GetTicksMsec();
                    var error = await TalkByWalking(world, npc, dialogues, 20);
                    if (error is not null)
                    {
                        problems.Add(error);
                        continue;
                    }

                    var line = dialogues[before];
                    Log.Info($"cidade (qa): falei com {npc.Def.Id} em {(Time.GetTicksMsec() - started) / 1000.0:0.0} s: \"{line.Lines[0]}\"");
                    if (line.Speaker != npc.Def.Name)
                    {
                        problems.Add($"falei com {npc.Def.Id} e o diálogo veio de {line.Speaker}");
                    }

                    var still = npc.Position;
                    await WaitSeconds(1.0);
                    if (npc.Position.DistanceTo(still) > 0.5f)
                    {
                        problems.Add($"{npc.Def.Id} saiu andando no meio da conversa");
                    }

                    await Shot($"qa-falar-{npc.Def.Id}");
                }
            }

            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            bus.DialogueRequested -= OnDialogue;
            StopCity(city);
        }
    }

    /// <summary>Persegue o NPC com input (replaneja a cada 0,3 s) e aperta interagir quando o foco é ele.</summary>
    private async Task<string?> TalkByWalking(World.World world, TownNpc npc, List<(string Speaker, string[] Lines)> dialogues, double timeoutSeconds)
    {
        var player = FindPlayer()!;
        var area = world.WalkArea;
        var count0 = dialogues.Count;
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        var presses = 0;
        WalkPlanner? planner = null;
        var planned = new List<MapPoint>();
        bool Focused() => player.FocusedInteractable is NpcInteractable { Npc: var n } && n == npc;
        try
        {
            while (Time.GetTicksMsec() < deadline)
            {
                if (Focused())
                {
                    PressDirection(Vector2.Zero);
                    presses++;
                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
                    await ProcessFrames(2);
                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
                    var t0 = Time.GetTicksMsec();
                    while (dialogues.Count == count0 && Time.GetTicksMsec() - t0 < 1000)
                    {
                        await ProcessFrames(1);
                    }

                    if (dialogues.Count > count0)
                    {
                        return dialogues.Count == count0 + 1 ? null : $"uma interação com {npc.Def.Id} abriu {dialogues.Count - count0} diálogos";
                    }

                    continue;
                }

                var p = player.GlobalPosition;
                var start = area.CanStand(p.X, p.Y) ? new MapPoint(p.X, p.Y) : area.NearestWalkable(p.X, p.Y) ?? new MapPoint(p.X, p.Y);
                // A malha do A* (x = 2 mod 4) passa por onde o NPC passa; a BFS ancorada no jogador pode perder vãos estreitos.
                planner ??= new WalkPlanner(area, WalkAreaExtensions.PlayerRadius);
                var target = new MapPoint(npc.Position.X, npc.Position.Y);
                IReadOnlyList<MapPoint>? path = planner.TryPlan(start, target, planned) ? planned : null;
                for (var k = 0; path is null && k < 9; k++)
                {
                    // BFS com a malha deslocada 0–2 u: vãos estreitos só passam em certos alinhamentos.
                    var s = new MapPoint(start.X + (k % 3), start.Y + (k / 3));
                    if (area.CanStand(s.X, s.Y))
                    {
                        path = WalkPathfinder.FindPathNear(area, s, target, TownNpc.TalkRadius - 6);
                    }
                }
                if (path is null)
                {
                    return $"sem caminho do jogador em ({p.X:0}, {p.Y:0}) até {npc.Def.Id} em ({npc.Position.X:0}, {npc.Position.Y:0})";
                }

                var next = 1;
                for (var frame = 0; frame < 18 && !Focused(); frame++)
                {
                    var q = player.GlobalPosition;
                    while (next < path.Count && q.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                    {
                        next++;
                    }

                    PressDirection(next < path.Count ? (new Vector2(path[next].X, path[next].Y) - q).Normalized() : Vector2.Zero);
                    await NextPhysicsFrame();
                }
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }

        return $"não consegui falar com {npc.Def.Id} em {timeoutSeconds} s ({presses} apertos; jogador em {player.GlobalPosition}, NPC em {npc.Position}, foco {player.FocusedInteractable?.Prompt ?? "nenhum"})";
    }

    [SmokeScenario("cidade (qa): os dois portais da Rua da Forja são alcançáveis a pé e abrem o diálogo", order: 75)]
    private async Task QaReachBothPortals()
    {
        var (world, city) = await StartCity("forge", new Vector2(1500, 250), 18 * 60);
        try
        {
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount == 1, 3, "1ª Brecha");
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount == 2, 3, "2ª Brecha");
            city.TimeScale = 0f;
            var problems = new List<string>();
            foreach (var spot in new[] { 1, 0 })
            {
                var id = $"portal{spot}";
                var portal = world.GetInteractable(id)!;
                Require(portal.Enabled, $"{id} deveria estar ligado com as duas Brechas abertas");
                var (kind, got, error) = await WalkAndInteract(world, id, 40);
                if (error is not null)
                {
                    problems.Add(error);
                    continue;
                }

                if (kind != "portalDialog" || got != id)
                {
                    problems.Add($"{id} emitiu ({kind}, {got})");
                }

                await Shot($"qa-portal{spot}-de-perto");
            }

            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            StopCity(city);
        }
    }

    /// <summary>Anda com input da posição atual até o interagível (caminho inteiro), aperta interagir e espera o pedido até 1 s.</summary>
    private async Task<(string? Kind, string? Id, string? Error)> WalkAndInteract(World.World world, string interactableId, double timeoutSeconds)
    {
        var it = world.GetInteractable(interactableId)!;
        var player = FindPlayer()!;
        var area = world.WalkArea;
        var p = player.GlobalPosition;
        var path = WalkPathfinder.FindPathNear(area, new MapPoint(p.X, p.Y), new MapPoint(it.Position.X, it.Position.Y), it.Radius - 4);
        if (path is null)
        {
            return (null, null, $"{interactableId} inalcançável de ({p.X:0}, {p.Y:0})");
        }

        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        var next = 1;
        var anchor = p;
        var anchorMs = Time.GetTicksMsec();
        try
        {
            while (player.FocusedInteractable != it)
            {
                var q = player.GlobalPosition;
                if (Time.GetTicksMsec() > deadline)
                {
                    return (null, null, $"timeout andando até {interactableId}; parei em ({q.X:0}, {q.Y:0})");
                }

                if (q.DistanceTo(anchor) >= 1f)
                {
                    anchor = q;
                    anchorMs = Time.GetTicksMsec();
                }
                else if (Time.GetTicksMsec() - anchorMs > 1000)
                {
                    return (null, null, $"travei em ({q.X:0}, {q.Y:0}) indo para {interactableId}");
                }

                while (next < path.Count && q.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 4f)
                {
                    next++;
                }

                var target = next < path.Count ? new Vector2(path[next].X, path[next].Y) : it.Position;
                PressDirection((target - q).Normalized());
                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }

        (string, string)? got = null;
        void OnRequest(string kind, string id) => got = (kind, id);
        EventBus.Instance.InteractionRequested += OnRequest;
        try
        {
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
            await ProcessFrames(2);
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
            var t0 = Time.GetTicksMsec();
            while (got is null && Time.GetTicksMsec() - t0 < 1000)
            {
                await ProcessFrames(1);
            }
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
        }

        return got is { } g ? (g.Item1, g.Item2, null) : (null, null, $"interagir com {interactableId} não emitiu nada em 1 s");
    }

    [SmokeScenario("cidade (qa): prints da Rua da Forja de dia, véspera, noite e madrugada, interiores, ruptura e pausa", order: 76)]
    private async Task QaShotsThroughTheDay()
    {
        var spot = World.World.Catalog["forge"].PortalSpots[0];
        var stand = new Vector2(spot.X + 30, spot.Y + 60);
        var (world, city) = await StartCity("forge", stand, 16 * 60 + 30);
        var res = DisplayServer.GetName() == "headless" ? "headless" : $"{DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y}";
        var state = GameManager.Instance.State;
        try
        {
            city.TimeScale = 0f;
            var player = FindPlayer()!;
            var darkness = new List<(string, float, Color)>();
            async Task At(double clock, string name)
            {
                state.Save.ClockMinutes = clock;
                player.Teleport(world.CorrectToWalkable(stand));
                world.Camera.Snap();
                await ProcessFrames(4);
                darkness.Add((name, city.Lighting.CurrentDarkness, city.Lighting.CurrentGrade));
                await Shot($"qa-{res}-{name}");
            }

            await At(16 * 60 + 30, "1-dia-1630");
            await At(17 * 60 + 30, "2-lampadas-1730");
            state.Save.ClockMinutes = 18 * 60;
            city.Brechas.OpenSoon(0);
            city.TimeScale = 1f;
            await WaitUntil(() => city.Brechas.OpenAt(0) is not null, 3, "Brecha no portal 0");
            city.TimeScale = 0f;
            await WaitSeconds(0.3);
            await Shot($"qa-{res}-3-portal-abrindo");
            await WaitSeconds(1.2);
            await At(18 * 60, "4-vespera-portal-1800");
            await At(19 * 60 + 30, "5-noite-1930");
            await At(22 * 60, "6-noite-2200");
            await At(26 * 60, "7-madrugada-0200");
            await At(29 * 60 + 30, "8-aurora-0530");

            // Moradores à noite, de perto.
            state.Save.ClockMinutes = 20 * 60;
            var town = city.Townsfolk;
            await WaitUntil(() => town.Npcs.Count > 0 && town.Npcs.All(n => n.Walker is not null), 10, "NPCs prontos");
            var faisca = town.Npcs.FirstOrDefault(n => n.Def.Id == "faisca") ?? town.Npcs[0];
            player.Teleport(world.CorrectToWalkable(faisca.Position + new Vector2(-20, 20)));
            world.Camera.Snap();
            await ProcessFrames(4);
            await Shot($"qa-{res}-9-npc-noite");

            // Interiores.
            foreach (var (mapId, clock) in new[] { ("inn", 22.0 * 60), ("guild", 18.0 * 60), ("shop_arm", 23.0 * 60) })
            {
                state.Save.ClockMinutes = clock;
                var map = World.World.Catalog[mapId];
                world.GoMap(mapId, new Vector2(map.Spawn.X, map.Spawn.Y), instant: true);
                await WaitMapSettled(world);
                await ProcessFrames(4);
                darkness.Add((mapId, city.Lighting.CurrentDarkness, city.Lighting.CurrentGrade));
                await Shot($"qa-{res}-10-{mapId}");
            }

            // Ruptura e pausa na rua.
            world.GoMap("forge", stand, instant: true);
            await WaitMapSettled(world);
            state.Save.ClockMinutes = 21 * 60;
            var invaded = false;
            void OnInvasion(string t, int r) => invaded = true;
            EventBus.Instance.InvasionRequested += OnInvasion;
            try
            {
                city.TimeScale = 150f;
                city.Brechas.OpenSoon(0);
                await WaitUntil(() => invaded, 40, "ruptura");
                city.TimeScale = 0f;
            }
            finally
            {
                EventBus.Instance.InvasionRequested -= OnInvasion;
            }

            player.Teleport(world.CorrectToWalkable(stand));
            world.Camera.Snap();
            await WaitSeconds(0.4);
            darkness.Add(("ruptura", city.Lighting.CurrentDarkness, city.Lighting.CurrentGrade));
            await Shot($"qa-{res}-11-ruptura");
            EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, true);
            await WaitSeconds(0.3);
            await Shot($"qa-{res}-12-pausa");
            EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, false);

            Log.Info($"cidade (qa) {res}: escuridão/cor por hora: {string.Join(" | ", darkness.Select(d => $"{d.Item1} {d.Item2:0.000} {d.Item3}"))}");
            var byName = darkness.ToDictionary(d => d.Item1, d => d.Item2);
            Require(byName["1-dia-1630"] < byName["4-vespera-portal-1800"] && byName["4-vespera-portal-1800"] < byName["6-noite-2200"], "a rua escurece do dia para a véspera e para a noite");
            Require(byName["8-aurora-0530"] < byName["7-madrugada-0200"], "a aurora (05:30) clareia em relação à madrugada");
        }
        finally
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (qa): A* acha caminho entre pontos da mesma região da Rua da Forja e custa pouco", order: 77)]
    private async Task QaPlannerCost()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 16 * 60 + 30);
        try
        {
            var area = world.WalkArea;
            var planner = new WalkPlanner(area);
            var sw = Stopwatch.StartNew();
            planner.Prepare();
            var prepareMs = sw.Elapsed.TotalMilliseconds;
            var rng = new SeededRandom(77);
            var path = new List<MapPoint>();
            var times = new List<double>();
            var failures = new List<string>();
            var maxExpansions = 0;
            for (var i = 0; i < 600; i++)
            {
                var a = WalkMover.RandomPoint(area, world.Map.ImageWidth, world.Map.ImageHeight, rng, 0, 0, radius: 6f);
                var b = WalkMover.RandomPoint(area, world.Map.ImageWidth, world.Map.ImageHeight, rng, 0, 0, radius: 6f);
                var ra = planner.RegionOf(a);
                if (ra == 0 || ra != planner.RegionOf(b))
                {
                    continue;
                }

                sw.Restart();
                var ok = planner.TryPlan(a, b, path);
                times.Add(sw.Elapsed.TotalMilliseconds);
                maxExpansions = Math.Max(maxExpansions, planner.LastExpansions);
                if (!ok)
                {
                    failures.Add($"({a.X:0}, {a.Y:0}) → ({b.X:0}, {b.Y:0}) ({planner.LastExpansions} nós)");
                }
            }

            times.Sort();
            var p50 = times[times.Count / 2];
            var p95 = times[(int)(times.Count * 0.95)];
            Log.Info($"cidade (qa): A* na Rua da Forja ({planner.Columns}×{planner.Rows} nós, Prepare {prepareMs:0.0} ms): {times.Count} pedidos, p50 {p50:0.00} ms, p95 {p95:0.00} ms, pior {times[^1]:0.00} ms, até {maxExpansions} nós abertos (limite {planner.MaxExpansions}), {failures.Count} falha(s)");
            Require(failures.Count == 0, $"{failures.Count} pedido(s) sem caminho na mesma região: {string.Join(", ", failures.Take(5))}");
            // O caçador sorteia o destino no mapa inteiro (mesma distribuição daqui) e planeja no frame principal.
            Require(p95 < 4.0 && times[^1] < 8.0, $"A* no frame principal: p95 {p95:0.00} ms, pior {times[^1]:0.00} ms (orçamento: p95 < 4, pior < 8 = meio frame)");
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (qa): a malha do A* liga tudo o que o jogador alcança a pé na Rua da Forja e na Guilda", order: 78)]
    private async Task QaPlannerRegionsMatchPlayer()
    {
        var (world, city) = await StartCity("forge", new Vector2(1500, 250), 16 * 60 + 30);
        try
        {
            var problems = new List<string>();
            foreach (var (mapId, entry) in new[] { ("forge", new MapPoint(1500, 250)), ("guild", new MapPoint(250, 560)) })
            {
                if (world.MapId != mapId)
                {
                    world.GoMap(mapId, new Vector2(entry.X, entry.Y), instant: true);
                    await WaitMapSettled(world);
                }

                // Tudo que a BFS do jogador (raio 4, passo 4) alcança desde a chegada é chão onde o jogador anda.
                var reached = new List<MapPoint>();
                WalkPathfinder.FindPath(world.WalkArea, entry, p =>
                {
                    reached.Add(p);
                    return false;
                });
                var planner = new WalkPlanner(world.WalkArea);
                var home = planner.RegionOf(entry);
                var cut = new Dictionary<int, (int Count, float MinX, float MinY, float MaxX, float MaxY)>();
                for (var i = 0; i < reached.Count; i += 5)
                {
                    var p = reached[i];
                    if (!planner.CanStandFirmly(p))
                    {
                        continue; // ponto no fio de uma borda: a malha do A* fica longe dele de propósito
                    }

                    var r = planner.RegionOf(p);
                    if (r == home)
                    {
                        continue;
                    }

                    var c = cut.TryGetValue(r, out var v) ? v : (Count: 0, MinX: float.MaxValue, MinY: float.MaxValue, MaxX: float.MinValue, MaxY: float.MinValue);
                    cut[r] = (c.Count + 1, Math.Min(c.MinX, p.X), Math.Min(c.MinY, p.Y), Math.Max(c.MaxX, p.X), Math.Max(c.MaxY, p.Y));
                }

                Log.Info($"cidade (qa): {mapId}: o jogador alcança {reached.Count} pontos; a malha do A* põe a chegada na região {home} ({planner.RegionSize(home)} nós) e separa {cut.Sum(c => c.Value.Count) * 5} deles em {cut.Count} região(ões): " +
                         string.Join("; ", cut.Select(c => $"região {c.Key} ({planner.RegionSize(c.Key)} nós) em x {c.Value.MinX:0}–{c.Value.MaxX:0}, y {c.Value.MinY:0}–{c.Value.MaxY:0}")));
                foreach (var c in cut.Where(c => c.Value.Count * 5 > 40))
                {
                    problems.Add($"{mapId}: a área x {c.Value.MinX:0}–{c.Value.MaxX:0}, y {c.Value.MinY:0}–{c.Value.MaxY:0} é alcançável a pé, mas para o A* dos moradores é outra região ({c.Key}, {planner.RegionSize(c.Key)} nós): caçadores e NPCs nunca atravessam");
                }
            }

            Require(problems.Count == 0, string.Join("; ", problems));
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("cidade (qa): caçador ambiente nunca nasce onde o próprio corpo não cabe", order: 79)]
    private async Task QaHunterSpawnFits()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60);
        try
        {
            city.TimeScale = 0f;
            var bad = new List<string>();
            var spawned = 0;
            foreach (var mapId in new[] { "forge", "inn" })
            {
                if (world.MapId != mapId)
                {
                    var map = World.World.Catalog[mapId];
                    world.GoMap(mapId, new Vector2(map.Spawn.X, map.Spawn.Y), instant: true);
                    await WaitMapSettled(world);
                }

                // O mesmo sorteio do diretor (RandomPoint de raio 8, longe 70 u do jogador), muitas vezes.
                var rng = new SeededRandom(7);
                var area = world.WalkArea;
                var pp = FindPlayer()!.GlobalPosition;
                var sampledBad = 0;
                for (var i = 0; i < 5000; i++)
                {
                    var q = WalkMover.RandomPoint(area, world.Map.ImageWidth, world.Map.ImageHeight, rng, pp.X, pp.Y, minDistance: 70f);
                    if (!area.CanStand(q.X, q.Y, WalkMover.NpcRadius))
                    {
                        sampledBad++;
                        if (sampledBad <= 3)
                        {
                            bad.Add($"{mapId}: sorteio em ({q.X:0.0}, {q.Y:0.0})");
                        }
                    }
                }

                Log.Info($"cidade (qa): {mapId}: {sampledBad} de 5000 sorteios de nascimento de caçador caem onde o corpo (raio 4) não cabe");

                // O diretor de verdade sorteia os caçadores a cada Populate (ligar a simulação de novo = entrar no mapa).
                for (var i = 0; i < 60; i++)
                {
                    city.SetSimulationEnabled(false);
                    city.SetSimulationEnabled(true);
                    foreach (var h in city.Townsfolk.Hunters)
                    {
                        spawned++;
                        if (!world.WalkArea.CanStand(h.Brain.X, h.Brain.Y, WalkMover.NpcRadius))
                        {
                            bad.Add($"{mapId}: {h.Brain.Def.Name} em ({h.Brain.X:0.0}, {h.Brain.Y:0.0})");
                        }
                    }

                    await ProcessFrames(1);
                }
            }

            // Quem nasceu assim fica parado: confere um deles no jogo real.
            var frozenNote = string.Empty;
            if (bad.Count > 0)
            {
                Log.Info($"cidade (qa): {bad.Count} de {spawned} caçadores nasceram onde o corpo (raio 4) não cabe: {string.Join("; ", bad.Take(6))}");
            }

            Require(bad.Count == 0, $"{bad.Count} de {spawned} caçadores nasceram onde o corpo não cabe (ficam parados para sempre: o A* recusa a partida){frozenNote}; ex.: {bad.FirstOrDefault()}");
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("estresse: cidade (qa): 50 trocas de mapa com a cidade ligada não vazam nós, rótulos nem luzes", order: 201)]
    private async Task QaFiftyMapChanges()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60 + 30);
        try
        {
            city.TimeScale = 0f;
            var maps = new[] { "guild", "forge", "inn", "forge", "shop_arm", "forge", "shop_mag", "forge", "shop_alq", "forge" };
            var samples = new List<(int I, double Nodes, int InUse, int Pool, int Lights, double Memory)>();
            for (var i = 0; i < 50; i++)
            {
                var id = maps[i % maps.Length];
                var map = World.World.Catalog[id];
                var at = id == "forge" ? new Vector2(800, 520) : new Vector2(map.Spawn.X, map.Spawn.Y);
                world.GoMap(id, at, instant: i % 5 != 0);
                await WaitMapSettled(world);
                await WaitUntil(() => city.Townsfolk.Npcs.All(n => n.Walker is not null), 10, $"moradores de {id}");
                await ProcessFrames(3);
                if (id == "forge")
                {
                    samples.Add((i, Performance.GetMonitor(Performance.Monitor.ObjectNodeCount), city.Labels.InUse, city.Labels.GetChildCount(), LightEmitter.Active.Count, Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024)));
                }
            }

            foreach (var s in samples)
            {
                Log.Info($"cidade (qa): troca {s.I + 1} na Rua da Forja: nós {s.Nodes}, rótulos em uso {s.InUse} (pool {s.Pool}), luzes {s.Lights}, memória {s.Memory:0.0} MB");
            }

            var first = samples[1];
            var last = samples[^1];
            Require(last.Nodes - first.Nodes <= 12, $"nós crescem com as trocas: {first.Nodes} → {last.Nodes}");
            Require(last.InUse <= first.InUse + 2, $"rótulos alugados crescem: {first.InUse} → {last.InUse}");
            Require(last.Pool <= first.Pool + 6, $"pool de rótulos cresce: {first.Pool} → {last.Pool}");
            Require(last.Lights <= first.Lights + 2, $"luzes ativas crescem: {first.Lights} → {last.Lights}");
            Require(Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount) == 0, "nós órfãos depois das trocas");
        }
        finally
        {
            StopCity(city);
        }
    }

    [SmokeScenario("estresse: cidade (qa): spam de pausa, interagir, esquiva e ataque com a cidade cheia", order: 202)]
    private async Task QaInputSpam()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60);
        var dialogues = 0;
        void OnDialogue(string s, string[] l) => dialogues++;
        var bus = EventBus.Instance;
        bus.DialogueRequested += OnDialogue;
        try
        {
            var town = city.Townsfolk;
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Schedule.OpenCount == 1, 3, "Brecha abrindo");
            await WaitUntil(() => town.Npcs.Count == 3 && town.Npcs.All(n => n.Walker is not null), 15, "moradores");

            // Pausa ligando e desligando a cada 2 frames durante a abertura do portal.
            for (var i = 0; i < 40; i++)
            {
                bus.EmitSignal(EventBus.SignalName.PauseChanged, i % 2 == 0);
                await ProcessFrames(2);
            }

            bus.EmitSignal(EventBus.SignalName.PauseChanged, false);
            Require(!city.IsFrozen, "a cidade deveria voltar a andar depois do spam de pausa");
            var before = town.Npcs.Select(n => n.Position).ToArray();
            await WaitSeconds(2.0);
            Require(town.Npcs.Where((n, k) => n.Walker!.IsMoving && n.Position.DistanceTo(before[k]) < 1f).Count() == 0, "algum NPC não voltou a andar depois do spam de pausa");

            // Spam de interagir colado na Marta: no máximo um diálogo por aperto.
            var marta = town.Npcs.First(n => n.Def.Id == "marta");
            var player = FindPlayer()!;
            var presses = 0;
            for (var i = 0; i < 30; i++)
            {
                player.Teleport(world.CorrectToWalkable(marta.Position + new Vector2(0, 10)));
                await NextPhysicsFrame();
                presses++;
                Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
                await ProcessFrames(1);
                Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
                await ProcessFrames(1);
            }

            await ProcessFrames(3);
            Log.Info($"cidade (qa): {presses} apertos de interagir na Marta → {dialogues} diálogos; ela espera {marta.Walker!.Wait:0.0} s");
            Require(dialogues <= presses && dialogues > 0, $"{presses} apertos geraram {dialogues} diálogos");

            // Esquiva e ataque andando no meio dos moradores.
            for (var i = 0; i < 60; i++)
            {
                PressDirection(new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)));
                Input.ParseInputEvent(new InputEventAction { Action = i % 2 == 0 ? InputActions.Dodge : InputActions.Attack, Pressed = true });
                await NextPhysicsFrame();
                Input.ParseInputEvent(new InputEventAction { Action = i % 2 == 0 ? InputActions.Dodge : InputActions.Attack, Pressed = false });
                await NextPhysicsFrame();
                var p = player.GlobalPosition;
                Require(world.WalkArea.IsWalkable(p.X, p.Y), $"jogador em área bloqueada depois de esquivar/atacar: {p}");
            }

            PressDirection(Vector2.Zero);
            foreach (var n in town.Npcs)
            {
                Require(world.WalkArea.CanStand(n.Position.X, n.Position.Y, WalkMover.NpcRadius), $"{n.Def.Id} na parede depois do spam em {n.Position}");
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
            bus.DialogueRequested -= OnDialogue;
            StopCity(city);
        }
    }
}
