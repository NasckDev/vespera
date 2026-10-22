using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Capturas da revisão visual da Camada 1 (só com janela e <c>--qa --shots=pasta</c>): visão geral, sequências de
/// caminhada nas 4 direções, oclusão atrás da estátua e do monumento, aviso e painel de interação, lampião apagado
/// e as três classes lado a lado. Tudo numa única execução.
/// </summary>
public sealed class LevelQaCapture(Level level, SmokeTest host)
{
    private const int SequenceFrames = 8;
    private const int FrameGap = 4;

    /// <summary>
    /// Prova de proporção: três caçadores parados ao lado de uma porta pintada, de um banco pintado e da grande
    /// lanterna, numa só imagem com a câmera fixa; repete com o chão em filtro nearest para comparar a nitidez.
    /// </summary>
    public async Task RunProof()
    {
        var world = level.World;
        var atDoor = world.SpawnPlayer(new ScriptedInput(), new Vector2(190, 133));
        var atBench = world.SpawnPlayer(new ScriptedInput(), new Vector2(452, 252));
        atDoor.CollisionMask = 0;
        atBench.CollisionMask = 0;
        await Place(new Vector2(772, 478));
        world.Camera.FixedFocus = new Vector2(500, 330);
        await Settle(30);
        await host.Capture("c1_00_prova_proporcao");
        var ground = world.GetNode<Sprite2D>("Ground");
        ground.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        await Settle(4);
        await host.Capture("c1_00b_prova_chao_nearest");
        ground.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        world.Camera.FixedFocus = null;
        atDoor.QueueFree();
        atBench.QueueFree();
        await Settle(4);
    }

    public async Task Run()
    {
        var player = level.Player;
        var original = player.Input;
        var input = new ScriptedInput();
        player.Input = input;

        var k = level.World.Data.ActorScale;
        var spawn = level.World.SpawnPoint;
        await Place(spawn);
        await host.Capture("c1_01_visao_geral");

        await WalkSequence(input, spawn + (new Vector2(-260, 0) * k), Vector2.Right, "c1_02_andar_direita");
        await WalkSequence(input, spawn + (new Vector2(260, 0) * k), Vector2.Left, "c1_03_andar_esquerda");
        await WalkSequence(input, spawn + (new Vector2(0, -300) * k), Vector2.Down, "c1_04_andar_baixo");
        await WalkSequence(input, spawn + (new Vector2(0, 300) * k), Vector2.Up, "c1_05_andar_cima");
        await WalkSequence(input, spawn + (new Vector2(-300, 200) * k), new Vector2(0.7071f, -0.7071f), "c1_06_andar_diagonal");

        var statue = level.World.Props.First(p => p.Type == "estatua_anjo");
        await Place(statue.GlobalPosition + (new Vector2(6, -44) * k));
        await Settle(30);
        await host.Capture("c1_07_atras_da_estatua");
        var lantern = level.World.Props.First(p => p.Type == "lanterna_bronze");
        await Place(lantern.GlobalPosition + (new Vector2(0, -90) * k));
        await Settle(30);
        await host.Capture("c1_08_atras_da_lanterna");
        await Place(statue.GlobalPosition + (new Vector2(0, 60) * k));
        await Settle(30);
        await host.Capture("c1_09_na_frente_da_estatua");

        var board = level.World.Props.First(p => p.Type == "mural");
        await Place(board.GlobalPosition + (new Vector2(0, 50) * k));
        await Settle(20);
        await host.Capture("c1_10_aviso_mural");
        input.Tap(InputActions.Interact);
        await Settle(10);
        await host.Capture("c1_11_painel_mural");
        input.Tap(InputActions.Interact);
        await Settle(4);

        var lamp = level.World.Props.First(p => p.Interaction?.Kind == "lamp");
        await Place(lamp.GlobalPosition + (new Vector2(0, 50) * k));
        input.Tap(InputActions.Interact);
        await Settle(10);
        await host.Capture("c1_12_lampiao_apagado");
        input.Tap(InputActions.Interact);
        await Settle(4);

        var exit = level.World.Zones.First(z => z.Kind is "door" or "exit");
        await Place(exit.GlobalPosition + (new Vector2(0, 60) * k));
        input.Tap(InputActions.Interact);
        await Settle(10);
        await host.Capture("c1_13_saida_trancada");

        await Lineup();
        player.Input = original;
    }

    private async Task WalkSequence(ScriptedInput input, Vector2 start, Vector2 dir, string name)
    {
        await Place(start);
        input.Move = dir;
        await Settle(24);
        for (var i = 0; i < SequenceFrames; i++)
        {
            await host.Capture($"{name}_{i:00}");
            await Settle(FrameGap);
        }

        input.Move = Vector2.Zero;
        await Settle(20);
        await host.Capture($"{name}_parado");
    }

    private async Task Lineup()
    {
        var k = level.World.Data.ActorScale;
        var at = level.World.SpawnPoint;
        await Place(at + (new Vector2(-170, 0) * k));
        var arco = level.World.SpawnPlayer(new ScriptedInput(), at, HunterClass.Arco);
        var martelo = level.World.SpawnPlayer(new ScriptedInput(), at + (new Vector2(170, 0) * k), HunterClass.Martelo);
        await Settle(20);
        await host.Capture("c1_14_tres_classes");
        arco.QueueFree();
        martelo.QueueFree();
        await Settle(2);
    }

    private async Task Place(Vector2 feet)
    {
        level.Player.Teleport(feet);
        level.World.Camera.Snap();
        await Settle(6);
    }

    private async Task Settle(int physicsFrames) => await host.PhysicsFrames(physicsFrames);

}
