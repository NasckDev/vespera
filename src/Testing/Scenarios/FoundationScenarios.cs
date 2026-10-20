using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>Cenários da fundação: boot, colisão por máscara e progressão básica.</summary>
public partial class SmokeTest
{
    [SmokeScenario("cidade carrega com jogador e colisão", order: 0)]
    private async Task CityLoads()
    {
        await WaitForScene("Lumeira", timeoutSeconds: 10);
        Require(FindPlayer() is not null, "Player não encontrado na cidade");
        Require(Find<MaskCollision>() is not null, "MaskCollision não encontrado na cidade");
        await Shot("cidade-inicio");
    }

    [SmokeScenario("jogador respeita a máscara do mapa", order: 1)]
    private async Task PlayerRespectsMask()
    {
        var player = FindPlayer() ?? throw new SmokeFailure("sem Player");
        var collision = Find<MaskCollision>() ?? throw new SmokeFailure("sem MaskCollision");

        StringName[] directions = [InputActions.MoveRight, InputActions.MoveDown, InputActions.MoveLeft, InputActions.MoveUp];
        foreach (var action in directions)
        {
            Input.ActionPress(action);
            for (var frame = 0; frame < 150; frame++)
            {
                await NextPhysicsFrame();
                var local = collision.ToLocal(player.GlobalPosition);
                Require(collision.IsWalkableAt(local), $"jogador entrou em área bloqueada em {local} andando com {action}");
            }

            Input.ActionRelease(action);
        }
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
        // v2: +12 pelo Selo D e +12 pelos dois níveis (100 XP = Nv. 3), spec 02 §1.3.
        Require(game.MaxHp == hpBefore + 24, $"HP máximo deveria subir 24 (de {hpBefore} para {hpBefore + 24}), está {game.MaxHp}");
    }
}
