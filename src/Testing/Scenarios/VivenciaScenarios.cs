using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Ranks;
using Vespera.Core.World;
using Vespera.Enemies;
using Vespera.Items;
using Vespera.UI;
using Vespera.World;
using Vespera.World.City;
using Vespera.World.Dungeon;

namespace Vespera.Testing;

/// <summary>
/// Playtest de vivência (QA de produto, não de regressão): joga como um jogador curioso, com input simulado, e registra
/// no log (prefixo <c>VIV</c>) tudo o que o jogo mostra — falas, textos de inspeção, recompensas, tempos de luta e alturas
/// dos sprites em u — para o relatório de produto (<c>docs/design/relatorio-vivencia-v0.1.md</c>). Os cenários não
/// falham por gosto: só registram. Rode com <c>-- --smoke-test --smoke-filter=vivencia --smoke-shots=&lt;pasta&gt;</c>.
/// </summary>
public partial class SmokeTest
{
    private static readonly string[] VivExitWords = ["Sair", "Até logo", "Ficar na rua", "Agora não", "Ainda não", "Voltar", "Entendi"];

    private static void Viv(string text) => Log.Info($"VIV {text}");

    private static void VivProblem(string text) => Log.Info($"VIV-PROBLEMA {text}");

    // ------------------------------------------------------------------ 1. Guilda, Rua da Forja, lojas, estalagem

    [SmokeScenario("vivencia 1: explorar a Guilda, a Rua da Forja inteira, lojas e estalagem", order: 1001)]
    private async Task VivExplore()
    {
        var world = await FreshGame("sword");
        using var listen = new VivListener();
        using var speed = new SpeedUp(TesterSpeedUp);
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        Viv($"início: {VivSnapshot()}");
        await WaitSeconds(0.5);
        await Shot("viv-01-guilda");
        VivMeasure(world, "guilda");

        await VivTalkMenu(world, ui, "odete");
        if (world.GetInteractable("board") is { } board && await VivUse(world, board, "Mural"))
        {
            await WaitSeconds(0.5);
            await Shot("viv-02-mural");
            ui.CloseAll();
            await WaitSeconds(0.2);
        }

        await VivHitDummies(world);
        await VivTour(world, ui, "guilda");

        // Rua da Forja de manhã
        state.Save.ClockMinutes = 10 * 60;
        var toForge = world.Map.Exits.First(e => e.To == "forge");
        await JourneyWalkInto(world, toForge.Area, () => world.MapId == "forge", "porta da Guilda");
        await WaitMapSettled(world);
        await WaitSeconds(0.4);
        await Shot("viv-03-rua-manha");
        VivMeasure(world, "rua-manha");
        await JourneyPress(InputActions.Map);
        await WaitSeconds(0.6);
        await Shot("viv-04-minimapa");
        Viv($"minimapa aberto: {world.Minimap.Expanded}, marcadores {world.Minimap.MarkerCount}");
        await JourneyPress(InputActions.Map);
        await WaitSeconds(0.3);

        await VivTalkMenu(world, ui, "bras");
        await VivTalkMenu(world, ui, "oficios");
        await VivTour(world, ui, "rua");
        Viv($"depois da rua: {VivSnapshot()}");
        await Shot("viv-05-rua-depois");

        // Lojas e estalagem pela porta de cada uma
        foreach (var (mapId, keeper) in new[] { ("inn", "lucia"), ("shop_arm", "gaspar"), ("shop_rel", "abel"), ("shop_alq", "ivo"), ("shop_mag", "vesna") })
        {
            await VivVisitInterior(world, ui, mapId, keeper);
        }

        Viv($"fim da exploração: {VivSnapshot()}");
    }

    private async Task VivVisitInterior(World.World world, GameUi ui, string mapId, string keeper)
    {
        if (world.MapId != "forge")
        {
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
        }

        var exit = world.Map.Exits.FirstOrDefault(e => e.To == mapId);
        if (exit is null)
        {
            VivProblem($"rua sem saída para {mapId}");
            return;
        }

        try
        {
            await JourneyWalkInto(world, exit.Area, () => world.MapId == mapId, $"porta de {mapId}");
        }
        catch (SmokeFailure e)
        {
            VivProblem($"não consegui andar até a porta de {mapId}: {e.Message}; uso a placa Rua dos Ofícios");
            world.GoMap(mapId, exit.At.X, exit.At.Y, instant: true);
        }

        await WaitMapSettled(world);
        await WaitSeconds(0.4);
        Viv($"== interior {mapId} ({world.Map.Name})");
        await Shot($"viv-10-{mapId}");
        VivMeasure(world, mapId);
        await VivTalkMenu(world, ui, keeper);
        await VivTour(world, ui, mapId);
        if (mapId == "inn")
        {
            for (var i = 0; i < 3; i++)
            {
                await VivTalkMenu(world, ui, keeper, only: "Ouvir um boato");
            }
        }

        var back = world.Map.Exits.First(e => e.To == "forge");
        try
        {
            await JourneyWalkInto(world, back.Area, () => world.MapId == "forge", $"saída de {mapId}");
        }
        catch (SmokeFailure e)
        {
            VivProblem($"saída de {mapId}: {e.Message}");
            world.GoMap("forge", back.At.X, back.At.Y, instant: true);
        }

        await WaitMapSettled(world);
    }

    private async Task VivHitDummies(World.World world)
    {
        var combat = CombatDirector.Instance;
        if (combat is null)
        {
            return;
        }

        var dummies = combat.Enemies.Where(e => e.Body.IsDummy).ToList();
        Viv($"bonecos de treino: {dummies.Count}");
        var player = FindPlayer()!;
        foreach (var d in dummies.Take(2))
        {
            var at = new MapPoint((float)d.Body.X, (float)d.Body.Y);
            if (await VivGo(world, at, 20, "boneco"))
            {
                for (var i = 0; i < 8; i++)
                {
                    SetAim((new Vector2((float)d.Body.X, (float)d.Body.Y - 10) - (player.GlobalPosition - new Vector2(0, 12))).Normalized());
                    await Hold(InputActions.Attack, 6);
                    await Frames(10);
                }
            }
        }

        SetAim(Vector2.Zero);
        Viv($"contador de bonecos: {GameManager.Instance.State.Save.Quests.Counter("dummy")}; missão atual: {GameManager.Instance.State.CurrentQuest?.Name}");
        await Shot("viv-01b-bonecos");
    }

    /// <summary>Usa tudo o que o mapa oferece: decoração (lampião, flor, pesca, banco, placa, inspeção), baús (indo de balsa se precisar) e quebráveis.</summary>
    private async Task VivTour(World.World world, GameUi ui, string label)
    {
        var dressing = world.Dressing;
        Viv($"[{label}] decoração {dressing.Decor.Count}, baús {dressing.Chests.Count}, quebráveis {dressing.Breakables.Count}");
        var seenText = new Dictionary<string, int>();
        var used = new HashSet<int>();

        await VivDecorPass(world, ui, label, used);



        // Baús: tenta a pé; sem caminho, procura uma balsa que leve até lá.
        var chestN = 0;
        foreach (var chest in dressing.Chests.ToList())
        {
            if (chest.IsOpen)
            {
                Viv($"[{label}] baú já aberto em {chest.GlobalPosition}");
                continue;
            }

            var before = VivSnapshot();
            var at = new MapPoint(chest.Interactable.GlobalPosition.X, chest.Interactable.GlobalPosition.Y);
            if (!await VivReach(world, at, chest.Interactable.Radius - 4, $"baú tier {chest.Tier}", () => FindPlayer()!.FocusedInteractable == chest.Interactable))
            {
                continue;
            }

            await JourneyPress(InputActions.Interact);
            await WaitSeconds(1.4);
            chestN++;
            Viv($"[{label}] BAÚ tier {chest.Tier} em ({at.X:0},{at.Y:0}) aberto={chest.IsOpen}: {VivDiff(before)}");
            if (chestN <= 2)
            {
                await Shot($"viv-bau-{label}-{chestN}");
            }
        }

        // Quebráveis
        var broken = 0;
        var player = FindPlayer()!;
        foreach (var b in dressing.Breakables.ToList())
        {
            if (b.IsBroken || !IsInstanceValid(b))
            {
                continue;
            }

            var before = VivSnapshot();
            var at = new MapPoint(b.GlobalPosition.X, b.GlobalPosition.Y);
            if (!await VivReach(world, at, 14, $"quebrável {b.Kind}", null))
            {
                continue;
            }

            var deadline = Time.GetTicksMsec() + 4000;
            while (!b.IsBroken && Time.GetTicksMsec() < deadline)
            {
                SetAim((b.GlobalPosition - new Vector2(0, 6) - (player.GlobalPosition - new Vector2(0, 12))).Normalized());
                await Hold(InputActions.Attack, 4);
                await Frames(8);
            }

            SetAim(Vector2.Zero);
            await WaitSeconds(0.9); // saque cai e é coletado
            broken += b.IsBroken ? 1 : 0;
            Viv($"[{label}] quebrar {b.Kind} em ({at.X:0},{at.Y:0}): {(b.IsBroken ? "quebrou" : $"NÃO quebrou (hp {b.Hp}/{b.MaxHp})")} {VivDiff(before)}");
        }

        await VivDecorPass(world, ui, label, used);

        Viv($"[{label}] quebrados {broken}/{dressing.Breakables.Count}; {VivSnapshot()}");
        if (ui.Stack.Count > 0)
        {
            ui.CloseAll();
        }
    }

    private async Task VivDecorPass(World.World world, GameUi ui, string label, HashSet<int> used)

    {

        var dressing = world.Dressing;

        var seenText = new Dictionary<string, int>();

        foreach (var spot in dressing.Decor.Where(s => s.Def.Kind != "boat").ToList())
        {
            if (used.Contains(spot.Index))

            {

                continue;

            }


            if (!spot.Interactable.Enabled)
            {
                continue;
            }

            var before = VivSnapshot();
            if (!await VivReach(world, new MapPoint(spot.Def.X, spot.Def.Y), spot.Def.Radius - 6, $"{spot.Def.Kind} '{spot.Def.Label}'", () => FindPlayer()!.FocusedInteractable == spot.Interactable))
            {
                continue;
            }

            used.Add(spot.Index);

            await JourneyPress(InputActions.Interact);
            await WaitSeconds(0.3);
            switch (spot.Def.Kind)
            {
                case "fishing":
                    var caught = await VivFish(world);
                    Viv($"[{label}] pescar em ({spot.Def.X},{spot.Def.Y}): {caught}; {VivDiff(before)}");
                    if (!seenText.ContainsKey("fish"))
                    {
                        seenText["fish"] = 1;
                    }

                    break;
                case "bench":
                    await WaitSeconds(0.8);
                    Viv($"[{label}] banco: \"{dressing.Lore.Text}\"");
                    await Shot($"viv-banco-{label}-{spot.Index}");
                    PressDirection(Vector2.Down);
                    await Frames(10);
                    PressDirection(Vector2.Zero);
                    break;
                default:
                    await WaitSeconds(0.2);
                    var text = dressing.Lore.Text;
                    Viv($"[{label}] {spot.Def.Kind} '{spot.Interactable.Prompt}' ({spot.Def.X},{spot.Def.Y}): \"{(text.Length > 0 ? text : spot.Def.Text)}\" {VivDiff(before)}");
                    if (spot.Def.Kind is "inspect" or "sign" && !seenText.ContainsKey(spot.Def.Kind))
                    {
                        seenText[spot.Def.Kind] = 1;
                        await Shot($"viv-{spot.Def.Kind}-{label}");
                    }

                    break;
            }

            if (ui.Stack.Count > 0)
            {
                ui.CloseAll();
            }
        }

    }



    private async Task<string> VivFish(World.World world)
    {
        var dressing = world.Dressing;
        var deadline = Time.GetTicksMsec() + 9000;
        var shot = false;
        while (dressing.InActivity && Time.GetTicksMsec() < deadline)
        {
            if (dressing.Fishing is { } game && game.State == FishingState.Biting)
            {
                if (!shot)
                {
                    shot = true;
                    await Shot("viv-pesca");
                }

                if (game.NeedleInZone && game.Needle > game.ZoneStart + (game.ZoneWidth * 0.3))
                {
                    var r = game.State;
                    await JourneyPress(InputActions.Interact);
                    await WaitSeconds(0.2);
                    return $"puxou ({dressing.Fishing?.State.ToString() ?? "fim"})";
                }
            }

            await NextPhysicsFrame();
        }

        return dressing.InActivity ? "ainda pescando (timeout)" : "acabou";
    }

    /// <summary>Anda até o ponto; sem caminho a pé, tenta uma balsa que deixe perto dele.</summary>
    private async Task<bool> VivReach(World.World world, MapPoint target, float radius, string what, Func<bool>? done)
    {
        if (await VivGo(world, target, radius, what, done, quiet: true))
        {
            return true;
        }

        foreach (var boat in world.Dressing.Decor.Where(s => s.Def.Kind == "boat" && s.Def.To is not null))
        {
            var to = boat.Def.To!.Value;
            var from = world.WalkArea.NearestWalkable(to.X, to.Y, maxDistance: 16f) ?? new MapPoint(to.X, to.Y);
            if (WalkPathfinder.FindPathNear(world.WalkArea, from, target, MathF.Max(2f, radius)) is null)
            {
                continue;
            }

            if (!await VivGo(world, new MapPoint(boat.Def.X, boat.Def.Y), boat.Def.Radius - 6, $"balsa '{boat.Def.Label}'", () => FindPlayer()!.FocusedInteractable == boat.Interactable, quiet: true))
            {
                continue;
            }

            await JourneyPress(InputActions.Interact);
            await WaitSeconds(0.5);
            await Shot($"viv-balsa-{boat.Index}");
            await WaitUntil(() => !world.Dressing.InActivity, 12, "fim da balsa");
            Viv($"balsa '{boat.Def.Label}' → chegou em {FindPlayer()!.GlobalPosition}");
            if (await VivGo(world, target, radius, what, done))
            {
                return true;
            }
        }

        VivProblem($"{world.MapId}: inalcançável a pé e de balsa: {what} ({target.X:0},{target.Y:0}) a partir de {FindPlayer()!.GlobalPosition}");
        return false;
    }

    private async Task<bool> VivGo(World.World world, MapPoint target, float radius, string what, Func<bool>? done = null, bool quiet = false)
    {
        try
        {
            await JourneyWalkNear(world, target, MathF.Max(2f, radius), what, done);
            return true;
        }
        catch (SmokeFailure e)
        {
            if (!quiet)
            {
                VivProblem($"{world.MapId}: andando até {what}: {e.Message}");
            }

            PressDirection(Vector2.Zero);
            return false;
        }
    }

    private async Task<bool> VivUse(World.World world, Interactable it, string what)
    {
        if (!await VivReach(world, new MapPoint(it.GlobalPosition.X, it.GlobalPosition.Y), it.Radius - 6, what, () => FindPlayer()!.FocusedInteractable == it))
        {
            return false;
        }

        await JourneyPress(InputActions.Interact);
        await WaitSeconds(0.3);
        return true;
    }

    /// <summary>
    /// Conversa com um interagível (NPC/serviço): registra a saudação e as opções e, reabrindo a conversa a cada vez,
    /// escolhe cada opção que não é de saída e registra a resposta e as opções seguintes (um nível).
    /// </summary>
    private async Task VivTalkMenu(World.World world, GameUi ui, string id, string? only = null)
    {
        var it = world.GetInteractable(id);
        if (it is null)
        {
            VivProblem($"{world.MapId}: sem interagível '{id}'");
            return;
        }

        if (!await VivUse(world, it, id))
        {
            return;
        }

        if (!await VivWaitDialog(ui))
        {
            VivProblem($"{world.MapId}: '{id}' não abriu diálogo");
            return;
        }

        VivLogDialog(ui, id);
        if (only is null)
        {
            await Shot($"viv-fala-{id}");
        }

        var labels = ui.Dialog.Options.Where(o => !VivExitWords.Contains(o.Label) && !o.Disabled).Select(o => o.Label).ToList();
        if (only is not null)
        {
            labels = labels.Where(l => l == only).ToList();
        }

        ui.CloseAll();
        await WaitSeconds(0.2);
        if (id == "oficios")
        {
            return; // cada opção leva para uma loja; as lojas são visitadas pela porta
        }

        var mapId = world.MapId;
        foreach (var label in labels)
        {
            it = world.MapId == mapId ? world.GetInteractable(id) : null;
            if (it is null || !await VivUse(world, it, id) || !await VivWaitDialog(ui))
            {
                continue;
            }

            if (!ui.Dialog.Options.Any(o => o.Label == label))
            {
                ui.CloseAll();
                continue;
            }

            var before = VivSnapshot();
            await VivChoose(ui, label);
            await WaitSeconds(0.2);
            if (ui.Dialog.IsOpen)
            {
                await SkipTyping(ui);
                VivLogDialog(ui, $"{id} › {label}");
                // um nível a mais só para conversas de informação (sem custo na legenda)
                var info = ui.Dialog.Options.Where(o => !VivExitWords.Contains(o.Label) && !o.Disabled && o.Sub is null && o.Label.Contains('?')).Select(o => o.Label).ToList();
                foreach (var sub in info.Take(2))
                {
                    await VivChoose(ui, sub);
                    if (ui.Dialog.IsOpen)
                    {
                        await SkipTyping(ui);
                        VivLogDialog(ui, $"{id} › {label} › {sub}");
                    }
                }
            }
            else
            {
                Viv($"{id} › {label}: (fechou a conversa)");
            }

            var diff = VivDiff(before);
            if (diff.Length > 0)
            {
                Viv($"{id} › {label}: {diff}");
            }

            ui.CloseAll();
            await WaitSeconds(0.25);
        }
    }

    /// <summary>Escolhe a opção navegando com ↓ como o jogador; se o foco não chega, registra o problema e escolhe direto.</summary>
    private async Task VivChoose(GameUi ui, string label)
    {
        var (scale, ticks) = (Engine.TimeScale, Engine.PhysicsTicksPerSecond);
        Engine.TimeScale = 1;
        Engine.PhysicsTicksPerSecond = 60;
        try
        {
            await ChooseOption(ui, label);
        }
        catch (SmokeFailure e)
        {
            var focus = ui.Dialog.Rows.ToList().FindIndex(r => r.HasFocus());
            VivProblem($"navegação do diálogo: {e.Message} (foco na linha {focus} de {ui.Dialog.Rows.Count}; opções {string.Join(" | ", ui.Dialog.Options.Select(o => o.Label))})");
            await Shot($"viv-problema-foco-{label.Replace(' ', '_').Replace('?', '_')}");
            var index = ui.Dialog.Options.ToList().FindIndex(o => o.Label == label);
            if (index >= 0)
            {
                ui.Dialog.Choose(index);
                await WaitSeconds(0.25);
            }
        }
        finally
        {
            Engine.TimeScale = scale;
            Engine.PhysicsTicksPerSecond = ticks;
        }
    }

    private async Task<bool> VivWaitDialog(GameUi ui)
    {
        var deadline = Time.GetTicksMsec() + 2500;
        while (!ui.Dialog.IsOpen && Time.GetTicksMsec() < deadline)
        {
            await NextPhysicsFrame();
        }

        if (!ui.Dialog.IsOpen)
        {
            return ui.Stack.Count > 0; // mural e outras telas
        }

        await SkipTyping(ui);
        return true;
    }

    private static void VivLogDialog(GameUi ui, string ctx)
    {
        var opts = string.Join(" | ", ui.Dialog.Options.Select(o => o.Sub is null ? o.Label : $"{o.Label} ({o.Sub}){(o.Disabled ? " [off]" : string.Empty)}"));
        Viv($"FALA {ctx} — {ui.Dialog.Speaker}: \"{ui.Dialog.Text}\"{(opts.Length > 0 ? $" ⟶ [{opts}]" : string.Empty)}");
    }

    // ------------------------------------------------------------------ 2. Moradores ao longo do dia, véspera, ruptura

    [SmokeScenario("vivencia 2: moradores ao longo do dia, véspera e ruptura", order: 1002)]
    private async Task VivTownsfolk()
    {
        var world = await FreshGame("sword");
        using var listen = new VivListener();
        var ui = UiOrFail();
        var state = GameManager.Instance.State;
        var city = CityLife.Instance ?? throw new SmokeFailure("sem CityLife");
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        city.Brechas.Reset();
        city.SetSimulationEnabled(true);
        using var speed = new SpeedUp(TesterSpeedUp);
        try
        {
            foreach (var hour in new[] { 9, 16, 18, 20, 22 })
            {
                state.Save.ClockMinutes = hour * 60;
                await WaitSeconds(2.5);
                Viv($"== {hour}h na Rua da Forja: moradores {string.Join(", ", city.Townsfolk.Npcs.Select(n => n.Def.Name))}; caçadores {string.Join(", ", city.Townsfolk.Hunters.Select(h => $"{h.Brain.Def.Name}/{h.Brain.Def.Rank}/{h.Brain.Def.Weapon}"))}; Brechas {string.Join(", ", city.Brechas.Brechas.Select(b => $"{b.ThemeId}/r{b.Rank}/{b.Status}"))}");
                await Shot($"viv-20-rua-{hour}h");
                foreach (var npc in city.Townsfolk.Npcs.ToList())
                {
                    for (var k = 0; k < 2; k++)
                    {
                        await VivTalkToWalker(world, ui, npc);
                    }
                }

                foreach (var h in city.Townsfolk.Hunters)
                {
                    if (h.Bubble is { } bubble)
                    {
                        Viv($"balão {h.Brain.Def.Name}: \"{bubble}\"");
                    }
                }
            }

            // Vesna só aparece na Guilda à noite
            state.Save.ClockMinutes = 19 * 60;
            world.GoMap("guild", 250, 560, instant: true);
            await WaitMapSettled(world);
            await WaitSeconds(2.5);
            await Shot("viv-21-guilda-noite");
            VivMeasure(world, "guilda-noite");
            foreach (var npc in city.Townsfolk.Npcs.ToList())
            {
                await VivTalkToWalker(world, ui, npc);
                await VivTalkToWalker(world, ui, npc);
            }

            await VivTalkMenu(world, ui, "odete");

            // A véspera: deixa a Brecha abrir e não fecha — até romper
            world.GoMap("forge", 470, 520, instant: true);
            await WaitMapSettled(world);
            state.Save.ClockMinutes = 16 * 60 + 30;
            city.Brechas.OpenSoon(0);
            await WaitUntil(() => city.Brechas.Brechas.Any(b => b.Status == BrechaStatus.Open), 20, "uma Brecha abrir");
            await WaitSeconds(1.3);
            await Shot("viv-22-portal-abrindo");
            var open = city.Brechas.Brechas.First(b => b.Status == BrechaStatus.Open);
            Viv($"Brecha aberta: {open.Bairro} — {open.Title} (rank {open.Rank}, {open.MaxTime:0}s)");
            if (world.GetInteractable($"portal{open.Spot}") is { } portal && await VivUse(world, portal, "portal"))
            {
                if (await VivWaitDialog(ui))
                {
                    VivLogDialog(ui, "portal");
                    await Shot("viv-23-dialogo-portal");
                }

                ui.CloseAll();
            }

            if (world.GetInteractable("board") is null)
            {
                world.GoMap("guild", 400, 300, instant: true);
                await WaitMapSettled(world);
                ui.OpenBoard();
                await WaitSeconds(0.5);
                await Shot("viv-24-mural-com-brecha");
                ui.CloseAll();
                world.GoMap("forge", 800, 520, instant: true);
                await WaitMapSettled(world);
            }

            city.TimeScale = 12f;
            await WaitUntil(() => city.Brechas.InvasionActive, 120, "a Brecha ignorada romper");
            city.TimeScale = 1f;
            await WaitSeconds(1.5);
            Viv($"RUPTURA: preço da poção da Lúcia agora {state.CostLumes(GameManager.Db.Services["inn_potion"].Cost.Lumes)} Lumes; monstros na rua {CombatDirector.Instance?.AliveMonsters}");
            await Shot("viv-25-invasao");
            VivMeasure(world, "invasao");
            await VivTalkMenu(world, ui, "bras");
            var combat = CombatDirector.Instance!;
            var before = VivSnapshot();
            try
            {
                await Bot(combat, () => !city.Brechas.InvasionActive || combat.AliveMonsters == 0, 120, "limpar a invasão", shotAt: "viv-26-luta-invasao");
            }
            catch (SmokeFailure e)
            {
                VivProblem($"invasão: {e.Message}");
            }

            await WaitSeconds(1.5);
            Viv($"invasão resolvida={!city.Brechas.InvasionActive}: {VivDiff(before)}");
            await Shot("viv-27-depois-invasao");
        }
        finally
        {
            city.TimeScale = 1f;
            city.SetSimulationEnabled(false);
            city.Brechas.Reset();
        }
    }

    private async Task VivTalkToWalker(World.World world, GameUi ui, TownNpc npc)
    {
        if (!IsInstanceValid(npc) || npc.IsLeaving || !npc.Visible)
        {
            return;
        }

        var talk = npc.GetChildren().OfType<Interactable>().FirstOrDefault();
        if (talk is null)
        {
            return;
        }

        for (var attempt = 0; attempt < 4 && FindPlayer()!.FocusedInteractable != talk; attempt++)
        {
            await VivGo(world, new MapPoint(npc.GlobalPosition.X, npc.GlobalPosition.Y), 12, npc.Def.Name, () => FindPlayer()!.FocusedInteractable == talk, quiet: true);
        }

        if (FindPlayer()!.FocusedInteractable != talk)
        {
            VivProblem($"não alcancei {npc.Def.Name} andando (ele anda; {npc.GlobalPosition})");
            return;
        }

        await JourneyPress(InputActions.Interact);
        if (await VivWaitDialog(ui))
        {
            VivLogDialog(ui, $"morador {npc.Def.Name}");
            await Shot($"viv-morador-{npc.Def.Id}");
        }

        ui.CloseAll();
        await WaitSeconds(0.3);
    }

    // ------------------------------------------------------------------ 3. Uma Brecha completa com cada classe

    [SmokeScenario("vivencia 3: uma Brecha inteira com cada classe", order: 1003)]
    private async Task VivClasses()
    {
        foreach (var cls in AllClasses)
        {
            var world = await FreshGame(cls);
            using var listen = new VivListener();
            using var speed = new SpeedUp(TesterSpeedUp);
            var combat = CombatDirector.Instance!;
            world.GoMap("forge", 1350, 535, instant: true);
            await WaitMapSettled(world);
            await WaitSeconds(0.4);
            await Shot($"viv-30-{cls}-rua");
            VivMeasure(world, $"classe-{cls}");
            var report = await VivRunBrecha(world, combat, "fish", 0, $"{cls}", fullBoss: true);
            Viv($"CLASSE {cls}: {report}");
            await VivSmith(world, UiOrFail(), cls);
        }
    }

    /// <summary>Joga uma Brecha do começo ao fim (ondas, porta, Núcleo, chefe, saída) e devolve um resumo.</summary>
    private async Task<string> VivRunBrecha(World.World world, CombatDirector combat, string theme, int rank, string tag, bool fullBoss)
    {
        var state = GameManager.Instance.State;
        var dmg = new VivDamage();
        var xp0 = state.Save.Xp;
        var lv0 = state.Level;
        var before = VivSnapshot();
        var t0 = Time.GetTicksMsec();
        await EnterBrecha(world, combat, spot: 0, theme: theme, rank: rank);
        var run = combat.Brecha.Run!;
        Viv($"[{tag}] entrou em {world.Map.Name} (tema {theme}, rank {rank}), ondas {string.Join(" / ", run.Waves.Select(w => string.Join(",", w)))}; HP {state.Hp}/{state.MaxHp}");
        await WaitSeconds(0.8);
        await Shot($"viv-31-{tag}-sala");
        VivMeasure(world, $"sala-{theme}-{tag}");
        string waves;
        try
        {
            await Bot(combat, () => world.DoorOpen, 200, $"{tag}: ondas", shotAt: $"viv-32-{tag}-onda");
            waves = $"ondas em {(Time.GetTicksMsec() - t0) * TesterSpeedUp / 1000.0:0}s de jogo";
        }
        catch (SmokeFailure e)
        {
            VivProblem($"[{tag}] ondas: {e.Message}");
            await FleeRun(world, combat);
            return $"FUGIU nas ondas; {dmg}";
        }

        var door = world.Map.Door ?? throw new SmokeFailure("sem porta");
        await JourneyWalkInto(world, door, () => world.MapId == World.World.CoreMapId, "porta do Núcleo");
        await WaitMapSettled(world);
        Enemy? boss = null;
        await WaitUntil(() => (boss = FindBoss(combat)) is not null, 6, "chefe nascer");
        await WaitUntil(() => boss!.Body.SpawnT <= 0, 5, "chefe terminar de nascer");
        Viv($"[{tag}] chefe {boss!.Body.Def.Name} HP {boss.Body.Hp:0}");
        await Shot($"viv-33-{tag}-chefe");
        VivMeasure(world, $"chefe-{theme}-{tag}");
        var tb = Time.GetTicksMsec();
        await JourneyPress(InputActions.Ultimate);
        string bossText;
        try
        {
            if (!fullBoss)
            {
                boss.Body.Hp = Math.Min(boss.Body.Hp, 80);
            }

            await Bot(combat, () => boss.Body.Dead, 150, $"{tag}: chefe", shotAt: $"viv-34-{tag}-chefe-luta");
            bossText = $"chefe em {(Time.GetTicksMsec() - tb) * TesterSpeedUp / 1000.0:0}s de jogo";
        }
        catch (SmokeFailure e)
        {
            VivProblem($"[{tag}] chefe não caiu em 150 s reais (×{TesterSpeedUp}): HP restante {boss.Body.Hp:0}; {e.Message}");
            boss.Body.Hp = Math.Min(boss.Body.Hp, 10);
            await Bot(combat, () => boss.Body.Dead, 40, $"{tag}: chefe (atalho)");
            bossText = "chefe NÃO caiu no tempo (atalho de HP)";
        }

        ExitPortal? exit = null;
        var portalDeadline = Time.GetTicksMsec() + 15000;
        while ((exit = Find<ExitPortal>()) is null && Time.GetTicksMsec() < portalDeadline)
        {
            UiOrFail().CloseAll();
            await NextPhysicsFrame();
        }

        if (exit is null)
        {
            VivProblem($"[{tag}] chefe {boss.Body.Def.Name} morto (Dead={boss.Body.Dead}, HP {boss.Body.Hp:0}) e o portal de saída não apareceu em 15 s reais (×{TesterSpeedUp}); vivos {combat.AliveMonsters}; mapa {world.MapId}");
            await Shot($"viv-problema-sem-portal-{tag}");
            await FleeRun(world, combat);
            return $"{waves}; {bossText}; SEM PORTAL DE SAÍDA (fugiu); {dmg}";
        }

        await WaitSeconds(1.2); // saque
        await Shot($"viv-35-{tag}-saque");
        await JourneyWalkNear(world, new MapPoint(exit.GlobalPosition.X, exit.GlobalPosition.Y), 10f, "portal de saída", () => FindPlayer()!.FocusedInteractable == exit.Interaction);
        await JourneyPress(InputActions.Interact);
        await WaitUntil(() => world.MapId == World.World.CityMapId, 8, "voltar à rua");
        await WaitMapSettled(world);
        await WaitSeconds(1.0);
        UiOrFail().CloseAll();
        var summary = $"{waves}; {bossText}; total {(Time.GetTicksMsec() - t0) * TesterSpeedUp / 1000.0:0}s; XP +{state.Save.Xp - xp0} (Nv {lv0}→{state.Level}, Selo {state.Rank}); {dmg}; ganhos: {VivDiff(before)}";
        dmg.Dispose();
        return summary;
    }

    private async Task VivSmith(World.World world, GameUi ui, string cls)
    {
        if (world.MapId != "forge")
        {
            return;
        }

        var bras = world.GetInteractable("bras");
        if (bras is null || !await VivUse(world, bras, "Brás") || !await VivWaitDialog(ui))
        {
            return;
        }

        foreach (var (menu, prefix) in new[] { ("Melhorar armas", ""), ("Forjar equipamento", "") })
        {
            if (!ui.Dialog.IsOpen)
            {
                if (!await VivUse(world, bras, "Brás") || !await VivWaitDialog(ui))
                {
                    break;
                }
            }

            if (!ui.Dialog.Options.Any(o => o.Label == menu))
            {
                continue;
            }

            await VivChoose(ui, menu);
            await SkipTyping(ui);
            VivLogDialog(ui, $"Brás › {menu} ({cls})");
            var buy = ui.Dialog.Options.FirstOrDefault(o => !o.Disabled && !VivExitWords.Contains(o.Label) && o.Sub is not null && !o.Label.StartsWith("Melhorar", StringComparison.Ordinal));
            if (buy is not null)
            {
                var before = VivSnapshot();
                await VivChoose(ui, buy.Label);
                await WaitSeconds(0.3);
                Viv($"Brás › {menu} › {buy.Label} ({buy.Sub}): {VivDiff(before)}; resposta \"{(ui.Dialog.IsOpen ? ui.Dialog.Text : string.Empty)}\"");
            }

            ui.CloseAll();
            await WaitSeconds(0.2);
        }

        ui.CloseAll();
    }

    // ------------------------------------------------------------------ 4. Evoluir: várias Brechas, Selo, equipar, morrer e a Lanterna

    [SmokeScenario("vivencia 4: evoluir (Brechas seguidas, Selo, equipamento), morrer e recuperar a Lanterna", order: 1004)]
    private async Task VivProgress()
    {
        var world = await FreshGame("sword");
        using var listen = new VivListener();
        using var speed = new SpeedUp(TesterSpeedUp);
        var state = GameManager.Instance.State;
        var combat = CombatDirector.Instance!;
        var ui = UiOrFail();
        world.GoMap("forge", 1350, 535, instant: true);
        await WaitMapSettled(world);
        var started = Time.GetTicksMsec();
        for (var i = 0; i < 4 && Time.GetTicksMsec() - started < 12 * 60 * 1000; i++)
        {
            var theme = GameManager.Db.ThemeList.Where(t => t.MinRank <= state.RankIndex).Last();
            var rank = state.RankIndex;
            var report = await VivRunBrecha(world, combat, theme.Id, rank, $"prog{i + 1}-{theme.Id}", fullBoss: true);
            Viv($"PROGRESSÃO #{i + 1} ({theme.Id}, r{rank}): {report}; missão atual: {state.CurrentQuest?.Name}");
            await VivSmith(world, ui, "sword");
            // equipar o que der pela mochila
            foreach (var gearId in state.Save.Backpack.ToList())
            {
                var reason = state.EquipLockReason(gearId);
                var result = state.EquipById(gearId);
                Viv($"equipar {GameManager.Db.Gear[gearId].Name}: {(result is null ? "ok" : $"bloqueado — {result}")}{(reason is null ? string.Empty : $" ({reason})")}");
            }

            JourneyPressNoWait(InputActions.Inventory);
            await WaitSeconds(0.6);
            await Shot($"viv-40-inventario-{i + 1}");
            ui.CloseAll();
            await WaitSeconds(0.2);
        }

        Viv($"depois da progressão: {VivSnapshot()}; Selo {state.Rank} Nv {state.Level}; HP máx {state.MaxHp}; Brechas fechadas {state.Save.BrechasClosed}");
        await VivTalkMenu(world, ui, "bras");

        // Morrer de propósito e recuperar a Lanterna
        var hpMax0 = state.MaxHp;
        var themeId = GameManager.Db.ThemeList.Where(t => t.MinRank <= state.RankIndex).Last().Id;
        await EnterBrecha(world, combat, 0, themeId, state.RankIndex);
        await WaitUntil(() => combat.AliveMonsters > 0, 8, "onda nascer");
        await Bot(combat, () => combat.Brecha.Run!.Kills >= 1, 60, "matar 1 para ter algo na bolsa");
        var bag = combat.Brecha.Run?.Bag;
        Viv($"bolsa antes de morrer: {bag?.Lumes} Lumes, mats {string.Join(",", bag?.Materials.Select(kv => $"{kv.Key}×{kv.Value}") ?? [])}");
        PressDirection(Vector2.Zero);
        var deathAt = FindPlayer()!.GlobalPosition;
        await WaitUntil(() => combat.PlayerDead || ui.Death.IsOpen, 120, "morrer parado no meio da onda");
        await WaitSeconds(1.2);
        await Shot("viv-41-morte");
        Viv($"tela de morte: \"{ui.Death.Title}\"");
        await WaitUntil(() => ui.Death.PromptVisible, 8, "prompt da tela de morte");
        await UiPress("ui_accept");
        await WaitSeconds(2.5);
        await WaitMapSettled(world);
        await Shot("viv-42-depois-da-morte");
        Viv($"depois da morte: mapa {world.MapId}; Lanterna perdida={state.HasLostLantern}; HP máx {hpMax0}→{state.MaxHp}; {VivSnapshot()}");
        if (world.MapId != "guild")
        {
            world.GoMap("guild", 250, 330, instant: true);
            await WaitMapSettled(world);
        }

        await VivTalkMenu(world, ui, "odete");
        world.GoMap("forge", 1350, 535, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, 0, themeId, state.RankIndex);
        await WaitSeconds(1.5);
        var lantern = combat.Pickups.FirstOrDefault(p => p.Kind == PickupKind.MyLantern && !p.Collected);
        Viv($"voltando à Brecha {themeId}: minha Lanterna na sala={lantern is not null} {(lantern is null ? string.Empty : $"em {lantern.GlobalPosition} (morri em {deathAt})")}");
        if (lantern is not null)
        {
            try
            {
                await Bot(combat, () => combat.AliveMonsters == 0, 60, "limpar perto da Lanterna");
            }
            catch (SmokeFailure)
            {
            }

            await VivGo(world, new MapPoint(lantern.GlobalPosition.X, lantern.GlobalPosition.Y), 10, "minha Lanterna", () => FindPlayer()!.FocusedInteractable == lantern.Interaction);
            await Shot("viv-43-lanterna");
            await JourneyPress(InputActions.Interact);
            await WaitSeconds(1);
            Viv($"Lanterna recuperada={!state.HasLostLantern}; HP máx {state.MaxHp}; {VivSnapshot()}");
            await Shot("viv-44-lanterna-recuperada");
        }

        await FleeRun(world, combat);
    }

    private void JourneyPressNoWait(StringName action)
    {
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = true });
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = false });
    }

    // ------------------------------------------------------------------ 5. Lado a lado: caçadores, NPCs, monstros, elites, chefes, objetos

    [SmokeScenario("vivencia 5: coerência visual lado a lado (alturas em u)", order: 1005)]
    private async Task VivLineup()
    {
        var world = await FreshGame("sword");
        var combat = CombatDirector.Instance!;
        var city = CityLife.Instance!;
        var state = GameManager.Instance.State;
        world.GoMap("forge", 1300, 560, instant: true);
        await WaitMapSettled(world);
        state.Save.ClockMinutes = 12 * 60;
        UiOrFail().CloseAll();
        await WaitSeconds(6); // toasts e Novo Selo do cenário anterior somem
        var player = FindPlayer()!;
        var extra = new List<Node2D>();
        try
        {
            // as 5 classes + NPCs de rua + caçador ambiente, em fila no pátio da Guilda
            var keys = new List<(string Key, float Scale)>
            {
                ("c_bow_front", 1f), ("c_staff_front", 1f), ("c_hammer_front", 1f), ("c_dag_front", 1f),
                ("npc_lauro_f", 1f), ("npc_marta_f", 1f), ("npc_faisca_f", 1f), ("npc_vesna_f", 1f), ("npc_odete_f", 1f), ("c_sword_front", 0.85f),
            };
            player.Teleport(world.CorrectToWalkable(new Vector2(1120, 540)));
            await WaitSeconds(0.3);
            var x0 = player.GlobalPosition.X + 46;
            foreach (var (key, scale) in keys)
            {
                var n = new Node2D { Name = $"Viv_{key}", Position = new Vector2(x0, player.GlobalPosition.Y) };
                n.AddChild(new AtlasSprite { Key = key, SpriteScale = scale });
                world.Entities.AddChild(n);
                extra.Add(n);
                x0 += 46;
            }

            GameManager.Instance.BlockGameplay("vivencia");
            await WaitSeconds(0.6);
            await Shot("viv-50-fila-cacadores-npcs");
            VivMeasureNodes("fila-personagens", [player, .. extra]);

            // monstros comuns, elites e chefes, cada grupo numa fileira
            GameManager.Instance.UnblockGameplay("vivencia");
            var groups = new[]
            {
                ("comuns", new[] { "wolf", "fishm", "cnev", "coss", "cvel", "mbes", "maut" }),
                ("comuns2", new[] { "mesc", "gbro", "gsei", "gmus", "kesc", "ksin", "ksom" }),
                ("elites", new[] { "wolfE", "cosr", "msen", "gflo", "kcav" }),
                ("chefes", new[] { "angler", "reaper", "colosso", "rainha", "knight" }),
            };
            foreach (var (name, ids) in groups)
            {
                world.GoMap("guild", 250, 330, instant: true);
                await WaitMapSettled(world);
                world.GoMap("forge", 1300, 560, instant: true);
                await WaitMapSettled(world);
                player.Teleport(world.CorrectToWalkable(new Vector2(name == "chefes" ? 1060 : 1100, 560)));
                await WaitSeconds(0.3);
                var spacing = name == "chefes" ? 125f : name == "elites" ? 100f : 64f;
                var x = player.GlobalPosition.X + (spacing * 0.8f);
                var row = 0;
                var spawned = new List<Node2D> { player };
                foreach (var id in ids)
                {
                    var def = GameManager.Db.Monsters[id];
                    Enemy.Preload(def);
                    if (name == "chefes" && row == 0 && spawned.Count == 4)
                    {
                        row = 1;
                        x = player.GlobalPosition.X - 40;
                    }

                    var at = world.CorrectToWalkable(new Vector2(x, player.GlobalPosition.Y - (row * 120)));
                    spawned.Add(combat.SpawnMonsterNow(id, at, chase: false, invasion: false));
                    x += spacing;
                }

                await WaitSeconds(1.2); // nascer
                GameManager.Instance.BlockGameplay("vivencia");
                await WaitSeconds(0.3);
                await Shot($"viv-51-fila-{name}");
                VivMeasureNodes($"fila-{name}", spawned);
                GameManager.Instance.UnblockGameplay("vivencia");
            }

            world.GoMap("guild", 250, 330, instant: true);
            await WaitMapSettled(world);
        }
        finally
        {
            GameManager.Instance.UnblockGameplay("vivencia");
            foreach (var n in extra)
            {
                if (IsInstanceValid(n))
                {
                    n.QueueFree();
                }
            }
        }
    }

    // ------------------------------------------------------------------ 7. Morrer e buscar a Lanterna (jogo novo, sem o bot mexendo nas telas)

    [SmokeScenario("vivencia 7: morrer na Brecha e recuperar a Lanterna", order: 1007)]
    private async Task VivDeath()
    {
        var world = await FreshGame("sword");
        using var listen = new VivListener();
        var state = GameManager.Instance.State;
        var combat = CombatDirector.Instance!;
        var ui = UiOrFail();
        world.GoMap("forge", 1350, 535, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, 0, "fish", 0);
        await WaitUntil(() => combat.AliveMonsters > 0, 8, "onda nascer");
        using (new SpeedUp(TesterSpeedUp))
        {
            await Bot(combat, () => combat.Brecha.Run!.Kills >= 1, 60, "matar 1 para ter algo na bolsa");
        }

        var bag = combat.Brecha.Run?.Bag;
        Viv($"bolsa antes de morrer: {bag?.Lumes} Lumes, mats {string.Join(",", bag?.Materials.Select(kv => $"{kv.Key}×{kv.Value}") ?? [])}; HP máx {state.MaxHp}");
        var hpMax0 = state.MaxHp;
        PressDirection(Vector2.Zero);
        var deathAt = FindPlayer()!.GlobalPosition;
        await WaitUntil(() => ui.Death.IsOpen, 120, "a tela de morte abrir (parado no meio da onda)");
        await WaitSeconds(1.5);
        await Shot("viv-70-tela-de-morte");
        Viv($"tela de morte: \"{ui.Death.Title}\" (na Brecha={ui.Death.InBrecha})");
        await WaitUntil(() => ui.Death.PromptVisible, 8, "prompt da tela de morte");
        await UiPress("ui_accept");
        await WaitUntil(() => !ui.Death.IsOpen, 5, "tela de morte fechar");
        await WaitSeconds(2);
        await WaitMapSettled(world);
        await Shot("viv-71-depois-da-morte");
        Viv($"depois da morte: mapa {world.MapId}; Lanterna perdida={state.HasLostLantern} ({state.Save.LostLantern?.Bairro}); HP {state.Hp}/{state.MaxHp} (antes {hpMax0}); {VivSnapshot()}");
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        await VivTalkMenu(world, ui, "odete");
        world.GoMap("forge", 1350, 535, instant: true);
        await WaitMapSettled(world);
        await EnterBrecha(world, combat, 0, "fish", 0);
        await WaitSeconds(1.5);
        var lantern = combat.Pickups.FirstOrDefault(p => p.Kind == PickupKind.MyLantern && !p.Collected);
        Viv($"de volta ao Mercado: minha Lanterna na sala={lantern is not null} {(lantern is null ? string.Empty : $"em {lantern.GlobalPosition} (morri em {deathAt})")}");
        if (lantern is not null)
        {
            using (new SpeedUp(TesterSpeedUp))
            {
                try
                {
                    await Bot(combat, () => combat.AliveMonsters == 0, 60, "limpar a onda");
                }
                catch (SmokeFailure e)
                {
                    VivProblem($"limpar perto da Lanterna: {e.Message}");
                }

                await VivGo(world, new MapPoint(lantern.GlobalPosition.X, lantern.GlobalPosition.Y), 10, "minha Lanterna", () => FindPlayer()!.FocusedInteractable == lantern.Interaction);
            }

            await Shot("viv-72-lanterna-no-chao");
            await JourneyPress(InputActions.Interact);
            await WaitSeconds(1.2);
            Viv($"Lanterna recuperada={!state.HasLostLantern}; HP {state.Hp}/{state.MaxHp}; {VivSnapshot()}");
            await Shot("viv-73-lanterna-recuperada");
        }

        await FleeRun(world, combat);
    }

    // ------------------------------------------------------------------ 6. Lampiões, flores e bancos (o andar automático não chegou neles)

    [SmokeScenario("vivencia 6: lampiões, flores e bancos da Rua da Forja", order: 1006)]
    private async Task VivLampsAndFlowers()
    {
        var world = await FreshGame("sword");
        using var listen = new VivListener();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        foreach (var spot in world.Dressing.Decor.Where(s => s.Def.Kind is "lamp" or "flower" or "bench").ToList())
        {
            var at = new MapPoint(spot.Def.X, spot.Def.Y);
            var fromHere = world.WalkArea.NearestWalkable(player.GlobalPosition.X, player.GlobalPosition.Y, maxDistance: 8f);
            var path = fromHere is null ? null : WalkPathfinder.FindPathNear(world.WalkArea, fromHere.Value, at, spot.Def.Radius - 6);
            player.Teleport(world.CorrectToWalkable(new Vector2(spot.Def.X, spot.Def.Y + 10)));
            var deadline = Time.GetTicksMsec() + 1500;
            while (player.FocusedInteractable != spot.Interactable && Time.GetTicksMsec() < deadline)
            {
                await NextPhysicsFrame();
            }

            var focus = player.FocusedInteractable;
            var before = VivSnapshot();
            if (focus == spot.Interactable)
            {
                await JourneyPress(InputActions.Interact);
                await WaitSeconds(0.6);
            }

            Viv($"{spot.Def.Kind} ({spot.Def.X},{spot.Def.Y}) caminho do Core {(path is null ? "NÃO" : "sim")}; habilitado {spot.Interactable.Enabled}; foco {(focus == spot.Interactable ? "ok" : $"em '{focus?.Prompt ?? "nada"}'")}; prompt agora '{spot.Interactable.Prompt}'; texto \"{world.Dressing.Lore.Text}\" {VivDiff(before)}");
            if (spot.Def.Kind == "bench")
            {
                await Shot($"viv-60-banco-{spot.Index}");
                PressDirection(Vector2.Down);
                await Frames(12);
                PressDirection(Vector2.Zero);
            }
            else if (spot.Index % 3 == 0)
            {
                await Shot($"viv-60-{spot.Def.Kind}-{spot.Index}");
            }
        }
    }

    // ------------------------------------------------------------------ medidas

    /// <summary>Altura/largura opacas em u de todos os sprites visíveis do mapa (personagens, monstros, objetos), agrupadas por textura.</summary>
    private static readonly Dictionary<AnimatedSprite2D, Texture2D> VivAnimated = [];

    private void VivMeasure(World.World world, string label)
    {
        var sprites = new List<Sprite2D>();
        VivAnimated.Clear();
        CollectSprites(world.Entities, sprites);
        var seen = new HashSet<string>();
        var sb = new StringBuilder();
        foreach (var s in sprites)
        {
            var m = VivSpriteSize(s);
            if (m is null)
            {
                continue;
            }

            var (key, h, w, ppu) = m.Value;
            if (!seen.Add($"{key}:{h:0.0}"))
            {
                continue;
            }

            sb.Append($"\n    {key,-34} alt {h,6:0.0} u  larg {w,6:0.0} u  {ppu,4:0.0} px-de-textura/u  ({VivOwner(s)})");
        }

        foreach (var (an, tex) in VivAnimated)
        {
            var used = tex.GetImage().GetUsedRect();
            var sc = an.GlobalScale.Abs();
            sb.Append(VivAnimLine(an, used, sc));
        }

        Viv($"MEDIDA [{label}] mapa {world.MapId} (pintura 1 px/u){sb}");
    }

    private static string VivAnimLine(AnimatedSprite2D an, Rect2I used, Vector2 sc) =>
        System.Environment.NewLine + $"    {an.Animation + "#" + an.Frame,-34} alt {used.Size.Y * sc.Y,6:0.0} u  larg {used.Size.X * sc.X,6:0.0} u  {1f / sc.Y,4:0.0} px-de-textura/u  ({VivOwner(an)}, quadro animado)";

    private void VivMeasureNodes(string label, IEnumerable<Node> roots)
    {
        var sb = new StringBuilder();
        foreach (var root in roots)
        {
            var sprites = new List<Sprite2D>();
            VivAnimated.Clear();
            CollectSprites(root, sprites);
            foreach (var (an, tex) in VivAnimated)
            {
                var used = tex.GetImage().GetUsedRect();
                var sc = an.GlobalScale.Abs();
                sb.Append(VivAnimLine(an, used, sc));
            }

            foreach (var s in sprites)
            {
                if (VivSpriteSize(s) is { } m && m.H > 4)
                {
                    sb.Append($"\n    {m.Key,-34} alt {m.H,6:0.0} u  larg {m.W,6:0.0} u  {m.Ppu,4:0.0} px-de-textura/u  ({VivOwner(s)})");
                }
            }
        }

        Viv($"MEDIDA [{label}]{sb}");
    }

    private static string VivOwner(Node s)
    {
        for (Node? n = s; n is not null; n = n.GetParent())
        {
            switch (n)
            {
                case Player.Player:
                    return "jogador";
                case Enemy e:
                    return $"monstro {e.Body.Def.Id}{(e.Body.IsBoss ? " (chefe)" : e.Body.Def.Elite ? " (elite)" : string.Empty)}";
                case TownNpc npc:
                    return $"morador {npc.Def.Name}";
                case TownHunter h:
                    return $"caçador {h.Brain.Def.Name}";
            }
        }

        return s.GetParent()?.Name ?? "?";
    }

    private static (string Key, float H, float W, float Ppu)? VivSpriteSize(Sprite2D s)
    {
        if (!s.IsVisibleInTree() || s.Texture is null)
        {
            return null;
        }

        var img = s.Texture.GetImage();
        if (img is null)
        {
            return null;
        }

        var used = s.RegionEnabled ? img.GetRegion((Rect2I)s.RegionRect).GetUsedRect() : img.GetUsedRect();
        var scale = s.GlobalScale.Abs();
        var key = s is AtlasSprite a ? a.Key : System.IO.Path.GetFileNameWithoutExtension(s.Texture.ResourcePath);
        if (string.IsNullOrEmpty(key))
        {
            key = s.Name;
        }

        return (key, used.Size.Y * scale.Y, used.Size.X * scale.X, scale.Y > 0 ? 1f / scale.Y : 0f);
    }

    private static void CollectSprites(Node node, List<Sprite2D> into)
    {
        if (node is Sprite2D s)
        {
            into.Add(s);
        }
        else if (node is AnimatedSprite2D an && an.IsVisibleInTree() && an.SpriteFrames?.GetFrameTexture(an.Animation, an.Frame) is { } tex)
        {
            // mede o quadro atual como um Sprite2D com a mesma escala global (o nó auxiliar não entra na árvore)
            VivAnimated[an] = tex;
        }

        foreach (var c in node.GetChildren())
        {
            CollectSprites(c, into);
        }
    }

    // ------------------------------------------------------------------ estado e eventos

    private static string VivSnapshot()
    {
        var s = GameManager.Instance.State;
        var mats = string.Join(",", s.Save.Materials.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}×{kv.Value}"));
        return $"Lumes {s.Lumes}; poções {s.Save.Potions}; XP {s.Save.Xp}; mochila [{string.Join(",", s.Save.Backpack)}]; mats [{mats}]";
    }

    private static string VivDiff(string before)
    {
        var now = VivSnapshot();
        return now == before ? string.Empty : $"Δ {before}  ⟶  {now}";
    }

    /// <summary>Registra toasts, notas, chat, missões, Selo e nível enquanto o cenário roda.</summary>
    private sealed class VivListener : IDisposable
    {
        public VivListener()
        {
            var bus = EventBus.Instance;
            bus.ToastRequested += OnToast;
            bus.NoteRequested += OnNote;
            bus.ChatMessage += OnChat;
            bus.QuestCompleted += OnQuest;
            bus.LevelChanged += OnLevel;
            bus.RankChanged += OnRank;
            bus.MonsterPartBroken += OnPart;
        }

        public void Dispose()
        {
            var bus = EventBus.Instance;
            bus.ToastRequested -= OnToast;
            bus.NoteRequested -= OnNote;
            bus.ChatMessage -= OnChat;
            bus.QuestCompleted -= OnQuest;
            bus.LevelChanged -= OnLevel;
            bus.RankChanged -= OnRank;
            bus.MonsterPartBroken -= OnPart;
        }

        private static void OnToast(string message) => Viv($"TOAST \"{message.Replace("\n", " / ", StringComparison.Ordinal)}\"");

        private static void OnNote(string message, string icon) => Viv($"NOTA \"{message}\"");

        private static void OnChat(string who, string rank, string text) => Viv($"CHAT {who} [{rank}]: \"{text}\"");

        private static void OnQuest(string questId) => Viv($"MISSÃO cumprida: {questId} → próxima: {GameManager.Instance.State.CurrentQuest?.Name}");

        private static void OnLevel(int previous, int level) => Viv($"NÍVEL {previous} → {level}");

        private static void OnRank(Rank previous, Rank rank) => Viv($"SELO {previous} → {rank}");

        private static void OnPart(string monsterId, string partId, string partName, bool boss) => Viv($"PARTE quebrada: {monsterId}/{partName}{(boss ? " (chefe)" : string.Empty)}");
    }

    /// <summary>Soma o dano recebido e conta as vezes em que o HP passou de 35% para baixo (onde o bot do smoke enche a vida).</summary>
    private sealed class VivDamage : IDisposable
    {
        private int _taken;
        private int _hits;
        private int _nearDeath;
        private int _potions;
        private int _lastPotions = GameManager.Instance.State.Save.Potions;

        public VivDamage()
        {
            EventBus.Instance.PlayerDamaged += OnDamaged;
            EventBus.Instance.PotionsChanged += OnPotions;
        }

        public void Dispose()
        {
            EventBus.Instance.PlayerDamaged -= OnDamaged;
            EventBus.Instance.PotionsChanged -= OnPotions;
        }

        public override string ToString() => $"dano recebido {_taken} em {_hits} golpes; abaixo de 35% {_nearDeath}× (o bot enche a vida aí); poções usadas {_potions}";

        private void OnDamaged(int amount, int currentHp, int maxHp)
        {
            _taken += amount;
            _hits++;
            if (currentHp < maxHp * 0.35)
            {
                _nearDeath++;
            }
        }

        private void OnPotions(int count)
        {
            if (count < _lastPotions)
            {
                _potions += _lastPotions - count;
            }

            _lastPotions = count;
        }
    }
}
