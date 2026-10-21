using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// Cidade viva (frente B2): relógio e fases do dia (com aceleração só no teste), pausa congelando o relógio,
/// escuridão e lâmpadas, portal abrindo com a animação, Brecha ignorada rompendo em <c>InvasionRequested</c>,
/// NPCs andando sem atravessar parede e respondendo, caçadores com balão de chat. Cada cenário liga a simulação
/// da cidade e desliga no fim (ela começa desligada no smoke para não mexer nos cenários das outras frentes).
/// </summary>
public partial class SmokeTest
{
    private const double Clock1630 = 16 * 60 + 30;

    [SmokeScenario("cidade: relógio, fases do dia, escuridão e pausa", order: 60)]
    private async Task CityClockAndPhases()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), Clock1630);
        var state = GameManager.Instance.State;
        var phases = new List<int>();
        var days = new List<int>();
        var toasts = new List<string>();
        void OnPhase(int p) => phases.Add(p);
        void OnDay(int d) => days.Add(d);
        void OnToast(string t) => toasts.Add(t);
        var bus = EventBus.Instance;
        bus.DayPhaseChanged += OnPhase;
        bus.DayChanged += OnDay;
        bus.ToastRequested += OnToast;
        try
        {
            // 1 min de jogo por segundo real.
            var before = state.Save.ClockMinutes;
            await WaitSeconds(1.0);
            var advanced = state.Save.ClockMinutes - before;
            Require(advanced > 0.7 && advanced < 1.4, $"relógio deveria andar ~1 min por segundo, andou {advanced:0.00}");
            Require(city.Phase == DayPhase.Vespera, $"16:30 é véspera, fase {city.Phase}");
            Require(AudioManager.Instance.CurrentMusic == AudioManager.VesperaMusic, $"na véspera a cidade toca 'vespera', toca {AudioManager.Instance.CurrentMusic}");
            Require(city.Lighting.CurrentDarkness < 0.12f, $"16:30 na Rua da Forja: escuridão {city.Lighting.CurrentDarkness:0.000} (v2: 0,096)");
            Require(city.Lighting.LitStreetLamps == 0, $"antes das 17:00 as lâmpadas da rua estão apagadas ({city.Lighting.LitStreetLamps} acesas)");
            Require(city.Lighting.CurrentLightCount >= 1, "a lanterna do jogador sempre fura a escuridão");
            await Shot("cidade-dia");

            // Pausa e telas que bloqueiam o jogo congelam o relógio.
            bus.EmitSignal(EventBus.SignalName.PauseChanged, true);
            var paused = state.Save.ClockMinutes;
            await WaitSeconds(0.6);
            Require(Math.Abs(state.Save.ClockMinutes - paused) < 1e-9, $"pausa deveria congelar o relógio ({paused} → {state.Save.ClockMinutes})");
            bus.EmitSignal(EventBus.SignalName.PauseChanged, false);
            GameManager.Instance.BlockGameplay("smoke-dialogo");
            var blocked = state.Save.ClockMinutes;
            await WaitSeconds(0.6);
            Require(Math.Abs(state.Save.ClockMinutes - blocked) < 1e-9, "diálogo/menu (BlockGameplay) deveria congelar o relógio");
            GameManager.Instance.UnblockGameplay("smoke-dialogo");
            await WaitSeconds(0.3);
            Require(state.Save.ClockMinutes > blocked, "relógio deveria voltar a andar ao fechar a tela");

            // Acelera (só no teste) até a noite.
            city.TimeScale = 300f;
            await WaitUntil(() => state.Save.ClockMinutes >= 18 * 60, 10, "18:00");
            city.TimeScale = 0f;
            await Frames(3);
            Require(city.Lighting.LitStreetLamps > 30, $"às 18:00 as lâmpadas da rua deveriam estar acesas ({city.Lighting.LitStreetLamps})");
            await Shot("cidade-vespera");
            city.TimeScale = 300f;
            await WaitUntil(() => phases.Contains((int)DayPhase.Night), 10, "fase noite às 19:00");
            await WaitUntil(() => state.Save.ClockMinutes >= 23 * 60, 10, "23:00");
            city.TimeScale = 0f;
            await Frames(3);
            Require(Math.Abs(city.Lighting.CurrentDarkness - 0.372f) < 0.01f, $"noite fechada na Rua da Forja deveria ser 0,372, é {city.Lighting.CurrentDarkness:0.000}");
            Require(AudioManager.Instance.CurrentMusic == AudioManager.DefaultLocationMusic, $"à noite a cidade volta a city_day, toca {AudioManager.Instance.CurrentMusic}");
            await Shot("cidade-noite");

            world.GoMap("guild", 250, 330, instant: true);
            await WaitMapSettled(world);
            Require(Math.Abs(city.Lighting.CurrentDarkness - 0.481f) < 0.01f, $"Guilda à noite: 0,481, é {city.Lighting.CurrentDarkness:0.000}");
            Require(city.Lighting.Weather.ActiveSeason is null, "sem clima dentro da Guilda");
            await Shot("cidade-noite-guilda");

            var day = state.Save.Day;
            city.TimeScale = 600f;
            await WaitUntil(() => phases.Contains((int)DayPhase.Dawn), 15, "fase aurora às 05:00");
            await WaitUntil(() => days.Count > 0, 15, "DayChanged às 06:00");
            city.TimeScale = 0f;
            Require(state.Save.Day == day + 1 && days[^1] == day + 1, $"dia deveria virar para {day + 1}, está em {state.Save.Day}");
            Require(state.Save.ClockMinutes < 16 * 60 + 15, $"dia novo começa às 16:00, relógio {state.Save.ClockMinutes}");
            Require(phases[^1] == (int)DayPhase.Vespera, "dia novo começa na véspera");
            var expected = $"Dia {day + 1} · {state.Season.Name}\nA noite passou sem você perceber.";
            Require(toasts.Contains(expected), $"toast do dia novo: esperava \"{expected}\", veio [{string.Join(" | ", toasts)}]");
            Require(phases.SequenceEqual(new[] { (int)DayPhase.Night, (int)DayPhase.Dawn, (int)DayPhase.Vespera }), $"ordem das fases: [{string.Join(",", phases)}]");

            // Dormir na Estalagem: a Interface chama GameState.NewDay; a cidade reage ao DayChanged.
            state.NewDay();
            Require(toasts[^1].EndsWith("Você dorme. Lá fora, a cidade aguenta.", StringComparison.Ordinal), $"toast de dormir: {toasts[^1]}");
        }
        finally
        {
            bus.DayPhaseChanged -= OnPhase;
            bus.DayChanged -= OnDay;
            bus.ToastRequested -= OnToast;
            StopCity(city);
        }
    }

    [SmokeScenario("cidade: véspera abre o portal com animação e o portal fecha", order: 61)]
    private async Task CityPortalOpens()
    {
        var (world, city) = await StartCity("forge", new Vector2(440, 560), 20 * 60, firstBrecha: true);
        var opened = new List<(int Spot, string Theme, int Rank)>();
        var closed = new List<int>();
        var toasts = new List<string>();
        var shakes = new List<float>();
        void OnOpened(int s, string t, int r) => opened.Add((s, t, r));
        void OnClosed(int s) => closed.Add(s);
        void OnToast(string t) => toasts.Add(t);
        void OnShake(float a) => shakes.Add(a);
        var bus = EventBus.Instance;
        bus.PortalOpened += OnOpened;
        bus.PortalClosed += OnClosed;
        bus.ToastRequested += OnToast;
        bus.CameraShakeRequested += OnShake;
        try
        {
            Require(world.GetInteractable("portal0") is { Enabled: false }, "sem Brecha o portal0 começa desligado");
            Require(city.Brechas.NextOpenIn > 10, $"primeira Brecha de um jogo novo abre em 16 s, falta {city.Brechas.NextOpenIn:0.0}");
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => opened.Count > 0, 3, "PortalOpened");
            // A primeira é sempre o Mercado, 300 s, sem bônus: rank = max(minRk 0, Selo do jogador) (cenários anteriores dão XP).
            var rankIndex = GameManager.Instance.State.RankIndex;
            var rankId = GameManager.Db.Ranks[rankIndex].Id;
            Require(opened[0] == (0, "fish", rankIndex), $"primeira Brecha: spot 0, Mercado, rank {rankId}; veio {opened[0]}");
            Require(city.Brechas.OpenAt(0) is { MaxTime: 300 }, "primeira Brecha dura 300 s");
            Require(toasts.Any(t => t.StartsWith("Uma Brecha se abriu — Mercado de Peixes\n", StringComparison.Ordinal) && t.Contains($"· Rank {rankId} · Portal na Rua da Forja", StringComparison.Ordinal)),
                $"toast da abertura (v2): [{string.Join(" | ", toasts)}]");
            Require(shakes.Count > 0 && shakes[0] > 0 && shakes[0] <= 4, $"tremor leve ao abrir: [{string.Join(",", shakes)}]");
            Require(GameManager.Instance.State.Save.FirstBrechaDone, "firstDone deveria ir para o save (D6)");
            var portal = world.GetInteractable("portal0")!;
            Require(portal.Enabled && portal.Prompt == "Brecha · Mercado de Peixes", $"portal0: ligado={portal.Enabled}, rótulo \"{portal.Prompt}\"");

            var visual = Find<PortalVisual>() ?? throw new SmokeFailure("PortalVisual não apareceu na Rua da Forja");
            Require(visual.IsOpening, "o portal deveria começar tocando a abertura");
            var frames = new List<int>();
            var shotOpening = false;
            while (visual.IsOpening)
            {
                if (frames.Count == 0 || frames[^1] != visual.Frame)
                {
                    frames.Add(visual.Frame);
                }

                if (!shotOpening && visual.Age > 0.45f)
                {
                    shotOpening = true;
                    await Shot("portal-abrindo");
                }

                await NextPhysicsFrame();
            }

            Require(frames.Count >= 4 && frames.SequenceEqual(frames.OrderBy(f => f)), $"abertura deveria avançar pelos quadros 0→5: [{string.Join(",", frames)}]");
            Require(visual.Age < 1.6f, $"abertura deveria durar ~1 s, durou {visual.Age:0.00}");
            var loop = new HashSet<int>();
            for (var i = 0; i < 60; i++)
            {
                loop.Add(visual.Frame);
                await NextPhysicsFrame();
            }

            Require(loop.SetEquals(new[] { 3, 4 }), $"depois de aberto o portal alterna os quadros 3/4: [{string.Join(",", loop)}]");
            Require(LightEmitter.Active.Any(e => e.Kind == "m" && e.Radius >= 89f), "portal aberto deveria acender a luz magenta 90");
            await Shot("portal-aberto");

            var (kind, id) = await InteractWith(world, "portal0");
            Require(kind == "portalDialog" && id == "portal0", $"portal emitiu ({kind}, {id})");

            // Dentro da Brecha o timer para; fugir volta a contar; chefe derrotado fecha o portal.
            var b = city.Brechas.OpenAt(0)!;
            bus.EmitSignal(EventBus.SignalName.BrechaStartRequested, 0);
            var t0 = b.TimeLeft;
            await WaitSeconds(0.5);
            Require(Math.Abs(b.TimeLeft - t0) < 1e-9, "com o jogador dentro, o timer da Brecha não corre");
            bus.EmitSignal(EventBus.SignalName.BrechaEnded, "fish", false);
            await WaitSeconds(0.3);
            Require(b.TimeLeft < t0 && b.IsOpen, "depois da fuga a Brecha continua aberta e o timer volta");
            bus.EmitSignal(EventBus.SignalName.BrechaStartRequested, 0);
            bus.EmitSignal(EventBus.SignalName.BrechaEnded, "fish", true);
            Require(closed.Contains(0) && b.Status == BrechaStatus.Closed, "Brecha limpa deveria fechar e emitir PortalClosed(0)");
            Require(!portal.Enabled, "portal0 deveria desligar ao fechar");
            await WaitSeconds(0.6);
            Require(Find<PortalVisual>() is null, "o portal fechado deveria sumir do mapa");
        }
        finally
        {
            bus.PortalOpened -= OnOpened;
            bus.PortalClosed -= OnClosed;
            bus.ToastRequested -= OnToast;
            bus.CameraShakeRequested -= OnShake;
            StopCity(city);
        }
    }

    [SmokeScenario("cidade: Brecha ignorada rompe e pede a invasão", order: 62)]
    private async Task CityRupture()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 21 * 60);
        var invasions = new List<(string Theme, int Rank)>();
        var toasts = new List<string>();
        var shakes = new List<float>();
        void OnInvasion(string t, int r) => invasions.Add((t, r));
        void OnToast(string t) => toasts.Add(t);
        void OnShake(float a) => shakes.Add(a);
        var bus = EventBus.Instance;
        bus.InvasionRequested += OnInvasion;
        bus.ToastRequested += OnToast;
        bus.CameraShakeRequested += OnShake;
        try
        {
            // Acelera os timers: as Brechas abrem, algumas a Casa fecha, a que ninguém fecha rompe.
            city.TimeScale = 150f;
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => invasions.Count > 0, 30, "InvasionRequested (ruptura)");
            city.TimeScale = 0f;
            var ruptured = city.Brechas.Schedule.Ruptured ?? throw new SmokeFailure("nenhuma Brecha ficou rompida");
            Require(invasions[0] == (ruptured.ThemeId, ruptured.Rank), $"InvasionRequested{invasions[0]} deveria ser da Brecha rompida ({ruptured.ThemeId}, {ruptured.Rank})");
            Require(toasts.Contains($"A Brecha do {ruptured.Bairro} ROMPEU\nMonstros invadem a Rua da Forja. A forja fechou e os preços subiram."), "toast da ruptura (v2)");
            Require(shakes.Contains(BrechaDirector.RuptureShake), "ruptura treme a câmera (8)");
            Require(city.Brechas.OpenAt(ruptured.Spot) is null && world.GetInteractable($"portal{ruptured.Spot}") is { Enabled: false }, "portal rompido deveria desligar");
            Require(city.Brechas.InvasionActive, "invasão ativa depois da ruptura");
            await Frames(5);
            await Shot("ruptura");

            // O Combate resolve a invasão: a Brecha rompida fecha.
            bus.EmitSignal(EventBus.SignalName.InvasionStateChanged, true);
            bus.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
            Require(ruptured.Status == BrechaStatus.Closed && !city.Brechas.InvasionActive, "invasão resolvida deveria fechar a Brecha rompida");
        }
        finally
        {
            bus.InvasionRequested -= OnInvasion;
            bus.ToastRequested -= OnToast;
            bus.CameraShakeRequested -= OnShake;
            StopCity(city);
        }
    }

    [SmokeScenario("cidade: NPCs com rotina andam sem atravessar parede e conversam", order: 63)]
    private async Task CityNpcs()
    {
        var (world, city) = await StartCity("forge", new Vector2(800, 520), 18 * 60 + 30);
        var dialogues = new List<(string Speaker, string[] Lines)>();
        var chats = new List<string>();
        void OnDialogue(string s, string[] l) => dialogues.Add((s, l));
        void OnChat(string w, string r, string t) => chats.Add(t);
        var bus = EventBus.Instance;
        bus.DialogueRequested += OnDialogue;
        bus.ChatMessage += OnChat;
        try
        {
            var town = city.Townsfolk;
            await WaitUntil(() => town.Npcs.Count == 3 && town.Npcs.All(n => n.Walker is not null), 15, "Lauro, Marta e Faísca na Rua da Forja às 18:30");
            var ids = town.Npcs.Select(n => n.Def.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
            Require(ids.SequenceEqual(new[] { "faisca", "lauro", "marta" }), $"NPCs da Rua da Forja: [{string.Join(",", ids)}]");
            Require(town.Hunters.Count is >= 1 and <= 2, $"Rua da Forja tem 1–2 caçadores ambiente, tem {town.Hunters.Count}");
            var classId = GameManager.Instance.State.Save.ClassId;
            Require(town.Hunters.All(h => h.Brain.Def.Weapon != classId), "caçadores ambiente não usam a arma do jogador");

            // Anda 6 s: ninguém entra em área bloqueada e todo mundo se move.
            var start = town.Npcs.ToDictionary(n => n.Def.Id, n => n.Position);
            var moved = new Dictionary<string, float>();
            for (var frame = 0; frame < 6 * Engine.PhysicsTicksPerSecond; frame++)
            {
                foreach (var n in town.Npcs)
                {
                    var p = n.Position;
                    Require(world.WalkArea.CanStand(p.X, p.Y, WalkMover.NpcRadius), $"{n.Def.Id} atravessou parede em {p}");
                    moved[n.Def.Id] = Math.Max(moved.GetValueOrDefault(n.Def.Id), p.DistanceTo(start[n.Def.Id]));
                }

                foreach (var h in town.Hunters)
                {
                    Require(world.WalkArea.CanStand(h.Position.X, h.Position.Y, WalkMover.NpcRadius), $"caçador {h.Brain.Def.Name} atravessou parede em {h.Position}");
                }

                await NextPhysicsFrame();
            }

            Require(moved.Values.Any(d => d > 15f), $"NPCs deveriam andar: [{string.Join(", ", moved.Select(kv => $"{kv.Key} {kv.Value:0}"))}]");
            await Shot("npcs-rua");

            // Conversar com a Marta: fala verbatim da v2, em ordem; ela para e vira para o jogador.
            var marta = town.Npcs.First(n => n.Def.Id == "marta");
            var player = FindPlayer()!;
            for (var talk = 0; talk < 2; talk++)
            {
                // Encosta na Marta e aperta interagir (como uma tecla real); repete se ela saiu do alcance no meio.
                var deadline = Time.GetTicksMsec() + 5000;
                while (dialogues.Count <= talk)
                {
                    Require(Time.GetTicksMsec() < deadline, $"interagir com a Marta deveria emitir DialogueRequested (foco: {player.FocusedInteractable?.Prompt ?? "nenhum"})");
                    player.Teleport(world.CorrectToWalkable(marta.Position + new Vector2(0, 12)));
                    await WaitUntil(() => player.FocusedInteractable is NpcInteractable { Npc: var npc } && npc == marta, 3, "foco na Marta");
                    Require(player.FocusedInteractable!.Prompt == "Falar com Marta", $"prompt: {player.FocusedInteractable.Prompt}");
                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
                    await Frames(2);
                    Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
                    // Com janela, o evento só é entregue no próximo frame de processo (depois de um print podem passar vários de física).
                    var pressedAt = Time.GetTicksMsec();
                    while (dialogues.Count <= talk && Time.GetTicksMsec() - pressedAt < 1000)
                    {
                        await NextPhysicsFrame();
                    }
                }

                Require(dialogues.Count == talk + 1, $"uma fala por interação ({dialogues.Count})");
                var expected = GameManager.Db.Npcs.Walkers.First(w => w.Id == "marta").Lines[talk];
                Require(dialogues[talk].Speaker == "Marta" && dialogues[talk].Lines.Length == 1 && dialogues[talk].Lines[0] == expected,
                    $"fala {talk + 1} da Marta: \"{string.Join("|", dialogues[talk].Lines)}\"");
                Require(marta.Walker!.Wait >= 3.5, "NPC para ~4 s ao conversar");
                var toPlayer = player.GlobalPosition - marta.Position;
                var facing = Math.Abs(toPlayer.Y) >= Math.Abs(toPlayer.X) ? (toPlayer.Y < 0 ? TownFacing.Up : TownFacing.Down) : (toPlayer.X < 0 ? TownFacing.Left : TownFacing.Right);
                Require(marta.Walker.Facing == facing, $"Marta deveria olhar para o jogador ({facing}), olha {marta.Walker.Facing}");
                var still = marta.Position;
                await WaitSeconds(0.5);
                Require(marta.Position.DistanceTo(still) < 0.01f, "NPC parado enquanto conversa");
            }

            // Chat simulado: balão sobre um caçador do mapa.
            town.Chat("Nina", "C", "boa véspera, Lumeira");
            await Frames(2);
            Require(chats.Contains("boa véspera, Lumeira"), "ChatMessage do chat simulado");
            Require(town.Hunters.Any(h => h.Bubble == "boa véspera, Lumeira"), "o chat deveria aparecer como balão sobre um caçador");
            player.Teleport(world.CorrectToWalkable(town.Hunters[0].Position + new Vector2(0, 30)));
            await Frames(10);
            await Shot("chat-balao");

            // Fim do horário: às 21:00 a Marta sai do mapa.
            GameManager.Instance.State.Save.ClockMinutes = (21 * 60) - 0.5;
            await WaitUntil(() => marta.IsLeaving, 5, "Marta saindo às 21:00");
            await WaitUntil(() => !GodotObject.IsInstanceValid(marta) || marta.IsQueuedForDeletion(), 5, "Marta sumindo");
            Require(town.Npcs.Count(n => GodotObject.IsInstanceValid(n) && !n.IsLeaving) == 2, "Lauro e Faísca continuam depois das 21:00");

            // Vesna na Guilda entre 18:00 e 24:00.
            world.GoMap("guild", 250, 560, instant: true);
            await WaitMapSettled(world);
            await WaitUntil(() => town.Npcs.Count == 1 && town.Npcs[0].Walker is not null, 10, "Vesna na Guilda");
            Require(town.Npcs[0].Def.Id == "vesna", $"NPC da Guilda: {town.Npcs[0].Def.Id}");
            await Shot("npc-guilda");

            // Alocação por frame com a cidade inteira andando.
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await WaitUntil(() => town.Npcs.Count >= 2 && town.Npcs.All(n => n.Walker is not null), 10, "NPCs prontos");
            await WaitSeconds(0.5);
            var before = GC.GetTotalAllocatedBytes(precise: true);
            var physics = Engine.GetPhysicsFrames();
            await WaitSeconds(2.0);
            var perFrame = (GC.GetTotalAllocatedBytes(precise: true) - before) / (long)Math.Max(1, Engine.GetPhysicsFrames() - physics);
            Log.Info($"cidade: {perFrame} B/frame com {town.Npcs.Count} NPCs, {town.Hunters.Count} caçadores, {city.Lighting.CurrentLightCount} luzes");
            Require(perFrame < 2048, $"cidade viva aloca {perFrame} B por frame; esperado ~0");
        }
        finally
        {
            bus.DialogueRequested -= OnDialogue;
            bus.ChatMessage -= OnChat;
            StopCity(city);
        }
    }

    [SmokeScenario("cidade: Mural pede a tela e clima da estação", order: 64)]
    private async Task CityBoardAndWeather()
    {
        var (world, city) = await StartCity("guild", new Vector2(250, 330), Clock1630);
        var screens = new List<string>();
        void OnScreen(string id) => screens.Add(id);
        EventBus.Instance.ScreenRequested += OnScreen;
        try
        {
            var (kind, _) = await InteractWith(world, "board");
            Require(kind == "openBoard" && screens.Contains("board"), $"Mural: {kind}, telas [{string.Join(",", screens)}]");
            Require(BoardTexts.Empty == "Nenhuma Brecha aberta. A próxima véspera não deve demorar.", "texto do Mural vazio");

            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            await Frames(5);
            var season = GameManager.Instance.State.Season.Id;
            Require(city.Lighting.Weather.ActiveSeason == season && city.Lighting.Weather.Count > 0, $"clima de {season} na Rua da Forja: {city.Lighting.Weather.ActiveSeason} ({city.Lighting.Weather.Count})");
            await Shot("clima-" + season);

            // Inverno: neve (90 flocos) e tinta azul.
            GameManager.Instance.State.Save.Day = 4;
            await WaitUntil(() => city.Lighting.Weather.ActiveSeason == "inverno", 3, "clima do Inverno");
            await Frames(2);
            Require(city.Lighting.Weather.ActiveSeason == "inverno" && city.Lighting.Weather.Count == 90, $"Inverno: {city.Lighting.Weather.ActiveSeason} ({city.Lighting.Weather.Count})");
            await Shot("clima-inverno");
            GameManager.Instance.State.Save.Day = 1;
        }
        finally
        {
            EventBus.Instance.ScreenRequested -= OnScreen;
            StopCity(city);
        }
    }

    // --- utilidades da cidade ---

    private async Task<(World.World World, CityLife City)> StartCity(string mapId, Vector2 at, double clockMinutes, bool firstBrecha = false)
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        var city = CityLife.Instance ?? throw new SmokeFailure("World sem CityLife");
        var state = GameManager.Instance.State;
        state.Save.ClockMinutes = clockMinutes;
        if (firstBrecha)
        {
            state.Save.FirstBrechaDone = false;
        }

        city.TimeScale = 1f;
        city.Brechas.Reset();
        world.GoMap(mapId, at, instant: true); // a chegada já é corrigida para um ponto andável (D25)
        await WaitMapSettled(world);
        city.SetSimulationEnabled(true);
        await Frames(2);
        return (world, city);
    }

    private void StopCity(CityLife city)
    {
        city.TimeScale = 1f;
        city.SetSimulationEnabled(false);
        city.Brechas.Reset();
        GameManager.Instance.UnblockGameplay("smoke-dialogo");
        EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, false);
        // Devolve a música da cidade ao normal para os cenários seguintes (fase fora da véspera).
        EventBus.Instance.EmitSignal(EventBus.SignalName.DayPhaseChanged, (int)DayPhase.Night);
        GameManager.Instance.State.Save.ClockMinutes = Clock1630;
    }
}
