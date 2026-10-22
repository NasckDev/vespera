using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.World;
using Vespera.UI;
using Vespera.UI.Title;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// Jornada da v0.1 (integração das 8 frentes): joga o fluxo inteiro com input simulado, do título ao Continuar.
/// Título → novo jogo → história → classe → Guilda (tutorial da Odete) → Rua da Forja a pé (escadaria, ponte grande,
/// escada de madeira e cais) → véspera abre o portal (relógio acelerado só aqui) → diálogo do portal → Brecha → ondas
/// → porta → Núcleo → chefe (atalho só no HP dele) → portal de saída → Forja do Brás (melhorar a arma) → salvar →
/// voltar ao título → Continuar. Atalhos de teste, todos registrados no log: relógio acelerado, HP do chefe e, se a
/// caçada não pagou a melhoria, o que faltar entra na carteira antes de falar com o Brás. Prints em cada etapa
/// (<c>--smoke-shots</c>, sem --headless).
/// </summary>
public partial class SmokeTest
{
    private const int JourneySpeedUp = TesterSpeedUp; // TesterWalkPath conta o tempo nessa escala

    [SmokeScenario("jornada v0.1: título → classe → Rua da Forja → véspera → Brecha → chefe → forja → salvar → continuar", order: 990)]
    private async Task JourneyV01()
    {
        await WaitForScene("World", 10);
        DirAccess.RemoveAbsolute(SaveService.Instance.SavePath);
        GameManager.Instance.ChangeScene(TitleScreen.ScenePath);
        await WaitForScene("Title", 10);
        try
        {
            await JourneyBody();
        }
        finally
        {
            PressDirection(Vector2.Zero);
            if (GetTree().CurrentScene?.Name != "World")
            {
                GameManager.Instance.ChangeScene(TitleScreen.WorldScenePath);
                await WaitForScene("World", 10);
            }

            if (CityLife.Instance is { } c)
            {
                c.TimeScale = 1f;
                c.SetSimulationEnabled(false);
                c.Brechas.Reset();
            }
        }
    }

    private async Task JourneyBody()
    {
        // 1. Título → novo jogo → história → classe
        var title = (TitleScreen)GetTree().CurrentScene;
        Require(title.ContinueRow.Disabled && title.NewGameRow.HasFocus(), "sem save: Continuar desabilitado e foco em Novo jogo");
        await WaitSeconds(0.3);
        await Shot("jornada-01-titulo");
        await UiPress("ui_accept");
        await WaitUntil(() => title.Stack.Top == title.Story, 3, "história abrir");
        await WaitSeconds(0.4);
        await Shot("jornada-02-historia");
        for (var i = 0; i < 8 && title.Stack.Top != title.ClassSelect; i++)
        {
            await UiPress("ui_accept");
            await WaitSeconds(0.08);
        }

        await WaitUntil(() => title.Stack.Top == title.ClassSelect, 3, "escolha de classe abrir");
        await WaitSeconds(0.3);
        for (var i = 0; i < 10 && title.ClassSelect.PlayerName.Length > 0; i++)
        {
            await UiKey(Key.Backspace);
        }

        title.ClassSelect.Type("Alva");
        for (var i = 0; i < 6 && title.ClassSelect.SelectedClass != "sword"; i++)
        {
            await UiPress("ui_left");
        }

        Require(title.ClassSelect.SelectedClass == "sword", $"classe escolhida: {title.ClassSelect.SelectedClass}");
        await Shot("jornada-03-classe");
        await UiPress("ui_accept");
        await WaitForScene("World", 10);

        var state = GameManager.Instance.State;
        var world = FindWorld();
        var player = FindPlayer()!;
        var ui = UiOrFail();
        Require(state.Save.Name == "Alva" && state.Save.ClassId == "sword", $"jogo novo: {state.Save.Name} ({state.Save.ClassId})");
        Require(world.MapId == "guild" && player.ClassId == "sword", $"começa na Guilda com a Lâmina: {world.MapId}, {player.ClassId}");

        // 2. Tutorial da Odete (3 falas)
        await WaitUntil(() => ui.Dialog.IsOpen, 4, "tutorial da Odete");
        await WaitSeconds(0.4);
        await Shot("jornada-04-tutorial");
        for (var line = 0; line < 6 && ui.Dialog.IsOpen; line++)
        {
            await SkipTyping(ui);
            await UiPress("interact");
            await WaitSeconds(0.2);
        }

        await WaitUntil(() => !ui.Dialog.IsOpen, 3, "tutorial fechar");
        await Shot("jornada-05-guilda");

        // A simulação da cidade nasce desligada no smoke (api-cidade); a jornada joga com ela ligada.
        var city = CityLife.Instance ?? throw new SmokeFailure("World sem CityLife");
        city.Brechas.Reset();
        city.SetSimulationEnabled(true);
        using var speed = new SpeedUp(JourneySpeedUp);

        // 3. Guilda → Rua da Forja a pé
        var toForge = world.Map.Exits.First(e => e.To == "forge");
        await JourneyWalkInto(world, toForge.Area, () => world.MapId == "forge", "porta da Guilda");
        await WaitMapSettled(world);
        Require(world.MapId == "forge", $"deveria chegar à Rua da Forja, está em {world.MapId}");
        await Shot("jornada-06-rua-da-forja");
        (string Label, float X, float Y, string? Shot)[] route =
        [
            ("patamar da guilda", 1497, 340, null),
            ("escadaria da guilda", 1497, 490, "jornada-07-escadaria"),
            ("praça da guilda", 1350, 535, null),
            ("cabeceira leste da ponte grande", 1010, 520, null),
            ("ponte grande", 850, 505, "jornada-08-ponte-grande"),
            ("cabeceira oeste da ponte grande", 680, 505, null),
            ("pé da escada de madeira", 585, 425, null),
            ("cais oeste", 700, 360, "jornada-09-cais"),
            ("pé da escada de madeira (volta)", 585, 425, null),
            ("largo do portal", 470, 500, "jornada-10-largo"),
        ];
        foreach (var (label, x, y, shot) in route)
        {
            await JourneyWalkNear(world, new MapPoint(x, y), 8f, label);
            if (shot is not null)
            {
                await Shot(shot);
            }
        }

        // 4. Véspera: o relógio corre acelerado (só aqui) até a cidade abrir uma Brecha que o Selo permite
        BrechaInstance? brecha = null;
        bool Enterable(BrechaInstance b) => b.Status == BrechaStatus.Open && state.Brechas.CanEnter(b.Rank, state.RankIndex);
        try
        {
            city.TimeScale = 12f;
            await WaitUntil(() => (brecha = city.Brechas.Brechas.FirstOrDefault(Enterable)) is not null, 40, "a véspera abrir uma Brecha do meu Selo (relógio ×12)");
        }
        finally
        {
            city.TimeScale = 1f;
        }

        var (portalSpot, theme, rank) = (brecha!.Spot, brecha.ThemeId, brecha.Rank);
        Log.Info($"jornada: Brecha aberta no portal {portalSpot} ({theme}, rank {rank}) às {state.Save.ClockMinutes / 60:0}h; abertas: {string.Join(", ", city.Brechas.Brechas.Select(b => $"{b.ThemeId}/r{b.Rank}/{b.Status}"))}");
        await WaitSeconds(1.2); // animação do portal abrindo
        await Shot("jornada-11-portal-aberto");

        // 5. Diálogo do portal → Entrar
        var portal = world.GetInteractable($"portal{portalSpot}") ?? throw new SmokeFailure($"sem portal{portalSpot}");
        await JourneyWalkNear(world, new MapPoint(portal.Position.X, portal.Position.Y), portal.Radius - 6, $"portal{portalSpot}", () => player.FocusedInteractable == portal);
        await JourneyPress(InputActions.Interact);
        await WaitUntil(() => ui.Dialog.IsOpen, 3, "diálogo do portal");
        await SkipTyping(ui);
        await Shot("jornada-12-dialogo-portal");
        await ChooseOption(ui, "Entrar na Brecha");
        var dungeon = GameManager.Db.Themes[theme].Map;
        await WaitUntil(() => world.MapId == dungeon, 8, $"carregar a Brecha {dungeon}");
        await WaitMapSettled(world);
        var combat = CombatDirector.Instance ?? throw new SmokeFailure("sem CombatDirector");
        Require(combat.Brecha.InBrecha, "incursão ativa depois do portal");
        await WaitSeconds(0.6);
        await Shot("jornada-13-brecha");

        // 6. Ondas → porta
        await Bot(combat, () => world.DoorOpen, 150, "limpar as ondas", shotAt: "jornada-14-onda");
        await Shot("jornada-15-porta-aberta");
        var door = world.Map.Door ?? throw new SmokeFailure($"{world.MapId} sem porta");
        await JourneyWalkInto(world, door, () => world.MapId == World.World.CoreMapId, "porta do Núcleo");
        await WaitMapSettled(world);

        // 7. Chefe (atalho: só o HP dele)
        Enemies.Enemy? boss = null;
        await WaitUntil(() => (boss = FindBoss(combat)) is not null, 6, "chefe nascer");
        await WaitUntil(() => boss!.Body.SpawnT <= 0, 4, "chefe terminar de nascer");
        await WaitSeconds(0.3);
        await Shot("jornada-16-chefe");
        boss!.Body.Hp = Math.Min(boss.Body.Hp, 60);
        Log.Info("jornada: atalho de teste — HP do chefe reduzido para 60");
        await Bot(combat, () => boss.Body.Dead, 90, "derrotar o chefe");
        var lumesBag = combat.Brecha.Run?.Bag.Lumes ?? 0;
        var exit = await WaitForExitPortal(combat);
        await WaitSeconds(0.4);
        await Shot("jornada-17-portal-saida");
        await JourneyWalkNear(world, new MapPoint(exit.GlobalPosition.X, exit.GlobalPosition.Y), 10f, "portal de saída", () => player.FocusedInteractable == exit.Interaction);
        await JourneyPress(InputActions.Interact);
        await WaitUntil(() => world.MapId == World.World.CityMapId, 6, "voltar à Rua da Forja");
        await WaitMapSettled(world);
        Require(combat.Brecha.Run is null && state.Save.BrechasClosed >= 1, $"Brecha fechada ({state.Save.BrechasClosed})");
        Log.Info($"jornada: voltou com {lumesBag} Lumes na bolsa; carteira {state.Lumes}");
        await WaitSeconds(0.5);
        await Shot("jornada-18-volta");

        // 8. Forja do Brás: melhorar a Lâmina
        var levelBefore = state.Save.WeaponLevel("sword");
        if (state.Upgrades.WeaponUpgrade("sword", levelBefore) is { } cost && !state.CanPay(cost))
        {
            var missing = $"{Math.Max(0, cost.Lumes - state.Lumes)} Lumes";
            state.AddLumes(Math.Max(0, (int)Math.Ceiling(cost.Lumes * 1.4) - state.Lumes));
            foreach (var (mat, n) in cost.Mats)
            {
                var have = state.MaterialCount(mat);
                if (have < n)
                {
                    missing += $", {n - have} {mat}";
                    state.AddMaterial(mat, n - have);
                }
            }

            Log.Info($"jornada: atalho de teste — a caçada não pagou a melhoria; faltava {missing}");
        }

        var bras = world.GetInteractable("bras") ?? throw new SmokeFailure("sem o Brás");
        await JourneyWalkNear(world, new MapPoint(bras.Position.X, bras.Position.Y), bras.Radius - 8, "Forja do Brás", () => player.FocusedInteractable == bras);
        await JourneyPress(InputActions.Interact);
        await WaitUntil(() => ui.Dialog.IsOpen, 3, "diálogo do Brás");
        await SkipTyping(ui);
        await Shot("jornada-19-forja");
        await ChooseOption(ui, "Melhorar armas");
        await SkipTyping(ui);
        var upgrade = ui.Dialog.Options.FirstOrDefault(o => o.Label.StartsWith("Lâmina", StringComparison.Ordinal))
                      ?? throw new SmokeFailure($"sem a Lâmina na forja: {string.Join(" | ", ui.Dialog.Options.Select(o => o.Label))}");
        await Shot("jornada-20-melhorar");
        await ChooseOption(ui, upgrade.Label);
        await WaitSeconds(0.4);
        Require(state.Save.WeaponLevel("sword") == levelBefore + 1, $"a Lâmina deveria ir a +{levelBefore + 1}, está +{state.Save.WeaponLevel("sword")}");
        await Shot("jornada-21-melhorada");
        ui.CloseAll();
        await WaitSeconds(0.3);

        // 9. Salvar pela pausa e voltar ao título
        var lumes = state.Lumes;
        var weaponLevel = state.Save.WeaponLevel("sword");
        await JourneyPress(InputActions.Pause);
        await WaitUntil(() => ui.Pause.IsOpen, 2, "pausa");
        await WaitSeconds(0.25);
        await Shot("jornada-22-pausa");
        var quit = JourneyRowIndex(ui.Pause.Rows, UiText.PauseQuit);
        var visited = new List<int>();
        for (var guard = 0; guard < 12 && !ui.Pause.Rows[quit].HasFocus(); guard++)
        {
            await QaKeyTap(Key.Down); // tecla de verdade (↓), como o cenário de QA da pausa
            await WaitSeconds(0.05);
            visited.Add(ui.Pause.Rows.ToList().FindIndex(r => r.HasFocus()));
        }

        Log.Info($"jornada: foco na pausa passou por [{string.Join(",", visited)}]");

        Require(ui.Pause.Rows[quit].HasFocus(), $"foco em 'Voltar ao menu principal' (linha {quit}; foco: {GetViewport().GuiGetFocusOwner()?.Name})");
        await QaKeyTap(Key.Enter);
        await WaitForScene("Title", 10);

        // 10. Continuar
        title = (TitleScreen)GetTree().CurrentScene;
        Require(!title.ContinueRow.Disabled && title.ContinueRow.HasFocus(), "com save, Continuar habilitado e com foco");
        await WaitSeconds(0.3);
        await Shot("jornada-23-titulo-continuar");
        await UiPress("ui_accept");
        await WaitForScene("World", 10);
        state = GameManager.Instance.State;
        Require(state.Save.Name == "Alva" && state.Lumes == lumes && state.Save.WeaponLevel("sword") == weaponLevel,
            $"Continuar: {state.Save.Name}, {state.Lumes} Lumes (esperava {lumes}), Lâmina +{state.Save.WeaponLevel("sword")} (esperava +{weaponLevel})");
        Require(state.Save.BrechasClosed >= 1, "a Brecha fechada ficou no save");
        Require(FindPlayer()!.ClassId == "sword", "o caçador volta com a Lâmina");
        await WaitSeconds(0.4);
        await Shot("jornada-24-continuar");
    }

    /// <summary>Aperta e solta como evento de input (passa por _Input → GUI → _UnhandledInput, como uma tecla).</summary>
    private async Task JourneyPress(StringName action)
    {
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = true });
        await Frames(2);
        GetViewport().PushInput(new InputEventAction { Action = action, Pressed = false });
        await Frames(2);
    }

    private static int JourneyRowIndex(IReadOnlyList<UI.Widgets.SelectableRow> rows, string label)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].FindChildren("*", nameof(Label), true, false).OfType<Label>().Any(l => l.Text.Trim() == label))
            {
                return i;
            }
        }

        throw new SmokeFailure($"linha '{label}' não existe");
    }

    /// <summary>Anda com input da posição atual até perto de <paramref name="target"/> (caminho do Core).</summary>
    private async Task JourneyWalkNear(World.World world, MapPoint target, float radius, string what, Func<bool>? done = null)
    {
        var player = FindPlayer()!;
        var p = player.GlobalPosition;
        var start = world.WalkArea.NearestWalkable(p.X, p.Y, maxDistance: 8f) ?? new MapPoint(p.X, p.Y);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, target, MathF.Max(2f, radius))
                   ?? throw new SmokeFailure($"{world.MapId}: sem caminho a pé até {what} ({target.X:0},{target.Y:0}) de ({p.X:0},{p.Y:0})");
        var end = new Vector2(path[^1].X, path[^1].Y);
        var error = await TesterWalkPath(world, path, done ?? (() => player.GlobalPosition.DistanceTo(end) < 3f), (path.Count * 4f / 78f * 2f) + 6f);
        Require(error is null, $"{world.MapId}: andando até {what}: {error}");
        PressDirection(Vector2.Zero);
        await Frames(4);
    }

    /// <summary>Anda com input até entrar no retângulo (saída, porta) e espera <paramref name="done"/>.</summary>
    private async Task JourneyWalkInto(World.World world, MapRect target, Func<bool> done, string what)
    {
        var player = FindPlayer()!;
        var p = player.GlobalPosition;
        var start = world.WalkArea.NearestWalkable(p.X, p.Y, maxDistance: 8f) ?? new MapPoint(p.X, p.Y);
        var path = WalkPathfinder.FindPathInto(world.WalkArea, start, target)
                   ?? throw new SmokeFailure($"{world.MapId}: sem caminho a pé até {what} de ({p.X:0},{p.Y:0})");
        var error = await TesterWalkPath(world, path, done, (path.Count * 4f / 78f * 2f) + 8f);
        Require(error is null, $"{world.MapId}: andando até {what}: {error}");
        PressDirection(Vector2.Zero);
    }
}
