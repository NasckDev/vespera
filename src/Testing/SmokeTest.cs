using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;

namespace Vespera.Testing;

/// <summary>Marca um método <c>async Task</c> de <see cref="SmokeTest"/> como cenário. Menor <c>Order</c> roda primeiro.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SmokeScenarioAttribute(string name, int order = 100) : Attribute
{
    public string Name { get; } = name;

    public int Order { get; } = order;
}

/// <summary>
/// Playtest automático. Rode com: <c>godot --headless --path . -- --smoke-test</c>
/// (filtre com <c>-- --smoke-test --smoke-filter=forja</c>).
/// Cada cenário joga o jogo de verdade (injeta input, espera frames) e verifica invariantes.
/// Sai com código 0 se tudo passou, 1 se algo falhou. Usado no CI e pelos testers.
/// Para adicionar cenários: crie um arquivo em <c>src/Testing/Scenarios/</c> com
/// <c>public partial class SmokeTest</c> e métodos <c>[SmokeScenario("nome")] private async Task X()</c>
/// que lançam <see cref="SmokeFailure"/> (via <see cref="Require"/>).
/// </summary>
public partial class SmokeTest : Node
{
    public const string CommandLineFlag = "--smoke-test";
    private const string FilterPrefix = "--smoke-filter=";

    private readonly List<string> _failures = [];

    public static bool IsRequested() => OS.GetCmdlineUserArgs().Contains(CommandLineFlag);

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var filter = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(FilterPrefix, StringComparison.Ordinal))?[FilterPrefix.Length..];
        Log.Info($"=== SMOKE TEST {(filter is null ? string.Empty : $"(filtro: {filter})")}===");
        var started = Time.GetTicksMsec();
        var ran = 0;

        foreach (var (name, method) in DiscoverScenarios())
        {
            if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ran++;
            var scenarioStart = Time.GetTicksMsec();
            try
            {
                await (Task)method.Invoke(this, null)!;
                Log.Info($"PASS  {name} ({(Time.GetTicksMsec() - scenarioStart) / 1000.0:0.0}s)");
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
                _failures.Add(name);
                Log.Error($"FAIL  {name}: {inner.Message}");
            }
            finally
            {
                ReleaseAllActions();
            }
        }

        var seconds = (Time.GetTicksMsec() - started) / 1000.0;
        Log.Info(_failures.Count == 0
            ? $"=== SMOKE OK: {ran} cenário(s) ({seconds:0.0}s) ==="
            : $"=== SMOKE FALHOU: {_failures.Count}/{ran} cenário(s) ({seconds:0.0}s): {string.Join(", ", _failures)} ===");
        GetTree().Quit(_failures.Count == 0 && ran > 0 ? 0 : 1);
    }

    private IEnumerable<(string Name, MethodInfo Method)> DiscoverScenarios() =>
        GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(m => (Method: m, Attr: m.GetCustomAttribute<SmokeScenarioAttribute>()))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr!.Order)
            .ThenBy(x => x.Attr!.Name, StringComparer.Ordinal)
            .Select(x => (x.Attr!.Name, x.Method));

    private static void ReleaseAllActions()
    {
        foreach (var action in InputMap.GetActions())
        {
            Input.ActionRelease(action);
        }
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

    protected async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await NextPhysicsFrame();
        }
    }

    protected async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Espera até <paramref name="condition"/> ser verdadeira ou falha após o timeout.</summary>
    protected async Task WaitUntil(Func<bool> condition, double timeoutSeconds, string description)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (!condition())
        {
            Require(Time.GetTicksMsec() < deadline, $"timeout de {timeoutSeconds}s esperando: {description}");
            await NextPhysicsFrame();
        }
    }

    protected async Task WaitForScene(string sceneRootName, double timeoutSeconds)
    {
        await WaitUntil(() => GetTree().CurrentScene?.Name == sceneRootName, timeoutSeconds, $"cena {sceneRootName} (atual: {GetTree().CurrentScene?.Name})");
        await WaitSeconds(0.5); // fade + _Ready dos filhos
    }

    /// <summary>Segura uma ação por N frames de física e solta.</summary>
    protected async Task Hold(StringName action, int frames)
    {
        Input.ActionPress(action);
        await Frames(frames);
        Input.ActionRelease(action);
    }

    /// <summary>Aperta e solta uma ação (um "tap").</summary>
    protected async Task Tap(StringName action)
    {
        Input.ActionPress(action);
        await NextPhysicsFrame();
        await NextPhysicsFrame();
        Input.ActionRelease(action);
        await NextPhysicsFrame();
    }

    protected Player.Player? FindPlayer() => Find<Player.Player>();

    protected T? Find<T>()
        where T : Node => GetTree().CurrentScene is { } scene ? FindIn<T>(scene) : null;

    protected List<T> FindAll<T>()
        where T : Node
    {
        var found = new List<T>();
        if (GetTree().CurrentScene is { } scene)
        {
            CollectIn(scene, found);
        }

        return found;
    }

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

    private static void CollectIn<T>(Node node, List<T> found)
        where T : Node
    {
        if (node is T match)
        {
            found.Add(match);
        }

        foreach (var child in node.GetChildren())
        {
            CollectIn(child, found);
        }
    }
}

public sealed class SmokeFailure(string message) : Exception(message);
