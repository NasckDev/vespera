using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Art;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.Enemies;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Testes do tester de experiência da frente C2 (arte aprovada animada), rodada 1. Jogam como um jogador exigente:
/// medem (em u, a partir dos pixels realmente desenhados) se o caçador "pula" de escala ou de pé ao parar, andar e virar,
/// se a esquiva casa com o dash nas 8 direções das 5 classes, se cada chefe é maior que o comum e troca de pose pela IA
/// de verdade nas 5 Brechas, se os objetos do mapa real mudam de estado ao serem usados, se os PNG novos não têm halo,
/// fundo nem corte, e estressam (classes trocando, esquivas, chefes criados e liberados, trocas de mapa) vigiando nós,
/// memória e tempo de quadro. Os "estresse:" rodam com <c>--smoke-stress</c>. Prints <c>arteqa-*.png</c> com <c>--smoke-shots</c>.
/// </summary>
public partial class SmokeTest
{
    private static readonly string[] ArteQaClasses = ["sword", "bow", "staff", "hammer", "dag"];
    private static readonly Dictionary<Texture2D, Rect2I> ArteQaUsed = [];

    /// <summary>Medida do que está desenhado agora: altura, pé (u abaixo/acima do ponto do chão) e centro horizontal.</summary>
    private readonly record struct ArteQaMetric(string Anim, bool Valid, float Height, float Foot, float CenterX, int Layers);

    private static Rect2I ArteQaUsedRect(Texture2D texture)
    {
        if (ArteQaUsed.TryGetValue(texture, out var cached))
        {
            return cached;
        }

        var image = texture.GetImage();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        var used = image.GetUsedRect();
        ArteQaUsed[texture] = used;
        return used;
    }

    /// <summary>Mede o quadro visível do caçador (arte nova, kit ou atlas) em u do mundo, já com a escala viva do corpo.</summary>
    private static ArteQaMetric ArteQaMeasure(Player.PlayerAnimator anim)
    {
        var layers = (anim.Walker.Visible ? 1 : 0) + (anim.Hunter.Visible ? 1 : 0) + (anim.Sprite.Visible ? 1 : 0);
        var name = anim.CurrentAnimation.Length > 0 ? anim.CurrentAnimation : $"atlas:{anim.Sprite.Key}";
        if (!anim.TryGetGhost(out var texture, out var anchor, out var upp))
        {
            return new ArteQaMetric(name, false, 0, 0, 0, layers);
        }

        var used = ArteQaUsedRect(texture);
        var s = anim.Body.Scale;
        var k = upp * MathF.Abs(s.Y);
        var height = used.Size.Y * k;
        var foot = ((used.End.Y - anchor.Y) * k) + anim.Body.Position.Y;
        var center = ((used.Position.X + (used.Size.X / 2f)) - anchor.X) * upp * s.X;
        return new ArteQaMetric(name, true, height, foot, center, layers);
    }

    private static string ArteQaDirName(Vector2 d) => d.X switch
    {
        > 0.5f when d.Y > 0.5f => "baixo-direita",
        < -0.5f when d.Y > 0.5f => "baixo-esquerda",
        > 0.5f when d.Y < -0.5f => "cima-direita",
        < -0.5f when d.Y < -0.5f => "cima-esquerda",
        > 0.5f => "direita",
        < -0.5f => "esquerda",
        _ => d.Y > 0 ? "baixo" : "cima",
    };

    private static readonly Vector2[] ArteQaDirs8 =
    [
        Vector2.Right, new Vector2(1, 1).Normalized(), Vector2.Down, new Vector2(-1, 1).Normalized(),
        Vector2.Left, new Vector2(-1, -1).Normalized(), Vector2.Up, new Vector2(1, -1).Normalized(),
    ];

    /// <summary>Toque de ataque como uma tecla: tempo real entre apertar e soltar (sem janela, a física pode não ver o toque).</summary>
    private async Task ArteQaTapAttack()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = false });
        await NextPhysicsFrame();
    }

    // ---------------------------------------------------------------- jogar como jogador: o caçador

    [SmokeScenario("arte (qa): testa pé e escala contínuos ao parar, andar e virar nas 5 classes", order: 150)]
    private async Task ArteQaContinuity()
    {
        var (world, _) = await CombatSetup();
        var failures = new List<string>();
        var summary = new List<string>();
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true); // piso aberto
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            await WaitSeconds(0.4);
            var idle = new Dictionary<string, ArteQaMetric>();
            var walk = new Dictionary<string, List<ArteQaMetric>>();
            var worstDh = 0f;
            var worstDfoot = 0f;
            var worstPair = string.Empty;
            var invisible = 0;
            var multi = 0;
            var turns = new[] { Vector2.Down, Vector2.Right, Vector2.Up, Vector2.Left, Vector2.Down, Vector2.Left, Vector2.Up, Vector2.Right };
            var shotDone = false;
            foreach (var dir in turns)
            {
                var key = ArteQaDirName(dir);
                PressDirection(dir);
                var list = new List<ArteQaMetric>();
                var prev = ArteQaMeasure(anim);
                var t0 = Time.GetTicksMsec();
                while (Time.GetTicksMsec() - t0 < 600)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var m = ArteQaMeasure(anim);
                    invisible += m.Valid ? 0 : 1;
                    multi += m.Layers != 1 ? 1 : 0;
                    if (m.Valid && prev.Valid)
                    {
                        worstDh = MathF.Max(worstDh, MathF.Abs(m.Height - prev.Height) / MathF.Max(1f, prev.Height));
                        if (MathF.Abs(m.Foot - prev.Foot) > worstDfoot)
                        {
                            worstDfoot = MathF.Abs(m.Foot - prev.Foot);
                            worstPair = $"{prev.Anim}[pé {prev.Foot:0.0}] > {m.Anim}[pé {m.Foot:0.0}] andando para {key}";
                        }
                    }

                    prev = m;
                    if (m.Valid && Time.GetTicksMsec() - t0 > 150)
                    {
                        list.Add(m);
                    }

                    if (!shotDone && dir == Vector2.Right && Time.GetTicksMsec() - t0 > 300)
                    {
                        shotDone = true;
                        await Shot($"arteqa-andar-{cls}");
                    }
                }

                var facingX = anim.Body.Scale.X;
                if (dir.X != 0f)
                {
                    Require(facingX * dir.X > 0f, $"{cls}: andando para {key} o corpo está espelhado ao contrário (scale.x {facingX:0.00})");
                }

                PressDirection(Vector2.Zero);
                await WaitSeconds(0.45);
                idle[key] = ArteQaMeasure(anim);
                walk[key] = list;
            }

            Require(invisible == 0, $"{cls}: {invisible} quadro(s) sem nenhum sprite visível ao andar/parar (pisca)");
            Require(multi == 0, $"{cls}: {multi} quadro(s) com mais de uma camada do caçador visível (imagem em cima da imagem)");
            foreach (var (key, list) in walk)
            {
                if (list.Count == 0 || !idle.TryGetValue(key, out var i) || !i.Valid)
                {
                    continue;
                }

                var h = list.Average(m => m.Height);
                var f = list.Average(m => m.Foot);
                var dh = MathF.Abs(h - i.Height) / i.Height;
                var df = MathF.Abs(f - i.Foot);
                summary.Add($"{cls}/{key}: parado {i.Anim} {i.Height:0.0} u pé {i.Foot:0.0} → andando {list[0].Anim} {h:0.0} u pé {f:0.0} (Δaltura {dh * 100:0}%, Δpé {df:0.0} u)");
                if (dh > 0.12f)
                {
                    failures.Add($"{cls}/{key}: a altura pula {dh * 100:0}% ao começar a andar ({i.Height:0.0} → {h:0.0} u)");
                }

                if (df > 3.5f)
                {
                    failures.Add($"{cls}/{key}: o pé pula {df:0.0} u ao começar a andar");
                }
            }

            Log.Info($"arte qa: {cls} pior salto quadro a quadro: altura {worstDh * 100:0.0}%, pé {worstDfoot:0.0} u ({worstPair})");
            if (worstDh > 0.10f)
            {
                failures.Add($"{cls}: salto de altura de {worstDh * 100:0}% entre dois quadros seguidos");
            }

            if (worstDfoot > 5f)
            {
                failures.Add($"{cls}: salto de pé de {worstDfoot:0.0} u entre dois quadros seguidos ({worstPair})");
            }
        }

        foreach (var line in summary)
        {
            Log.Info($"arte qa: {line}");
        }

        GameManager.Instance.NewGame("Bot", "sword");
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    [SmokeScenario("arte (qa): testa esquiva em 8 direções nas 5 classes casando com o dash", order: 151)]
    private async Task ArteQaDodgeEightWays()
    {
        var (world, combat) = await CombatSetup();
        var failures = new List<string>();
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            foreach (var dir in ArteQaDirs8)
            {
                var name = ArteQaDirName(dir);
                player.Teleport(world.CorrectToWalkable(new Vector2(250, 600)));
                await WaitSeconds(0.9); // energia e recarga
                var start = player.GlobalPosition;
                PressDirection(dir);
                Input.ActionPress(InputActions.Dodge);
                var startedDash = -1;
                var startedAnim = -1;
                var endDash = -1;
                var endAnim = -1;
                var maxOffset = 0f;
                var worstH = 0f;
                var views = new HashSet<string>();
                var frames = new HashSet<int>();
                var best = 0f;
                var flip = 0f;
                for (var i = 0; i < 40; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (i == 1)
                    {
                        Input.ActionRelease(InputActions.Dodge);
                    }

                    var dashing = combat.PlayerCombat.State.IsDashing;
                    if (dashing && startedDash < 0)
                    {
                        startedDash = i;
                    }

                    if (!dashing && startedDash >= 0 && endDash < 0)
                    {
                        endDash = i;
                    }

                    if (anim.IsDodging && startedAnim < 0)
                    {
                        startedAnim = i;
                    }

                    if (!anim.IsDodging && startedAnim >= 0 && endAnim < 0)
                    {
                        endAnim = i;
                    }

                    if (anim.IsDodging && i > startedAnim)
                    {
                        // a 1ª amostra é do quadro anterior: o sinal process_frame vem antes do _Process do animador
                        maxOffset = MathF.Max(maxOffset, anim.Body.Position.Length());
                        var m = ArteQaMeasure(anim);
                        Require(m.Valid && m.Layers == 1, $"{cls} esquiva {name}: quadro sem sprite ou com camadas empilhadas");
                        views.Add(m.Anim);
                        frames.Add(anim.CurrentFrame);
                        worstH = MathF.Max(worstH, m.Height);
                        best = MathF.Max(best, m.Height);
                        flip = anim.Body.Scale.X;
                        if (m.Height < 25f || m.Height > 80f)
                        {
                            failures.Add($"{cls} esquiva {name}: altura desenhada {m.Height:0.0} u fora do razoável");
                        }
                    }
                }

                Input.ActionRelease(InputActions.Dodge);
                PressDirection(Vector2.Zero);
                var moved = player.GlobalPosition - start;
                var cos = moved.Length() > 0.1f ? moved.Normalized().Dot(dir) : 0f;
                if (startedDash < 0)
                {
                    failures.Add($"{cls} esquiva {name}: o dash não começou");
                    continue;
                }

                if (startedAnim < 0 || Math.Abs(startedAnim - startedDash) > 2)
                {
                    failures.Add($"{cls} esquiva {name}: a animação começou no quadro {startedAnim} e o dash no {startedDash}");
                }

                if (endAnim >= 0 && endDash >= 0 && Math.Abs(endAnim - endDash) > 3)
                {
                    failures.Add($"{cls} esquiva {name}: a animação acabou no quadro {endAnim} e o dash no {endDash}");
                }

                if (maxOffset > 0.01f)
                {
                    failures.Add($"{cls} esquiva {name}: o sprite anda {maxOffset:0.00} u sozinho (deslocamento duplo)");
                }

                if (moved.Length() < 25f || cos < 0.85f)
                {
                    failures.Add($"{cls} esquiva {name}: o corpo foi {moved.Length():0.0} u na direção errada (cos {cos:0.00})");
                }

                if (MathF.Abs(dir.X) > 0.5f && flip * dir.X <= 0f)
                {
                    failures.Add($"{cls} esquiva {name}: sprite espelhado para o lado errado (scale.x {flip:0.00})");
                }

                if (dir == Vector2.Right || dir == Vector2.Down || dir == Vector2.Up)
                {
                    Log.Info($"arte qa: {cls} esquiva {name}: {string.Join(",", views)} {frames.Count} quadros, até {best:0.0} u, corpo {moved.Length():0.0} u");
                }
            }

            await Shot($"arteqa-esquiva-{cls}-fim");
        }

        GameManager.Instance.NewGame("Bot", "sword");
        Require(failures.Count == 0, string.Join("\n  ", failures.Take(30)));
    }

    [SmokeScenario("arte (qa): testa esquivar no meio do golpe mostra a esquiva e não desliza em pose de ataque", order: 152)]
    private async Task ArteQaDodgeDuringAttack()
    {
        var (world, combat) = await CombatSetup();
        var failures = new List<string>();
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            await WaitSeconds(0.9);
            PressDirection(Vector2.Right);
            await ArteQaTapAttack();
            await Frames(2);
            Input.ActionPress(InputActions.Dodge);
            var slidingAttackFrames = 0;
            var dashFrames = 0;
            var shot = false;
            for (var i = 0; i < 30; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (i == 1)
                {
                    Input.ActionRelease(InputActions.Dodge);
                }

                if (combat.PlayerCombat.State.IsDashing)
                {
                    dashFrames++;
                    if (anim.IsAttacking && !anim.IsDodging || anim.IsAttacking)
                    {
                        slidingAttackFrames++;
                    }

                    if (!shot && dashFrames == 4)
                    {
                        shot = true;
                        await Shot($"arteqa-esquiva-no-golpe-{cls}");
                    }
                }
            }

            PressDirection(Vector2.Zero);
            if (dashFrames == 0)
            {
                Log.Info($"arte qa: {cls}: o dash não começou depois do golpe (recarga/energia) — nada a medir");
                continue;
            }

            if (slidingAttackFrames > 0)
            {
                failures.Add($"{cls}: {slidingAttackFrames} de {dashFrames} quadros do dash saíram deslizando em pose de ataque (a esquiva desenhada não aparece)");
            }

            await WaitSeconds(0.6);
        }

        GameManager.Instance.NewGame("Bot", "sword");
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    // ---------------------------------------------------------------- chefes

    private static readonly string[] ArteQaBossIds = ["angler", "reaper", "colosso", "rainha", "knight", "wolfE"];
    private static readonly string[] ArteQaPoses = ["front", "back", "attack", "special", "death"];

    /// <summary>Retângulo desenhado da pose e medidas em u (já com a escala dos dados do monstro).</summary>
    private static bool ArteQaPoseMetric(ArtFrames art, string pose, double defScale, out float height, out float width, out float foot)
    {
        height = width = foot = 0f;
        if (!art.Has(pose) || art.Texture(pose, 0) is not { } tex)
        {
            return false;
        }

        var used = ArteQaUsedRect(tex);
        var upp = art.UnitsPerPixel(pose) * (float)defScale;
        height = used.Size.Y * upp;
        width = used.Size.X * upp;
        foot = (used.End.Y - art.Anchor(pose).Y) * upp;
        return true;
    }

    [SmokeScenario("arte (qa): testa chefes e Alfa maiores que o comum, com pé na base e poses do mesmo porte", order: 153)]
    private async Task ArteQaBossSizes()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var db = GameManager.Db;
        var hunter = WorldScale.CharacterHeight;
        var failures = new List<string>();
        // o maior monstro comum (sem chefe, sem elite) pelo atlas, em u
        var commonMax = 0f;
        var commonName = string.Empty;
        foreach (var def in db.Monsters.Values)
        {
            if (def.Boss || def.Elite || def.Sprite is null || !AtlasLibrary.TryGet($"{def.Sprite}_f", out var f))
            {
                continue;
            }

            var used = ArteQaUsedRect(f.Texture);
            var h = used.Size.Y / f.Density * (float)def.Scale * WorldScale.Character;
            if (h > commonMax)
            {
                commonMax = h;
                commonName = def.Id;
            }
        }

        Log.Info($"arte qa: caçador {hunter:0.0} u; maior monstro comum {commonName} {commonMax:0.0} u");
        foreach (var id in ArteQaBossIds)
        {
            var def = db.Monsters[id];
            var path = Enemy.ArtPathFor(def) ?? throw new SmokeFailure($"{id}: sem arte aprovada");
            ArtFrames.TryLoad(path, out var art);
            Require(ArteQaPoseMetric(art, "front", def.Scale, out var fh, out var fw, out var ffoot), $"{id}: sem pose front");
            var line = $"{id}: front {fh:0.0} u ({fh / hunter:0.00}x o caçador) largura {fw:0.0} pé {ffoot:0.0}";
            foreach (var pose in ArteQaPoses.Skip(1))
            {
                if (!ArteQaPoseMetric(art, pose, def.Scale, out var h, out var w, out var foot))
                {
                    continue;
                }

                line += $" | {pose} {h:0.0}x{w:0.0} pé {foot:0.0}";
                if (MathF.Abs(foot) > fh * 0.08f)
                {
                    failures.Add($"{id}/{pose}: o pé fica a {foot:0.0} u do ponto do chão (>{fh * 0.08f:0.0})");
                }

                if (pose != "death" && (h < fh * 0.55f || h > fh * 1.6f))
                {
                    failures.Add($"{id}/{pose}: altura {h:0.0} u contra {fh:0.0} u da pose parada (pula de escala)");
                }
            }

            Log.Info($"arte qa: {line}");
            if (id == "wolfE")
            {
                var wolf = db.Monsters["wolf"];
                AtlasLibrary.TryGet("wolf_f", out var wf);
                var wh = ArteQaUsedRect(wf.Texture).Size.Y / wf.Density * (float)wolf.Scale * WorldScale.Character;
                if (fh < wh * 1.05f)
                {
                    failures.Add($"Alfa {fh:0.0} u não é maior que o lobo comum {wh:0.0} u");
                }
            }
            else if (fh < hunter * 1.8f || fh < commonMax * 1.1f)
            {
                failures.Add($"{id}: {fh:0.0} u não é claramente maior (caçador {hunter:0.0} u, maior comum {commonMax:0.0} u)");
            }

            if (MathF.Abs(ffoot) > fh * 0.08f)
            {
                failures.Add($"{id}/front: o pé fica a {ffoot:0.0} u do ponto do chão");
            }
        }

        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: arte (qa) chefe real de cada Brecha troca de pose pela IA de verdade (5 temas)", order: 155)]
    private async Task ArteQaRealBosses()
    {
        var failures = new List<string>();
        foreach (var (theme, cls) in QaThemes)
        {
            var (world, combat) = await CombatSetup();
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
            var hasDoor = world.Map.Door is not null;
            await WaitUntil(() =>
            {
                KillAllMonsters(combat);
                return hasDoor ? world.DoorOpen : FindBoss(combat) is not null;
            }, 40, $"{theme}: terminar as ondas");
            if (hasDoor)
            {
                FindPlayer()!.Teleport(new Vector2(world.Map.Spawn.X, world.Map.Spawn.Y));
                await NextPhysicsFrame();
                await TravelInto(world, world.Map.Door!.Value, () => world.MapId == World.World.CoreMapId);
                await WaitMapSettled(world);
            }

            await WaitUntil(() => FindBoss(combat) is { } b && b.Body.SpawnT <= 0, 10, $"{theme}: chefe nascer");
            var boss = FindBoss(combat)!;
            var id = boss.Body.Def.Id;
            Require(boss.UsesArt, $"{theme}/{id}: o chefe real não usa a arte aprovada");
            var poses = new Dictionary<string, int>();
            var flicker = 0;
            var minY = 9f;
            var maxY = 0f;
            var t1 = Time.GetTicksMsec();
            var frameStats = new ArteQaFrameStats();
            var wantShot = string.Empty;
            var shotsTaken = new HashSet<string>();
            while (Time.GetTicksMsec() - t1 <= 12000 && !boss.Body.Dead)
            {
                frameStats.Reset();
                await Bot(combat, () =>
                {
                    combat.PlayerCombat.State.GrantInvulnerability(0.2);
                    frameStats.Tick();
                    var pose = boss.ArtPose;
                    poses[pose] = poses.GetValueOrDefault(pose) + 1;
                    minY = MathF.Min(minY, boss.LiveScale.Y);
                    maxY = MathF.Max(maxY, boss.LiveScale.Y);
                    if (pose.Length == 0)
                    {
                        flicker++;
                    }

                    if (pose is "attack" or "special" && shotsTaken.Add(pose))
                    {
                        wantShot = pose;
                    }

                    return wantShot.Length > 0 || Time.GetTicksMsec() - t1 > 12000 || boss.Body.Dead;
                }, 20, $"{theme}: observar o chefe");
                if (wantShot.Length > 0)
                {
                    await Shot($"arteqa-chefe-{id}-{wantShot}");
                    wantShot = string.Empty;
                }
            }

            Log.Info($"arte qa: {theme}/{id}: tempo de quadro na luta (inclui prints): {frameStats.Summary(out _)}");
            Log.Info($"arte qa: {theme}/{id}: poses {string.Join(", ", poses.Select(p => $"{p.Key}={p.Value}"))}; escala viva {minY:0.000}-{maxY:0.000}; quadros sem arte {flicker}");
            await Shot($"arteqa-chefe-{id}");
            if (!poses.ContainsKey("attack") && !poses.ContainsKey("special"))
            {
                failures.Add($"{theme}/{id}: em 12 s de luta o chefe nunca mostrou a pose de ataque/especial ({string.Join(", ", poses.Keys)})");
            }

            if (maxY - minY < 0.015f)
            {
                failures.Add($"{theme}/{id}: não respira/antecipa (escala {minY:0.000}-{maxY:0.000})");
            }

            boss.Body.Hp = Math.Min(boss.Body.Hp, 60);
            await Bot(combat, () => boss.Body.Dead, 60, $"{theme}: derrotar o chefe");
            await WaitSeconds(0.5);
            Log.Info($"arte qa: {theme}/{id}: ao morrer mostra '{boss.ArtPose}'");
            await Shot($"arteqa-chefe-{id}-morte");
            if (id != "angler" && boss.ArtPose != "death")
            {
                failures.Add($"{theme}/{id}: ao morrer mostra '{boss.ArtPose}', esperava 'death'");
            }

            var portal = await WaitForExitPortal(combat);
            await InteractWithNode(world, portal.Interaction);
            await WaitUntil(() => world.MapId == World.World.CityMapId, 6, $"{theme}: voltar");
            await WaitMapSettled(world);
        }

        GameManager.Instance.NewGame("Bot", "sword");
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    // ---------------------------------------------------------------- objetos do mapa REAL

    [SmokeScenario("arte (qa): testa barris, caixotes e baús do mapa real mudando de estado ao jogar", order: 156)]
    private async Task ArteQaRealProps()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        GameManager.Instance.NewGame("Bot", "sword");
        var world = FindWorld();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var d = world.Dressing;
        var failures = new List<string>();
        var kinds = d.Breakables.GroupBy(b => b.Kind).ToDictionary(g => g.Key, g => (g.Count(), g.Count(b => b.HasStateArt)));
        Log.Info($"arte qa: Rua da Forja real: quebráveis {string.Join(", ", kinds.Select(k => $"{k.Key}={k.Value.Item1} (com estados: {k.Value.Item2})"))}; baús {d.Chests.Count} (com arte {d.Chests.Count(c => c.HasStateArt)})");
        foreach (var kind in new[] { "barrel", "crate" })
        {
            var b = d.Breakables.Where(x => x.Kind == kind && !x.IsBroken && world.WalkArea.CanStand(x.GlobalPosition.X, x.GlobalPosition.Y + 16f)).OrderBy(x => x.GlobalPosition.DistanceTo(new Vector2(800, 480))).FirstOrDefault() ?? throw new SmokeFailure($"sem {kind} alcançável pela frente na Rua da Forja");
            Require(b.HasStateArt, $"{kind} real sem a arte de estados");
            player.Teleport(b.GlobalPosition + new Vector2(0, 16f));
            await WaitSeconds(0.5);
            Log.Info($"arte qa: {kind} em {b.GlobalPosition}, jogador em {player.GlobalPosition}, vida {b.Hp}/{b.MaxHp}");
            await Shot($"arteqa-real-{kind}-inteiro");
            var seen = new List<BreakableStage> { b.Stage };
            var hits = 0;
            for (var i = 0; i < 10 && !b.IsBroken; i++)
            {
                var to = b.GlobalPosition - player.GlobalPosition;
                PressDirection(MathF.Abs(to.X) > MathF.Abs(to.Y) ? new Vector2(MathF.Sign(to.X), 0) : new Vector2(0, MathF.Sign(to.Y)));
                await Frames(3); // a direção precisa ser vista por um passo de física inteiro para virar a mira
                PressDirection(Vector2.Zero);
                await NextPhysicsFrame();
                await ArteQaTapAttack();
                hits++;
                await WaitSeconds(0.4);
                Log.Info($"arte qa: {kind} golpe {hits}: vida {b.Hp}/{b.MaxHp}, jogador {player.GlobalPosition}, atacando {player.Animator.IsAttacking}, mira {player.Aim:0.00}, bloqueado {GameManager.Instance.IsGameplayBlocked}");
                if (seen[^1] != b.Stage)
                {
                    seen.Add(b.Stage);
                    await Shot($"arteqa-real-{kind}-{b.Stage}");
                }
            }

            Log.Info($"arte qa: {kind} real: {hits} golpe(s), estágios vistos {string.Join(" > ", seen)}");
            if (!b.IsBroken)
            {
                failures.Add($"{kind} real não quebrou em {hits} golpes reais");
                continue;
            }

            if (!seen.Contains(BreakableStage.Cracked))
            {
                failures.Add($"{kind} real quebrou direto de inteiro para quebrado: o jogador nunca vê o rachado ({hits} golpes)");
            }

            await WaitSeconds(0.8);
            await Shot($"arteqa-real-{kind}-quebrado");
        }

        foreach (var c in d.Chests.ToList())
        {
            if (!c.HasStateArt)
            {
                failures.Add($"baú tier {c.Tier} em {c.Position} sem arte de estados");
                continue;
            }

            await FocusOn(world, c.Interactable);
            var frames = new List<string> { c.ArtFrame };
            await TapInteract();
            var t0 = Time.GetTicksMsec();
            var shot = false;
            while (Time.GetTicksMsec() - t0 < 2200)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (frames[^1] != c.ArtFrame)
                {
                    frames.Add(c.ArtFrame);
                }

                if (!shot && c.ArtFrame == "open")
                {
                    shot = true;
                    await Shot($"arteqa-real-bau-tier{c.Tier}-aberto");
                }
            }

            Log.Info($"arte qa: baú tier {c.Tier}: {string.Join(" > ", frames)}");
            if (!c.IsOpen || c.ArtFrame != "empty" || !frames.Contains("opening_1") || !frames.Contains("open"))
            {
                failures.Add($"baú tier {c.Tier}: quadros {string.Join(" > ", frames)} (esperado closed > opening > open > empty)");
            }

            if (c.Interactable.Enabled)
            {
                failures.Add($"baú tier {c.Tier}: continua interagível depois de vazio");
            }
        }

        // lampiões e flores do mapa real: o que o jogador vê ao usar?
        foreach (var kind in new[] { "lamp", "flower" })
        {
            var spot = d.Decor.FirstOrDefault(s => s.Def.Kind == kind);
            if (spot is null)
            {
                continue;
            }

            await FocusDecor(world, spot);
            await WaitSeconds(0.3);
            await Shot($"arteqa-real-{kind}-antes");
            await TapInteract();
            await WaitSeconds(0.6);
            await Shot($"arteqa-real-{kind}-depois");
            Log.Info($"arte qa: {kind} real em {spot.Def.X},{spot.Def.Y}: objeto com arte de estados = {(spot.Object is not null ? "sim" : "NAO (o desenho do mapa não muda ao usar)")}");
        }

        PressDirection(Vector2.Zero);
        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    // ---------------------------------------------------------------- higiene dos PNG novos e proporções

    private static void ArteQaCollectFrames(string dir, List<string> found)
    {
        using var access = DirAccess.Open(dir);
        if (access is null)
        {
            return;
        }

        foreach (var sub in access.GetDirectories())
        {
            ArteQaCollectFrames($"{dir}/{sub}", found);
        }

        foreach (var file in access.GetFiles())
        {
            if (file == "frames.tres")
            {
                found.Add($"{dir}/{file}");
            }
        }
    }

    [SmokeScenario("arte (qa): testa PNG novos sem corte, sem fundo, sem halo claro e com pé no pivô", order: 157)]
    private async Task ArteQaAssetHygiene()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var files = new List<string>();
        ArteQaCollectFrames("res://assets/sprites/player", files);
        ArteQaCollectFrames("res://assets/sprites/enemies/anim", files);
        ArteQaCollectFrames("res://assets/sprites/props", files);
        Require(files.Count >= 29, $"só {files.Count} SpriteFrames novos encontrados");
        var failures = new List<string>();
        var seen = new HashSet<Texture2D>();
        var checkedFrames = 0;
        foreach (var path in files)
        {
            if (!ArtFrames.TryLoad(path, out var art))
            {
                failures.Add($"{path}: não carrega");
                continue;
            }

            foreach (var anim in art.Frames.GetAnimationNames())
            {
                for (var i = 0; i < art.Count(anim); i++)
                {
                    var tex = art.Texture(anim, i);
                    if (tex is null || !seen.Add(tex))
                    {
                        continue;
                    }

                    checkedFrames++;
                    var image = tex.GetImage();
                    if (image.IsCompressed())
                    {
                        image.Decompress();
                    }

                    image.Convert(Image.Format.Rgba8);
                    var data = image.GetData();
                    int w = image.GetWidth(), h = image.GetHeight();
                    var edge = 0;
                    for (var x = 0; x < w; x++)
                    {
                        edge += (data[(x * 4) + 3] > 8 ? 1 : 0) + (data[((((h - 1) * w) + x) * 4) + 3] > 8 ? 1 : 0);
                    }

                    for (var y = 0; y < h; y++)
                    {
                        edge += (data[(y * w * 4) + 3] > 8 ? 1 : 0) + (data[((((y * w) + w) - 1) * 4) + 3] > 8 ? 1 : 0);
                    }

                    long visible = 0, haloBright = 0;
                    for (var p = 0; p < w * h; p++)
                    {
                        var a = data[(p * 4) + 3];
                        if (a <= 8)
                        {
                            continue;
                        }

                        visible++;
                        if (a < 200 && (data[p * 4] + data[(p * 4) + 1] + data[(p * 4) + 2]) / 3 > 215)
                        {
                            haloBright++;
                        }
                    }

                    var used = image.GetUsedRect();
                    var name = $"{path.Replace("res://assets/sprites/", string.Empty).Replace("/frames.tres", string.Empty)}/{anim}[{i}]";
                    if (visible == 0)
                    {
                        failures.Add($"{name}: quadro vazio");
                        continue;
                    }

                    var prop = path.Contains("/props/", StringComparison.Ordinal);
                    if (edge > (prop ? 64 : 6)) // objetos são recortes justos (a tábua de avisos encosta nas bordas); gente e chefes não
                    {
                        failures.Add($"{name}: {edge} px opacos encostam na borda do PNG (recorte cortado)");
                    }

                    // flor colhida: as pétalas desbotadas translúcidas são o desenho do designer (nas três cores), não halo
                    var fadedFlower = name.StartsWith("props/flowers/", StringComparison.Ordinal) && name.Contains("/harvested", StringComparison.Ordinal);
                    if (haloBright > 40 && haloBright > visible / 60 && !fadedFlower)
                    {
                        failures.Add($"{name}: {haloBright} px claros semitransparentes (halo) em {visible} visíveis");
                    }

                    // pé no pivô (só quem tem corpo no chão: personagens e chefes)
                    if (path.Contains("/player/", StringComparison.Ordinal) || path.Contains("/enemies/", StringComparison.Ordinal))
                    {
                        var ay = art.Anchor(anim).Y;
                        var ax = art.Anchor(anim).X;
                        if (ay < used.Position.Y || MathF.Abs(used.End.Y - ay) > used.Size.Y * 0.1f || ax < used.Position.X || ax > used.End.X)
                        {
                            failures.Add($"{name}: o pivô ({ax:0},{ay:0}) não fica na base do desenho (caixa {used.Position.X},{used.Position.Y} {used.Size.X}x{used.Size.Y})");
                        }
                    }
                }
            }
        }

        Log.Info($"arte qa: {checkedFrames} quadros em {files.Count} SpriteFrames verificados, {failures.Count} problema(s)");
        foreach (var f in failures)
        {
            Log.Info($"arte qa: higiene: {f}");
        }

        Require(failures.Count == 0, string.Join("\n  ", failures.Take(25)));
    }

    [SmokeScenario("arte (qa): testa proporções dos objetos com arte nova contra o caçador", order: 158)]
    private async Task ArteQaProportions()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var hunter = WorldScale.CharacterHeight;
        // (pasta, animação, mín×, máx×) em múltiplos da altura do caçador
        (string Dir, string Anim, float Min, float Max)[] limits =
        [
            ("props/breakable/barrel", "whole", 0.3f, 1.0f), ("props/breakable/crate", "whole", 0.3f, 1.0f),
            ("props/chests/tier_0", "closed", 0.25f, 0.9f), ("props/chests/tier_2", "closed", 0.25f, 0.9f), ("props/chests/tier_4", "closed", 0.25f, 0.9f),
            ("props/lights/lamp_post", "on", 1.5f, 4.0f), ("props/lights/brazier", "on", 0.6f, 2.2f),
            ("props/flowers/blue", "flowering", 0.2f, 1.0f), ("props/flowers/yellow", "flowering", 0.2f, 1.0f), ("props/flowers/violet", "flowering", 0.2f, 1.0f),
            ("props/mechanisms/door", "closed", 1.0f, 3.2f), ("props/mechanisms/lever", "off", 0.4f, 1.6f), ("props/mechanisms/board", "board", 0.8f, 2.6f),
        ];
        var failures = new List<string>();
        foreach (var (dir, anim, min, max) in limits)
        {
            var path = $"res://assets/sprites/{dir}/frames.tres";
            if (!ArtFrames.TryLoad(path, out var art) || !art.Has(anim))
            {
                var names = art is null ? "?" : string.Join(",", art.Frames.GetAnimationNames());
                failures.Add($"{dir}: sem animação '{anim}' (tem: {names})");
                continue;
            }

            var used = ArteQaUsedRect(art.Texture(anim, 0)!);
            var upp = art.UnitsPerPixel(anim);
            var h = used.Size.Y * upp;
            var w = used.Size.X * upp;
            var k = h / hunter;
            Log.Info($"arte qa: {dir}/{anim}: {h:0.0} x {w:0.0} u = {k:0.00}x a altura do caçador ({hunter:0.0} u)");
            if (k < min || k > max)
            {
                failures.Add($"{dir}/{anim}: {h:0.0} u = {k:0.00}x o caçador, fora de {min:0.0}-{max:0.0}x");
            }
        }

        foreach (var boat in new[] { "raft", "rowboat" })
        {
            var path = $"res://assets/sprites/props/boats/{boat}/frames.tres";
            if (!ArtFrames.TryLoad(path, out var art))
            {
                failures.Add($"{boat}: sem arte");
                continue;
            }

            var anim = (StringName)"rest";
            var used = ArteQaUsedRect(art.Texture(anim, 0)!);
            var upp = art.UnitsPerPixel(anim);
            Log.Info($"arte qa: {boat}/{anim}: {used.Size.X * upp:0.0} x {used.Size.Y * upp:0.0} u (caçador {hunter:0.0} u)");
            if (used.Size.X * upp > hunter * 5f || used.Size.X * upp < hunter * 0.8f || used.Size.Y * upp < hunter * 0.4f)
            {
                failures.Add($"{boat}: {used.Size.X * upp:0.0} x {used.Size.Y * upp:0.0} u desproporcional ao caçador");
            }
        }

        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    // ---------------------------------------------------------------- resoluções e prints

    [SmokeScenario("arte (qa): testa prints e tempo de quadro em 1280x720, 1920x1080, 2560x1440 e 3840x2160", order: 159)]
    private async Task ArteQaResolutionShots()
    {
        await CombatSetup();
        var failures = new List<string>();
        if (DisplayServer.GetName() == "headless")
        {
            await ArteQaShotsAt("headless", failures);
            Require(failures.Count == 0, string.Join("\n  ", failures));
            return;
        }

        var original = DisplayServer.WindowGetSize();
        var vsync = DisplayServer.WindowGetVsyncMode();
        var maxFps = Engine.MaxFps;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        try
        {
            foreach (var want in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(2560, 1440), new Vector2I(3840, 2160) })
            {
                DisplayServer.WindowSetSize(want);
                await WaitSeconds(0.8);
                var got = GetViewport().GetVisibleRect().Size;
                var tag = $"{want.X}x{want.Y}";
                Log.Info($"arte qa: janela pedida {want}, janela real {DisplayServer.WindowGetSize()}, viewport {got}, tela {DisplayServer.ScreenGetSize()}");
                await ArteQaShotsAt(tag, failures);
                // tempo de quadro andando com o caçador nesta resolução
                var world = FindWorld();
                world.GoMap("forge", 800, 520, instant: true);
                await WaitMapSettled(world);
                FindPlayer()!.Teleport(world.CorrectToWalkable(new Vector2(820, 540)));
                await WaitSeconds(0.4);
                var frames = new List<double>(512);
                var last = Time.GetTicksUsec();
                var t0 = last;
                var leg = 0;
                Vector2[] dirs = [Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up];
                while (Time.GetTicksUsec() - t0 < 3_000_000)
                {
                    PressDirection(dirs[(leg++ / 80) % 4]);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var now = Time.GetTicksUsec();
                    frames.Add((now - last) / 1000.0);
                    last = now;
                }

                PressDirection(Vector2.Zero);
                var sorted = frames.Skip(5).OrderBy(v => v).ToList();
                var p95 = sorted[(int)(sorted.Count * 0.95)];
                Log.Info($"arte qa: {tag} (viewport {got}): média {sorted.Average():0.00} ms ({1000 / sorted.Average():0} fps), p95 {p95:0.00} ms, máx {sorted[^1]:0.0} ms");
                if (want.X <= 1920 && p95 > 20.0)
                {
                    failures.Add($"{tag}: p95 do quadro {p95:0.00} ms > 20 ms");
                }
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(original);
            DisplayServer.WindowSetVsyncMode(vsync);
            Engine.MaxFps = maxFps;
        }

        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    private async Task ArteQaShotsAt(string tag, List<string> failures)
    {
        var (world, _) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2(900, 470)));
        await WaitSeconds(0.6);
        PressDirection(Vector2.Right);
        await WaitSeconds(0.45);
        await Shot($"arteqa-res-{tag}-andando");
        PressDirection(Vector2.Zero);

        // HUD inteiro dentro da tela, em qualquer proporção
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        var screen = GetViewport().GetVisibleRect();
        foreach (var (name, control) in new (string, Control)[] { ("esquiva", hud.DodgeSlot), ("poção", hud.PotionSlot), ("supremo", hud.Ultimate) })
        {
            var r = control.GetGlobalRect();
            if (!screen.Encloses(r.Grow(-1f)))
            {
                failures.Add($"{tag}: {name} do HUD fora da tela ({r} em {screen})");
            }
        }

        // chefes ao lado do caçador
        var db = GameManager.Db;
        var rng = new SeededRandom(11);
        var placed = new List<Enemy>();
        var origin = player.GlobalPosition;
        var slot = 0;
        foreach (var id in new[] { "colosso", "reaper", "angler" })
        {
            var def = db.Monsters[id];
            Enemy.Preload(def);
            var body = new MonsterBody(new ScaledMonster(def, def.Hp, def.Damage, []), origin.X + ((slot - 1) * 150f), origin.Y + 70f, Enemy.HitHeightFor(def), rng) { SpawnT = 0 };
            slot++;
            var enemy = Enemy.Create(body);
            world.Entities.AddChild(enemy);
            placed.Add(enemy);
        }

        try
        {
            for (var i = 0; i < 30; i++)
            {
                foreach (var e in placed)
                {
                    e.Body.ForceState(MonsterState.Chase);
                    e.Sync(i / 30f, origin + new Vector2(0, 300));
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await Shot($"arteqa-res-{tag}-chefes");
        }
        finally
        {
            foreach (var e in placed)
            {
                e.QueueFree();
            }
        }

        // diálogo com busto
        var ui = UiOrFail();
        EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueRequested, "Odete", new[] { "Bem-vindo à Guilda. A Brecha desta véspera abre no cais; leve a lâmina e volte inteiro." });
        await WaitUntil(() => ui.Dialog.IsOpen, 2, "fala de Odete");
        await SkipTyping(ui);
        await Shot($"arteqa-res-{tag}-dialogo");
        var box = ui.Dialog.GetGlobalRect();
        if (!screen.Encloses(box))
        {
            failures.Add($"{tag}: caixa de diálogo fora da tela ({box} em {screen})");
        }

        await AdvanceDialog();
        await WaitUntil(() => !ui.Dialog.IsOpen, 2, "fechar a fala");
    }

    // ---------------------------------------------------------------- estresse

    private sealed class ArteQaFrameStats
    {
        private readonly List<double> _frames = new(4096);
        private ulong _last = Time.GetTicksUsec();

        public void Tick()
        {
            var now = Time.GetTicksUsec();
            _frames.Add((now - _last) / 1000.0);
            _last = now;
        }

        public void Reset() => _last = Time.GetTicksUsec();

        public string Summary(out double p95)
        {
            var sorted = _frames.Skip(10).OrderBy(v => v).ToList();
            if (sorted.Count == 0)
            {
                p95 = 0;
                return "sem quadros";
            }

            p95 = sorted[(int)(sorted.Count * 0.95)];
            return $"{sorted.Count} quadros, média {sorted.Average():0.00} ms, p95 {p95:0.00} ms, p99 {sorted[(int)(sorted.Count * 0.99)]:0.00} ms, máx {sorted[^1]:0.0} ms";
        }
    }

    [SmokeScenario("estresse: arte (qa) 5 classes andando, esquivando e atacando 20 s cada sem vazar nós nem piscar", order: 160)]
    private async Task ArteQaStressClasses()
    {
        var (world, _) = await CombatSetup();
        var failures = new List<string>();
        var rng = new Random(2026);
        var nodes0 = 0.0;
        var mem0 = 0.0;
        var stats = new ArteQaFrameStats();
        var invisible = 0;
        var multi = 0;
        var samples = 0;
        var first = true;
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            await WaitSeconds(0.5);
            if (first)
            {
                first = false;
                nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
            }

            stats.Reset();
            var start = Time.GetTicksMsec();
            var nextAction = 0UL;
            while (Time.GetTicksMsec() - start < 20_000)
            {
                var now = Time.GetTicksMsec();
                if (now >= nextAction)
                {
                    nextAction = now + 120 + (ulong)rng.Next(500);
                    var a = rng.Next(9);
                    PressDirection(a == 8 ? Vector2.Zero : new Vector2(MathF.Cos(a * MathF.PI / 4f), MathF.Sin(a * MathF.PI / 4f)));
                    switch (rng.Next(5))
                    {
                        case 0:
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Dodge, Pressed = true });
                            break;
                        case 1:
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = true });
                            break;
                        case 2:
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.AttackHeavy, Pressed = true });
                            break;
                        default:
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Dodge, Pressed = false });
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = false });
                            Input.ParseInputEvent(new InputEventAction { Action = InputActions.AttackHeavy, Pressed = false });
                            break;
                    }
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                stats.Tick();
                var m = ArteQaMeasure(anim);
                samples++;
                invisible += m.Valid ? 0 : 1;
                multi += m.Layers != 1 ? 1 : 0;
                if (world.MapId != "guild" || player.GlobalPosition.Y < 0 || player.GlobalPosition.Y > 2000)
                {
                    world.GoMap("guild", 250, 600, instant: true);
                    await WaitMapSettled(world);
                }
            }

            PressDirection(Vector2.Zero);
            foreach (var act in new[] { InputActions.Dodge, InputActions.Attack, InputActions.AttackHeavy })
            {
                Input.ParseInputEvent(new InputEventAction { Action = act, Pressed = false });
            }

            await WaitSeconds(0.8);
            Log.Info($"arte qa: {cls}: {stats.Summary(out var p95)}");
            if (DisplayServer.GetName() != "headless" && p95 > 20.0)
            {
                failures.Add($"{cls}: p95 do quadro {p95:0.00} ms > 20 ms");
            }
        }

        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        Log.Info($"arte qa: 100 s de esquiva/golpe/andar — {samples} amostras, sem sprite {invisible}, camadas empilhadas {multi}, nós {nodes0} > {nodes1}, memória {mem0:0.0} > {mem1:0.0} MB, órfãos {orphans}");
        GameManager.Instance.NewGame("Bot", "sword");
        if (invisible > 0)
        {
            failures.Add($"{invisible} quadro(s) sem nenhum sprite do caçador visível");
        }

        if (multi > 0)
        {
            failures.Add($"{multi} quadro(s) com mais de uma camada do caçador");
        }

        if (nodes1 - nodes0 > 40)
        {
            failures.Add($"nós cresceram {nodes1 - nodes0} (vazamento: rastros/poeira/textos?)");
        }

        if (mem1 - mem0 > 12)
        {
            failures.Add($"memória cresceu {mem1 - mem0:0.0} MB");
        }

        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    [SmokeScenario("estresse: arte (qa) 60 rodadas de chefes criados e liberados e 40 trocas de classe sem vazar", order: 161)]
    private async Task ArteQaStressBossesAndClasses()
    {
        var (world, _) = await CombatSetup();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var db = GameManager.Db;
        var rng = new SeededRandom(5);
        foreach (var id in ArteQaBossIds)
        {
            Enemy.Preload(db.Monsters[id]);
        }

        // aquece o cache da arte das 5 classes (carregadas uma vez e guardadas: não é vazamento)
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await Frames(10);
        }

        player = FindPlayer()!;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await WaitSeconds(0.5);
        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var obj0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        var stats = new ArteQaFrameStats();
        for (var round = 0; round < 60; round++)
        {
            var placed = new List<Enemy>();
            var i = 0;
            foreach (var id in ArteQaBossIds)
            {
                var def = db.Monsters[id];
                var body = new MonsterBody(new ScaledMonster(def, def.Hp, def.Damage, []), player.GlobalPosition.X + ((i % 3) - 1) * 160f, player.GlobalPosition.Y + 60f + (i / 3) * 110f, Enemy.HitHeightFor(def), rng) { SpawnT = 0 };
                i++;
                var e = Enemy.Create(body);
                world.Entities.AddChild(e);
                placed.Add(e);
            }

            var states = new[] { MonsterState.Chase, MonsterState.Wind, MonsterState.Slam, MonsterState.Summon, MonsterState.Idle };
            for (var f = 0; f < 6; f++)
            {
                foreach (var e in placed)
                {
                    e.Body.ForceState(states[(round + f) % states.Length]);
                    e.Body.T = 0.3;
                    e.Sync((round * 6 + f) / 30f, player.GlobalPosition + new Vector2(0, 300));
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                stats.Tick();
            }

            foreach (var e in placed)
            {
                e.Body.Kill();
                e.Body.DeadT = 0.5;
                e.Sync(99f, player.GlobalPosition);
                e.QueueFree();
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            stats.Tick();
        }

        var objAfterBosses = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        for (var n = 0; n < 40; n++)
        {
            GameManager.Instance.NewGame("Bot", ArteQaClasses[n % ArteQaClasses.Length]);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            var anim = FindPlayer()!.Animator;
            PressDirection(Vector2.Right);
            await Frames(8);
            PressDirection(Vector2.Zero);
            Require(anim.HasWalkArt, $"troca {n}: classe {ArteQaClasses[n % ArteQaClasses.Length]} sem arte de andar");
        }

        await WaitSeconds(1.0);
        var objBeforeGc = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        GC.Collect(); // recursos (materiais, texturas) só saem quando o coletor do .NET solta os invólucros: vazamento real sobrevive a isto
        GC.WaitForPendingFinalizers();
        await Frames(10);
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var obj1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        var orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        Log.Info($"arte qa: 60 rodadas x 6 chefes e 40 trocas de classe: {stats.Summary(out var p95)}; nós {nodes0} > {nodes1}, objetos {obj0} > {objAfterBosses} (depois dos chefes) > {objBeforeGc} (depois das classes) > {obj1} (depois do GC), memória {mem0:0.0} > {mem1:0.0} MB, órfãos {orphans}");
        GameManager.Instance.NewGame("Bot", "sword");
        Require(nodes1 - nodes0 <= 12, $"nós cresceram {nodes1 - nodes0} depois de criar/liberar chefes e trocar de classe");
        Require(obj1 - obj0 <= 400, $"objetos cresceram {obj1 - obj0}");
        Require(mem1 - mem0 <= 12, $"memória cresceu {mem1 - mem0:0.0} MB");
        Require(orphans == 0, $"{orphans} nós órfãos");
    }

    [SmokeScenario("estresse: arte (qa) bots de caminhada das 5 classes na cidade sem travar nem sumir", order: 162)]
    private async Task ArteQaStressWalkBots()
    {
        var (world, _) = await CombatSetup();
        var failures = new List<string>();
        var rng = new Random(909);
        var gameSeconds = 0.0;
        using (new SpeedUp(QaSpeedUp))
        {
            foreach (var cls in ArteQaClasses)
            {
                GameManager.Instance.NewGame("Bot", cls);
                foreach (var id in new[] { "forge", "fish2", "mine" })
                {
                    var map = World.World.Catalog[id];
                    world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
                    await WaitMapSettled(world);
                    var player = FindPlayer()!;
                    var anim = player.Animator;
                    var window = new Queue<Vector2>();
                    var dir = Vector2.Zero;
                    var nextTurn = 0;
                    var invisible = 0;
                    var multi = 0;
                    var stuckEvents = 0;
                    var ticks = 0;
                    while (ticks < 8 * 60)
                    {
                        if (world.MapId != id || world.IsTransitioning)
                        {
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
                        var m = ArteQaMeasure(anim);
                        invisible += m.Valid ? 0 : 1;
                        multi += m.Layers != 1 ? 1 : 0;
                        var p = player.GlobalPosition;
                        window.Enqueue(p);
                        if (window.Count > 60)
                        {
                            window.Dequeue();
                            if (window.Peek().DistanceTo(p) < 1f)
                            {
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
                                    stuckEvents++;
                                    failures.Add($"{cls} em {id}: TRAVADO em ({p.X:0},{p.Y:0}), nenhuma das 8 direções anda 1 u");
                                    player.Teleport(world.CorrectToWalkable(new Vector2(map.Spawn.X, map.Spawn.Y)));
                                }

                                window.Clear();
                                nextTurn = ticks;
                            }
                        }
                    }

                    PressDirection(Vector2.Zero);
                    gameSeconds += ticks / 60.0;
                    if (invisible > 0 || multi > 0)
                    {
                        failures.Add($"{cls} em {id}: {invisible} quadro(s) sem sprite e {multi} com camadas empilhadas em {ticks} ticks");
                    }

                    Log.Info($"arte qa: bot {cls} em {id}: {ticks / 60.0:0} s de jogo, travamentos {stuckEvents}, sem sprite {invisible}, empilhados {multi}");
                }
            }
        }

        GameManager.Instance.NewGame("Bot", "sword");
        Log.Info($"arte qa: bots das 5 classes: {gameSeconds:0} s de jogo");
        Require(failures.Count == 0, string.Join("\n  ", failures.Take(30)));
    }

    [SmokeScenario("estresse: arte (qa) 50 trocas de mapa trocando de classe sem crescer nós nem memória", order: 163)]
    private async Task ArteQaStressMapSwapsWithClasses()
    {
        var (world, _) = await CombatSetup();
        var ids = new[] { "forge", "guild", "inn", "shop_arm", "fish2", "forge", "mine", "cath", "forge", "crypt2" };
        var nodes = new List<double>();
        var mem = new List<double>();
        var stats = new ArteQaFrameStats();
        for (var i = 0; i < 50; i++)
        {
            GameManager.Instance.NewGame("Bot", ArteQaClasses[i % ArteQaClasses.Length]);
            var id = ids[i % ids.Length];
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: i % 2 == 0);
            var t = Time.GetTicksUsec();
            stats.Reset();
            while (world.IsTransitioning || world.FadeAlpha > 0f)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                stats.Tick();
                Require(Time.GetTicksUsec() - t < 5_000_000, $"troca {i} para {id} não terminou");
            }

            await Frames(2);
            var anim = FindPlayer()!.Animator;
            Require(anim.HasWalkArt, $"troca {i}: sem arte de andar de {ArteQaClasses[i % ArteQaClasses.Length]}");
            Require(ArteQaMeasure(anim) is { Valid: true, Layers: 1 }, $"troca {i}: personagem sem sprite visível depois de entrar em {id}");
            nodes.Add(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
            mem.Add(Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024));
        }

        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var growthNodes = nodes[49] - nodes[19];
        var growthMem = mem[49] - mem[19];
        Log.Info($"arte qa: 50 trocas com classes — nós (20ª > 50ª) {nodes[19]} > {nodes[49]}, memória {mem[19]:0.0} > {mem[49]:0.0} MB; {stats.Summary(out _)}");
        Require(growthNodes <= 6, $"nós crescem com as trocas: +{growthNodes}");
        Require(growthMem <= 10, $"memória cresce com as trocas: +{growthMem:0.0} MB");
        Require(Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount) == 0, "nós órfãos depois das trocas");
    }

    // ---------------------------------------------------------------- tiras de quadros (olhar o andar e a esquiva em jogo)

    private Image? ArteQaCropAroundPlayer(Player.Player player, Vector2I size, Vector2I feet)
    {
        var viewport = GetViewport();
        var image = viewport.GetTexture().GetImage();
        var k = image.GetWidth() / viewport.GetVisibleRect().Size.X;
        var at = viewport.GetCanvasTransform() * player.GlobalPosition * k;
        var rect = new Rect2I((int)at.X - feet.X, (int)at.Y - feet.Y, size.X, size.Y);
        rect = rect.Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        if (rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            return null;
        }

        var crop = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
        crop.BlitRect(image, rect, new Vector2I(rect.Position.X - ((int)at.X - feet.X), rect.Position.Y - ((int)at.Y - feet.Y)));
        return crop;
    }

    [SmokeScenario("arte (qa): testa tira de quadros do andar e da esquiva em jogo nas 5 classes (prints)", order: 164)]
    private async Task ArteQaContactSheets()
    {
        var (world, combat) = await CombatSetup();
        if (DisplayServer.GetName() == "headless" || _shotsDir is null)
        {
            return;
        }

        var cell = new Vector2I(200, 200);
        var feet = new Vector2I(100, 150);
        const int columns = 12;
        foreach (var cls in ArteQaClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var rows = new List<List<Image>>();
            foreach (var (name, dir, dodge) in new[]
                     {
                         ("andar direita", Vector2.Right, false), ("andar esquerda", Vector2.Left, false), ("andar baixo", Vector2.Down, false),
                         ("andar cima", Vector2.Up, false), ("esquiva direita", Vector2.Right, true), ("esquiva baixo", Vector2.Down, true),
                         ("esquiva cima", Vector2.Up, true),
                     })
            {
                player.Teleport(world.CorrectToWalkable(new Vector2(250, 560)));
                await WaitSeconds(dodge ? 1.0 : 0.4);
                var row = new List<Image>();
                PressDirection(dir);
                if (dodge)
                {
                    Input.ActionPress(InputActions.Dodge);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Input.ActionRelease(InputActions.Dodge);
                }
                else
                {
                    await WaitSeconds(0.25);
                }

                var next = Time.GetTicksMsec();
                var guard = Time.GetTicksMsec();
                while (row.Count < columns && Time.GetTicksMsec() - guard < 4000)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    if (Time.GetTicksMsec() < next)
                    {
                        continue;
                    }

                    next = Time.GetTicksMsec() + (dodge ? 14UL : 45UL);
                    if (ArteQaCropAroundPlayer(player, cell, feet) is { } crop)
                    {
                        row.Add(crop);
                    }
                }

                PressDirection(Vector2.Zero);
                rows.Add(row);
                Log.Info($"arte qa: tira {cls} {name}: {row.Count} quadros");
            }

            var sheet = Image.CreateEmpty(cell.X * columns, cell.Y * rows.Count, false, Image.Format.Rgba8);
            sheet.Fill(new Color(0.05f, 0.05f, 0.08f));
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Count; c++)
                {
                    sheet.BlitRect(rows[r][c], new Rect2I(Vector2I.Zero, cell), new Vector2I(c * cell.X, r * cell.Y));
                }
            }

            DirAccess.MakeDirRecursiveAbsolute(_shotsDir);
            var path = System.IO.Path.Combine(_shotsDir, $"arteqa-tira-{cls}.png");
            sheet.SavePng(path);
            Log.Info($"SHOT  {path}");
            await WaitSeconds(0.5);
        }

        GameManager.Instance.NewGame("Bot", "sword");
    }

    [SmokeScenario("arte (qa): testa retratos de todos os NPCs que falam (busto do designer ou o que aparece no lugar)", order: 165)]
    private async Task ArteQaAllPortraits()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var ui = UiOrFail();
        var names = new List<string> { "Odete", "Brás", "Lúcia", "Gaspar", "Vesna", "Ivo", "Abel", "Iara", "Sargento Lauro", "Marta", "Faísca" };
        var plain = new List<string>();
        foreach (var npc in names)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueRequested, npc, new[] { "Boa noite, caçador. A véspera está chegando." });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, $"fala de {npc}");
            await SkipTyping(ui);
            await Shot($"arteqa-retrato-{npc.Replace(' ', '_')}");
            if (!ui.Dialog.PortraitIsBust)
            {
                plain.Add(npc);
            }

            await AdvanceDialog();
            await WaitUntil(() => !ui.Dialog.IsOpen, 2, $"fechar a fala de {npc}");
        }

        Log.Info($"arte qa: falas sem o busto do designer (diálogo só de texto, sem retrato): {(plain.Count == 0 ? "nenhuma" : string.Join(", ", plain))}");
        // Gaspar, Abel e Iara não têm retrato nos dados (o designer não entregou os bustos): conhecido, só registrado.
        var missing = plain.Where(n => n is not ("Gaspar" or "Abel" or "Iara")).ToList();
        Require(missing.Count == 0, $"{missing.Count} personagem(ns) com busto entregue falam sem ele: {string.Join(", ", missing)}");
    }

    [SmokeScenario("arte (qa): testa que cada família de objeto com estados aparece em algum mapa real do jogo", order: 166)]
    private async Task ArteQaRealMapsHaveStatefulObjects()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var perRole = new Dictionary<PropArtRole, List<string>>();
        foreach (var role in new[] { PropArtRole.Lamp, PropArtRole.Brazier, PropArtRole.Flower, PropArtRole.Lever, PropArtRole.Door, PropArtRole.Board })
        {
            perRole[role] = [];
        }

        var maps = new[] { "forge", "guild", "inn", "shop_arm", "shop_mag", "shop_alq", "shop_rel", "fish2", "crypt2", "mine", "garden", "cath", "core" };
        foreach (var id in maps)
        {
            var map = World.World.Catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y, instant: true);
            await WaitMapSettled(world);
            foreach (var o in world.Dressing.Objects)
            {
                if (perRole.TryGetValue(o.ArtRole, out var list))
                {
                    list.Add(id);
                }
            }
        }

        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        foreach (var (role, list) in perRole)
        {
            Log.Info($"arte qa: objetos com arte de estados {role} nos mapas reais: {(list.Count == 0 ? "NENHUM" : string.Join(", ", list.Distinct()))}");
        }

        var missing = perRole.Where(p => p.Value.Count == 0).Select(p => p.Key.ToString()).ToList();
        Require(missing.Count == 0, $"famílias aprovadas do designer que nenhum mapa real usa (só existem no fixture do smoke): {string.Join(", ", missing)}");
    }

    [SmokeScenario("arte (qa): testa Alfa mantém a arte com cristas parado e perseguindo e o tamanho perto do lobo do atlas", order: 167)]
    private async Task ArteQaAlphaKeepsArtInEveryState()
    {
        var (world, _) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2(900, 470)));
        var def = GameManager.Db.Monsters["wolfE"];
        Enemy.Preload(def);
        var rng = new SeededRandom(3);
        var origin = player.GlobalPosition;
        var body = new MonsterBody(new ScaledMonster(def, def.Hp, def.Damage, []), origin.X + 60f, origin.Y + 40f, Enemy.HitHeightFor(def), rng) { SpawnT = 0 };
        var alpha = Enemy.Create(body);
        world.Entities.AddChild(alpha);
        var failures = new List<string>();
        try
        {
            ArtFrames.TryLoad(Enemy.ArtPathFor(def)!, out var art);
            ArteQaPoseMetric(art, "front", def.Scale, out var artHeight, out _, out _);
            var shown = new List<string>();
            foreach (var state in new[] { MonsterState.Idle, MonsterState.Chase, MonsterState.Wind, MonsterState.Lunge, MonsterState.Rec })
            {
                body.ForceState(state);
                body.T = 0.2;
                for (var i = 0; i < 4; i++)
                {
                    alpha.Sync(1f + i * 0.05f, origin + new Vector2(0, 300));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                shown.Add($"{state}={(alpha.UsesArt ? alpha.ArtPose : "atlas")}");
                if (state is MonsterState.Chase or MonsterState.Lunge)
                {
                    await Shot($"arteqa-alfa-{state}");
                }

                // Wind/Lunge/morte caem no atlas do lobo: o designer não entregou golpe nem morte do Alfa (conhecido, só registrado)
                if (!alpha.UsesArt && state is MonsterState.Idle or MonsterState.Chase or MonsterState.Rec)
                {
                    failures.Add($"Alfa em {state}: a arte com cristas some e o lobo comum do atlas aparece no lugar");
                }
            }

            body.ForceState(MonsterState.Lunge);
            body.Kill();
            body.DeadT = 0.4;
            alpha.Sync(2f, origin);
            shown.Add($"morto={(alpha.UsesArt ? alpha.ArtPose : "atlas")}");
            var sizes = new List<string>();
            foreach (var key in new[] { "wolf_f", "wolf_a", "wolf_d" })
            {
                if (AtlasLibrary.TryGet(key, out var atlasFrame))
                {
                    var used = ArteQaUsedRect(atlasFrame.Texture);
                    var h = used.Size.Y / atlasFrame.Density * (float)def.Scale * WorldScale.For(key, SpriteScaleClass.Auto);
                    var w = used.Size.X / atlasFrame.Density * (float)def.Scale * WorldScale.For(key, SpriteScaleClass.Auto);
                    sizes.Add($"{key} {h:0.0}x{w:0.0} u");
                    if (key == "wolf_f" && MathF.Abs(h - artHeight) > artHeight * 0.25f)
                    {
                        failures.Add($"o lobo do atlas parado ({h:0.0} u) e a arte do Alfa ({artHeight:0.0} u) diferem de tamanho");
                    }
                }
            }

            Log.Info($"arte qa: Alfa ({artHeight:0.0} u de altura na arte): {string.Join(", ", shown)}; atlas do lobo (Alfa ×{def.Scale}): {string.Join(", ", sizes)}");
        }
        finally
        {
            alpha.QueueFree();
        }

        Require(failures.Count == 0, string.Join("\n  ", failures));
    }

    private static Label? ArteQaFindLabel(Node node, string contains)
    {
        if (node is Label label && label.Text.Contains(contains, StringComparison.Ordinal))
        {
            return label;
        }

        foreach (var child in node.GetChildren())
        {
            if (ArteQaFindLabel(child, contains) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    [SmokeScenario("arte (qa): testa que os ícones de estado não empurram o resto do HUD ao ligar e desligar", order: 168)]
    private async Task ArteQaStatusRowDoesNotShiftHud()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        GameManager.Instance.NewGame("Bot", "sword");
        await Frames(5);
        var hint = ArteQaFindLabel(hud, "Inventário") ?? throw new SmokeFailure("sem a dica de teclas (Inventário) no HUD");
        var quest = ArteQaFindLabel(hud, "Primeiros passos") ?? ArteQaFindLabel(hud, "PRIMEIROS PASSOS");
        var hintY = hint.GlobalPosition.Y;
        var questY = quest?.GlobalPosition.Y ?? 0f;
        // o i-frame de 0,75 s que vem depois de levar um golpe (GrantInvulnerability) acende o escudo de verdade
        var combat = CombatDirector.Instance ?? throw new SmokeFailure("sem CombatDirector");
        combat.PlayerCombat.State.GrantInvulnerability(0.75);
        await Frames(4);
        Require(hud.IsStatusShown("shield"), "os i-frames depois de um golpe deveriam acender o ícone de escudo");
        await Shot("arteqa-hud-com-estado");
        var shiftHint = hint.GlobalPosition.Y - hintY;
        var shiftQuest = quest is null ? 0f : quest.GlobalPosition.Y - questY;
        await WaitSeconds(1.0);
        Log.Info($"arte qa: ao aparecer o ícone de escudo, a dica de teclas desce {shiftHint:0.0} px e a missão {shiftQuest:0.0} px");
        Require(MathF.Abs(shiftHint) <= 1f && MathF.Abs(shiftQuest) <= 1f, $"o ícone de estado empurra o HUD: dica {shiftHint:0.0} px, missão {shiftQuest:0.0} px (treme a cada golpe sofrido: o escudo acende nos 0,75 s de i-frames)");
    }
}
