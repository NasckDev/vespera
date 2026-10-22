using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Cenários do mundo: visita todos os mapas pelas saídas reais (ida e volta), porta e fuga das Brechas, interagíveis,
/// câmera e sprites. Para cada trecho, o caminho é calculado no Core (<see cref="WalkPathfinder"/>, prova que a
/// chegada se conecta à saída); o jogador é colocado nos últimos passos e anda de verdade (input) até a saída.
/// </summary>
public partial class SmokeTest
{
    private const int WalkLastSteps = 8; // passos de 4 u andados com input real antes de cada saída

    [SmokeScenario("mundo: visita todos os mapas pelas saídas", order: 10)]
    private async Task VisitAllMapsThroughExits()
    {
        var world = FindWorld();
        var catalog = World.World.Catalog;
        world.GoMap("guild", 250, 560, instant: true);
        await WaitMapSettled(world);
        await ShotMap(world);

        // Guilda → Rua da Forja → Estalagem → Rua da Forja → Guilda → Rua da Forja (todas as saídas por retângulo).
        await TravelThroughExit(world, "forge");
        await ShotMap(world);
        await TravelThroughExit(world, "inn");
        await ShotMap(world);
        await TravelThroughExit(world, "forge");
        await TravelThroughExit(world, "guild");
        await TravelThroughExit(world, "forge");

        // Lojas: a placa "Rua dos Ofícios" pede o diálogo; o diálogo (outra frente) leva ao spawn da loja.
        foreach (var shop in new[] { "shop_arm", "shop_mag", "shop_alq", "shop_rel" })
        {
            var (kind, id) = await InteractWith(world, "oficios");
            Require(kind == "oficiosDialog" && id == "oficios", $"placa dos Ofícios emitiu ({kind}, {id})");
            var spawn = catalog[shop].Spawn;
            world.GoMap(shop, spawn.X, spawn.Y);
            await WaitMapSettled(world);
            Require(world.MapId == shop, $"esperava {shop}, está em {world.MapId}");
            await ShotMap(world);
            await TravelThroughExit(world, "forge");
        }

        Require(world.GetInteractable("portal0") is { Enabled: false }, "portal sem Brecha aberta deve começar desligado");
    }

    [SmokeScenario("mundo: Brechas têm porta, fuga e Núcleo", order: 11)]
    private async Task VisitDungeons()
    {
        var world = FindWorld();
        var catalog = World.World.Catalog;
        string[] dungeons = ["fish2", "crypt2", "mine", "garden", "cath"];
        for (var i = 0; i < dungeons.Length; i++)
        {
            var id = dungeons[i];
            var map = catalog[id];
            world.GoMap(id, map.Spawn.X, map.Spawn.Y); // como o portal faria
            await WaitMapSettled(world);
            Require(world.MapId == id, $"esperava {id}, está em {world.MapId}");
            await ShotMap(world);

            var spawn = new MapPoint(map.Spawn.X, map.Spawn.Y);
            if (map.Door is { } door)
            {
                Require(WalkPathfinder.FindPathInto(world.WalkArea, spawn, door) is not null, $"{id}: porta inalcançável do spawn");
            }

            foreach (var point in new[] { map.BossHere, map.ExitAt })
            {
                if (point is { } p)
                {
                    Require(WalkPathfinder.FindPathNear(world.WalkArea, spawn, p, 26) is not null, $"{id}: ({p.X}, {p.Y}) inalcançável");
                }
            }

            if (i == 0)
            {
                // Porta liberada → Câmara do Núcleo → fuga do Núcleo.
                world.DoorOpen = true;
                var doorSignal = new List<string>();
                void OnDoor(string mapId) => doorSignal.Add(mapId);
                EventBus.Instance.DungeonDoorEntered += OnDoor;
                try
                {
                    await TravelInto(world, map.Door!.Value, () => world.MapId == World.World.CoreMapId);
                }
                finally
                {
                    EventBus.Instance.DungeonDoorEntered -= OnDoor;
                }

                Require(doorSignal.Count == 1 && doorSignal[0] == id, $"DungeonDoorEntered: [{string.Join(",", doorSignal)}]");
                await WaitMapSettled(world);
                RequireStandingAt(world, catalog["core"].Spawn);
                await ShotMap(world);
                await EscapeAndLeave(world, portalSpot: 0);
                world.GoMap(id, map.Spawn.X, map.Spawn.Y);
                await WaitMapSettled(world);
            }

            await EscapeAndLeave(world, portalSpot: i % 2);
        }
    }

    [SmokeScenario("mundo: interagíveis emitem pedido no EventBus", order: 12)]
    private async Task InteractablesEmitRequests()
    {
        var world = FindWorld();
        world.GoMap("guild", 250, 330, instant: true); // "Continuar" da v2
        await WaitMapSettled(world);
        var (kind, id) = await InteractWith(world, "odete");
        Require(kind == "talkClerk" && id == "odete", $"Odete emitiu ({kind}, {id})");
        Require(FindPlayer()!.FocusedInteractable?.Prompt == "Falar com Odete", "prompt da Odete");
        await Shot("interacao-odete");

        world.GoMap("inn", 250, 520, instant: true);
        await WaitMapSettled(world);
        (kind, id) = await InteractWith(world, "rest");
        Require(kind == "restDialog" && id == "rest", $"Descansar emitiu ({kind}, {id})");
    }

    [SmokeScenario("mundo: câmera e sprites da v2", order: 13)]
    private async Task CameraAndSprites()
    {
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");

        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        // D39: zoom 1 e escala inteira da janela: a vista é a viewport (640×360 u numa tela 16:9) em qualquer mapa.
        var viewport = world.GetViewportRect().Size;
        var baseHeight = viewport.Y;
        Require(Mathf.IsEqualApprox(world.Camera.Zoom.X, 1f), $"zoom deve ser 1, é {world.Camera.Zoom.X}");
        Require(Mathf.IsEqualApprox(world.Camera.VisibleSize.Y, baseHeight, 0.5f), $"Rua da Forja deve mostrar {baseHeight} u de altura, mostra {world.Camera.VisibleSize.Y}");
        var window = DisplayServer.WindowGetSize();
        if (DisplayServer.GetName() != "headless" && window.X * 9 == window.Y * 16)
        {
            Require(Mathf.IsEqualApprox(viewport.Y, CameraFraming.BaseHeight, 0.5f), $"janela 16:9 ({window}) deveria mostrar 360 u, mostra {viewport.Y}");
            Require(Mathf.IsEqualApprox(world.GetViewport().GetStretchTransform().X.X, CameraFraming.IntegerScale(window.X, window.Y)), "escala da janela deveria ser inteira");
        }


        world.GoMap("guild", 250, 560, instant: true);
        await WaitMapSettled(world);
        var view = world.Camera.VisibleSize;
        Require(Mathf.IsEqualApprox(view.Y, baseHeight, 0.5f), $"Guilda deve mostrar {baseHeight} u de altura, mostra {view.Y}");
        Require(view.X >= CameraFraming.MinVisibleWidth - 0.5f, $"largura visível {view.X} < 380");
        // Guilda (500 u) é mais estreita que a vista: centralizada com offset negativo.
        Require(Mathf.IsEqualApprox(world.Camera.Position.X, (500f - view.X) / 2f, 0.5f), $"Guilda deveria centralizar em x: câmera em {world.Camera.Position.X}");

        world.Camera.Shake(8);
        await Frames(3);
        Require(world.Camera.CurrentShake > 0f && world.Camera.CurrentShake < 8f, $"tremor deveria decair, está {world.Camera.CurrentShake}");
        EventBus.Instance.EmitSignal(EventBus.SignalName.CameraShakeRequested, 6f);
        Require(world.Camera.CurrentShake >= 6f, "CameraShakeRequested não chegou à câmera");
        world.Camera.StopShake();

        Require(player.Sprite.Key == "c_sword_front", $"pose inicial deveria ser c_sword_front, é {player.Sprite.Key}");
        // D41: escala do atlas × characterScale (rules.json)
        Require(Mathf.IsEqualApprox(player.Sprite.Scale.X, WorldScale.Character / 5.0025f, 0.0001f), $"escala characterScale/k errada: {player.Sprite.Scale.X}");
        await Hold(InputActions.MoveLeft, 10);
        Require(player.Sprite.Key == "c_sword_side" && player.Body.Scale.X < 0, "andar para a esquerda usa c_sword_side espelhado");
        await Hold(InputActions.MoveUp, 10);
        Require(player.Sprite.Key == "c_sword_back", $"andar para cima usa c_sword_back, é {player.Sprite.Key}");
        await WaitSeconds(0.6);
        Require(player.Sprite.Key == "c_sword_front", "parado > 0,45 s volta a olhar para baixo");

        var before = player.GlobalPosition;
        await Hold(InputActions.MoveDown, 30);
        var speed = (player.GlobalPosition.Y - before.Y) / (30f / Engine.PhysicsTicksPerSecond);
        Require(speed > 70f && speed < 80f, $"velocidade deveria ser ~78 u/s, foi {speed:0.0}");

        Require(AtlasLibrary.TryGet("cnev_f", out var loose), "sprite solto cnev_f (assets/src) não carregou");
        Require(Mathf.IsEqualApprox(loose.Density, 3f), $"densidade do cnev_f deveria ser 3, é {loose.Density}");
        await Shot("jogador-guilda");
    }

    // --- utilidades ---

    /// <summary>Anda do ponto atual até a saída para <paramref name="to"/> e confere a chegada.</summary>
    private async Task TravelThroughExit(World.World world, string to)
    {
        var from = world.MapId;
        MapExit? exit = null;
        foreach (var e in world.Map.Exits)
        {
            if (e.To == to)
            {
                exit = e;
                break;
            }
        }

        Require(exit is not null, $"{from} não tem saída para {to}");
        var loads = world.MapLoads;
        await TravelInto(world, exit!.Area, () => world.MapLoads > loads);
        await WaitMapSettled(world);
        Require(world.MapId == to, $"saída de {from} deveria levar a {to}, levou a {world.MapId}");
        RequireStandingAt(world, exit.At);
    }

    /// <summary>Calcula o caminho até <paramref name="target"/>, coloca o jogador perto do fim e anda com input até <paramref name="done"/>.</summary>
    private async Task TravelInto(World.World world, MapRect target, Func<bool> done)
    {
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var path = WalkPathfinder.FindPathInto(world.WalkArea, start, target)
            ?? throw new SmokeFailure($"{world.MapId}: sem caminho de ({start.X:0}, {start.Y:0}) até {target}");
        await WalkPath(world, path, Math.Max(0, path.Count - 1 - WalkLastSteps), done);
    }

    private async Task WalkPath(World.World world, IReadOnlyList<MapPoint> path, int startIndex, Func<bool> done, double timeoutSeconds = 8)
    {
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var mapId = world.MapId;
        player.Teleport(new Vector2(path[startIndex].X, path[startIndex].Y));
        await NextPhysicsFrame();

        var last = path[^1];
        var beforeLast = path.Count > 1 ? path[^2] : new MapPoint(last.X, last.Y - 4);
        var finalDirection = new Vector2(last.X - beforeLast.X, last.Y - beforeLast.Y).Normalized();
        var next = startIndex + 1;
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        try
        {
            while (!done())
            {
                Require(Time.GetTicksMsec() < deadline, $"{mapId}: timeout andando até ({last.X}, {last.Y}); jogador em {player.GlobalPosition}");
                var p = player.GlobalPosition;
                if (world.MapId == mapId && !world.IsTransitioning)
                {
                    Require(world.WalkArea.IsWalkable(p.X, p.Y), $"{mapId}: jogador em área bloqueada em {p}");
                }

                while (next < path.Count && p.DistanceTo(new Vector2(path[next].X, path[next].Y)) < 1.5f)
                {
                    next++;
                }

                var direction = next < path.Count ? (new Vector2(path[next].X, path[next].Y) - p).Normalized() : finalDirection;
                PressDirection(direction);
                await NextPhysicsFrame();
            }
        }
        finally
        {
            PressDirection(Vector2.Zero);
        }
    }

    private static void PressDirection(Vector2 d)
    {
        SetAxis(InputActions.MoveRight, d.X);
        SetAxis(InputActions.MoveLeft, -d.X);
        SetAxis(InputActions.MoveDown, d.Y);
        SetAxis(InputActions.MoveUp, -d.Y);
    }

    private static void SetAxis(StringName action, float strength)
    {
        if (strength > 0.001f)
        {
            Input.ActionPress(action, strength);
        }
        else
        {
            Input.ActionRelease(action);
        }
    }

    /// <summary>Anda até a fuga da dungeon, confere o pedido de fuga e sai pelo portal (como "Fugir para Lumeira").</summary>
    private async Task EscapeAndLeave(World.World world, int portalSpot)
    {
        var map = world.Map;
        var requests = new List<string>();
        void OnEscape(string mapId) => requests.Add(mapId);
        EventBus.Instance.DungeonEscapeRequested += OnEscape;
        try
        {
            await TravelInto(world, map.Back!.Value, () => requests.Count > 0);
        }
        finally
        {
            EventBus.Instance.DungeonEscapeRequested -= OnEscape;
        }

        Require(requests[0] == map.Id, $"fuga pedida para {requests[0]}, esperava {map.Id}");
        var foot = FindPlayer()!.GlobalPosition;
        Require(world.WalkArea.IsWalkable(foot.X, foot.Y), $"{map.Id}: empurrão da fuga deixou o jogador em área bloqueada ({foot})");

        world.LeaveDungeon(portalSpot);
        await WaitMapSettled(world);
        Require(world.MapId == World.World.CityMapId, $"fuga deveria voltar à Rua da Forja, está em {world.MapId}");
        var spot = World.World.Catalog[World.World.CityMapId].PortalSpots[portalSpot];
        var player = FindPlayer()!.GlobalPosition;
        Require(world.WalkArea.CanStand(player.X, player.Y), $"volta da Brecha (spot {portalSpot}) em ponto onde não dá para ficar: {player}");
        // Spot 1 cai em célula bloqueada na v2 (1174, 558) e é corrigido ~26 u para cima (DECISIONS D25).
        Require(player.DistanceTo(new Vector2(spot.X, spot.Y + MapDef.PortalReturnOffset.Y)) < 32f, $"volta da Brecha longe do portal: {player}");
    }

    /// <summary>Anda até o interagível, aperta interagir e devolve o (kind, id) emitido.</summary>
    private async Task<(string Kind, string Id)> InteractWith(World.World world, string interactableId)
    {
        var it = world.GetInteractable(interactableId) ?? throw new SmokeFailure($"{world.MapId}: sem interagível {interactableId}");
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, new MapPoint(it.Position.X, it.Position.Y), it.Radius - 2)
            ?? throw new SmokeFailure($"{world.MapId}: {interactableId} inalcançável");
        await WalkPath(world, path, Math.Max(0, path.Count - 1 - WalkLastSteps), () => player.FocusedInteractable == it);

        (string, string)? got = null;
        void OnRequest(string kind, string id) => got = (kind, id);
        EventBus.Instance.InteractionRequested += OnRequest;
        try
        {
            // ActionPress não gera InputEvent; o Player lê "interact" em _UnhandledInput, como uma tecla real.
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
            await NextPhysicsFrame();
        }
        finally
        {
            EventBus.Instance.InteractionRequested -= OnRequest;
        }

        await DismissUi(); // a Interface abre o diálogo do interagível; fecha para o jogador voltar a andar

        Require(got is not null, $"interagir com {interactableId} não emitiu InteractionRequested");
        return got!.Value;
    }

    private async Task WaitMapSettled(World.World world)
    {
        await WaitUntil(() => !world.IsTransitioning && world.FadeAlpha <= 0f, 5, $"fim do fade em {world.MapId}");
        await NextPhysicsFrame();
    }

    private void RequireStandingAt(World.World world, MapPoint expected)
    {
        var p = FindPlayer()!.GlobalPosition;
        Require(world.WalkArea.CanStand(p.X, p.Y), $"{world.MapId}: chegada em ponto onde não dá para ficar ({p})");
        Require(p.DistanceTo(new Vector2(expected.X, expected.Y)) < 16f, $"{world.MapId}: chegada em {p}, esperava ~({expected.X}, {expected.Y})");
    }

    private async Task ShotMap(World.World world) => await Shot($"mapa-{world.MapId}");
}
