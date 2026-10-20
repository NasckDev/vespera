using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Playtest automático. Rode com: <c>godot --headless --path . -- --smoke-test</c>.
/// Cada cenário joga o jogo de verdade (injeta input, espera frames) e verifica invariantes.
/// Sai com código 0 se tudo passou, 1 se algo falhou. Usado no CI e pelos testers.
/// Para adicionar um cenário: crie um método async que lança <see cref="SmokeFailure"/> e registre em <see cref="Scenarios"/>.
/// </summary>
public partial class SmokeTest : Node
{
    public const string CommandLineFlag = "--smoke-test";

    private readonly List<string> _failures = [];

    public static bool IsRequested() => OS.GetCmdlineUserArgs().Contains(CommandLineFlag);

    private IEnumerable<(string Name, Func<Task> Run)> Scenarios() =>
    [
        ("cidade carrega com jogador e colisão", CityLoads),
        ("jogador respeita a máscara do mapa", PlayerRespectsMask),
        ("XP sobe rank e HP máximo", XpRaisesRank),
    ];

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Log.Info("=== SMOKE TEST ===");
        var started = Time.GetTicksMsec();

        foreach (var (name, run) in Scenarios())
        {
            try
            {
                await run();
                Log.Info($"PASS  {name}");
            }
            catch (Exception ex)
            {
                _failures.Add(name);
                Log.Error($"FAIL  {name}: {ex.Message}");
            }
        }

        var seconds = (Time.GetTicksMsec() - started) / 1000.0;
        Log.Info(_failures.Count == 0
            ? $"=== SMOKE OK ({seconds:0.0}s) ==="
            : $"=== SMOKE FALHOU: {_failures.Count} cenário(s) ({seconds:0.0}s) ===");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // --- Cenários ---

    private async Task CityLoads()
    {
        await WaitForScene("Lumeira", timeoutSeconds: 10);
        Require(FindPlayer() is not null, "Player não encontrado na cidade");
        Require(Find<MaskCollision>() is not null, "MaskCollision não encontrado na cidade");
    }

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

    // --- Utilidades para cenários ---

    protected static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SmokeFailure(message);
        }
    }

    protected async Task NextPhysicsFrame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

    protected async Task WaitSeconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);

    protected async Task WaitForScene(string sceneRootName, double timeoutSeconds)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (GetTree().CurrentScene?.Name != sceneRootName)
        {
            Require(Time.GetTicksMsec() < deadline, $"cena {sceneRootName} não carregou em {timeoutSeconds}s (atual: {GetTree().CurrentScene?.Name})");
            await NextPhysicsFrame();
        }

        await WaitSeconds(0.5); // fade + _Ready dos filhos
    }

    protected Player.Player? FindPlayer() => Find<Player.Player>();

    protected T? Find<T>()
        where T : Node => GetTree().CurrentScene is { } scene ? FindIn<T>(scene) : null;

    private static T? FindIn<T>(Node node)
        where T : Node
    {
        if (node is T match)
        {
            return match;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindIn<T>(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

public sealed class SmokeFailure(string message) : Exception(message);
