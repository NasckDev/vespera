using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>Cenários da fundação: boot no mundo, colisão (formas e máscara) e progressão básica.</summary>
public partial class SmokeTest
{
    [SmokeScenario("mundo carrega com jogador e colisão", order: 0)]
    private async Task WorldLoads()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var world = FindWorld();
        Require(FindPlayer() is not null, "Player não encontrado no mundo");
        Require(world.MapId == "guild", $"novo jogo deve começar na Guilda, está em {world.MapId}");
        Require(FindPlayer()!.GlobalPosition.DistanceTo(new Vector2(250, 560)) < 1f, $"início da v2 é (250, 560), está em {FindPlayer()!.GlobalPosition}");
        Require(Find<ShapeCollision>() is not null, "ShapeCollision não encontrado na Guilda");
        await Shot("mundo-inicio");
    }

    [SmokeScenario("jogador respeita a colisão do mapa", order: 1)]
    private async Task PlayerRespectsCollision()
    {
        var world = FindWorld();
        await WalkAllDirections(world, framesPerDirection: 75);

        world.GoMap("forge", 800, 520, instant: true);
        await Frames(2);
        Require(Find<MaskCollision>() is not null, "MaskCollision não encontrado na Rua da Forja");
        await WalkAllDirections(world, framesPerDirection: 75);
    }

    [SmokeScenario("XP sobe rank e HP máximo", order: 2)]
    private async Task XpRaisesRank()
    {
        var game = GameManager.Instance;
        Require(game.Rank == Core.Ranks.Rank.E && game.TotalXp == 0, "smoke test deve começar com save novo (rank E, 0 XP)");
        var hpBefore = game.MaxHp;
        game.AddXp(100);
        await NextPhysicsFrame();
        Require(game.Rank == Core.Ranks.Rank.D, $"100 XP deveria levar a D, está em {game.Rank}");
        Require(game.MaxHp == hpBefore + 20, $"HP máximo deveria subir 20 (de {hpBefore} para {hpBefore + 20}), está {game.MaxHp}");
    }

    private World.World FindWorld() => Find<World.World>() ?? throw new SmokeFailure("sem World");

    /// <summary>Anda nas 4 direções verificando, frame a frame, que o pé do jogador nunca fica em área bloqueada.</summary>
    private async Task WalkAllDirections(World.World world, int framesPerDirection)
    {
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        StringName[] directions = [InputActions.MoveRight, InputActions.MoveDown, InputActions.MoveLeft, InputActions.MoveUp];
        foreach (var action in directions)
        {
            Input.ActionPress(action);
            for (var frame = 0; frame < framesPerDirection; frame++)
            {
                await NextPhysicsFrame();
                var p = player.GlobalPosition;
                Require(world.WalkArea.IsWalkable(p.X, p.Y), $"{world.MapId}: jogador entrou em área bloqueada em {p} andando com {action}");
            }

            Input.ActionRelease(action);
        }
    }
}
