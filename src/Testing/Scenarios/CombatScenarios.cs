using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.World;
using Vespera.Enemies;
using Vespera.Items;
using Vespera.World.Dungeon;

namespace Vespera.Testing;

/// <summary>
/// Cenários do combate (frente B1) que JOGAM: um bot simples mira (analógico direito) no monstro mais próximo, anda até
/// o alcance da arma e ataca com as ações reais. Cobre entrar numa Brecha, matar uma onda com cada arma, esquivar com
/// i-frames, quebrar parte, coletar drops, matar o chefe (HP do chefe reduzido só no teste), morrer e recuperar a
/// Lanterna, fugir, invasão, bonecos, Ecos/Supremos e orçamento de alocação com 15 monstros.
/// A Interface ainda não existe nesta frente: <see cref="InterfaceStub"/> responde às escolhas e à morte (só no smoke).
/// </summary>
public partial class SmokeTest
{
    private static readonly string[] AllClasses = ["sword", "bow", "staff", "hammer", "dag"];

    [SmokeScenario("combate: entrar numa Brecha e matar a 1ª onda com cada arma", order: 60)]
    private async Task KillAWaveWithEachWeapon()
    {
        var (world, combat) = await CombatSetup();
        foreach (var cls in AllClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            Require(FindPlayer()!.ClassId == cls, $"visual da classe deveria ser {cls}");

            var started = new List<(string, int)>();
            void OnStarted(string theme, int rank) => started.Add((theme, rank));
            EventBus.Instance.BrechaStarted += OnStarted;
            try
            {
                await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
            }
            finally
            {
                EventBus.Instance.BrechaStarted -= OnStarted;
            }

            Require(started.Count == 1 && started[0] == ("fish", 0), $"BrechaStarted: {started.Count}");
            var run = combat.Brecha.Run ?? throw new SmokeFailure("sem incursão depois de BrechaStartRequested");
            Require(run.Easy && run.Waves.Count == 2 && run.Waves[0].Count == 2, "primeira Brecha da vida: 2 ondas de 2 e 3");
            await WaitUntil(() => run.Wave == 1 && combat.AliveMonsters > 0, 8, "1ª onda nascer");
            var first = run.Waves[0].Count;
            var t0 = Time.GetTicksMsec();
            await Bot(combat, () => run.Kills >= first, 40, $"matar a 1ª onda com {cls}", shotAt: $"combate-{cls}");
            Log.Info($"{cls}: 1ª onda ({string.Join(", ", run.Waves[0])}) em {(Time.GetTicksMsec() - t0) / 1000.0:0.0}s");
            Require(run.Kills >= first, $"{cls}: {run.Kills}/{first} mortes");
            await FleeRun(world, combat);
        }
    }

    [SmokeScenario("combate: esquiva com i-frames atravessa o bote do lobo", order: 61)]
    private async Task DodgeThroughLunge()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var wolf = combat.SpawnMonsterNow("wolf", player.GlobalPosition + new Vector2(30, 0), chase: true, invasion: false).Body;
        wolf.SpawnT = 0;
        wolf.Cd = 0;
        var hp = GameManager.Instance.CurrentHp;
        await WaitUntil(() => wolf.State == MonsterState.Lunge, 3, "lobo dar o bote");
        Input.ActionPress(InputActions.MoveRight);
        Input.ActionPress(InputActions.Dodge);
        await Frames(2); // o sinal physics_frame vem antes dos _PhysicsProcess do frame
        Require(combat.PlayerCombat.State.IsDashing, "esquiva não começou");
        Require(combat.PlayerCombat.State.IsInvulnerable, "esquiva deveria dar i-frames");
        await Shot("combate-esquiva");
        var crossed = false;
        for (var i = 0; i < 40 && wolf.State == MonsterState.Lunge; i++)
        {
            crossed |= wolf.Done; // o bote "acertou" (done) enquanto o jogador esquivava
            if (i == 3)
            {
                Input.ActionRelease(InputActions.Dodge);
            }

            await NextPhysicsFrame();
        }

        Input.ActionRelease(InputActions.Dodge);
        Input.ActionRelease(InputActions.MoveRight);
        Require(crossed, "o bote deveria ter cruzado o jogador durante a esquiva");
        Require(GameManager.Instance.CurrentHp == hp, $"a esquiva não protegeu: HP {hp} → {GameManager.Instance.CurrentHp}");
        Require(combat.PlayerCombat.State.Energy < 100, "esquiva deveria gastar energia");

        // Sem esquiva o mesmo bote fere e dá 0,75 s de i-frames.
        wolf.SetState(MonsterState.Chase);
        wolf.X = player.GlobalPosition.X + 30;
        wolf.Y = player.GlobalPosition.Y;
        wolf.Cd = 0;
        await WaitUntil(() => GameManager.Instance.CurrentHp < hp, 4, "lobo ferir sem esquiva");
        Require(combat.PlayerCombat.State.Invulnerable > 0.6, "ferido deveria ganhar i-frames de 0,75 s");
    }

    [SmokeScenario("combate: martelo quebra a mandíbula e os drops vão para a bolsa", order: 62)]
    private async Task BreakPartAndCollect()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "hammer");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 1, theme: "fish", rank: 0);
        var run = combat.Brecha.Run!;
        var player = FindPlayer()!;
        var p = player.GlobalPosition;
        var wolf = combat.SpawnMonsterNow("wolf", p + new Vector2(24, 0), chase: false, invasion: false).Body;
        wolf.SpawnT = 0;
        wolf.Face = -1; // olhando para o jogador: o golpe pega a frente (Mandíbula)
        wolf.Stun = 3;
        var broken = new List<string>();
        void OnBroken(string monster, string part, string name, bool boss) => broken.Add(part);
        EventBus.Instance.MonsterPartBroken += OnBroken;
        try
        {
            SetAim(Vector2.Right);
            await Frames(2);
            await Tap(InputActions.Attack);
            await WaitUntil(() => wolf.IsPartBroken(0), 2, "martelo quebrar a Mandíbula (16 HP × parte 2,2)");
        }
        finally
        {
            EventBus.Instance.MonsterPartBroken -= OnBroken;
            SetAim(Vector2.Zero);
        }

        Require(broken.Count == 1 && broken[0] == "jaw", $"MonsterPartBroken: [{string.Join(",", broken)}]");
        Require(run.PartsBroken == 1, "a incursão deveria contar a parte");
        await Shot("combate-parte-quebrada");
        await WaitUntil(() => run.Bag.Materials.GetValueOrDefault("presa") >= 1, 3, "Presa Rubra entrar na bolsa pelo ímã");
        Require(GameManager.Instance.State.MaterialCount("presa") == 0, "dentro da Brecha o material vai para a bolsa, não para o save");

        wolf.Stun = 0;
        await Bot(combat, () => wolf.Dead, 10, "matar o lobo");
        await WaitUntil(() => run.Bag.Lumes > 0, 4, "Lumes do lobo coletados");
        await FleeRun(world, combat);
        Require(GameManager.Instance.State.MaterialCount("presa") >= 1 && GameManager.Instance.Lumes > 60, "fugir leva a bolsa para o save");
    }

    [SmokeScenario("combate: ondas, porta, Núcleo, chefe, recompensa e portal de saída", order: 63)]
    private async Task FullBrechaWithBoss()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        var state = GameManager.Instance.State;
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
        var run = combat.Brecha.Run!;
        var waves = new List<int>();
        void OnWave(int w, int total) => waves.Add(w);
        EventBus.Instance.WaveStarted += OnWave;
        try
        {
            await Bot(combat, () => world.DoorOpen, 90, "limpar as 2 ondas", shotAt: "combate-ondas");
        }
        finally
        {
            EventBus.Instance.WaveStarted -= OnWave;
        }

        Require(waves.Count == 2 && waves[0] == 1 && waves[1] == 2, $"WaveStarted: [{string.Join(",", waves)}]");
        // INT-D4: sala com layout desenhado (frente de mapas) usa os quebráveis e baús do layout, montados pelo motor.
        var designedProps = world.Dressing.Breakables.Count + world.Dressing.Chests.Count;
        Require(world.Layout.Props.Count > 0 ? designedProps >= world.Layout.Props.Count && combat.Props.Count == 0 : combat.Props.Count >= 9,
            $"sala do tema: {combat.Props.Count} props sorteados, {designedProps} do layout ({world.Layout.Props.Count} quebráveis no layout)");
        await Shot("combate-porta-aberta");

        // Porta → Câmara do Núcleo
        var door = world.Map.Door!.Value;
        FindPlayer()!.Teleport(new Vector2(world.Map.Spawn.X, world.Map.Spawn.Y)); // do spawn a porta é alcançável (cenário do mundo)
        await NextPhysicsFrame();
        await TravelInto(world, door, () => world.MapId == World.World.CoreMapId);
        await WaitMapSettled(world);
        Require(run.Room == BrechaRooms.Core, "deveria estar no Núcleo");

        var bossSpawned = new List<string>();
        var bossHp = new List<int>();
        void OnBoss(string name, int max) => bossSpawned.Add(name);
        void OnBossHp(int hp, int max) => bossHp.Add(hp);
        var ended = new List<bool>();
        void OnEnded(string theme, bool cleared) => ended.Add(cleared);
        EventBus.Instance.BossSpawned += OnBoss;
        EventBus.Instance.BossHealthChanged += OnBossHp;
        EventBus.Instance.BrechaEnded += OnEnded;
        try
        {
            await WaitUntil(() => bossSpawned.Count == 1, 5, "chefe nascer (1,8 s)");
            Require(bossSpawned[0] == "O Abissal do Mercado", $"chefe: {bossSpawned[0]}");
            Require(world.Camera.LookAtTarget is Enemy, "câmera deveria olhar para o chefe");
            var boss = FindBoss(combat) ?? throw new SmokeFailure("chefe não está na lista");
            await WaitUntil(() => boss.Body.SpawnT <= 0, 3, "chefe terminar de nascer");
            await Shot("combate-chefe");
            boss.Body.Hp = 40; // atalho de teste: só o HP do chefe, para o bot terminar rápido
            var lumesBefore = state.Lumes;
            await Bot(combat, () => boss.Body.Dead, 60, "derrotar o chefe");
            Require(bossHp.Count > 0 && bossHp[^1] == 0, "BossHealthChanged deveria chegar a 0");
            Require(run.BossDefeated && state.Save.BrechasClosed == 1, "chefe derrotado fecha a Brecha");
            Require(run.Bag.Lumes >= 40, $"bônus do chefe (40 + 25·rk) na bolsa: {run.Bag.Lumes}");
            Require(state.Save.Quests.Counter(Core.Rules.QuestCounters.Bosses) == 1, "missão de chefe contou");

            var portal = await WaitForExitPortal(combat);
            await Shot("combate-portal-saida");
            await InteractWithNode(world, portal.Interaction);
            await WaitUntil(() => world.MapId == World.World.CityMapId, 4, "voltar à Rua da Forja");
            await WaitMapSettled(world);
            Require(ended.Count == 1 && ended[0], "BrechaEnded(cleared: true)");
            Require(state.Lumes >= lumesBefore + 40, $"a bolsa entrou no save: {lumesBefore} → {state.Lumes}");
            Require(combat.Brecha.Run is null, "incursão encerrada");
        }
        finally
        {
            EventBus.Instance.BossSpawned -= OnBoss;
            EventBus.Instance.BossHealthChanged -= OnBossHp;
            EventBus.Instance.BrechaEnded -= OnEnded;
        }
    }

    [SmokeScenario("combate: morrer na Brecha deixa a Lanterna e ela é recuperada", order: 64)]
    private async Task DieAndRecoverLantern()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        var state = GameManager.Instance.State;
        var fullHp = state.MaxHp;
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
        var run = combat.Brecha.Run!;
        run.Bag.Lumes += 17; // bolsa conhecida
        run.Bag.Materials["presa"] = 2;
        using var stub = new InterfaceStub(this);
        var died = 0;
        void OnDied() => died++;
        EventBus.Instance.PlayerDied += OnDied;
        try
        {
            var deathAt = FindPlayer()!.GlobalPosition;
            combat.HurtPlayer(9999, deathAt + new Vector2(10, 0));
            await NextPhysicsFrame();
            Require(died == 1 && combat.PlayerDead, "dano letal deveria matar (PlayerDied)");
            await Shot("combate-morte");
            await WaitUntil(() => world.MapId == "guild", 8, "respawn na Guilda");
            await WaitMapSettled(world);
            Require(!combat.PlayerDead, "deveria ter voltado da morte");
            Require(state.HasLostLantern, "a Lanterna deveria ter ficado na Brecha");
            Require(state.MaxHp == state.LanternRules.PenalizedHp(fullHp), $"HP máximo sem Lanterna: {state.MaxHp}");
            Require(state.Hp == state.MaxHp, "volta com o HP máximo reduzido cheio");
            Require(combat.PlayerCombat.State.Invulnerable > 1, "respawn dá 1,5 s de i-frames");
            Require(state.Save.LostLantern!.Bag.Lumes == 17, "a bolsa ficou na Lanterna");

            // Próxima Brecha do mesmo tema: a Lanterna está lá.
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            var lumes = state.Lumes;
            await EnterBrecha(world, combat, spot: 1, theme: "fish", rank: 0);
            Pickup? lantern = null;
            foreach (var k in combat.Pickups)
            {
                if (k.Kind == PickupKind.MyLantern)
                {
                    lantern = k;
                }
            }

            Require(lantern is not null, "a Lanterna perdida deveria aparecer na Brecha do tema");
            Require(lantern!.Position.DistanceTo(deathAt) < 12, $"Lanterna longe do ponto da morte: {lantern.Position} vs {deathAt}");
            await Shot("combate-lanterna");
            await InteractWithNode(world, lantern.Interaction!);
            await WaitUntil(() => !state.HasLostLantern, 2, "pegar a Lanterna deveria recuperá-la");
            Require(state.MaxHp == fullHp, $"HP máximo voltou: {state.MaxHp}/{fullHp}");
            Require(state.Lumes == lumes + 17 && state.MaterialCount("presa") >= 2, "os itens da Lanterna voltaram");
            await FleeRun(world, combat);
        }
        finally
        {
            EventBus.Instance.PlayerDied -= OnDied;
        }
    }

    [SmokeScenario("combate: fugir pela borda pede escolha e leva a bolsa", order: 65)]
    private async Task FleeThroughBack()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "bow");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, spot: 0, theme: "fish", rank: 0);
        combat.Brecha.Run!.Bag.Lumes += 5;
        var lumes = GameManager.Instance.Lumes;
        using var stub = new InterfaceStub(this);
        var ended = new List<bool>();
        void OnEnded(string theme, bool cleared) => ended.Add(cleared);
        EventBus.Instance.BrechaEnded += OnEnded;
        try
        {
            await TravelInto(world, world.Map.Back!.Value, () => stub.Choices.Count > 0);
            Require(stub.Choices[0] == "flee", $"escolha pedida: {stub.Choices[0]}");
            await WaitUntil(() => world.MapId == World.World.CityMapId, 5, "fuga para a Rua da Forja");
            await WaitMapSettled(world);
        }
        finally
        {
            EventBus.Instance.BrechaEnded -= OnEnded;
        }

        Require(ended.Count == 1 && !ended[0], "BrechaEnded(cleared: false)");
        Require(GameManager.Instance.Lumes == lumes + 5, "fuga leva a bolsa");
        Require(combat.Brecha.Run is null, "incursão encerrada");
    }

    [SmokeScenario("combate: invasão na Rua da Forja é resolvida", order: 66)]
    private async Task InvasionResolves()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "staff");
        var state = GameManager.Instance.State;
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var states = new List<bool>();
        void OnInvasion(bool active) => states.Add(active);
        EventBus.Instance.InvasionStateChanged += OnInvasion;
        try
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionRequested, "fish", 0);
            Require(states.Count == 1 && states[0] && state.Invasion, "InvasionStateChanged(true) e preços de invasão");
            await WaitUntil(() => combat.AliveMonsters == 6, 3, "5 mobs + Alfa nascendo");
            await WaitUntil(() => combat.Hazards.PendingSpawns == 0, 4, "invasão nascer");
            await Shot("combate-invasao");
            var closed = state.Save.BrechasClosed;
            foreach (var e in combat.Enemies)
            {
                if (!e.Body.IsDummy)
                {
                    e.Body.Hp = Math.Min(e.Body.Hp, 12); // atalho de teste: invasão rápida
                }
            }

            await Bot(combat, () => !combat.Brecha.Invasion.Active, 60, "limpar a invasão");
            Require(states.Count == 2 && !states[1] && !state.Invasion, "InvasionStateChanged(false)");
            Require(state.Save.BrechasClosed == closed + 1, "invasão resolvida conta como Brecha fechada");
        }
        finally
        {
            EventBus.Instance.InvasionStateChanged -= OnInvasion;
        }
    }

    [SmokeScenario("combate: bonecos da Guilda mostram dano e contam a missão", order: 67)]
    private async Task TrainingDummies()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "dag");
        world.GoMap("guild", 250, 500, instant: true);
        await WaitMapSettled(world);
        var dummies = 0;
        foreach (var e in combat.Enemies)
        {
            dummies += e.Body.IsDummy ? 1 : 0;
        }

        Require(dummies == 4, $"a Guilda tem 4 bonecos, achei {dummies}");
        var player = FindPlayer()!;
        player.Teleport(new Vector2(155 + 14, 455));
        SetAim(Vector2.Left);
        await Frames(2);
        for (var i = 0; i < 3; i++)
        {
            await Tap(InputActions.Attack);
            await WaitSeconds(0.2);
        }

        SetAim(Vector2.Zero);
        var counter = GameManager.Instance.State.Save.Quests.Counter(Core.Rules.QuestCounters.Dummy);
        Require(counter >= 3, $"acertos no boneco: {counter}");
        Require(combat.Overlay.LiveFloats > 0, "números de dano deveriam aparecer");
        Require(combat.PlayerCombat.State.Essence == 0, "bonecos não dão Essência");
        await Shot("combate-bonecos");
    }

    [SmokeScenario("combate: Ecos, poção, troca de arma e Golpe Supremo", order: 68)]
    private async Task EcosAndUltimate()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "sword");
        var state = GameManager.Instance.State;
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var notes = new List<string>();
        void OnNote(string message, string icon) => notes.Add(message);
        EventBus.Instance.NoteRequested += OnNote;
        try
        {
            await Tap(InputActions.Eco1);
            Require(notes.Count == 1 && notes[0] == "Nenhum Eco. Chefes às vezes deixam um ao morrer.", $"nota sem Eco: {string.Join("|", notes)}");
            await Tap(InputActions.WeaponSlots[1]);
            Require(notes[^1] == "Sua classe é Lâmina. A arma Arco fica bloqueada.", $"troca de arma bloqueada: {notes[^1]}");
            await Tap(InputActions.Potion);
            Require(notes[^1] == "Vida já está cheia.", $"poção com vida cheia: {notes[^1]}");
            state.Damage(50);
            var potions = state.Save.Potions;
            await Tap(InputActions.Potion);
            Require(state.Save.Potions == potions - 1 && state.Hp == 95, $"poção cura 45%: HP {state.Hp}");

            // Eco do Alfa (Investida Rubra) atravessa e fere.
            state.AbsorbEco("uivo");
            combat.PlayerCombat.FillEssence();
            var p = FindPlayer()!.GlobalPosition;
            var wolf = combat.SpawnMonsterNow("wolf", p + new Vector2(40, 0), chase: false, invasion: false).Body;
            wolf.SpawnT = 0;
            wolf.Stun = 5;
            SetAim(Vector2.Right);
            await Frames(2);
            await Tap(InputActions.Eco1);
            Require(combat.PlayerCombat.ChargingEco, "Investida Rubra deveria estar em curso");
            await WaitSeconds(0.5);
            Require(wolf.Hp < wolf.MaxHp, "a Investida deveria ferir o lobo");
            Require(combat.PlayerCombat.State.Essence < 100 - 30, "o Eco gasta Essência");

            // Supremo: começa com 6 s de recarga; depois corta.
            await Tap(InputActions.Ultimate);
            Require(notes[^1].StartsWith("Fenda Rubra recarrega em", StringComparison.Ordinal), $"Supremo em recarga: {notes[^1]}");
            await WaitUntil(() => combat.PlayerCombat.State.UltimateReady, 8, "Supremo carregar (6 s)");
            wolf.X = FindPlayer()!.GlobalPosition.X + 30;
            wolf.Y = FindPlayer()!.GlobalPosition.Y;
            var hpBefore = wolf.Hp;
            await Tap(InputActions.Ultimate);
            Require(combat.PlayerCombat.ActiveUltimate == "sword", "Fenda Rubra deveria estar ativa");
            await Shot("combate-supremo");
            await WaitSeconds(0.6);
            Require(wolf.Dead || wolf.Hp < hpBefore, "Fenda Rubra deveria acertar");
            Require(combat.PlayerCombat.State.UltCooldown > 30, "recarga de 40 s depois do Supremo");
        }
        finally
        {
            EventBus.Instance.NoteRequested -= OnNote;
            SetAim(Vector2.Zero);
        }
    }

    [SmokeScenario("combate: 15 monstros na tela sem alocar por frame", order: 69)]
    private async Task FifteenMonstersBudget()
    {
        var (world, combat) = await CombatSetup();
        GameManager.Instance.NewGame("Bot", "staff");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var p = FindPlayer()!.GlobalPosition;
        string[] types = ["wolf", "fishm", "cnev", "cvel", "mbes", "gsei", "ksin", "gmus", "coss", "kesc", "mesc", "gbro", "ksom", "maut", "wolfE"];
        for (var i = 0; i < types.Length; i++)
        {
            var a = i / (float)types.Length * MathF.Tau;
            var at = world.CorrectToWalkable(p + (new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f) * 110));
            combat.SpawnMonsterNow(types[i], at, chase: true, invasion: false).Body.SpawnT = 0;
        }

        combat.PlayerCombat.State.GrantInvulnerability(60); // atalho de teste: o jogador não morre durante a medição
        await WaitSeconds(1.5); // aquece caminhos de código, caches de texto e pools
        await Shot("combate-15-monstros");
        var frames = 0;
        long physicsUsec = 0;
        double combatMs = 0;
        double combatMax = 0;
        long combatBytes = 0;
        var peak = string.Empty;
        var framesWithAlloc = 0;
        GC.Collect(); // o lixo dos cenários anteriores não entra na medição (uma coleta gen2 custa ~25 ms aqui)
        GC.WaitForPendingFinalizers();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Time.GetTicksUsec();
        while (frames < 180)
        {
            var before = Time.GetTicksUsec();
            await NextPhysicsFrame();
            physicsUsec += (long)(Time.GetTicksUsec() - before);
            combatMs += combat.LastStepMs;
            if (combat.LastStepMs > combatMax)
            {
                combatMax = combat.LastStepMs;
                peak = string.Join(" / ", combat.LastSectionMs);
            }
            combatBytes += combat.LastStepBytes;
            framesWithAlloc += combat.LastStepBytes > 0 ? 1 : 0;
            frames++;
        }

        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var physicsMs = combatMs / frames;
        Log.Info($"15 monstros: combate alocou {combatBytes} B em {framesWithAlloc}/{frames} frames; teste+motor {bytes / 1024.0:0.0} KB ({bytes / frames} B/frame), combate {physicsMs:0.00} ms/frame (pico {combatMax:0.00}: {peak}), frame {physicsUsec / frames / 1000.0:0.00} ms, {combat.Projectiles.Live} projéteis, {combat.Glow.LiveParticles + combat.Fx.LiveParticles} partículas");
        Require(combat.AliveMonsters >= 14, $"os monstros deveriam estar vivos: {combat.AliveMonsters}");
        Require(physicsMs < 4, $"combate acima do orçamento: {physicsMs:0.00} ms/frame (16,6 ms = 60 FPS)");
        Require(combatMax < 8, $"pico de frame do combate: {combatMax:0.00} ms ({peak})");
        Require(combatBytes / frames < 256, $"combate alocando por frame: {combatBytes} B em {frames} frames");
        foreach (var e in combat.Enemies)
        {
            e.Body.Hp = 0;
        }
    }

    [SmokeScenario("combate: lâmina, flecha e orbe acertam objetos do grupo hittable", order: 70)]
    private async Task HitsTheHittableGroup()
    {
        var (world, combat) = await CombatSetup();
        foreach (var (cls, distance) in new[] { ("sword", 16f), ("bow", 60f), ("staff", 60f) })
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var target = new SmokeHittable { Position = player.GlobalPosition + new Vector2(distance, 0) };
            world.Entities.AddChild(target);
            try
            {
                SetAim(Vector2.Right);
                await Frames(2);
                if (cls == "bow")
                {
                    Input.ActionPress(InputActions.Attack);
                    await WaitUntil(() => combat.PlayerCombat.State.Charge >= PlayerCombatState.FullChargeAt, 2, "carregar o arco");
                    Input.ActionRelease(InputActions.Attack);
                }
                else
                {
                    await Tap(InputActions.Attack);
                }

                await WaitUntil(() => target.Hits > 0, 2, $"{cls} acertar o objeto 'hittable'");
                Require(target.LastDamage > 0, $"{cls}: dano {target.LastDamage}");
                Require(target.LastOrigin.X < target.Position.X, $"{cls}: origem do golpe deveria vir do lado do jogador ({target.LastOrigin})");
                Log.Info($"{cls}: objeto 'hittable' levou {target.LastDamage} de ({target.LastOrigin.X:0},{target.LastOrigin.Y:0})");
            }
            finally
            {
                SetAim(Vector2.Zero);
                Input.ActionRelease(InputActions.Attack);
                target.QueueFree();
            }
        }
    }

    // --- utilidades ---

    private async Task<(World.World World, CombatDirector Combat)> CombatSetup()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var combat = CombatDirector.Instance ?? throw new SmokeFailure("sem CombatDirector no World");
        await WaitMapSettled(world);
        ReleaseAllActions();
        if (combat.Brecha.Run is not null)
        {
            await FleeRun(world, combat); // limpa a incursão de um cenário anterior que falhou
        }
        return (world, combat);
    }

    /// <summary>Faz o papel da Cidade (PortalOpened) e da Interface (BrechaStartRequested) e espera a sala do tema.</summary>
    private async Task EnterBrecha(World.World world, CombatDirector combat, int spot, string theme, int rank)
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.PortalOpened, spot, theme, rank);
        EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStartRequested, spot);
        var map = GameManager.Db.Themes[theme].Map;
        await WaitUntil(() => world.MapId == map, 5, $"carregar {map}");
        await WaitMapSettled(world);
        Require(combat.Brecha.Run is not null && combat.Brecha.InBrecha, "incursão ativa");
    }

    /// <summary>Encerra a incursão como "Fugir para Lumeira" (escolha 0).</summary>
    private async Task FleeRun(World.World world, CombatDirector combat)
    {
        if (combat.Brecha.Run is null)
        {
            return;
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceMade, "flee", 0);
        await WaitUntil(() => world.MapId == World.World.CityMapId, 5, "fuga");
        await WaitMapSettled(world);
    }

    /// <summary>
    /// Bot: mira (analógico direito) no monstro mais próximo, fica no alcance da arma e ataca com a ação real. Se o HP
    /// cair abaixo de 35%, enche a vida (atalho de teste) para o cenário medir a arma, não a sorte.
    /// </summary>
    private async Task Bot(CombatDirector combat, Func<bool> done, double timeoutSeconds, string what, string? shotAt = null)
    {
        var player = FindPlayer()!;
        var state = GameManager.Instance.State;
        var weapon = GameManager.Db.Weapons[state.Save.CurrentWeapon];
        var range = weapon.Kind switch
        {
            Core.Data.WeaponKind.Bow => 90f,
            Core.Data.WeaponKind.Orb => 70f,
            Core.Data.WeaponKind.Smash => 24f,
            _ => (float)(weapon.Range ?? 24) - 6,
        };
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        var frame = 0;
        var shotTaken = shotAt is null;
        // VESPERA_BOT_TRACE=N: log + print a cada N frames (diagnóstico do bot; 1 = a cada 120).
        var traceEvery = int.TryParse(OS.GetEnvironment("VESPERA_BOT_TRACE"), out var every) ? (every == 1 ? 120 : every) : 0;
        IReadOnlyList<MapPoint>? path = null;
        var pathAge = 0;
        try
        {
            while (!done())
            {
                if (Time.GetTicksMsec() >= deadline)
                {
                    var t = NearestFoe(combat, player.GlobalPosition);
                    throw new SmokeFailure($"timeout de {timeoutSeconds}s: {what} (jogador {player.GlobalPosition}, alvo {(t is null ? "nenhum" : $"{t.Def.Id} em ({t.X:0},{t.Y:0}) {t.State} hp {t.Hp:0}")}, vivos {combat.AliveMonsters})");
                }
                frame++;
                if (state.Hp < state.MaxHp * 0.35)
                {
                    state.RestoreFullHealth();
                }

                var target = NearestFoe(combat, player.GlobalPosition);
                if (target is null)
                {
                    PressDirection(Vector2.Zero);
                    SetAim(Vector2.Zero);
                    Input.ActionRelease(InputActions.Attack);
                    await NextPhysicsFrame();
                    continue;
                }

                var p = player.GlobalPosition;
                var to = new Vector2((float)target.X, (float)target.Y) - p;
                var d = to.Length() - (float)target.Radius;
                var aim = new Vector2((float)target.X, (float)(target.Y - (target.HitHeight * 0.5))) - (p - new Vector2(0, 12));
                SetAim(aim.Normalized());
                var ranged = weapon.Kind is Core.Data.WeaponKind.Bow or Core.Data.WeaponKind.Orb;
                var clear = ClearShot(combat, p - new Vector2(0, 12), new Vector2((float)target.X, (float)(target.Y - (target.HitHeight * 0.5))));
                if (!clear && d > 150)
                {
                    PressDirection(Vector2.Zero); // longe e atrás de obstáculo: espera o monstro vir (ele contorna, B1-D43)
                }
                else if (d > range || (ranged && !clear))
                {
                    var near = d > range ? range : 16; // sem linha de tiro: chega perto pelo caminho
                    PressDirection(BotStep(combat, p, new Vector2((float)target.X, (float)target.Y), near, ref path, ref pathAge));
                }
                else if (d < range * 0.5f && ranged)
                {
                    PressDirection(-to.Normalized());
                }
                else
                {
                    PressDirection(Vector2.Zero);
                }

                if (weapon.Kind == Core.Data.WeaponKind.Bow)
                {
                    // segura até a carga cheia e solta
                    if (combat.PlayerCombat.State.Charge >= PlayerCombatState.FullChargeAt && d < range * 1.6f)
                    {
                        Input.ActionRelease(InputActions.Attack);
                    }
                    else
                    {
                        Input.ActionPress(InputActions.Attack);
                    }
                }
                else if (d < range + 10 && frame % 2 == 0)
                {
                    Input.ActionPress(InputActions.Attack);
                }
                else
                {
                    Input.ActionRelease(InputActions.Attack);
                }

                if (shotAt is not null && traceEvery > 0 && frame % traceEvery == 0)
                {
                    Log.Info($"bot {frame}: p={p} alvo {target.Def.Id} ({target.X:0},{target.Y:0}) {target.State} hp {target.Hp:0} d={d:0} {combat.DescribeChase(target)}");
                    await Shot($"{shotAt}-{frame}");
                }

                if (!shotTaken && d < range + 10)
                {
                    shotTaken = true;
                    await Shot(shotAt!);
                }

                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
            SetAim(Vector2.Zero);
            Input.ActionRelease(InputActions.Attack);
        }
    }

    /// <summary>Direção do bot: reta se livre; senão segue um caminho (BFS do Core) recalculado a cada 30 frames.</summary>
    private static Vector2 BotStep(CombatDirector combat, Vector2 from, Vector2 target, float range, ref IReadOnlyList<MapPoint>? path, ref int pathAge)
    {
        var area = combat.World.WalkArea;
        var clear = true;
        var steps = (int)(from.DistanceTo(target) / 6);
        for (var i = 1; i <= steps && clear; i++)
        {
            var q = from.Lerp(target, i / (float)(steps + 1));
            clear = area.IsWalkable(q.X, q.Y);
        }

        if (clear)
        {
            path = null;
            return (target - from).Normalized();
        }

        if (path is null || ++pathAge > 30)
        {
            pathAge = 0;
            path = WalkPathfinder.FindPathNear(area, new MapPoint(from.X, from.Y), new MapPoint(target.X, target.Y), MathF.Max(8, range));
        }

        if (path is null)
        {
            return (target - from).Normalized();
        }

        foreach (var wp in path)
        {
            var w = new Vector2(wp.X, wp.Y);
            if (w.DistanceTo(from) > 6)
            {
                return (w - from).Normalized();
            }
        }

        return (target - from).Normalized();
    }

    /// <summary>Projétil chega: a reta (com o teste <c>y + 10</c> dos projéteis) só passa por chão andável.</summary>
    private static bool ClearShot(CombatDirector combat, Vector2 from, Vector2 to)
    {
        var steps = (int)(from.DistanceTo(to) / 5);
        for (var i = 1; i < steps; i++)
        {
            var q = from.Lerp(to, i / (float)steps);
            if (!combat.IsWalkable(q.X, q.Y + 10))
            {
                return false;
            }
        }

        return true;
    }

    private static MonsterBody? NearestFoe(CombatDirector combat, Vector2 from)
    {
        MonsterBody? best = null;
        var bestD = float.MaxValue;
        foreach (var e in combat.Enemies)
        {
            var m = e.Body;
            if (m.IsDummy || !m.IsTargetable)
            {
                continue;
            }

            var d = from.DistanceSquaredTo(new Vector2((float)m.X, (float)m.Y));
            if (d < bestD)
            {
                bestD = d;
                best = m;
            }
        }

        return best;
    }

    private static Enemy? FindBoss(CombatDirector combat)
    {
        foreach (var e in combat.Enemies)
        {
            if (e.Body.IsBoss)
            {
                return e;
            }
        }

        return null;
    }

    private async Task<ExitPortal> WaitForExitPortal(CombatDirector combat)
    {
        ExitPortal? portal = null;
        await WaitUntil(() => (portal = Find<ExitPortal>()) is not null, 4, "portal de saída (1,4 s depois do chefe)");
        return portal!;
    }

    /// <summary>Mira pelo analógico direito (gamepad), como o jogador faria.</summary>
    private static void SetAim(Vector2 d)
    {
        SetAxis(InputActions.AimRight, d.X);
        SetAxis(InputActions.AimLeft, -d.X);
        SetAxis(InputActions.AimDown, d.Y);
        SetAxis(InputActions.AimUp, -d.Y);
    }

    /// <summary>Coloca o jogador ao lado do interagível, espera o foco e aperta interagir (evento real).</summary>
    private async Task InteractWithNode(World.World world, Vespera.World.Interactable it)
    {
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(it.GlobalPosition));
        await WaitUntil(() => player.FocusedInteractable == it, 2, $"foco em '{it.Prompt}'");
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
        await NextPhysicsFrame();
    }

    /// <summary>Faz o papel da Interface no smoke: responde "Fugir" a toda escolha e volta da morte depois de 2,4 s.</summary>
    private sealed class InterfaceStub : IDisposable
    {
        private readonly SmokeTest _test;

        public InterfaceStub(SmokeTest test)
        {
            _test = test;
            EventBus.Instance.ChoiceRequested += OnChoice;
            EventBus.Instance.PlayerDied += OnDied;
        }

        public List<string> Choices { get; } = [];

        public void Dispose()
        {
            EventBus.Instance.ChoiceRequested -= OnChoice;
            EventBus.Instance.PlayerDied -= OnDied;
        }

        private void OnChoice(string requestId, string speaker, string text, string[] options)
        {
            Choices.Add(requestId);
            EventBus.Instance.CallDeferred(GodotObject.MethodName.EmitSignal, EventBus.SignalName.ChoiceMade, requestId, 0);
        }

        private async void OnDied()
        {
            await _test.WaitSeconds(2.4);
            EventBus.Instance.EmitSignal(EventBus.SignalName.RespawnRequested);
        }
    }
}

/// <summary>Objeto do mundo de mentira para o smoke: só o contrato do grupo <c>hittable</c> (<c>Hit(int, Vector2)</c>).</summary>
internal sealed partial class SmokeHittable : Node2D
{
    public int Hits { get; private set; }

    public int LastDamage { get; private set; }

    public Vector2 LastOrigin { get; private set; }

    public override void _Ready() => AddToGroup(CombatDirector.HittableGroup);

    public void Hit(int damage, Vector2 origin)
    {
        Hits++;
        LastDamage = damage;
        LastOrigin = origin;
    }
}
