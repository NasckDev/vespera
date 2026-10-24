using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Parte do roteiro dentro da fase: interações (mural, lampião, porta) e, com <c>--bot</c>, o bot de colisão;
/// com <c>--qa</c> (e <c>--shots</c>), as capturas de revisão visual da Camada 1.
/// </summary>
public partial class SmokeTest
{
    public const string BotFlag = "--bot";
    public const string QaFlag = "--qa";
    public const string QaProofFlag = "--qa-prova";
    public const string CombatBotFlag = "--combat-bot";
    public const string CombatQaFlag = "--qa-combate";
    public const string ScaleFlag = "--escala";
    public const string CameraFlag = "--camera";

    private async Task RunLevelExtras(Level level)
    {
        if (Array.IndexOf(_args, ClicksFlag) >= 0)
        {
            await RunClickProbe(level);
            return;
        }

        await CheckInteractions(level);
        if (Array.IndexOf(_args, BotFlag) >= 0)
        {
            level.World.SetNpcsActive(false);
            var bot = new CollisionBot(level.World, this);
            var failures = await bot.Run();
            _failures.AddRange(failures);
            level.World.SetNpcsActive(true);
            await RunRiftCollisionBots(level);
        }

        if (Array.IndexOf(_args, ScaleFlag) >= 0)
        {
            var shots = _shotsDir is not null && DisplayServer.GetName() != "headless";
            _failures.AddRange(await new ScaleCheck(level, this, shots).Run());
        }

        if (Array.IndexOf(_args, CameraFlag) >= 0)
        {
            var shots = _shotsDir is not null && DisplayServer.GetName() != "headless";
            _failures.AddRange(await new CameraCheck(level, this, shots).Run());
        }

        if (Array.IndexOf(_args, CombatBotFlag) >= 0)
        {
            _failures.AddRange(await new CombatBot(level, this).Run(HunterClass.Lamina));
        }

        if (Array.IndexOf(_args, CombatQaFlag) >= 0 && _shotsDir is not null && DisplayServer.GetName() != "headless")
        {
            await new CombatQaCapture(level, this).Run();
        }

        var canShoot = _shotsDir is not null && DisplayServer.GetName() != "headless";
        if (Array.IndexOf(_args, QaProofFlag) >= 0 && canShoot)
        {
            await new LevelQaCapture(level, this).RunProof();
        }

        if (Array.IndexOf(_args, QaFlag) >= 0 && canShoot)
        {
            await new LevelQaCapture(level, this).Run();
        }

        level.Player.Teleport(level.World.SpawnPoint);
        level.World.Camera.Snap();
        await PhysicsFrames(2);
        if (Array.IndexOf(_args, StoryFlag) >= 0)
        {
            await RunStoryBot(level);
        }

        if (Array.IndexOf(_args, ActOneFlag) >= 0)
        {
            await RunActOne(level);
        }

        if (Array.IndexOf(_args, Ato45Flag) >= 0)
        {
            await RunAto45(level);
        }

        if (Array.IndexOf(_args, WarningsQaFlag) >= 0 && _shotsDir is not null && DisplayServer.GetName() != "headless")
        {
            await CaptureWarnings(level);
        }
    }

    /// <summary>Bot de colisão nas outras fases do Ato I (Forjas, forja da Odete e salas da Brecha sem monstros) e volta à Praça.</summary>
    private async Task RunRiftCollisionBots(Level level)
    {
        string[] city = ["forjas", "forja_interior"];
        foreach (var room in city.Concat(RiftSession.Brecha.Rooms))
        {
            await level.GoToRoom(room, withDirector: false);
            await PhysicsFrames(4);
            level.World.SetNpcsActive(false);
            Log.Info($"bot de colisão na sala {room}:");
            _failures.AddRange((await new CollisionBot(level.World, this).Run()).Select(f => $"{room}: {f}"));
        }

        await level.GoToRoom(Level.HubLevelId);
        await PhysicsFrames(4);
    }

    private async Task CheckInteractions(Level level)
    {
        var k = level.World.Data.ActorScale;
        var board = level.World.Props.First(p => p.Type == "mural");
        await StandAt(level, board.GlobalPosition + new Vector2(0, 50 * k));
        Check(level.Player.Focus?.Kind == "board", $"perto do mural o foco deveria ser o mural, foi '{level.Player.Focus?.Kind}'");
        Check(level.Hud.PromptText.Length > 0, "o aviso de interação deveria aparecer perto do mural");
        await PressInWorld(Key.E);
        Check(level.DialogueOpen && level.Dialogue.SpeakerName == "Mestre Benício", "E no mural deveria abrir a fala do Benício");
        await SkipDialogue(level);
        Check(!level.DialogueOpen, "a fala do mural deveria fechar");

        // A 1ª resposta do quadro (v2) é "Ver os contratos.": fecha o mural para seguir.
        if (Vespera.Game.Hunt.ContractsScreen.Active is { } contracts)
        {
            contracts.Close();
            await Frames(4);
            await Seconds(0.4);
        }

        Check(!GetTree().Paused, "depois do mural o jogo deveria voltar a correr");

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
        Check(level.Hud.PanelText.Contains("rancad", StringComparison.Ordinal) || level.Hud.PanelText.Contains("fechad", StringComparison.Ordinal), $"a saída deveria dizer que está trancada/fechada, disse '{level.Hud.PanelText}'");
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

    /// <summary>Captura sem esperar a cena assentar (sequências quadro a quadro).</summary>
    internal async Task CaptureNow(string name) => await Shot(name, 0);
}
