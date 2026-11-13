using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.Actors;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Revisão visual da rodada 2 (<c>--qa-visual --shots=pasta</c>, só com janela): efeitos dos 7 Ecos, HUD com 2 Ecos por
/// caçador, chefes de perto (nitidez com compressão), HUD do co-op em 640x360 e a linha do prato da Horta na Brecha.
/// </summary>
public sealed partial class SmokeTest
{
    public const string VisualQaFlag = "--qa-visual";

    private static readonly string[] AllEchoes = ["eco_viscoso", "eco_semente", "eco_farol", "eco_vela", "eco_sino", "eco_lar", "eco_avesso"];
    private readonly ScriptedInput _visualInput = new();

    private async Task RunVisualQa(Level level)
    {
        RiftSession.Cheat = true;
        var engine = Progression.Engine;
        foreach (var id in AllEchoes)
        {
            if (!Progression.State.Echoes.Contains(id))
            {
                Progression.State.Echoes.Add(id);
            }
        }

        engine.UnlockEchoSlot(1);
        level.World.Player.Input = _visualInput;
        await VisualEchoes(level);
        await VisualBosses(level);
        engine.EquipEcho(0, "eco_semente");
        engine.EquipEcho(1, "eco_sino");
        level.World.Player.Combat.ResetEchoCooldowns();
        level.World.Player.Combat.TryEcho(0, Vector2.Right);
        await PhysicsFrames(20);
        await Shot("v_hud_solo_2ecos", 0);
        await VisualCoop(level);
        Finish();
        await Seconds(30);
    }

    private async Task VisualEchoes(Level level)
    {
        var world = level.World;
        var scale = world.Data.ActorScale;
        var engine = Progression.Engine;
        world.Player.Teleport(world.SpawnPoint);
        world.Camera.Snap();
        for (var i = 0; i < AllEchoes.Length; i++)
        {
            var id = AllEchoes[i];
            engine.EquipEcho(0, id);
            world.Player.Combat.ResetEchoCooldowns();
            var dummies = new Enemy[3];
            for (var d = 0; d < dummies.Length; d++)
            {
                dummies[d] = new Lodo(world) { Position = world.ClampToFloor(world.Player.GlobalPosition + new Vector2((200f + (d * 60f)) * scale, (d - 1) * 90f * scale)), StartDelay = 30f };
                world.AddEnemy(dummies[d]);
            }

            world.Player.Face(Vector2.Right);
            await PhysicsFrames(20);
            Log.Info($"visual: Eco {id} lançado: {world.Player.Combat.TryEcho(0, Vector2.Right)}");
            foreach (var frames in new[] { 8, 14, 24 })
            {
                await PhysicsFrames(frames);
                await Shot($"v_eco_{i + 1}_{id[4..]}_{frames:00}", 0);
            }

            foreach (var dummy in dummies)
            {
                dummy.QueueFree();
            }

            await PhysicsFrames(90);
        }
    }

    private async Task VisualBosses(Level level)
    {
        var world = level.World;
        var scale = world.Data.ActorScale;
        Func<LevelWorld, Enemy>[] makers =
        [
            w => new SineiroMudo(w), w => new Velora(w), w => new Reflexo(w), w => new Inquilino(w), w => new LodoMae(w), w => new MacieiraOca(w), w => new FarolAfogado(w),
        ];
        foreach (var make in makers)
        {
            var boss = make(world);
            boss.Position = world.ClampToFloor(world.Player.GlobalPosition + new Vector2(150f * scale, -20f * scale));
            world.AddEnemy(boss);
            await PhysicsFrames(30);
            await Shot($"v_chefe_{boss.GetType().Name.ToLowerInvariant()}", 0);
            boss.QueueFree();
            await PhysicsFrames(20);
        }
    }

    private async Task VisualCoop(Level level)
    {
        CoopSession.SourceOverride = null;
        await JoinGuestThroughPause(level);
        var guest = PartyProgression.EngineFor(1);
        if (guest is not null)
        {
            foreach (var id in AllEchoes)
            {
                guest.Progress.Echoes.Add(id);
            }

            guest.UnlockEchoSlot(1);
            guest.EquipEcho(0, "eco_farol");
            guest.EquipEcho(1, "eco_lar");
        }

        await PhysicsFrames(30);
        await Shot("v_hud_coop_praca", 0);
        var window = GetWindow();
        var before = window.Size;
        window.Size = new Vector2I(640, 360);
        await Seconds(1.0);
        await Shot("v_hud_coop_praca_640", 0);

        HuntService.HortaState.Meal = "ensopado_de_raiz";
        UserFlags();
        level.EnterRift(HunterClass.Lamina);
        await WaitFor(() => !level.Switching && level.World.Director is not null, "sala da Brecha");
        await Seconds(2.5);
        await Shot("v_hud_coop_brecha_640", 0);
        window.Size = before;
        await Seconds(1.0);
        await Shot("v_hud_coop_brecha", 0);
        await Shot("v_horta_linha", 0);
    }

    private static void UserFlags() => Vespera.Game.Story.StoryService.SetFlag(Vespera.Core.Story.StoryFlags.StarterBlade);
}
