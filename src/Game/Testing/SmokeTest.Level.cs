using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Parte do roteiro dentro da fase: interações (mural, lampião, porta) e, com <c>--bot</c>, o bot de colisão;
/// com <c>--qa</c> (e <c>--shots</c>), as capturas de revisão visual da Camada 1.
/// </summary>
public partial class SmokeTest
{
    public const string BotFlag = "--bot";
    public const string QaFlag = "--qa";

    private async Task RunLevelExtras(Level level)
    {
        await CheckInteractions(level);
        if (Array.IndexOf(_args, BotFlag) >= 0)
        {
            var bot = new CollisionBot(level.World, this);
            var failures = await bot.Run();
            _failures.AddRange(failures);
        }

        if (Array.IndexOf(_args, QaFlag) >= 0 && _shotsDir is not null && DisplayServer.GetName() != "headless")
        {
            await new LevelQaCapture(level, this).Run();
        }

        level.Player.Teleport(level.World.SpawnPoint);
        level.World.Camera.Snap();
        await PhysicsFrames(2);
    }

    private async Task CheckInteractions(Level level)
    {
        var k = level.World.Data.ActorScale;
        var board = level.World.Props.First(p => p.Type == "mural");
        await StandAt(level, board.GlobalPosition + new Vector2(0, 50 * k));
        Check(level.Player.Focus?.Kind == "board", $"perto do mural o foco deveria ser o mural, foi '{level.Player.Focus?.Kind}'");
        Check(level.Hud.PromptText.Length > 0, "o aviso de interação deveria aparecer perto do mural");
        await PressInWorld(Key.E);
        Check(level.Hud.PanelOpen && level.Hud.PanelText.Contains("CONTRATO", StringComparison.Ordinal), "E no mural deveria abrir o contrato");
        await PressInWorld(Key.E);
        Check(!level.Hud.PanelOpen, "E de novo deveria fechar o mural");

        var lamp = level.World.Props.First(p => p.Interaction?.Kind == "lamp");
        await StandAt(level, lamp.GlobalPosition + new Vector2(0, 50 * k));
        var before = lamp.LightOn;
        await PressInWorld(Key.E);
        Check(lamp.LightOn != before, "E no lampião deveria apagar/acender");
        await PressInWorld(Key.E);
        Check(lamp.LightOn == before, "E de novo deveria voltar o lampião");

        var exit = level.World.Zones.First(z => z.Kind is "door" or "exit");
        await StandAt(level, exit.GlobalPosition + new Vector2(0, 60 * k));
        await PressInWorld(Key.E);
        Check(level.Hud.PanelText.Contains("rancad", StringComparison.Ordinal), $"a saída deveria dizer que está trancada, disse '{level.Hud.PanelText}'");
        await StandAt(level, level.World.SpawnPoint);
    }

    private async Task StandAt(Level level, Vector2 feet)
    {
        level.Player.Teleport(feet);
        level.World.Camera.Snap();
        await PhysicsFrames(4);
        await Frames(2);
    }

    /// <summary>Aperta e solta uma tecla dando tempo para a física (onde o jogador lê o input) ver as duas bordas.</summary>
    internal async Task PressInWorld(Key key)
    {
        Input.ParseInputEvent(MakeKey(key, pressed: true));
        await PhysicsFrames(3);
        Input.ParseInputEvent(MakeKey(key, pressed: false));
        await PhysicsFrames(3);
        await Frames(2);
    }

    /// <summary>Espera quadros de física (o movimento do jogador acontece neles).</summary>
    internal async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    internal async Task Capture(string name) => await Shot(name);
}
