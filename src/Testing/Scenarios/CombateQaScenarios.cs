using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.Enemies;
using Vespera.Items;
using Vespera.World.Dungeon;

namespace Vespera.Testing;

/// <summary>
/// QA de experiência da frente B1 (tester): joga cada arma com o input real e mede ritmo, alcance, combo, carga, windup,
/// costas, knockback, i-frames, esquiva, poção e ímã; vê o verbo e o telegraph de cada IA; joga a Brecha de cada tema até
/// o chefe; e estressa (muitos monstros/projéteis, spam de botões, 50 trocas de mapa, bots de exploração nas salas).
/// Cenários "estresse: …" só rodam com <c>--smoke-stress</c>.
/// </summary>
public partial class SmokeTest
{
    private static readonly (string Theme, string Class)[] QaThemes =
    [
        ("crypt", "dag"),
        ("mines", "hammer"),
        ("garden", "bow"),
        ("cathedral", "staff"),
        ("fish", "sword"),
    ];

    // --- Armas: ritmo, alcance, combo, carga, windup ---

    [SmokeScenario("combate qa: testa ritmo das armas segurando o ataque (1/recarga)", order: 80)]
    private async Task QaWeaponRhythm()
    {
        var (world, combat) = await CombatSetup();
        var report = new List<string>();
        const double Hitstop = 0.035 * (1 - CombatClock.HitstopScale); // cada acerto congela ~0,032 s de combate (v2)
        foreach (var (cls, expectedPerSecond, distance) in new[] { ("sword", 3 / (1.12 + (3 * Hitstop)), 20f), ("dag", 1 / (0.15 + Hitstop), 16f), ("staff", 1 / (0.72 + Hitstop), 50f) })
        {
            var (wolf, player) = await QaDuelSetup(world, combat, cls, distance);
            wolf.Hp = 99999;
            SetAim(Vector2.Right);
            await Frames(2);
            var hits = 0;
            var last = wolf.Hp;
            Input.ActionPress(InputActions.Attack);
            for (var i = 0; i < 180; i++)
            {
                await NextPhysicsFrame();
                wolf.Stun = 3;
                wolf.X = player.GlobalPosition.X + distance; // segura o lobo no lugar (o knockback empurraria para fora do alcance)
                wolf.Y = player.GlobalPosition.Y;
                if (wolf.Hp < last)
                {
                    hits++;
                }

                last = wolf.Hp;
            }

            Input.ActionRelease(InputActions.Attack);
            SetAim(Vector2.Zero);
            var perSecond = hits / 3.0;
            report.Add($"{cls} {perSecond:0.00}/s (esperado {expectedPerSecond:0.00})");
            Require(Math.Abs(perSecond - expectedPerSecond) <= expectedPerSecond * 0.2, $"{cls}: segurar o ataque deu {perSecond:0.00} acertos/s, esperado ~{expectedPerSecond:0.00}");
            wolf.Kill();
        }

        Log.Info("ritmo: " + string.Join(" · ", report));
    }

    [SmokeScenario("combate qa: testa alcance das armas corpo a corpo na borda", order: 81)]
    private async Task QaMeleeReach()
    {
        var (world, combat) = await CombatSetup();
        foreach (var (cls, range) in new[] { ("sword", 30f), ("dag", 24f), ("hammer", 36f) })
        {
            foreach (var (extra, shouldHit) in new[] { (-4f, true), (8f, false) })
            {
                var (wolf, player) = await QaDuelSetup(world, combat, cls, 40);
                var dist = range + (float)wolf.Radius + extra;
                wolf.X = player.GlobalPosition.X + dist;
                wolf.Y = player.GlobalPosition.Y;
                wolf.Hp = 99999;
                var hp = wolf.Hp;
                SetAim(Vector2.Right);
                await Frames(2);
                await Tap(InputActions.Attack);
                await WaitSeconds(0.45); // martelo: windup 0,24 s
                SetAim(Vector2.Zero);
                var hit = wolf.Hp < hp;
                Require(hit == shouldHit, $"{cls}: lobo a {dist:0.0} u (alcance {range} + raio {wolf.Radius} {extra:+0;-0}) {(hit ? "foi acertado" : "não foi acertado")}");
                wolf.Kill();
            }
        }
    }

    [SmokeScenario("combate qa: testa combo da Lâmina em jogo (3º forte, janela 0,6 s)", order: 82)]
    private async Task QaSwordComboInGame()
    {
        var (world, combat) = await CombatSetup();
        var (wolf, _) = await QaDuelSetup(world, combat, "sword", 20);
        wolf.Hp = 99999;
        SetAim(Vector2.Right);
        await Frames(2);
        var st = combat.PlayerCombat.State;
        var steps = new List<int>();
        var damage = new List<double>();
        for (var i = 0; i < 4; i++)
        {
            await WaitUntil(() => st.CanAttack, 1, "recarga da Lâmina");
            var hp = wolf.Hp;
            await Tap(InputActions.Attack);
            steps.Add(st.Combo);
            damage.Add(hp - wolf.Hp);
            wolf.Stun = 3;
        }

        Require(string.Join(",", steps) == "0,1,2,0", $"combo de 3 golpes seguidos: [{string.Join(",", steps)}]");
        Require(damage[2] > Math.Min(damage[0], damage[1]) * 1.3, $"3º golpe deveria bater ~1,6×: [{string.Join(", ", damage)}]");
        await WaitSeconds(0.75);
        await Tap(InputActions.Attack);
        Require(st.Combo == 0, $"depois de 0,75 s parado o combo deveria recomeçar, está em {st.Combo}");
        SetAim(Vector2.Zero);
        wolf.Kill();
    }

    [SmokeScenario("combate qa: testa arco — tiro rápido para no 1º lobo, carga cheia perfura dois", order: 83)]
    private async Task QaBowPierceInGame()
    {
        var (world, combat) = await CombatSetup();
        foreach (var full in new[] { false, true })
        {
            var (first, player) = await QaDuelSetup(world, combat, "bow", 40);
            var second = combat.SpawnMonsterNow("wolf", player.GlobalPosition + new Vector2(80, 0), chase: false, invasion: false).Body;
            second.SpawnT = 0;
            first.Hp = second.Hp = 99999;
            second.Stun = 5;
            SetAim(Vector2.Right);
            await Frames(2);
            var hp1 = first.Hp;
            var hp2 = second.Hp;
            Input.ActionPress(InputActions.Attack);
            if (full)
            {
                await WaitUntil(() => combat.PlayerCombat.State.Charge >= PlayerCombatState.FullChargeAt, 2, "carga cheia");
            }
            else
            {
                await Frames(3);
            }

            Input.ActionRelease(InputActions.Attack);
            await WaitSeconds(0.6);
            SetAim(Vector2.Zero);
            var hitFirst = first.Hp < hp1;
            var hitSecond = second.Hp < hp2;
            Log.Info($"arco {(full ? "cheio" : "rápido")}: 1º {hp1 - first.Hp:0}, 2º {hp2 - second.Hp:0}");
            Require(hitFirst, $"arco {(full ? "cheio" : "rápido")} deveria acertar o 1º lobo");
            Require(hitSecond == full, full ? "flecha cheia deveria perfurar e acertar o 2º lobo" : "flecha rápida não deveria perfurar");
            first.Kill();
            second.Kill();
        }
    }

    [SmokeScenario("combate qa: testa orbe do Cajado curvando até o alvo fora da mira", order: 84)]
    private async Task QaStaffHoming()
    {
        var (world, combat) = await CombatSetup();
        var (wolf, player) = await QaDuelSetup(world, combat, "staff", 60);
        wolf.Y = player.GlobalPosition.Y + 22; // ~20° abaixo da mira
        wolf.Hp = 99999;
        var hp = wolf.Hp;
        SetAim(Vector2.Right);
        await Frames(2);
        await Tap(InputActions.Attack);
        await WaitUntil(() => wolf.Hp < hp, 1.2, "orbe teleguiado acertar o lobo fora da linha de mira");
        await Shot("qa-cajado-orbe");
        SetAim(Vector2.Zero);
        wolf.Kill();
    }

    [SmokeScenario("combate qa: testa windup do Martelo (0,24 s) antes do dano", order: 85)]
    private async Task QaHammerWindupInGame()
    {
        var (world, combat) = await CombatSetup();
        var (wolf, _) = await QaDuelSetup(world, combat, "hammer", 22);
        wolf.Hp = 99999;
        var hp = wolf.Hp;
        SetAim(Vector2.Right);
        await Frames(2);
        Input.ActionPress(InputActions.Attack);
        await NextPhysicsFrame();
        await NextPhysicsFrame();
        Input.ActionRelease(InputActions.Attack);
        Require(combat.PlayerCombat.State.IsWindingUp, "martelo deveria estar no windup");
        var frames = 2; // o windup começou no 1º frame do toque
        while (wolf.Hp >= hp && frames < 60)
        {
            await NextPhysicsFrame();
            frames++;
        }

        // contado em frames de física (60 Hz): o tempo real não serve porque Shot() congela o jogo enquanto grava o PNG
        Log.Info($"martelo: dano {frames} frames ({frames / 60.0:0.00} s) depois do windup começar");
        Require(frames >= 13 && frames <= 20, $"windup do martelo deveria levar ~0,24 s (15 frames), levou {frames} frames");
        await WaitUntil(() => combat.PlayerCombat.State.CanAttack, 2, "recarga do martelo");
        await Tap(InputActions.Attack);
        await Shot("qa-martelo-windup");
        await WaitUntil(() => !combat.PlayerCombat.State.IsWindingUp, 1, "golpe do martelo");
        await Frames(3);
        await Shot("qa-martelo-onda");
        SetAim(Vector2.Zero);
        wolf.Kill();
    }

    [SmokeScenario("combate qa: testa Adagas pelas costas ≈ 2× o dano de frente", order: 86)]
    private async Task QaDaggerBackstabInGame()
    {
        var (world, combat) = await CombatSetup();
        var avg = new double[2];
        for (var side = 0; side < 2; side++)
        {
            var (wolf, player) = await QaDuelSetup(world, combat, "dag", 16);
            wolf.Hp = 99999;
            var total = 0.0;
            var n = 0;
            SetAim(Vector2.Right);
            await Frames(2);
            for (var i = 0; i < 12; i++)
            {
                wolf.Face = side == 0 ? -1 : 1; // -1 olha para o jogador (frente); 1 dá as costas
                wolf.Stun = 3;
                wolf.X = player.GlobalPosition.X + 16;
                wolf.Y = player.GlobalPosition.Y;
                await WaitUntil(() => combat.PlayerCombat.State.CanAttack, 1, "recarga das adagas");
                var hp = wolf.Hp;
                await Tap(InputActions.Attack);
                if (wolf.Hp < hp)
                {
                    total += hp - wolf.Hp;
                    n++;
                }
            }

            SetAim(Vector2.Zero);
            Require(n >= 10, $"adagas acertaram só {n}/12 golpes {(side == 0 ? "de frente" : "pelas costas")}");
            avg[side] = total / n;
            wolf.Kill();
        }

        var ratio = avg[1] / avg[0];
        Log.Info($"adagas: frente {avg[0]:0.0}, costas {avg[1]:0.0}, razão {ratio:0.00}");
        Require(ratio > 1.6 && ratio < 2.5, $"costas/frente das adagas deveria ser ~2, deu {ratio:0.00}");
    }

    // --- Monstros: knockback, i-frames, esquiva, poção, ímã ---

    [SmokeScenario("combate qa: testa knockback em jogo — comum, elite ×0,35, chefe parado", order: 87)]
    private async Task QaKnockbackInGame()
    {
        var (world, combat) = await CombatSetup();
        var pushed = new Dictionary<string, double>();
        foreach (var id in new[] { "wolf", "wolfE", "angler" })
        {
            var (_, player) = await QaDuelSetup(world, combat, "sword", 40, spawnWolf: false);
            var m = combat.SpawnMonsterNow(id, player.GlobalPosition + new Vector2(20 + (id == "angler" ? 8 : 0), 0), chase: false, invasion: false).Body;
            m.SpawnT = 0;
            m.Hp = 99999;
            m.Stun = 3;
            var x0 = m.X;
            SetAim(Vector2.Right);
            await Frames(2);
            await Tap(InputActions.Attack);
            await WaitSeconds(0.5);
            SetAim(Vector2.Zero);
            pushed[id] = m.X - x0;
            m.Kill();
        }

        Log.Info($"knockback (u): lobo {pushed["wolf"]:0.0}, Alfa {pushed["wolfE"]:0.0}, Abissal {pushed["angler"]:0.0}");
        Require(pushed["wolf"] > 6, $"lobo deveria ser empurrado (~13 u), foi {pushed["wolf"]:0.0}");
        var ratio = pushed["wolfE"] / pushed["wolf"];
        Require(ratio > 0.2 && ratio < 0.5, $"elite deveria levar ~0,35× do empurrão: razão {ratio:0.00}");
        Require(Math.Abs(pushed["angler"]) < 0.5, $"chefe não deveria se mexer: {pushed["angler"]:0.00} u");
    }

    [SmokeScenario("combate qa: testa i-frames de 0,75 s em jogo e empurrão no jogador", order: 88)]
    private async Task QaPlayerIFramesInGame()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var state = GameManager.Instance.State;
        var player = FindPlayer()!;
        var p0 = player.GlobalPosition;
        Require(combat.HurtPlayer(10, p0 + new Vector2(-20, 0)), "1º golpe deveria ferir");
        var hp = state.Hp;
        await WaitSeconds(0.25);
        var pushed = player.GlobalPosition.X - p0.X;
        Require(pushed > 4, $"golpe deveria empurrar o jogador para longe da fonte: {pushed:0.0} u");
        await WaitSeconds(0.35); // ~0,6 s
        Require(!combat.HurtPlayer(10, player.GlobalPosition), "dentro dos 0,75 s não deveria ferir");
        Require(state.Hp == hp, "HP mudou durante os i-frames");
        await WaitUntil(() => !combat.PlayerCombat.State.IsInvulnerable, 0.5, "i-frames acabarem");
        Require(combat.HurtPlayer(10, player.GlobalPosition), "depois de 0,75 s deveria ferir de novo");
        Require(state.Hp < hp, "HP deveria cair no 2º golpe");
    }

    [SmokeScenario("combate qa: testa esquiva em jogo — distância, energia e recarga", order: 89)]
    private async Task QaDodgeInGame()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var st = combat.PlayerCombat.State;
        SetAim(Vector2.Right);
        await Frames(3);
        var x0 = player.GlobalPosition.X;
        await Tap(InputActions.Dodge);
        await WaitUntil(() => !st.IsDashing, 0.5, "esquiva terminar");
        var dx = player.GlobalPosition.X - x0;
        Log.Info($"esquiva: {dx:0.0} u, energia {st.Energy:0}");
        Require(dx > 40 && dx < 56, $"esquiva parada deveria andar ~46–50 u na mira, andou {dx:0.0}");
        Require(st.Energy < 80, $"esquiva deveria gastar 22 de energia: {st.Energy:0.0}");

        // spam: 10 toques de esquiva em 1 s → recarga de 0,32 s e energia limitam a ~3–4
        var dashes = 0;
        var was = false;
        for (var i = 0; i < 60; i++)
        {
            if (i % 6 == 0)
            {
                Input.ActionPress(InputActions.Dodge);
            }
            else if (i % 6 == 2)
            {
                Input.ActionRelease(InputActions.Dodge);
            }

            await NextPhysicsFrame();
            if (st.IsDashing && !was)
            {
                dashes++;
            }

            was = st.IsDashing;
        }

        Input.ActionRelease(InputActions.Dodge);
        SetAim(Vector2.Zero);
        Log.Info($"spam de esquiva: {dashes} esquivas em 1 s");
        Require(dashes >= 2 && dashes <= 4, $"spam de esquiva em 1 s deveria dar 2–4 esquivas (recarga 0,32 s), deu {dashes}");
    }

    [SmokeScenario("combate qa: testa poção em jogo — 45% e recarga de 1 s", order: 90)]
    private async Task QaPotionInGame()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var state = GameManager.Instance.State;
        state.Damage(80);
        var potions = state.Save.Potions;
        await Tap(InputActions.Potion);
        Require(state.Hp == 65, $"poção deveria curar 45 (45% de 100): HP {state.Hp}");
        await Tap(InputActions.Potion);
        Require(state.Save.Potions == potions - 1, "2ª poção em menos de 1 s não deveria sair");
        await WaitSeconds(1.05);
        await Tap(InputActions.Potion);
        Require(state.Save.Potions == potions - 2 && state.Hp == 100, $"depois de 1 s a poção sai: HP {state.Hp}, poções {state.Save.Potions}");
    }

    [SmokeScenario("combate qa: testa ímã dos drops — perto vem, longe fica", order: 91)]
    private async Task QaMagnetInGame()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
        var run = combat.Brecha.Run!;
        var p = FindPlayer()!.GlobalPosition;
        var lumes = run.Bag.Lumes;
        combat.DropLoot(new LootDrop(LootKind.Lumes, null, 5), p + new Vector2(20, -6));
        var far = world.CorrectToWalkable(p + new Vector2(-110, 0));
        combat.DropLoot(new LootDrop(LootKind.Lumes, null, 7), far - new Vector2(0, 6));
        await WaitUntil(() => run.Bag.Lumes == lumes + 5, 2, "Lume a 20 u entrar na bolsa pelo ímã");
        await WaitSeconds(1.5);
        Require(run.Bag.Lumes == lumes + 5, $"Lume a 110 u não deveria vir sozinho: bolsa {run.Bag.Lumes}");
        await FleeRun(world, combat);
    }

    [SmokeScenario("combate qa: testa números de dano em pool sob spam de acertos", order: 92)]
    private async Task QaDamageNumbersPool()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "dag");
        world.GoMap("guild", 250, 500, instant: true);
        await WaitMapSettled(world);
        var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var player = FindPlayer()!;
        player.Teleport(new Vector2(155 + 14, 455));
        SetAim(Vector2.Left);
        await Frames(2);
        Input.ActionPress(InputActions.Attack);
        var peak = 0;
        for (var i = 0; i < 300; i++)
        {
            await NextPhysicsFrame();
            peak = Math.Max(peak, combat.Overlay.LiveFloats);
        }

        Input.ActionRelease(InputActions.Attack);
        SetAim(Vector2.Zero);
        await Shot("qa-numeros-boneco");
        var after = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        Log.Info($"números: pico {peak} vivos (pool {CombatOverlay.MaxFloats}), nós {nodes} → {after}");
        Require(peak > 4 && peak <= CombatOverlay.MaxFloats, $"pico de números: {peak}");
        Require(after - nodes < 10, $"números de dano não deveriam criar nós: {nodes} → {after}");
    }

    // --- IAs: verbo e telegraph ---

    [SmokeScenario("combate qa: testa verbo e telegraph de cada IA", order: 93)]
    private async Task QaEveryAiVerb()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var missing = new List<string>();
        var report = new List<string>();
        foreach (var id in new[] { "wolf", "coss", "cnev", "ksin", "cvel", "mbes", "maut", "cosr", "gmus", "angler", "reaper", "colosso", "rainha", "knight" })
        {
            player.Teleport(new Vector2(800, 520));
            await NextPhysicsFrame();
            var e = combat.SpawnMonsterNow(id, world.CorrectToWalkable(player.GlobalPosition + new Vector2(50, 0)), chase: true, invasion: false);
            var m = e.Body;
            m.SpawnT = 0;
            m.Cd = 0;
            m.Hp = 99999;
            var seen = new HashSet<MonsterState>();
            var shot = false;
            var teleFrames = 0;
            var teleLongest = 0;
            for (var i = 0; i < 360; i++)
            {
                combat.PlayerCombat.State.GrantInvulnerability(1);
                await NextPhysicsFrame();
                seen.Add(m.State);
                if (QaGroundTelegraph(m))
                {
                    teleFrames++;
                    if (!shot && teleFrames > 12)
                    {
                        shot = true;
                        await Shot($"qa-ia-{id}");
                    }
                }
                else if (teleFrames > 0)
                {
                    teleLongest = Math.Max(teleLongest, teleFrames);
                    teleFrames = 0;
                }
            }

            var verbs = seen.Where(s => s is not (MonsterState.Idle or MonsterState.Chase or MonsterState.Rec)).Select(s => s.ToString()).ToList();
            report.Add($"{id}({m.Def.Ai}): {string.Join("/", verbs)} aviso no chão {teleLongest * 1000 / 60} ms");
            if (verbs.Count == 0)
            {
                missing.Add(id);
            }

            foreach (var other in combat.Enemies)
            {
                other.Body.Kill(); // inclui as invocações
            }

            await Frames(2);
        }

        Log.Info("IAs: " + string.Join(" · ", report));
        Require(missing.Count == 0, $"monstros sem nenhum ataque em 6 s perto do jogador: {string.Join(", ", missing)}");
    }

    // --- Brecha de cada tema até o chefe ---

    [SmokeScenario("estresse: combate qa testa a Brecha de cada tema até o chefe e o portal", order: 94)]
    private async Task QaEveryThemeToBoss()
    {
        var failures = new List<string>();
        foreach (var (theme, cls) in QaThemes)
        {
            try
            {
                await QaPlayTheme(theme, cls, failures);
            }
            catch (SmokeFailure ex)
            {
                failures.Add($"{theme}: {ex.Message}");
                Log.Error($"tema {theme} falhou: {ex.Message}");
                var (world, combat) = (FindWorld(), CombatDirector.Instance!);
                await FleeRun(world, combat);
            }
        }

        Require(failures.Count == 0, string.Join(" | ", failures));
    }

    private async Task QaPlayTheme(string theme, string cls, List<string> failures)
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", cls);
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
        var run = combat.Brecha.Run!;
        await Shot($"qa-{theme}-entrada");
        var hasDoor = world.Map.Door is not null;
        var t0 = Time.GetTicksMsec();
        Func<bool> wavesDone = () => hasDoor ? world.DoorOpen : FindBoss(combat) is not null;
        try
        {
            await Bot(combat, wavesDone, 120, $"{theme}: limpar as ondas com {cls}", shotAt: $"qa-{theme}-luta");
        }
        catch (SmokeFailure ex)
        {
            // registra e segue (mata o que sobrou) para ainda testar porta, chefe e portal deste tema
            failures.Add($"{theme}: {ex.Message}");
            Log.Error($"{theme}: onda travada: {ex.Message}");
            await Shot($"qa-{theme}-onda-travada");
            await WaitUntil(() =>
            {
                KillAllMonsters(combat);
                return wavesDone();
            }, 30, $"{theme}: terminar as ondas matando à força");
        }

        Log.Info($"{theme}/{cls}: ondas ({string.Join(" | ", run.Waves.Select(w => string.Join(",", w)))}) em {(Time.GetTicksMsec() - t0) / 1000.0:0.0}s, {run.Kills} mortes");
        if (hasDoor)
        {
            await Shot($"qa-{theme}-porta");
            FindPlayer()!.Teleport(new Vector2(world.Map.Spawn.X, world.Map.Spawn.Y));
            await NextPhysicsFrame();
            await TravelInto(world, world.Map.Door!.Value, () => world.MapId == World.World.CoreMapId);
            await WaitMapSettled(world);
        }

        await WaitUntil(() => FindBoss(combat) is { } b && b.Body.SpawnT <= 0, 8, $"{theme}: chefe nascer");
        var boss = FindBoss(combat)!;
        var seen = new HashSet<MonsterState>();
        var t1 = Time.GetTicksMsec();
        // 8 s com o jogador imune para ver o repertório do chefe; o bot luta normalmente
        await Bot(combat, () =>
        {
            combat.PlayerCombat.State.GrantInvulnerability(0.2);
            seen.Add(boss.Body.State);
            return Time.GetTicksMsec() - t1 > 8000 || boss.Body.Dead;
        }, 12, $"{theme}: observar o chefe", shotAt: $"qa-{theme}-chefe");
        Log.Info($"{theme}: chefe {boss.Body.Def.Id} ({boss.Body.Def.Ai}) usou {string.Join("/", seen)}; HP {boss.Body.Hp:0}/{boss.Body.MaxHp:0}");
        Require(seen.Count >= 3, $"{theme}: chefe quase parado em 8 s: {string.Join("/", seen)}");
        boss.Body.Hp = Math.Min(boss.Body.Hp, 60); // atalho de teste: só o HP do chefe
        await Bot(combat, () => boss.Body.Dead, 60, $"{theme}: derrotar o chefe");
        Require(run.BossDefeated, $"{theme}: chefe derrotado deveria fechar a Brecha");
        var portal = await WaitForExitPortal(combat);
        await Shot($"qa-{theme}-saida");
        await InteractWithNode(world, portal.Interaction);
        await WaitUntil(() => world.MapId == World.World.CityMapId, 4, $"{theme}: voltar à Rua da Forja");
        await WaitMapSettled(world);
    }

    [SmokeScenario("combate qa: testa Espectro voador contorna a parede e vem até o jogador (cripta)", order: 93)]
    private async Task QaFlyerComesAroundWalls()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "dag");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "crypt", rank: 0);
        var player = FindPlayer()!;
        player.Teleport(new Vector2(813, 741)); // ponto real onde a 1ª onda da cripta travou no estresse
        await NextPhysicsFrame();
        var spot = world.CorrectToWalkable(new Vector2(1461, 765));
        var ghost = combat.SpawnMonsterNow("cnev", spot, chase: true, invasion: false).Body;
        ghost.SpawnT = 0;
        var closest = double.MaxValue;
        var casts = 0;
        var was = false;
        // Integração v0.1: na cripta redesenhada (B4c) o caminho daqui até o jogador tem ~960 u (era ~650 na v2): 30 s.
        for (var i = 0; i < 60 * 30 && casts == 0; i++)
        {
            KillOthers(combat, ghost);
            combat.PlayerCombat.State.GrantInvulnerability(1);
            await NextPhysicsFrame();
            closest = Math.Min(closest, new Vector2((float)ghost.X, (float)ghost.Y).DistanceTo(player.GlobalPosition));
            casts += ghost.State == MonsterState.Cast && !was ? 1 : 0;
            was = ghost.State == MonsterState.Cast;
        }

        await Shot("qa-espectro-parede");
        Log.Info($"Espectro: de ({spot.X:0},{spot.Y:0}) chegou a {closest:0} u do jogador, lançou {casts}× em 30 s; parou em ({ghost.X:0},{ghost.Y:0}) {combat.DescribeChase(ghost)}");
        ghost.Kill();
        await FleeRun(world, combat);
        Require(casts > 0, $"Espectro da Névoa (voador) ficou preso: em 30 s chegou no máximo a {closest:0} u e nunca atacou — a onda não termina sem o jogador caçá-lo");
    }

    private static void KillOthers(CombatDirector combat, MonsterBody keep)
    {
        foreach (var e in combat.Enemies)
        {
            if (e.Body != keep && !e.Body.Dead && !e.Body.IsDummy)
            {
                e.Body.Kill();
            }
        }
    }

    [SmokeScenario("combate qa: testa sprites de monstros sem recorte reto na borda da imagem", order: 93)]
    private async Task QaEnemySpritesNotClipped()
    {
        await Task.CompletedTask;
        var bad = new List<string>();
        var dir = DirAccess.Open("res://assets/sprites/enemies");
        Require(dir is not null, "sem assets/sprites/enemies");
        foreach (var file in dir!.GetFiles())
        {
            if (!file.EndsWith(".png", StringComparison.Ordinal))
            {
                continue;
            }

            var tex = GD.Load<Texture2D>($"res://assets/sprites/enemies/{file}");
            var img = tex.GetImage();
            if (img.IsCompressed())
            {
                img.Decompress();
            }

            int w = img.GetWidth(), h = img.GetHeight();
            int top = 0, left = 0, right = 0;
            for (var x = 0; x < w; x++)
            {
                top += img.GetPixel(x, 0).A > 0.8f ? 1 : 0;
            }

            for (var y = 0; y < h; y++)
            {
                left += img.GetPixel(0, y).A > 0.8f ? 1 : 0;
                right += img.GetPixel(w - 1, y).A > 0.8f ? 1 : 0;
            }

            // a base (sombra/chão) pode encostar embaixo; em cima e dos lados a arte cortada vira uma reta visível no jogo
            if (top > 12 || left > 24 || right > 24)
            {
                bad.Add($"{file} (cima {top}, esq. {left}, dir. {right} px opacos na borda de {w}×{h})");
            }
        }

        Log.Info($"sprites recortados: {bad.Count}: {string.Join(" · ", bad)}");
        Require(bad.Count == 0, $"{bad.Count} sprite(s) de monstro com a arte cortada na borda: {string.Join(", ", bad)}");
    }

    // --- Estresse ---

    [SmokeScenario("estresse: combate qa testa 40 monstros e chuva de projéteis sem cair de 60 FPS", order: 95)]
    private async Task QaManyMonstersFrameTime()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "staff");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var p = FindPlayer()!.GlobalPosition;
        string[] types = ["cvel", "mbes", "gsei", "cnev", "ksin", "wolf", "fishm", "maut", "gmus", "coss"];
        for (var i = 0; i < 40; i++)
        {
            var a = i / 40f * MathF.Tau;
            var r = 70 + (i % 3 * 40);
            var at = world.CorrectToWalkable(p + (new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f) * r));
            combat.SpawnMonsterNow(types[i % types.Length], at, chase: true, invasion: false).Body.SpawnT = 0;
        }

        var nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        await WaitSeconds(1.5);
        await Shot("qa-40-monstros");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var frame = new List<double>(600);
        var step = new List<double>(600);
        var maxProj = 0;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var last = Time.GetTicksUsec();
        var frameIndex = 0;
        while (frame.Count < 600)
        {
            combat.PlayerCombat.State.GrantInvulnerability(1);
            if (frameIndex++ % 20 == 0)
            {
                Input.ActionPress(InputActions.Attack);
            }
            else
            {
                Input.ActionRelease(InputActions.Attack);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            frame.Add((now - last) / 1000.0);
            last = now;
            step.Add(combat.LastStepMs);
            maxProj = Math.Max(maxProj, combat.Projectiles.Live);
        }

        Input.ActionRelease(InputActions.Attack);
        await Shot("qa-40-monstros-fim");
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var (fMean, fP95, fMax) = QaStats(frame);
        var (sMean, sP95, sMax) = QaStats(step);
        Log.Info($"40 monstros: frame média {fMean:0.00} ms p95 {fP95:0.00} máx {fMax:0.00}; combate média {sMean:0.00} p95 {sP95:0.00} máx {sMax:0.00}; projéteis no pico {maxProj}; vivos {combat.AliveMonsters}; nós {nodes0} → {nodes1}");
        foreach (var e in combat.Enemies)
        {
            e.Body.Kill();
        }

        Require(maxProj >= 10, $"teste fraco: só {maxProj} projéteis ao mesmo tempo");
        Require(sP95 < 6, $"combate p95 {sP95:0.00} ms com 40 monstros");
        if (DisplayServer.GetName() != "headless")
        {
            Require(fP95 <= 20, $"p95 do frame {fP95:0.00} ms (> 20 ms) com 40 monstros");
        }
    }

    [SmokeScenario("estresse: combate qa testa spam de ataque, esquiva, poção, Eco, arma e pausa na Brecha", order: 96)]
    private async Task QaButtonSpam()
    {
        var (world, combat) = await CombatSetup();
        var rng = new Random(7);
        StringName[] actions = [InputActions.Attack, InputActions.AttackHeavy, InputActions.Dodge, InputActions.Potion, InputActions.Eco1, InputActions.Eco2,
            InputActions.Ultimate, InputActions.WeaponNext, InputActions.WeaponPrev, InputActions.WeaponSlots[2], InputActions.Interact];
        foreach (var cls in AllClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            var player = FindPlayer()!;
            var paused = false;
            for (var i = 0; i < 360; i++)
            {
                var a = actions[rng.Next(actions.Length)];
                if (Input.IsActionPressed(a))
                {
                    Input.ActionRelease(a);
                }
                else
                {
                    Input.ActionPress(a);
                }

                if (i % 30 == 0)
                {
                    var ang = rng.NextDouble() * Math.Tau;
                    PressDirection(new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)));
                    SetAim(new Vector2((float)Math.Sin(ang), (float)Math.Cos(ang)));
                }

                if (i % 45 == 0)
                {
                    paused = !paused;
                    EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, paused);
                }

                if (GameManager.Instance.State.Hp < 30)
                {
                    GameManager.Instance.State.RestoreFullHealth();
                }

                await NextPhysicsFrame();
            }

            EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, false);
            ReleaseAllActions();
            PressDirection(Vector2.Zero);
            SetAim(Vector2.Zero);
            UI.GameUi.Current?.CloseAll(); // o Interact do spam pode abrir uma fala/olhar: a tela trava o jogador de propósito, quem joga fecha
            await WaitSeconds(1.2);
            var st = combat.PlayerCombat.State;
            Require(!combat.PlayerDead, $"{cls}: morreu no spam (HP era restaurado)");
            Require(!st.IsCharging && !st.IsWindingUp && !st.IsDashing, $"{cls}: estado preso depois do spam (carga {st.Charge:0.00}, windup {st.WindTime:0.00})");
            Require(!player.MovementLocked, $"{cls}: jogador travado (MovementLocked) depois do spam");
            var x0 = player.GlobalPosition;
            PressDirection(x0.X > 600 ? Vector2.Left : Vector2.Right);
            await WaitSeconds(0.6);
            PressDirection(Vector2.Zero);
            var moved = player.GlobalPosition.DistanceTo(x0);
            if (moved < 10)
            {
                PressDirection(Vector2.Up);
                await WaitSeconds(0.6);
                PressDirection(Vector2.Zero);
                moved = player.GlobalPosition.DistanceTo(x0);
            }

            Require(moved > 10, $"{cls}: jogador não anda depois do spam ({moved:0.0} u em 0,6 s)");
            await FleeRun(world, combat);
        }
    }

    [SmokeScenario("estresse: combate qa testa 25 entradas e fugas de Brecha seguidas (50 trocas de mapa)", order: 97)]
    private async Task QaManyBrechaTransitions()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var themes = new[] { "fish", "crypt", "mines", "garden", "cathedral" };
        var samples = new List<string>();
        double nodes0 = 0;
        double mem0 = 0;
        for (var i = 0; i < 25; i++)
        {
            var theme = themes[i % themes.Length];
            await EnterBrecha(world, combat, spot: i % 2, theme: theme, rank: 0);
            await WaitUntil(() => combat.Brecha.Run!.Wave >= 1 && combat.AliveMonsters > 0, 8, $"{theme}: 1ª onda nascer");
            await Frames(20);
            await FleeRun(world, combat);
            Require(combat.Enemies.Count == 0 || combat.AliveMonsters == 0, $"monstros sobraram na cidade depois da fuga {i}: {combat.AliveMonsters}");
            if (i == 4)
            {
                GC.Collect();
                nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
            }

            if (i % 5 == 4)
            {
                samples.Add($"{i + 1}: nós {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} mem {Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024):0.0}MB pickups {combat.Pickups.Count} props {combat.Props.Count} proj {combat.Projectiles.Live}");
            }
        }

        GC.Collect();
        var nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024);
        Log.Info("trocas de mapa: " + string.Join(" · ", samples));
        Require(nodes1 - nodes0 < 40, $"nós crescendo com as trocas de mapa: {nodes0} → {nodes1}");
        Require(mem1 - mem0 < 24, $"memória crescendo com as trocas de mapa: {mem0:0.0} → {mem1:0.0} MB");
    }

    [SmokeScenario("estresse: combate qa testa bots de exploração nas salas da Brecha (alcance e travamentos)", order: 98)]
    private async Task QaExploreBrechaRooms()
    {
        var (world, combat) = await CombatSetup();
        var problems = new List<string>();
        var rng = new Random(11);
        var oldScale = Engine.TimeScale;
        try
        {
            foreach (var (theme, _) in QaThemes)
            {
                GameManager.Instance.NewGame("Bot", "sword");
                world.GoMap("forge", 800, 520, instant: true);
                await WaitMapSettled(world);
                await EnterBrecha(world, combat, spot: 0, theme: theme, rank: 0);
                await QaExploreRoom(world, combat, rng, problems);
                if (world.Map.Door is { } door && world.DoorOpen)
                {
                    FindPlayer()!.Teleport(new Vector2(world.Map.Spawn.X, world.Map.Spawn.Y));
                    await NextPhysicsFrame();
                    Engine.TimeScale = 1;
                    await TravelInto(world, door, () => world.MapId == World.World.CoreMapId);
                    await WaitMapSettled(world);
                    combat.PlayerCombat.State.GrantInvulnerability(99);
                    await QaExploreRoom(world, combat, rng, problems);
                }

                Engine.TimeScale = 1;
                await FleeRun(world, combat);
            }
        }
        finally
        {
            Engine.TimeScale = oldScale;
            PressDirection(Vector2.Zero);
        }

        Log.Info($"exploração: {problems.Count} problema(s)" + (problems.Count > 0 ? ": " + string.Join(" | ", problems) : string.Empty));
        Require(problems.Count == 0, $"{problems.Count} problema(s) de exploração: {string.Join(" | ", problems.Take(12))}");
    }

    /// <summary>
    /// Numa sala da Brecha: mata os monstros que nascem (golpe real do diretor, para as ondas andarem), tenta alcançar
    /// a pé cada ponto de interesse (baús, props quebráveis, porta, borda de fuga, portal), abre baús e quebra props,
    /// e passeia ao acaso ~60 s de jogo (TimeScale 2) marcando travamentos: input ativo e &lt; 1 u em 1 s com o
    /// caminho à frente livre.
    /// </summary>
    private async Task QaExploreRoom(World.World world, CombatDirector combat, Random rng, List<string> problems)
    {
        var mapId = world.MapId;
        var player = FindPlayer()!;
        var area = world.WalkArea;
        combat.PlayerCombat.State.GrantInvulnerability(99);
        await Shot($"qa-explora-{mapId}");

        // Pontos de interesse
        var pois = new List<(string Name, Vector2 At, float Near)>();
        foreach (var pp in combat.Props)
        {
            if (pp.IsChest || pp.Breakable)
            {
                pois.Add(($"{(pp.IsChest ? "baú" : pp.Kind)}", pp.Position, pp.IsChest ? 16 : 24));
            }
        }

        if (world.Map.Door is { } door)
        {
            pois.Add(("porta", new Vector2(door.Center.X, door.Center.Y), Math.Max(door.Width, door.Height) / 2 + 20));
        }

        if (world.Map.Back is { } back)
        {
            pois.Add(("borda de fuga", new Vector2(back.Center.X, back.Center.Y), Math.Max(back.Width, back.Height) / 2 + 24));
        }

        var spawn = new MapPoint(world.Map.Spawn.X, world.Map.Spawn.Y);
        foreach (var (name, at, near) in pois)
        {
            KillAllMonsters(combat);
            var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
            if (WalkPathfinder.FindPathNear(area, spawn, new MapPoint(at.X, at.Y), near) is null)
            {
                problems.Add($"{mapId}: {name} em ({at.X:0},{at.Y:0}) inalcançável a pé do spawn");
                continue;
            }

            if (name is "porta" or "borda de fuga")
            {
                continue; // só alcance; entrar troca de mapa (os outros cenários entram)
            }

            var path = WalkPathfinder.FindPathNear(area, start, new MapPoint(at.X, at.Y), near);
            if (path is null)
            {
                continue;
            }

            var walk = await QaWalkPath(world, combat, path, at, near + 6, 12, problems, name);
            if (walk == QaWalk.Timeout)
            {
                problems.Add($"{mapId}: não cheguei andando em {name} ({at.X:0},{at.Y:0}); parei em ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0})");
            }
        }

        // Interage com baús e quebra props alcançados
        foreach (var pp in combat.Props.ToList())
        {
            if (world.MapId != mapId || !GodotObject.IsInstanceValid(pp))
            {
                break; // a caminhada passou pela porta/portal
            }

            if (pp.IsChest && !pp.Open && pp.ChestInteraction is { } chest && player.GlobalPosition.DistanceTo(pp.Position) < 400)
            {
                var path = WalkPathfinder.FindPathNear(area, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), new MapPoint(pp.Position.X, pp.Position.Y), 14);
                if (path is not null && await QaWalkPath(world, combat, path, pp.Position, 20, 10, problems, "baú") == QaWalk.Reached)
                {
                    for (var w = 0; w < 90 && player.FocusedInteractable != chest; w++)
                    {
                        await NextPhysicsFrame();
                    }

                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
                    await Frames(2);
                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
                    await Frames(4);
                    if (!pp.Open)
                    {
                        problems.Add($"{mapId}: baú em ({pp.Position.X:0},{pp.Position.Y:0}) não abriu com interagir de perto (foco: {player.FocusedInteractable?.Prompt ?? "nenhum"})");
                    }
                    else
                    {
                        await Shot($"qa-explora-{mapId}-bau");
                    }
                }
            }
            else if (pp.Breakable && !pp.Broken && player.GlobalPosition.DistanceTo(pp.Position) < 400)
            {
                var path = WalkPathfinder.FindPathNear(area, new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y), new MapPoint(pp.Position.X, pp.Position.Y), 20);
                if (path is not null && await QaWalkPath(world, combat, path, pp.Position, 26, 10, problems, pp.Kind) == QaWalk.Reached)
                {
                    for (var k = 0; k < 4 && !pp.Broken; k++)
                    {
                        SetAim((pp.Position - new Vector2(0, 6) - (player.GlobalPosition - new Vector2(0, 10))).Normalized());
                        await Frames(2);
                        await Tap(InputActions.Attack);
                        await WaitSeconds(0.4);
                    }

                    SetAim(Vector2.Zero);
                    if (!pp.Broken)
                    {
                        problems.Add($"{mapId}: {pp.Kind} em ({pp.Position.X:0},{pp.Position.Y:0}) não quebrou com 4 golpes de perto ({player.GlobalPosition.DistanceTo(pp.Position):0} u)");
                    }
                }
            }
        }

        // Passeio aleatório
        Engine.TimeScale = 2;
        var window = 30; // 1 s de jogo a TimeScale 2 (60 Hz)
        var history = new Queue<Vector2>();
        var dir = Vector2.Right;
        var hold = 0;
        var stuckCells = new HashSet<(int, int)>();
        for (var f = 0; f < 60 * 30; f++)
        {
            KillAllMonsters(combat);
            combat.PlayerCombat.State.GrantInvulnerability(5);
            if (world.MapId != mapId)
            {
                break;
            }

            if (hold-- <= 0)
            {
                var a = rng.NextDouble() * Math.Tau;
                dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                hold = rng.Next(20, 60);
                history.Clear();
            }

            var p = player.GlobalPosition;
            if (world.Map.Back is { } bk && new Rect2(bk.X - 30, bk.Y - 30, bk.Width + 60, bk.Height + 60).HasPoint(p + (dir * 10)))
            {
                dir = -dir; // evita a borda de fuga (pediria escolha)
            }

            PressDirection(dir);
            await NextPhysicsFrame();
            history.Enqueue(player.GlobalPosition);
            if (history.Count > window)
            {
                var old = history.Dequeue();
                if (old.DistanceTo(player.GlobalPosition) < 1 && !world.IsTransitioning)
                {
                    var q = player.GlobalPosition;
                    var ahead = q + (dir * 5);
                    if (combat.Walkable(ahead.X, ahead.Y, false) && area.CanStand(ahead.X, ahead.Y) && stuckCells.Add(((int)q.X / 16, (int)q.Y / 16)))
                    {
                        problems.Add($"{mapId}: travei em ({q.X:0},{q.Y:0}) andando para ({dir.X:0.00},{dir.Y:0.00}) com o caminho livre à frente; {QaNearestProp(combat, q)}; {QaCollider(player, dir)}");
                        Log.Info("exploração: " + problems[^1]);
                        if (stuckCells.Count <= 3)
                        {
                            await Shot($"qa-trava-{mapId}-{(int)q.X}-{(int)q.Y}");
                        }
                    }

                    hold = 0;
                }
            }
        }

        Engine.TimeScale = 1;
        PressDirection(Vector2.Zero);
        await Shot($"qa-explora-{mapId}-fim");
    }

    /// <summary>Segue um caminho com input real (TimeScale 1); falso se não chegou a <paramref name="near"/> u do alvo.</summary>
    private async Task<QaWalk> QaWalkPath(World.World world, CombatDirector combat, IReadOnlyList<MapPoint> path, Vector2 target, float near, double timeoutSeconds, List<string> problems, string what)
    {
        Engine.TimeScale = 1;
        var player = FindPlayer()!;
        var next = 0;
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        var history = new Queue<Vector2>();
        try
        {
            while (player.GlobalPosition.DistanceTo(target) > near)
            {
                if (Time.GetTicksMsec() > deadline)
                {
                    return QaWalk.Timeout;
                }

                KillAllMonsters(combat);
                combat.PlayerCombat.State.GrantInvulnerability(5);
                var p = player.GlobalPosition;
                while (next < path.Count - 1 && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 3f)
                {
                    next++;
                }

                var wp = next < path.Count ? new Vector2(path[next].X, path[next].Y) : target;
                var d = (wp - p).Normalized();
                PressDirection(d);
                await NextPhysicsFrame();
                history.Enqueue(player.GlobalPosition);
                if (history.Count > 60)
                {
                    var old = history.Dequeue();
                    if (old.DistanceTo(player.GlobalPosition) < 1)
                    {
                        var q = player.GlobalPosition;
                        var ahead = q + (d * 6);
                        if (!combat.Walkable(ahead.X, ahead.Y, false))
                        {
                            Log.Info($"{world.MapId}: indo até {what}, um prop sólido em ({ahead.X:0},{ahead.Y:0}) fechou o caminho da máscara");
                            return QaWalk.PropInTheWay;
                        }

                        problems.Add($"{world.MapId}: travei em ({q.X:0},{q.Y:0}) indo até {what} ({target.X:0},{target.Y:0}) pelo caminho da máscara; {QaNearestProp(combat, q)}");
                        Log.Info("exploração: " + problems[^1]);
                        await Shot($"qa-trava-{world.MapId}-{(int)q.X}-{(int)q.Y}");
                        return QaWalk.Stuck;
                    }
                }
            }

            return QaWalk.Reached;
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }
    }

    private static string QaCollider(Player.Player player, Vector2 dir)
    {
        var hit = player.MoveAndCollide(dir * 3, testOnly: true);
        if (hit is null)
        {
            return "física livre (preso por outra coisa)";
        }

        var c = hit.GetCollider() as Node;
        return $"colide com {c?.Name ?? "?"} ({c?.GetParent()?.Name ?? "?"}) em ({hit.GetPosition().X:0},{hit.GetPosition().Y:0})";
    }

    private static string QaNearestProp(CombatDirector combat, Vector2 at)
    {
        DungeonProp? best = null;
        foreach (var pp in combat.Props)
        {
            if (!pp.Broken && (best is null || pp.Position.DistanceTo(at) < best.Position.DistanceTo(at)))
            {
                best = pp;
            }
        }

        return best is null ? "sem prop perto" : $"prop mais perto: {best.Kind} em ({best.Position.X:0},{best.Position.Y:0}) a {best.Position.DistanceTo(at):0} u, raio sólido {best.SolidRadius:0}, sólido={best.BlocksMovement}";
    }

    private enum QaWalk
    {
        Reached,
        Timeout,
        Stuck,
        PropInTheWay,
    }

    private static void KillAllMonsters(CombatDirector combat)
    {
        var enemies = combat.Enemies;
        for (var i = enemies.Count - 1; i >= 0; i--)
        {
            if (i >= enemies.Count)
            {
                continue;
            }

            var m = enemies[i].Body;
            if (!m.Dead && !m.IsDummy && m.SpawnT <= 0)
            {
                combat.HitMob(m, 99999, new Vector2((float)m.X, (float)m.Y - 30), default);
            }
        }
    }

    [SmokeScenario("combate qa: testa prints do combate na resolução da janela", order: 99)]
    private async Task QaResolutionShots()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "hammer");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
        var player = FindPlayer()!;
        var p = player.GlobalPosition;
        var a = combat.SpawnMonsterNow("wolf", p + new Vector2(24, 0), chase: false, invasion: false).Body;
        var b = combat.SpawnMonsterNow("cosr", world.CorrectToWalkable(p + new Vector2(-50, 10)), chase: true, invasion: false).Body;
        a.SpawnT = b.SpawnT = 0;
        a.Stun = 3;
        a.Face = -1;
        combat.PlayerCombat.State.GrantInvulnerability(30);
        SetAim(Vector2.Right);
        await Frames(2);
        await Tap(InputActions.Attack);
        await WaitSeconds(0.32);
        var size = DisplayServer.WindowGetSize();
        await Shot($"qa-res-{size.X}x{size.Y}-golpe");
        await WaitUntil(() => b.IsTelegraphing || b.State == MonsterState.Slam, 6, "Ossário erguer o golpe");
        await WaitSeconds(0.3);
        await Shot($"qa-res-{size.X}x{size.Y}-aviso");
        SetAim(Vector2.Zero);
        await FleeRun(world, combat);
    }

    // --- utilidades do QA ---

    /// <summary>Jogo novo da classe na Rua da Forja com um lobo atordoado à direita (olhando para o jogador).</summary>
    private async Task<(MonsterBody Wolf, Player.Player Player)> QaDuelSetup(World.World world, CombatDirector combat, string cls, float distance, bool spawnWolf = true)
    {
        GameManager.Instance.NewGame("Bot", cls);
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        combat.PlayerCombat.State.GrantInvulnerability(60);
        if (!spawnWolf)
        {
            return (null!, player);
        }

        var wolf = combat.SpawnMonsterNow("wolf", player.GlobalPosition + new Vector2(distance, 0), chase: false, invasion: false).Body;
        wolf.SpawnT = 0;
        wolf.Face = -1;
        wolf.Stun = 5;
        return (wolf, player);
    }

    /// <summary>Estados com aviso desenhado no chão (<c>CombatDirector.DrawGround</c>).</summary>
    private static bool QaGroundTelegraph(MonsterBody m) => m.State switch
    {
        MonsterState.Wind => true,
        MonsterState.Bite => m.T < 0.65,
        MonsterState.Tide => m.T < 0.9,
        MonsterState.Swipe => m.T < 0.45,
        MonsterState.Chain => m.T < 0.7,
        MonsterState.Slam => m.T < 0.75,
        MonsterState.Sweep => m.T < 0.7,
        MonsterState.Charge => m.T < 0.6,
        _ => false,
    };

    private static (double Mean, double P95, double Max) QaStats(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return (values.Average(), sorted[(int)(sorted.Count * 0.95)], sorted[^1]);
    }
}
