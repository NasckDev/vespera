using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Testes do tester de experiência da frente B4a (motor do mundo), rodada 1. Jogam como um jogador: andam com input real
/// até cada coisa interagível do layout de teste (<c>fixture_forge.json</c>) e usam, conferem oclusão na frente e atrás
/// de cada recorte, medem o andar quadro a quadro (latência, passos, câmera), proporções, minimapa e som ambiente.
/// Os "estresse:" (com <c>--smoke-stress</c>) repetem interações, trocam de mapa 50×, soltam bots de caminhada em todos
/// os mapas e martelam os botões. Prints: <c>qa-*.png</c> com <c>--smoke-shots</c>.
/// </summary>
public partial class SmokeTest
{
    private const int QaSpeedUp = 4;

    // ---------------------------------------------------------------- jogar como jogador

    [SmokeScenario("motor (qa): anda até cada interagível da Rua da Forja vestida e usa", order: 60)]
    private async Task QaWalkToEveryInteractable()
    {
        // Integração v0.1: a Rua da Forja tem layout real (frente B4b); a fixture do motor foi desenhada para a máscara
        // antiga e punha decorações sobrepostas. A Rua "vestida" de verdade é a do layout_forge.json.
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        world.GoMap("forge", new Vector2(600, 540), instant: true);
        await WaitMapSettled(world);
        FindPlayer()!.Teleport(world.CorrectToWalkable(new Vector2(600, 540)));
        await Frames(2);
        var d = world.Dressing;
        var player = FindPlayer()!;
        var failures = new List<string>();
        var report = new List<string>();
        var used = new List<string>();
        void OnUsed(string kind, string map, int index) => used.Add(kind);
        EventBus.Instance.WorldObjectUsed += OnUsed;

        // alvos: decoração, baús, objetos do catálogo com interação e os interagíveis do mapa (NPC, lojas, portais ligados)
        var targets = new List<(string Label, Interactable It, string Kind)>();
        foreach (var s in d.Decor)
        {
            targets.Add(($"decor {s.Def.Kind} ({s.Def.X},{s.Def.Y})", s.Interactable, s.Def.Kind));
        }

        foreach (var c in d.Chests)
        {
            targets.Add(($"baú tier {c.Tier} ({c.Position.X},{c.Position.Y})", c.Interactable, "chest"));
        }

        foreach (var it in world.Map.Interactables)
        {
            if (world.GetInteractable(it.Id) is { Enabled: true } node)
            {
                targets.Add(($"mapa {it.Id} ({it.X},{it.Y})", node, "map"));
            }
        }

        try
        {
            using var speed = new SpeedUp(QaSpeedUp);
            var remaining = new List<(string Label, Interactable It, string Kind)>(targets);
            var index = 0;
            while (remaining.Count > 0)
            {
                // vizinho mais perto primeiro, como um jogador faria
                var here = player.GlobalPosition;
                remaining.Sort((a, b) => a.It.GlobalPosition.DistanceSquaredTo(here).CompareTo(b.It.GlobalPosition.DistanceSquaredTo(here)));
                var (label, it, kind) = remaining[0];
                remaining.RemoveAt(0);
                index++;
                if (!it.Enabled)
                {
                    report.Add($"{label}: desligado agora (já usado?)");
                    continue;
                }

                var error = await QaWalkToFocus(world, it);
                if (error is not null && QaOnlyByBoat(world, it))
                {
                    // B4b-D62: três cais da Rua da Forja são só de balsa; o bot não embarca no meio do caminho.
                    report.Add($"{label} '{it.Prompt}': só de balsa (B4b-D62), alcançável a partir do destino de uma balsa");
                    continue;
                }

                if (error is not null)
                {
                    failures.Add($"{label} '{it.Prompt}': {error}");
                    continue;
                }

                await Frames(2);
                if (d.Prompt.ShownText != it.Prompt || d.Prompt.KeyGlyph.Length == 0)
                {
                    failures.Add($"{label}: em foco, mas o prompt no mundo mostra '{d.Prompt.ShownText}' [{d.Prompt.KeyGlyph}] em vez de '{it.Prompt}'");
                }

                await Shot($"qa-poi-{index:00}-{kind}");
                var problem = await QaUse(world, it, kind);
                report.Add($"{label} '{it.Prompt}': {(problem ?? "OK")} (pé {player.GlobalPosition.X:0},{player.GlobalPosition.Y:0})");
                if (problem is not null)
                {
                    failures.Add($"{label}: {problem}");
                }
            }

            // quebráveis: chega perto, vira para ele e golpeia como jogador
            var props = d.Breakables.Where(b => !b.IsBroken).ToList();
            foreach (var b in props)
            {
                var label = $"quebrável {b.Kind} ({b.Position.X},{b.Position.Y})";
                var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), new MapPoint(b.GlobalPosition.X, b.GlobalPosition.Y), 16f);
                if (path is null && QaOnlyByBoat(world, b.GlobalPosition, 16f))
                {
                    report.Add($"{label}: só de balsa (B4b-D62)");
                    continue;
                }

                if (path is null)
                {
                    failures.Add($"{label}: sem caminho até 16 u");
                    continue;
                }

                var walkError = await TesterWalkPath(world, path, () => player.GlobalPosition.DistanceTo(b.GlobalPosition) < 18f, timeoutGameSeconds: 25f);
                if (walkError is not null && player.GlobalPosition.DistanceTo(b.GlobalPosition) > 22f)
                {
                    failures.Add($"{label}: {walkError}");
                    continue;
                }

                var hits = await QaBreakByAttacking(b);
                report.Add($"{label}: {(b.IsBroken ? $"quebrou em {hits} golpe(s)" : $"NÃO quebrou ({hits} golpes, hp {b.Hp}/{b.MaxHp})")} (pé {player.GlobalPosition.X:0},{player.GlobalPosition.Y:0})");
                if (!b.IsBroken)
                {
                    failures.Add($"{label}: {hits} golpes de perto e não quebrou (hp {b.Hp}/{b.MaxHp}, distância {player.GlobalPosition.DistanceTo(b.GlobalPosition):0.0})");
                }
            }

            await Shot("qa-poi-fim");
        }
        finally
        {
            EventBus.Instance.WorldObjectUsed -= OnUsed;
            PressDirection(Vector2.Zero);
            EndMotor();
        }

        foreach (var line in report)
        {
            Log.Info($"qa: {line}");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s) andando até os interagíveis:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("motor (qa): oclusão — atrás do recorte cobre e fica translúcido, na frente fica opaco", order: 61)]
    private async Task QaOcclusionFrontAndBehind()
    {
        var world = await MotorWorld("forge", new Vector2(600, 540));
        var player = FindPlayer()!;
        var covered = (float)WorldScale.Rules!.Occlusion.CoveredAlpha;
        var failures = new List<string>();
        var report = new List<string>();
        var i = 0;
        foreach (var o in world.Dressing.Occluders)
        {
            i++;
            var b = o.Def.Bounds;
            var cx = b.X + (b.Width / 2f);
            var name = o.Def.Note ?? $"recorte {i}";

            // atrás: o pé acima da base, onde o corpo cai dentro do recorte e dá para ficar
            Vector2? behind = null;
            for (var y = o.Def.BaseY - 2f; y > b.Y && behind is null; y -= 2f)
            {
                for (var dx = 0f; dx <= b.Width / 2f && behind is null; dx += 2f)
                {
                    foreach (var x in new[] { cx - dx, cx + dx })
                    {
                        if (world.WalkArea.CanStand(x, y) && OcclusionRules.Covers(o.Def.Polygon, b, OcclusionRules.BodyRect(x, y, WorldScale.CharacterHeight * 0.45f, WorldScale.CharacterHeight)))
                        {
                            behind = new Vector2(x, y);
                            break;
                        }
                    }
                }
            }

            if (behind is { } at)
            {
                player.Teleport(at);
                await WaitSeconds(0.5);
                var ok = o.IsCovering && MathF.Abs(o.Alpha - covered) < 0.05f;
                report.Add($"{name}: atrás em {at} → cobrindo={o.IsCovering} alpha={o.Alpha:0.00}");
                if (!ok)
                {
                    failures.Add($"{name}: pé em {at} atrás (base {o.Def.BaseY}) e o recorte não ficou translúcido (cobrindo={o.IsCovering}, alpha {o.Alpha:0.00})");
                }

                await Shot($"qa-oclusao-{i}-atras");
            }
            else
            {
                report.Add($"{name}: não há ponto andável atrás (encostado em parede) — sem teste de 'atrás'");
            }

            // na frente: logo abaixo da base, o corpo sobe por cima do desenho e o recorte fica opaco
            Vector2? front = null;
            for (var y = o.Def.BaseY + 2f; y < o.Def.BaseY + 30f && front is null; y += 2f)
            {
                if (world.WalkArea.CanStand(cx, y))
                {
                    front = new Vector2(cx, y);
                }
            }

            if (front is { } f)
            {
                player.Teleport(f);
                await WaitSeconds(0.5);
                report.Add($"{name}: na frente em {f} → cobrindo={o.IsCovering} alpha={o.Alpha:0.00}");
                if (o.IsCovering || o.Alpha < 0.99f)
                {
                    failures.Add($"{name}: pé em {f} na frente (base {o.Def.BaseY}) e o recorte ficou translúcido (alpha {o.Alpha:0.00})");
                }

                await Shot($"qa-oclusao-{i}-frente");
            }
            else
            {
                report.Add($"{name}: sem ponto andável logo abaixo da base");
            }
        }

        foreach (var obj in world.Dressing.Objects.Where(o => o.Def.Occluder))
        {
            var p = obj.Position;
            var behindAt = world.CorrectToWalkable(p + new Vector2(0, -10f));
            player.Teleport(behindAt);
            await WaitSeconds(0.5);
            var behindAlpha = obj.Alpha;
            var frontAt = world.CorrectToWalkable(p + new Vector2(0, 14f));
            player.Teleport(frontAt);
            await WaitSeconds(0.5);
            report.Add($"objeto {obj.Def.Id}: atrás {behindAt} alpha {behindAlpha:0.00}, na frente {frontAt} alpha {obj.Alpha:0.00}");
            if (frontAt.Y > p.Y && obj.Alpha < 0.99f)
            {
                failures.Add($"objeto {obj.Def.Id}: na frente (pé {frontAt}) e ficou translúcido ({obj.Alpha:0.00})");
            }
        }

        EndMotor();
        foreach (var line in report)
        {
            Log.Info($"qa: {line}");
        }

        Require(failures.Count == 0, $"{failures.Count} problema(s) de oclusão:\n  " + string.Join("\n  ", failures));
    }

    [SmokeScenario("motor (qa): andar liso medido quadro a quadro (latência, passos, câmera, quadro)", order: 62)]
    private async Task QaWalkSmoothness()
    {
        var world = await MotorWorld("guild", new Vector2(250, 280));
        var player = FindPlayer()!;
        var cam = world.Camera;
        await WaitSeconds(0.4);

        // latência: quadros de desenho entre apertar e o corpo andar
        var x0 = player.VisualPosition.Y; // a Guilda é mais estreita que a vista: a câmera só segue em y
        Input.ActionPress(InputActions.MoveDown);
        var latency = 0;
        while (player.VisualPosition.Y - x0 < 0.05f && latency < 30)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            latency++;
        }

        // andando: passo desenhado por quadro, posição do corpo na tela e tempo de quadro
        var steps = new List<float>();
        var screen = new List<float>();
        var frameMs = new List<double>();
        var last = player.VisualPosition.Y;
        var t = Time.GetTicksUsec();
        for (var i = 0; i < 150; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            frameMs.Add((now - t) / 1000.0);
            t = now;
            var x = player.VisualPosition.Y;
            steps.Add(x - last);
            last = x;
            screen.Add((x - cam.Position.Y) * cam.PixelScale);
        }

        // frenagem: quadros e distância até parar
        var releaseX = player.GlobalPosition.Y;
        Input.ActionRelease(InputActions.MoveDown);
        var stopFrames = 0;
        while (player.Velocity.LengthSquared() > 0.0001f && stopFrames < 60)
        {
            await NextPhysicsFrame();
            stopFrames++;
        }

        var skid = player.GlobalPosition.Y - releaseX;

        // só os quadros em velocidade de cruzeiro (depois da aceleração e antes de encostar em algo)
        var cruise = steps.Skip(10).Where(s => s > 0.05f).ToList();
        var mean = cruise.Count > 0 ? cruise.Average() : 0f;
        var stalls = steps.Skip(10).Count(s => s < mean * 0.35f);
        var doubles = cruise.Count(s => s > mean * 1.8f);
        var settled = screen.Skip(90).ToList();
        var screenJitter = settled.Count > 1 ? settled.Zip(settled.Skip(1), (a, b) => MathF.Abs(b - a)).Max() : 0f;
        var backwards = screen.Zip(screen.Skip(1), (a, b) => b - a).Count(dx => dx < -0.5f); // corpo andando para trás na tela
        var sorted = frameMs.Skip(5).OrderBy(v => v).ToList();
        var p95 = sorted.Count > 0 ? sorted[(int)(sorted.Count * 0.95) - 1] : 0;
        var avg = sorted.Count > 0 ? sorted.Average() : 0;
        Log.Info($"qa: andar — latência {latency} quadro(s), passo médio {mean:0.000} u/quadro, travadas {stalls}, saltos duplos {doubles}, " +
                 $"corpo na tela oscilando até {screenJitter:0.0} px depois de assentar, recuos na tela {backwards}, frenagem {stopFrames} ticks / {skid:0.00} u, " +
                 $"quadro médio {avg:0.00} ms p95 {p95:0.00} ms (×{cam.PixelScale}, {DisplayServer.GetName()})");
        EndMotor();

        Require(latency <= 3, $"apertar → andar levou {latency} quadros (>3: o andar parece travado)");
        Require(skid <= 4f && stopFrames <= 6, $"soltar → parar: {stopFrames} ticks e {skid:0.00} u (patinando)");
        if (DisplayServer.GetName() != "headless")
        {
            Require(stalls + doubles <= steps.Count / 20, $"{stalls} quadros parados e {doubles} saltos duplos em {steps.Count} quadros andando reto (tremido)");
            Require(screenJitter <= 1.01f * cam.PixelScale, $"corpo do jogador oscila {screenJitter:0.0} px na tela com a câmera assentada (imagem tremendo)");
            Require(p95 <= 20.0, $"p95 do quadro {p95:0.00} ms > 20 ms");
        }
    }

    [SmokeScenario("motor (qa): proporções do jogador contra porta, lampião, barril, baú e NPC", order: 63)]
    private async Task QaProportions()
    {
        var world = await MotorWorld("forge", new Vector2(250, 312));
        var player = FindPlayer()!;
        player.ClassId = "sword";
        await Frames(3);
        var rules = WorldScale.Rules!;
        var hunterTex = player.Animator.Hunter.SpriteFrames.GetFrameTexture("idle_front", 0);
        var hunter = VisibleHeight(hunterTex) * MathF.Abs(player.Animator.Hunter.GlobalScale.Y);
        var rows = new List<(string What, float Height)>
        {
            ("caçador (quadro do kit, pixels visíveis)", hunter),
            ("caçador (VisualHeight)", player.VisualHeight),
            ("porta da Estalagem (calibração)", (float)rules.Calibration.DoorHeight),
        };
        foreach (var key in new[] { "c_bow_front", "npc_bras_f", "wolf_f", "pr_barrel", "pr_crate", "pr_sack", "pr_urn", "pr_chest1" })
        {
            if (AtlasLibrary.TryGet(key, out var f))
            {
                rows.Add((key, VisibleHeight(f.Texture) * WorldScale.For(key, SpriteScaleClass.Auto) / f.Density));
            }
        }

        foreach (var o in world.Dressing.Occluders)
        {
            rows.Add(($"recorte '{o.Def.Note}'", o.Def.BaseY - o.Def.Bounds.Y));
        }

        foreach (var o in world.Dressing.Objects)
        {
            rows.Add(($"objeto {o.Def.Id}", o.Bounds.Height));
        }

        await Shot("qa-proporcao-porta");
        EndMotor();
        foreach (var (what, h) in rows)
        {
            Log.Info($"qa: altura {what,-45} {h,6:0.0} u  ({h / hunter:0.00}× o caçador)");
        }

        var door = (float)rules.Calibration.DoorHeight;
        Require(hunter / door is >= 0.8f and <= 0.95f, $"caçador visível {hunter:0.0} u para porta {door} u ({hunter / door:0.00})");
        var npc = rows.First(r => r.What == "npc_bras_f").Height;
        Require(npc / hunter is >= 0.85f and <= 1.25f, $"NPC {npc:0.0} u vs caçador {hunter:0.0} u ({npc / hunter:0.00}×)");
        var barrel = rows.First(r => r.What == "pr_barrel").Height;
        Require(barrel / hunter is >= 0.35f and <= 0.7f, $"barril {barrel:0.0} u vs caçador {hunter:0.0} u ({barrel / hunter:0.00}×, esperado ~0,5)");
        var bow = rows.First(r => r.What == "c_bow_front").Height;
        Require(MathF.Abs(bow - hunter) / hunter < 0.15f, $"arqueiro {bow:0.0} u vs caçador da Lâmina {hunter:0.0} u: classes com tamanhos diferentes");
    }

    [SmokeScenario("motor (qa): minimapa projeta jogador e câmera no lugar certo", order: 64)]
    private async Task QaMinimapPlacement()
    {
        var world = await MotorWorld("forge", new Vector2(1500, 250));
        var player = FindPlayer()!;
        var report = new List<string>();
        foreach (var at in new[] { new Vector2(1500, 250), new Vector2(305, 790), new Vector2(820, 540) })
        {
            player.Teleport(world.CorrectToWalkable(at));
            await WaitSeconds(0.4);
            var screen = GetViewport().GetVisibleRect().Size;
            var panel = new MapRect(screen.X - 6f - 120f, 28f, 120f, 70f);
            var proj = new MinimapProjection(world.Map.ImageWidth, world.Map.ImageHeight, panel);
            var p = player.VisualPosition;
            var pp = proj.ToPanel(p.X, p.Y);
            var n = world.Camera.PixelScale;
            report.Add($"jogador em ({p.X:0},{p.Y:0}) → minimapa ({pp.X:0.0},{pp.Y:0.0}) u = ({pp.X * n:0},{pp.Y * n:0}) px de tela");
            Require(pp.X >= panel.X && pp.X <= panel.Right && pp.Y >= panel.Y && pp.Y <= panel.Bottom, $"jogador fora do painel do minimapa: {pp}");
            await Shot($"qa-minimapa-{(int)at.X}-{(int)at.Y}");
        }

        foreach (var line in report)
        {
            Log.Info($"qa: {line}");
        }

        EndMotor();
    }

    [SmokeScenario("motor (qa): som ambiente cresce ao se aproximar da fonte", order: 65)]
    private async Task QaAmbientApproach()
    {
        var world = await MotorWorld("forge", new Vector2(830, 400));
        var audio = AudioManager.Instance;
        var player = FindPlayer()!;
        var levels = new List<(float Distance, float Level)>();
        var water = world.Layout.Ambient.First(a => a.Kind == "water");
        var src = new Vector2(water.X, water.Y);
        foreach (var dist in new[] { 360f, 220f, 150f, 90f, 40f })
        {
            var at = world.CorrectToWalkable(src + new Vector2(0, -dist));
            player.Teleport(at);
            await WaitSeconds(1.6);
            levels.Add((at.DistanceTo(src), audio.AmbientLevel("water")));
        }

        EndMotor();
        Log.Info("qa: água por distância: " + string.Join(", ", levels.Select(l => $"{l.Distance:0} u → {l.Level:0.00}")));
        var drops = 0;
        for (var i = 1; i < levels.Count; i++)
        {
            if (levels[i].Distance < levels[i - 1].Distance - 5f && levels[i].Level < levels[i - 1].Level - 0.02f)
            {
                drops++;
            }
        }

        Require(drops == 0, "chegando mais perto da água o som diminuiu em algum passo");
        Require(levels[^1].Level > levels[0].Level + 0.3f, $"perto ({levels[^1].Level:0.00}) quase igual a longe ({levels[0].Level:0.00})");
    }

    [SmokeScenario("motor (qa): quebrável volta no dia seguinte com o jogador em cima sem prender nem pular", order: 66)]
    private async Task QaRestoreUnderPlayer()
    {
        var world = await MotorWorld("forge", new Vector2(628, 540));
        var d = world.Dressing;
        var player = FindPlayer()!;
        var b = d.Breakables.First(x => x.Kind == "barrel" && !x.Key.Contains(":object:", StringComparison.Ordinal));
        b.Hit(999, player.GlobalPosition);
        await Frames(3);
        Require(b.IsBroken, "barril quebrado");
        player.Teleport(b.GlobalPosition);
        await Frames(3);
        var before = player.GlobalPosition;
        EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
        await Frames(4);
        var jump = player.GlobalPosition.DistanceTo(before);
        var moved = 0f;
        foreach (var dir in new[] { Vector2.Down, Vector2.Left, Vector2.Right, Vector2.Up })
        {
            var p0 = player.GlobalPosition;
            PressDirection(dir);
            await Frames(20);
            PressDirection(Vector2.Zero);
            moved = MathF.Max(moved, player.GlobalPosition.DistanceTo(p0));
        }

        await Shot("qa-quebravel-volta-embaixo");
        Log.Info($"qa: barril voltou com o jogador em cima: deslocado {jump:0.0} u na hora, andou até {moved:0.0} u depois");
        EndMotor();
        Require(jump < 10f, $"o barril voltou e jogou o jogador {jump:0.0} u para longe");
        Require(moved > 8f, $"preso dentro do barril que voltou (andou só {moved:0.0} u)");
    }

    [SmokeScenario("motor (qa): trocar de mapa no meio da balsa, da pesca e sentado devolve o controle", order: 67)]
    private async Task QaActivityInterrupted()
    {
        var failures = new List<string>();
        foreach (var kind in new[] { "boat", "fishing", "bench" })
        {
            var world = await MotorWorld("forge", new Vector2(600, 540));
            var spot = DecorOf(world.Dressing, kind);
            await FocusDecor(world, spot);
            var mask = FindPlayer()!.CollisionMask;
            await TapInteract();
            await WaitSeconds(0.5);
            if (!world.Dressing.InActivity)
            {
                failures.Add($"{kind}: atividade não começou");
                continue;
            }

            world.GoMap("inn", 250, 520, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var p0 = player.GlobalPosition;
            await Hold(InputActions.MoveUp, 20);
            var moved = player.GlobalPosition.DistanceTo(p0);
            var hulls = world.Entities.GetChildren().Count(n => n.Name.ToString().Contains("FerryHull", StringComparison.Ordinal));
            if (world.Dressing.InActivity || player.MovementLocked || GameManager.Instance.IsGameplayBlocked || player.CollisionMask != mask || moved < 5f || hulls > 0)
            {
                failures.Add($"{kind}: depois de trocar de mapa — atividade={world.Dressing.InActivity}, travado={player.MovementLocked}, bloqueado={GameManager.Instance.IsGameplayBlocked}, máscara {player.CollisionMask}/{mask}, andou {moved:0.0} u, cascos {hulls}");
            }
        }

        EndMotor();
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    // ---------------------------------------------------------------- estresse

    [SmokeScenario("estresse: motor (qa) 30 quebras, 20 balsas ida e volta, 10 pescas, flores e lampiões sem vazar nós", order: 90)]
    private async Task QaStressInteractions()
    {
        var world = await MotorWorld("forge", new Vector2(600, 540));
        var player = FindPlayer()!;
        await WaitSeconds(0.5);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var objects0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var broken = 0;
        var chests = 0;
        void OnBroken(string m, string k, Vector2 at) => broken++;
        void OnChest(string m, int t, Vector2 at) => chests++;
        EventBus.Instance.PropBroken += OnBroken;
        EventBus.Instance.ChestOpened += OnChest;
        var failures = new List<string>();
        try
        {
            using var speed = new SpeedUp(QaSpeedUp);

            // 30 quebras: golpe de verdade (ataque provisório) parado ao lado de cada quebrável; dia seguinte e de novo
            while (broken < 30)
            {
                foreach (var b in world.Dressing.Breakables.Where(x => !x.IsBroken).ToList())
                {
                    var spot = world.CorrectToWalkable(b.GlobalPosition + new Vector2(0, 14f));
                    player.Teleport(spot);
                    await Frames(2);
                    await QaBreakByAttacking(b);
                    if (!b.IsBroken)
                    {
                        failures.Add($"quebrável {b.Kind} em {b.Position} não quebrou com o jogador em {spot}");
                        b.Hit(999, spot);
                    }
                }

                EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
                await Frames(3);
            }

            // baús: abre os dois, dia seguinte, 5 vezes
            for (var round = 0; round < 5; round++)
            {
                foreach (var c in world.Dressing.Chests)
                {
                    await FocusOn(world, c.Interactable);
                    await TapInteract();
                    await WaitUntil(() => c.IsOpen, 8, "baú abrir");
                }

                EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
                await Frames(3);
            }

            // 20 travessias de ida e volta
            var boats = world.Dressing.Decor.Where(s => s.Def.Kind == "boat").ToList();
            for (var trip = 0; trip < 40; trip++)
            {
                var boat = boats[trip % 2];
                await FocusDecor(world, boat);
                await TapInteract();
                await WaitUntil(() => !world.Dressing.InActivity, 20, $"fim da travessia {trip}");
                var to = boat.Def.To!.Value;
                if (player.GlobalPosition.DistanceTo(new Vector2(to.X, to.Y)) > 24f)
                {
                    failures.Add($"travessia {trip}: chegou em {player.GlobalPosition}, esperava ({to.X}, {to.Y})");
                }
            }

            // 10 pescas com a agulha na faixa
            var fishId = WorldScale.Rules!.Fishing.Material;
            var fish0 = GameManager.Instance.State.MaterialCount(fishId);
            for (var n = 0; n < 10; n++)
            {
                var spot = DecorOf(world.Dressing, "fishing");
                await FocusDecor(world, spot);
                await TapInteract();
                var game = world.Dressing.Fishing ?? throw new SmokeFailure("pesca não começou");
                await WaitUntil(() => game.State == FishingState.Biting, 10, "morder");
                await WaitUntil(() => game.NeedleInZone, 10, "agulha na faixa");
                await TapInteract();
                await WaitUntil(() => !world.Dressing.InActivity, 10, "fim da pesca");
            }

            var fished = GameManager.Instance.State.MaterialCount(fishId) - fish0;
            if (fished < 10)
            {
                failures.Add($"10 pescas com a agulha na faixa deram {fished} peixe(s)");
            }

            // flores e lampiões
            for (var n = 0; n < 10; n++)
            {
                var flower = DecorOf(world.Dressing, "flower");
                if (flower.Interactable.Enabled)
                {
                    await FocusDecor(world, flower);
                    await TapInteract();
                }

                EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
                await Frames(3);
                var lamp = DecorOf(world.Dressing, "lamp");
                await FocusDecor(world, lamp);
                await TapInteract();
                await TapInteract();
            }
        }
        finally
        {
            EventBus.Instance.PropBroken -= OnBroken;
            EventBus.Instance.ChestOpened -= OnChest;
        }

        await WaitSeconds(1.5); // estilhaços, textos e cascos liberados
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var objects1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        Log.Info($"qa: estresse de interações — {broken} quebras, {chests} baús, nós {nodes0} → {nodes1}, objetos {objects0} → {objects1}, órfãos {orphans}, popups ativos {world.Dressing.Popups.Active}");
        EndMotor();
        Require(broken >= 30 && chests >= 10, $"quebras {broken}, baús {chests}");
        Require(nodes1 - nodes0 <= 8, $"nós cresceram {nodes1 - nodes0} depois das interações (vazamento)");
        Require(orphans == 0, $"{orphans} nós órfãos");
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: motor (qa) 50 trocas de mapa seguidas sem crescer nós nem travar o quadro", order: 91)]
    private async Task QaStressMapSwaps()
    {
        var world = await MotorWorld("forge", new Vector2(600, 540));
        var ids = new[] { "forge", "guild", "forge", "inn", "forge", "shop_arm", "fish2", "core", "forge", "cath" };
        var nodes = new List<double>();
        var mem = new List<double>();
        var worstFrame = 0.0;
        for (var i = 0; i < 50; i++)
        {
            var id = ids[i % ids.Length];
            var map = World.World.Catalog[id];
            var t = Time.GetTicksUsec();
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: i % 2 == 0);
            var last = Time.GetTicksUsec();
            while (world.IsTransitioning || world.FadeAlpha > 0f)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksUsec();
                worstFrame = Math.Max(worstFrame, (now - last) / 1000.0);
                last = now;
                Require(Time.GetTicksUsec() - t < 5_000_000, $"troca {i} para {id} não terminou");
            }

            await Frames(2);
            nodes.Add(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
            mem.Add(Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024));
            if (i % 10 == 9)
            {
                Log.Info($"qa: troca {i + 1}: {id}, nós {nodes[^1]}, memória {mem[^1]:0.0} MB, órfãos {Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)}, pior quadro até aqui {worstFrame:0.0} ms");
            }
        }

        EndMotor();
        // mesma sequência de mapas a cada 10 trocas: nós e memória devem repetir, não subir
        var growthNodes = nodes[49] - nodes[19];
        var growthMem = mem[49] - mem[19];
        Log.Info($"qa: 50 trocas — nós (20ª → 50ª) {nodes[19]} → {nodes[49]}, memória {mem[19]:0.0} → {mem[49]:0.0} MB, pior quadro {worstFrame:0.0} ms");
        Require(growthNodes <= 5, $"nós crescem com as trocas: +{growthNodes}");
        Require(growthMem <= 8, $"memória cresce com as trocas: +{growthMem:0.0} MB");
        Require(Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount) == 0, "nós órfãos depois das trocas");
    }

    [SmokeScenario("estresse: motor (qa) bots de caminhada aleatória e de pontos de interesse em todos os mapas", order: 92)]
    private async Task QaStressBots()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var player = FindPlayer()!;
        var rng = new Random(4242);
        var traps = new List<string>();
        var unreachable = new List<string>();
        var stuck = new List<string>();
        var seen = new HashSet<string>();
        var gameSeconds = 0.0;
        var mapIds = new[] { "guild", "forge", "forge+fixture", "inn", "shop_arm", "shop_mag", "shop_alq", "shop_rel", "fish2", "crypt2", "mine", "garden", "cath", "core" };
        using (new SpeedUp(QaSpeedUp))
        {
            foreach (var entry in mapIds)
            {
                var id = entry.Split('+')[0];
                var fixture = entry.EndsWith("+fixture", StringComparison.Ordinal);
                if (fixture)
                {
                    World.World.LayoutOverrides[CityMapIdForFixture] = FixtureLayout;
                    WorldDressing.ExtraCatalogPath = FixtureObjects;
                    WorldDressing.ReloadCatalog();
                }

                var map = World.World.Catalog[id];
                world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                await WaitMapSettled(world);

                // 1) caminhada aleatória: troca de direção a cada 0,4–1,4 s de jogo; parado 1 s com input → testa as 8 direções
                var ticks = 0;
                var budget = 24 * 60; // 24 s de jogo por mapa
                var window = new Queue<Vector2>();
                var dir = Vector2.Zero;
                var nextTurn = 0;
                while (ticks < budget)
                {
                    if (world.MapId != id || world.IsTransitioning)
                    {
                        // saiu por uma saída: volta para o spawn deste mapa
                        PressDirection(Vector2.Zero);
                        await WaitMapSettled(world);
                        world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                        await WaitMapSettled(world);
                        window.Clear();
                    }

                    if (ticks >= nextTurn)
                    {
                        var a = rng.Next(8) * MathF.PI / 4f;
                        dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        nextTurn = ticks + 24 + rng.Next(60);
                        window.Clear();
                    }

                    PressDirection(dir);
                    await NextPhysicsFrame();
                    ticks++;
                    var p = player.GlobalPosition;
                    if (world.MapId == id && !world.IsTransitioning && !world.WalkArea.IsWalkable(p.X, p.Y) && seen.Add($"{entry}:{(int)(p.X / 8)}:{(int)(p.Y / 8)}"))
                    {
                        traps.Add($"{entry}: pé em área bloqueada em ({p.X:0},{p.Y:0})");
                    }

                    window.Enqueue(p);
                    if (window.Count > 60)
                    {
                        window.Dequeue();
                        if (window.Peek().DistanceTo(p) < 1f)
                        {
                            // encostado: alguma das 8 direções anda?
                            var escaped = false;
                            for (var k = 0; k < 8 && !escaped; k++)
                            {
                                var ak = k * MathF.PI / 4f;
                                var p0 = player.GlobalPosition;
                                PressDirection(new Vector2(MathF.Cos(ak), MathF.Sin(ak)));
                                await Frames(12);
                                ticks += 12;
                                escaped = player.GlobalPosition.DistanceTo(p0) > 1f || world.MapId != id;
                            }

                            if (!escaped)
                            {
                                traps.Add($"{entry}: TRAVADO em ({p.X:0},{p.Y:0}) — nenhuma das 8 direções anda 1 u");
                                var free = world.WalkArea.NearestWalkable(p.X, p.Y + 12f);
                                player.Teleport(free is { } fp ? new Vector2(fp.X, fp.Y) : new Vector2(map.Spawn.X, map.Spawn.Y));
                            }

                            window.Clear();
                            nextTurn = ticks;
                        }
                    }
                }

                PressDirection(Vector2.Zero);
                gameSeconds += ticks / 60.0;

                // 2) pontos de interesse: cada interagível ligado, porta e fuga das Brechas, a partir do spawn
                var pois = new List<(string Label, Func<Interactable?> Resolve)>();
                foreach (var it in map.Interactables)
                {
                    if (world.GetInteractable(it.Id) is { Enabled: true } node)
                    {
                        var itId = it.Id;
                        pois.Add(($"{it.Id} '{node.Prompt}' ({it.X},{it.Y})", () => world.GetInteractable(itId)));
                    }
                }

                if (fixture)
                {
                    for (var k = 0; k < world.Dressing.Decor.Count; k++)
                    {
                        var s = world.Dressing.Decor[k];
                        var dk = k;
                        pois.Add(($"decor {s.Def.Kind} ({s.Def.X},{s.Def.Y})", () => dk < world.Dressing.Decor.Count ? world.Dressing.Decor[dk].Interactable : null));
                    }

                    for (var k = 0; k < world.Dressing.Chests.Count; k++)
                    {
                        var c = world.Dressing.Chests[k];
                        var ck = k;
                        pois.Add(($"baú ({c.Position.X},{c.Position.Y})", () => ck < world.Dressing.Chests.Count ? world.Dressing.Chests[ck].Interactable : null));
                    }
                }

                foreach (var (label, resolve) in pois)
                {
                    world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                    await WaitMapSettled(world);
                    if (resolve() is not { Enabled: true } target)
                    {
                        continue;
                    }

                    var frames0 = Engine.GetPhysicsFrames();
                    var error = await QaWalkToFocus(world, target);
                    gameSeconds += (Engine.GetPhysicsFrames() - frames0) / 60.0;
                    if (error is null)
                    {
                        continue;
                    }

                    if (error.StartsWith("sem caminho", StringComparison.Ordinal) && QaOnlyByBoat(world, target))
                    {
                        continue; // B4b-D62: os cais da Rua da Forja são só de balsa (a fixture tem o par de balsas leste/oeste)
                    }

                    (error.StartsWith("sem caminho", StringComparison.Ordinal) ? unreachable : stuck).Add($"{entry} {label}: {error}");
                }

                if (fixture)
                {
                    World.World.LayoutOverrides.Remove(CityMapIdForFixture);
                    WorldDressing.ExtraCatalogPath = null;
                    WorldDressing.ReloadCatalog();
                }

                Log.Info($"qa: bots em {entry}: {gameSeconds:0} s de jogo acumulados, armadilhas {traps.Count}, inalcançáveis {unreachable.Count}, travados {stuck.Count}");
            }
        }

        foreach (var line in traps.Concat(unreachable).Concat(stuck))
        {
            Log.Info($"qa: bot — {line}");
        }

        Require(gameSeconds >= 300, $"bots rodaram só {gameSeconds:0} s de jogo");
        Require(traps.Count == 0 && unreachable.Count == 0 && stuck.Count == 0,
            $"{traps.Count} armadilha(s), {unreachable.Count} ponto(s) inalcançável(is), {stuck.Count} travamento(s) indo a pontos de interesse:\n  " + string.Join("\n  ", traps.Concat(unreachable).Concat(stuck).Take(40)));
    }

    [SmokeScenario("estresse: motor (qa) spam de interagir, esquivar, atacar, pausar e mapa", order: 93)]
    private async Task QaStressButtonSpam()
    {
        var world = await MotorWorld("forge", new Vector2(700, 500));
        var player = FindPlayer()!;
        await WaitSeconds(0.4);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var rng = new Random(77);
        StringName[] buttons = [InputActions.Interact, InputActions.Dodge, InputActions.Attack, InputActions.Pause, InputActions.Map, InputActions.AttackHeavy];
        var spots = world.Dressing.Decor.Select(s => s.Interactable.GlobalPosition).Concat(world.Dressing.Chests.Select(c => c.GlobalPosition)).ToList();
        using (new SpeedUp(2))
        {
            for (var i = 0; i < 2400; i++)
            {
                if (i % 240 == 0)
                {
                    // vai para perto de outra coisa interagível para o spam acertar atividades diferentes
                    var at = spots[rng.Next(spots.Count)];
                    if (!world.Dressing.InActivity)
                    {
                        player.Teleport(world.CorrectToWalkable(at + new Vector2(0, 10f)));
                    }
                }

                var b = buttons[rng.Next(buttons.Length)];
                Input.ParseInputEvent(new InputEventAction { Action = b, Pressed = rng.Next(2) == 0 });
                if (rng.Next(6) == 0)
                {
                    var a = rng.Next(9);
                    PressDirection(a == 8 ? Vector2.Zero : new Vector2(MathF.Cos(a * MathF.PI / 4f), MathF.Sin(a * MathF.PI / 4f)));
                }

                await NextPhysicsFrame();
            }
        }

        foreach (var b in buttons)
        {
            Input.ParseInputEvent(new InputEventAction { Action = b, Pressed = false });
        }

        PressDirection(Vector2.Zero);
        if (world.Minimap.Expanded)
        {
            world.Minimap.SetExpanded(false);
        }

        // o spam de Pause/Mapa abre o menu de pausa de verdade (é a tela "ui" que trava o jogador): quem joga fecha com Esc
        UI.GameUi.Current?.CloseAll();
        // levantar do banco / desistir da pesca / esperar a balsa: o jogo tem que devolver o controle
        await Hold(InputActions.MoveDown, 30);
        await WaitUntil(() => !world.Dressing.InActivity && !GameManager.Instance.IsGameplayBlocked, 10, "jogo destravar depois do spam");
        var p0 = player.GlobalPosition;
        await Hold(InputActions.MoveDown, 20);
        await Hold(InputActions.MoveLeft, 20);
        var moved = player.GlobalPosition.DistanceTo(p0);
        await WaitSeconds(1.0);
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        Log.Info($"qa: spam — nós {nodes0} → {nodes1}, andou {moved:0.0} u depois, máscara {player.CollisionMask}, travado {player.MovementLocked}");
        await Shot("qa-depois-do-spam");
        EndMotor();
        Require(moved > 10f, $"depois do spam o jogador não anda ({moved:0.0} u)");
        Require(!player.MovementLocked && player.CollisionMask != 0, "spam deixou o jogador travado ou sem colisão");
        Require(nodes1 - nodes0 <= 8, $"spam vazou {nodes1 - nodes0} nós");
    }

    [SmokeScenario("estresse: motor (qa) tempo de quadro e memória andando 20 s na Rua da Forja vestida", order: 94)]
    private async Task QaStressFrameTime()
    {
        var world = await MotorWorld("forge", new Vector2(820, 540));
        var player = FindPlayer()!;
        await WaitSeconds(0.5);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        var frames = new List<double>(1400);
        var t = Time.GetTicksUsec();
        var start = t;
        var leg = 0;
        Vector2[] dirs = [Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up];
        while (Time.GetTicksUsec() - start < 20_000_000)
        {
            PressDirection(dirs[(leg++ / 90) % 4]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            frames.Add((now - t) / 1000.0);
            t = now;
        }

        PressDirection(Vector2.Zero);
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        var sorted = frames.Skip(10).OrderBy(v => v).ToList();
        var avg = sorted.Average();
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var p99 = sorted[(int)(sorted.Count * 0.99)];
        var over = sorted.Count(v => v > 25.0);
        Log.Info($"qa: 20 s andando — {frames.Count} quadros, média {avg:0.00} ms ({1000 / avg:0} FPS), p95 {p95:0.00} ms, p99 {p99:0.00} ms, máx {sorted[^1]:0.0} ms, >25 ms: {over}, nós {nodes0} → {nodes1}, memória {mem0:0.0} → {mem1:0.0} MB (pé {player.GlobalPosition})");
        EndMotor();
        Require(nodes1 - nodes0 <= 3, $"nós crescem andando: +{nodes1 - nodes0}");
        Require(mem1 - mem0 < 4, $"memória cresce andando: +{mem1 - mem0:0.0} MB");
        if (DisplayServer.GetName() != "headless")
        {
            Require(p95 <= 20.0, $"p95 do quadro {p95:0.00} ms > 20 ms");
        }
    }

    // ---------------------------------------------------------------- utilidades do tester

    /// <summary>Anda (input real) até o <paramref name="target"/> ficar em foco. Devolve null se conseguiu.</summary>
    /// <summary>Sem caminho a pé daqui, mas com caminho a partir do destino de alguma balsa do mapa.</summary>
    private bool QaOnlyByBoat(World.World world, Interactable target) =>
        QaOnlyByBoat(world, target.GlobalPosition, MathF.Max(5f, target.Radius * 0.7f));

    private bool QaOnlyByBoat(World.World world, Vector2 at, float reach)
    {
        var player = FindPlayer()!;
        var c = new MapPoint(at.X, at.Y);
        if (WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), c, reach) is not null)
        {
            return false;
        }

        foreach (var spot in world.Dressing.Decor)
        {
            if (spot.Def is { Kind: "boat", To: { } to }
                && world.WalkArea.NearestWalkable(to.X, to.Y) is { } dock
                && WalkPathfinder.FindPathNear(world.WalkArea, dock, c, reach + 8f) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string?> QaWalkToFocus(World.World world, Interactable target, float timeoutGameSeconds = 40f)
    {
        var player = FindPlayer()!;
        var c = target.GlobalPosition;
        var reach = MathF.Max(5f, target.Radius * 0.7f);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), new MapPoint(c.X, c.Y), reach);
        if (path is null)
        {
            return $"sem caminho no Core até {reach:0} u de ({c.X:0},{c.Y:0})";
        }

        var end = new Vector2(path[^1].X, path[^1].Y);
        var error = await TesterWalkPath(world, path, () => player.FocusedInteractable == target || player.GlobalPosition.DistanceTo(end) < 2f, timeoutGameSeconds);
        await Frames(3);
        if (error is null && player.FocusedInteractable != target && player.FocusedInteractable is not null)
        {
            // dois interagíveis colados (baú ao lado de uma flor): o foco é do mais perto, então quem joga dá mais um passo
            // em direção ao alvo até ele virar o foco
            for (var step = 0; step < 90 && player.FocusedInteractable != target && player.GlobalPosition.DistanceTo(c) > 3f; step++)
            {
                PressDirection((c - player.GlobalPosition).Normalized());
                await NextPhysicsFrame();
            }

            PressDirection(Vector2.Zero);
            await Frames(3);
        }

        if (player.FocusedInteractable == target)
        {
            return null;
        }

        var dist = player.GlobalPosition.DistanceTo(c);
        return error is not null
            ? $"{error} (a {dist:0.0} u do alvo, raio {target.Radius:0})"
            : $"chegou a {dist:0.0} u (raio {target.Radius:0}) mas o foco é '{player.FocusedInteractable?.Prompt ?? "nenhum"}'";
    }

    /// <summary>Usa o interagível como jogador e confere o retorno. Devolve null se o retorno veio.</summary>
    private async Task<string?> QaUse(World.World world, Interactable it, string kind)
    {
        var d = world.Dressing;
        var player = FindPlayer()!;
        switch (kind)
        {
            case "lamp":
            {
                var lit = d.Lights.LitCount;
                await TapInteract();
                await Frames(2);
                var changed = d.Lights.LitCount != lit;
                await TapInteract();
                return changed && d.Lights.LitCount == lit ? null : $"lampião não alternou (acesos {lit} → {d.Lights.LitCount})";
            }

            case "flower":
            {
                var herb = WorldScale.Rules!.Flower.Material;
                var before = GameManager.Instance.State.MaterialCount(herb);
                await TapInteract();
                await Frames(3);
                return GameManager.Instance.State.MaterialCount(herb) == before + 1 && d.Popups.Active > 0 ? null : "colher não deu erva com texto subindo";
            }

            case "boat":
            {
                await TapInteract();
                await WaitUntil(() => !d.InActivity, 20, "fim da balsa");
                await Frames(2);
                var p = player.GlobalPosition;
                return world.WalkArea.CanStand(p.X, p.Y) && !player.MovementLocked ? null : $"balsa terminou em {p} (andável={world.WalkArea.CanStand(p.X, p.Y)})";
            }

            case "fishing":
            {
                await TapInteract();
                var game = d.Fishing;
                if (game is null)
                {
                    return "pesca não começou";
                }

                await WaitUntil(() => game.State == FishingState.Biting, 10, "morder");
                await WaitUntil(() => game.NeedleInZone, 10, "agulha na faixa");
                await TapInteract();
                await WaitUntil(() => !d.InActivity, 10, "fim da pesca");
                return game.State == FishingState.Caught ? null : $"puxou na faixa e ficou {game.State}";
            }

            case "bench":
            {
                await TapInteract();
                await Frames(4);
                var sat = d.InActivity && player.Animator.Sitting;
                await Shot("qa-sentado");
                await WaitSeconds(0.5);
                await Hold(InputActions.MoveDown, 8);
                return sat && !d.InActivity ? null : $"sentar/levantar falhou (sentou={sat}, atividade={d.InActivity})";
            }

            case "chest":
            {
                var chest = d.Chests.First(c => c.Interactable == it);
                await TapInteract();
                await WaitUntil(() => chest.IsOpen, 4, "baú abrir");
                return null;
            }

            case "map":
                return null; // NPC/loja/portal: o pedido no EventBus é coberto por "mundo: interagíveis emitem pedido no EventBus"

            default:
            {
                await TapInteract();
                await Frames(3);
                var text = d.Lore.Text;
                await Shot($"qa-texto-{kind}");
                await Hold(InputActions.MoveDown, 6);
                return text.Length > 0 ? null : "texto curto não apareceu";
            }
        }
    }

    /// <summary>Vira para o quebrável e golpeia (ataque provisório) até quebrar, no máximo 8 golpes.</summary>
    private async Task<int> QaBreakByAttacking(Breakable b)
    {
        var player = FindPlayer()!;
        var hits = 0;
        for (var i = 0; i < 8 && !b.IsBroken; i++)
        {
            var to = b.GlobalPosition - player.GlobalPosition;
            var face = MathF.Abs(to.X) > MathF.Abs(to.Y) ? new Vector2(MathF.Sign(to.X), 0) : new Vector2(0, MathF.Sign(to.Y));
            PressDirection(face);
            await NextPhysicsFrame();
            PressDirection(Vector2.Zero);
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = true });
            await NextPhysicsFrame();
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = false });
            hits++;
            await WaitSeconds(0.35); // tempo de jogo (o timer segue o Engine.TimeScale)
        }

        return hits;
    }

    /// <summary>Altura em px dos pixels não transparentes da textura (recorte com sobra transparente conta só o desenho).</summary>
    private static float VisibleHeight(Texture2D texture)
    {
        var image = texture.GetImage();
        if (image is null)
        {
            return texture.GetHeight();
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }

        return image.GetUsedRect().Size.Y;
    }
}
