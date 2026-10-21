using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.Testing;

/// <summary>
/// Cenário da Rua da Forja (frente B4b): anda com input real, do começo ao fim de cada trecho, por TODAS as escadas,
/// pontes, cais e portas de loja do layout novo (<c>assets/maps/city/layout_forge.json</c>), com prints de cada região.
/// As balsas ainda não existem no jogo (o layout só as declara como decor <c>boat</c> com <c>to</c>): aqui a travessia é
/// simulada com um teleporte para o destino da balsa, e o cais de destino precisa ter chão onde ficar de pé.
/// </summary>
public partial class SmokeTest
{
    private abstract record ForgeLeg(string Label);

    /// <summary>Anda até perto de (X, Y); com <c>Shot</c>, tira o print da região ao chegar.</summary>
    private sealed record ForgeWalk(string Label, float X, float Y, string? Shot = null) : ForgeLeg(Label);

    /// <summary>Anda até a balsa do layout com este rótulo (decor <c>boat</c>) e atravessa até o <c>to</c> dela.</summary>
    private sealed record ForgeBoat(string Label, string? Shot = null) : ForgeLeg(Label);

    /// <summary>Entra na loja pela porta da rua, sai pela porta da loja e confere que voltou para a porta certa.</summary>
    private sealed record ForgeShop(string Label, string Shop) : ForgeLeg(Label);

    private const string ForgeLayoutPath = "res://assets/maps/city/layout_forge.json";

    private static readonly ForgeLeg[] ForgeTour =
    [
        // Guilda → escadaria da Guilda → praça
        new ForgeWalk("patamar da guilda", 1497, 340),
        new ForgeWalk("pé da escadaria da guilda", 1497, 490, "forja-guilda"),
        new ForgeWalk("praça da guilda", 1350, 535),
        // praça leste → escada da forja → pátio → Armaria Rubra
        new ForgeWalk("praça leste", 1120, 470),
        new ForgeWalk("escada da forja", 1120, 330),
        new ForgeWalk("pátio da forja", 1100, 222, "forja-ferraria"),
        new ForgeShop("porta da Armaria Rubra", "shop_arm"),
        // ponte velha → caminho norte → Casa dos Feitiços
        new ForgeWalk("cabeceira leste da ponte velha", 960, 162),
        new ForgeWalk("ponte velha", 785, 122, "forja-ponte-velha"),
        new ForgeWalk("patamar oeste da ponte velha", 628, 132),
        new ForgeWalk("caminho norte", 625, 50),
        new ForgeShop("Casa dos Feitiços", "shop_mag"),
        // volta pela ponte velha e desce para a ponte grande
        new ForgeWalk("ponte velha (volta)", 785, 122),
        new ForgeWalk("pátio da forja (volta)", 1060, 222),
        new ForgeWalk("escada da forja (descendo)", 1120, 330),
        new ForgeWalk("cabeceira leste da ponte grande", 1010, 520),
        new ForgeWalk("ponte grande", 850, 505, "forja-ponte-grande"),
        new ForgeWalk("cabeceira oeste da ponte grande", 680, 505),
        new ForgeWalk("largo do portal", 440, 495, "forja-largo"),
        // escadas do mercado (as duas) e o mercado
        new ForgeWalk("escada do mercado (esquerda)", 535, 600),
        new ForgeWalk("mercado", 520, 700, "forja-mercado"),
        new ForgeWalk("escada do mercado (direita)", 640, 590),
        new ForgeWalk("alto da escada do mercado", 640, 545),
        new ForgeWalk("escada do mercado (descendo)", 535, 620),
        new ForgeWalk("placa da Rua dos Ofícios", 305, 790, "forja-oficios"),
        new ForgeWalk("sob a árvore grande", 70, 520),
        new ForgeWalk("frente da banca vermelha", 600, 915, "forja-mercado-sul"),
        new ForgeWalk("subindo do mercado", 535, 600),
        // estalagem: escada da varanda, varanda, terraço e escada de madeira até o cais
        new ForgeWalk("escada da varanda", 242, 395),
        new ForgeWalk("varanda da estalagem", 262, 336, "forja-estalagem"),
        new ForgeWalk("terraço", 425, 314),
        new ForgeWalk("terraço (toldo)", 600, 316),
        new ForgeWalk("pé da escada de madeira", 585, 425),
        new ForgeWalk("cais oeste (alto)", 700, 360, "forja-cais"),
        // balsas: cais oeste (baixo), cais do guindaste e cais leste (baixo)
        new ForgeBoat("Descer o canal até o cais do mercado", "forja-cais-baixo"),
        new ForgeWalk("passarela do cais baixo", 760, 860),
        new ForgeBoat("Subir o canal até o cais da estalagem"),
        new ForgeBoat("Pegar a balsa para o cais do guindaste", "forja-cais-guindaste"),
        new ForgeBoat("Descer o canal sob a ponte grande", "forja-cais-leste"),
        new ForgeBoat("Subir o canal até o cais do guindaste"),
        new ForgeBoat("Pegar a balsa para o cais da estalagem"),
        // noroeste: escadaria oeste, rua alta e escada norte → Alquimia
        new ForgeWalk("descendo para o largo", 585, 430),
        new ForgeWalk("pé da escadaria oeste", 80, 400),
        new ForgeWalk("escadaria oeste", 80, 300),
        new ForgeWalk("rua alta noroeste", 90, 190, "forja-noroeste"),
        new ForgeWalk("escada norte", 107, 70),
        new ForgeShop("escada da Alquimia", "shop_alq"),
        // sudeste: escada sudeste (dois lances) e beco atrás da casa das lanternas → Oficina de Relíquias
        new ForgeWalk("escadaria oeste (descendo)", 80, 330),
        new ForgeWalk("praça (vindo do oeste)", 1125, 540),
        new ForgeWalk("escada sudeste, lance de cima", 1125, 640),
        new ForgeWalk("patamar sudeste", 1110, 780, "forja-escada-sudeste"),
        new ForgeWalk("escada sudeste, lance de baixo", 1130, 925),
        new ForgeWalk("subindo a escada sudeste", 1125, 600),
        new ForgeWalk("escada do beco", 1270, 600),
        new ForgeWalk("beco", 1322, 720, "forja-beco"),
        new ForgeWalk("quintal escondido", 1400, 640),
        new ForgeWalk("alpendre da casa das lanternas", 1394, 884),
        new ForgeShop("Oficina de Relíquias", "shop_rel"),
    ];

    [SmokeScenario("rua da forja: anda por todas as escadas, pontes, cais e portas de loja", order: 30)]
    private async Task ForgeStreetTour()
    {
        await WaitForScene("World", timeoutSeconds: 10); // roda também sozinho, com --smoke-filter=forja
        var world = FindWorld();
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        world.GoMap("forge", 1500, 250, instant: true); // chegada da Guilda
        await WaitMapSettled(world);

        var boats = LoadForgeBoats();
        Require(boats.Count >= 6, $"layout da Rua da Forja deveria ter ao menos 6 balsas, tem {boats.Count}");
        var failures = new List<string>();
        var walked = 0f;
        using (new SpeedUp(TesterSpeedUp))
        {
            foreach (var leg in ForgeTour)
            {
                string? error;
                switch (leg)
                {
                    case ForgeWalk w:
                        error = await ForgeWalkTo(world, w.X, w.Y);
                        if (error is null && w.Shot is { } shot)
                        {
                            await ForgeShot(shot);
                        }

                        break;
                    case ForgeBoat b:
                        var boat = boats.TryGetValue(b.Label, out var found) ? found : throw new SmokeFailure($"layout sem balsa '{b.Label}'");
                        error = await ForgeWalkTo(world, boat.X, boat.Y);
                        if (error is null)
                        {
                            Require(world.WalkArea.CanStand(boat.To.X, boat.To.Y), $"balsa '{b.Label}': destino {boat.To} sem chão");
                            player.Teleport(new Vector2(boat.To.X, boat.To.Y)); // travessia simulada (balsa é de outra fase)
                            await Frames(4);
                            if (b.Shot is { } boatShot)
                            {
                                await ForgeShot(boatShot);
                            }
                        }

                        break;
                    case ForgeShop s:
                        error = await ForgeEnterShop(world, s.Shop);
                        break;
                    default:
                        error = "trecho desconhecido";
                        break;
                }

                if (error is not null)
                {
                    failures.Add($"{leg.Label}: {error}");
                    // segue do ponto do trecho para não esconder os outros problemas
                    if (leg is ForgeWalk fw && world.MapId == "forge")
                    {
                        player.Teleport(new Vector2(fw.X, fw.Y));
                        await Frames(2);
                    }

                    continue;
                }

                walked += _forgeLastLength;
                Log.Info($"forja: {leg.Label} OK");
            }
        }

        Log.Info($"forja: {ForgeTour.Length} trechos, ~{walked:0} u andados");
        Require(failures.Count == 0, $"{failures.Count} trecho(s) da Rua da Forja falharam:\n  " + string.Join("\n  ", failures));
    }

    private float _forgeLastLength;

    /// <summary>Balsas declaradas no layout (decor <c>boat</c> com <c>to</c>), por rótulo.</summary>
    private static Dictionary<string, (float X, float Y, MapPoint To)> LoadForgeBoats()
    {
        var boats = new Dictionary<string, (float X, float Y, MapPoint To)>();
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ForgeLayoutPath));
        foreach (var d in doc.RootElement.GetProperty("decor").EnumerateArray())
        {
            if (d.GetProperty("kind").GetString() == "boat" && d.TryGetProperty("to", out var to) && d.TryGetProperty("label", out var label))
            {
                boats[label.GetString()!] = (d.GetProperty("x").GetSingle(), d.GetProperty("y").GetSingle(), new MapPoint(to[0].GetSingle(), to[1].GetSingle()));
            }
        }

        return boats;
    }

    /// <summary>Anda o caminho inteiro (calculado no Core) até perto do ponto, com input. Null se chegou.</summary>
    private async Task<string?> ForgeWalkTo(World.World world, float x, float y)
    {
        var player = FindPlayer()!;
        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var target = new MapPoint(x, y);
        var path = WalkPathfinder.FindPathNear(world.WalkArea, start, target, 8);
        if (path is null)
        {
            return $"sem caminho no Core de ({start.X:0}, {start.Y:0}) até ({x}, {y})";
        }

        _forgeLastLength = path.Count * 4f;
        var loads = world.MapLoads;
        var end = path[^1];
        var error = await TesterWalkPath(
            world,
            path,
            () => world.MapLoads > loads || player.GlobalPosition.DistanceTo(new Vector2(end.X, end.Y)) < 3f,
            timeoutGameSeconds: (_forgeLastLength / 78f * 1.6f) + 3f);
        if (error is null && world.MapLoads > loads)
        {
            return $"entrou numa saída no caminho (foi para {world.MapId})";
        }

        return error;
    }

    /// <summary>Anda até a porta da loja, entra, sai pela porta da loja e confere a chegada na porta da rua.</summary>
    private async Task<string?> ForgeEnterShop(World.World world, string shop)
    {
        MapExit? door = null;
        foreach (var e in world.Map.Exits)
        {
            if (e.To == shop)
            {
                door = e;
                break;
            }
        }

        if (door is null)
        {
            return $"forge não tem porta para {shop}";
        }

        var player = FindPlayer()!;
        var start = new MapPoint(player.GlobalPosition.X, player.GlobalPosition.Y);
        var path = WalkPathfinder.FindPathInto(world.WalkArea, start, door.Area);
        if (path is null)
        {
            return $"sem caminho no Core até a porta {door.Area}";
        }

        _forgeLastLength = path.Count * 4f;
        var loads = world.MapLoads;
        var error = await TesterWalkPath(world, path, () => world.MapLoads > loads, timeoutGameSeconds: (_forgeLastLength / 78f * 1.6f) + 3f);
        if (error is not null)
        {
            return error;
        }

        await WaitMapSettled(world);
        if (world.MapId != shop)
        {
            return $"a porta levou a {world.MapId}";
        }

        await ForgeShot($"forja-loja-{shop}");
        MapExit? back = null;
        foreach (var e in world.Map.Exits)
        {
            if (e.To == "forge")
            {
                back = e;
                break;
            }
        }

        if (back is null)
        {
            return $"{shop} não tem saída para a forge";
        }

        await TravelThroughExit(world, "forge"); // confere a chegada em back.At
        var p = player.GlobalPosition;
        if (p.DistanceTo(new Vector2(door.Area.X + (door.Area.Width / 2), door.Area.Y + (door.Area.Height / 2))) > 40f)
        {
            return $"saiu de {shop} longe da porta da rua: {p}";
        }

        return null;
    }

    private async Task ForgeShot(string name)
    {
        await WaitSeconds(0.25); // a câmera alcança o jogador (tempo acelerado)
        await Shot(name);
    }
}
