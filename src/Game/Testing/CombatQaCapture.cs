using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Hunter;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Capturas da revisão visual das camadas 3 e 4 (com janela e <c>--qa-combate --shots=pasta</c>), numa execução:
/// portal e seletor, título da sala, fendas das ondas, aviso do Lodo, combo com pausa no impacto, esquiva, Espectro,
/// baú, Guardião (barra, aviso, engolir, divisão), proporções, Arco, Martelo e a queda de volta à Praça.
/// </summary>
public sealed class CombatQaCapture(Level level, SmokeTest host)
{
    private readonly ScriptedInput _input = new();

    private LevelWorld World => level.World;

    private float S => World.Data.ActorScale;

    public async Task Run()
    {
        await PortalAndPicker();
        await Corridor();
        await Crossing();
        await Treasure();
        await Arena();
        await Fall();
        RiftSession.Cheat = false;
    }

    private async Task PortalAndPicker()
    {
        Vespera.Game.Story.StoryService.SetFlag(Vespera.Core.Story.StoryFlags.StarterBlade);
        var portal = World.Zones.First(z => z.Kind == "brecha");
        Bind();
        World.Player.Teleport(portal.GlobalPosition + new Vector2(-30f * S, 70f * S));
        World.Camera.Snap();
        await Frames(40);
        await host.CaptureNow("c34_01_praca_portal");
        _input.Tap(InputActions.Interact);
        await Frames(20);
        await host.CaptureNow("c34_02_seletor");
        level.EnterRift(HunterClass.Lamina);
        await WaitRoom();
        await Frames(30);
        await host.CaptureNow("c34_03_corredor_titulo");
    }

    private async Task Corridor()
    {
        Place(World.ClampToFloor(new Vector2(640f, 500f)));
        await Frames(40);
        await host.CaptureNow("c34_04_fendas_onda");
        var lodo = await WaitFor<Lodo>(e => e.State == EnemyState.Telegraph, 600, chase: true);
        if (lodo is not null)
        {
            Place(lodo.GlobalPosition + new Vector2(-70f * S, 10f * S));
            for (var i = 0; i < 8; i++)
            {
                await host.CaptureNow($"c34_05_lodo_aviso_pulo_{i:00}");
                await Frames(4);
            }
        }

        var target = World.Enemies.OfType<Lodo>().FirstOrDefault(e => !e.Dead);
        if (target is not null)
        {
            Place(World.ClampToFloor(target.GlobalPosition + new Vector2(-40f * S, 0)));
            _input.Move = Vector2.Right * 0.2f;
            await Frames(2);
            _input.Move = Vector2.Zero;
            for (var i = 0; i < 30; i++)
            {
                if (i is 0 or 9 or 18)
                {
                    _input.Tap(InputActions.Attack);
                }

                await host.CaptureNow($"c34_06_combo_{i:00}");
                await Frames(2);
            }
        }

        _input.Move = Vector2.Left;
        _input.Tap(InputActions.Dodge);
        for (var i = 0; i < 8; i++)
        {
            await Frames(2);
            await host.CaptureNow($"c34_07_esquiva_{i:00}");
        }

        _input.Move = Vector2.Zero;
        await ClearAndExit("c34_08_corredor_limpo");
    }

    private async Task Crossing()
    {
        await WaitRoom();
        await Frames(50);
        await host.CaptureNow("c34_09_cruzamento");
        var ghost = await WaitFor<Espectro>(e => e.State == EnemyState.Telegraph, 900, chase: true);
        if (ghost is not null)
        {
            for (var i = 0; i < 6; i++)
            {
                await host.CaptureNow($"c34_10_espectro_aviso_{i:00}");
                await Frames(4);
            }
        }

        await ClearAndExit("c34_11_cruzamento_limpo");
    }

    private async Task Treasure()
    {
        await WaitRoom();
        if (World.Director?.Chest is not { } chest)
        {
            return;
        }

        await Frames(110);

        Place(World.ClampToFloor(chest.GlobalPosition + new Vector2(-70f * S, 30f * S)));
        World.Player.Face(Vector2.Right);
        await Frames(20);
        await host.CaptureNow("c34_12_bau");
        Place(World.ClampToFloor(chest.GlobalPosition + new Vector2(0, 40f * S)), snap: false);
        await Frames(3);
        _input.Tap(InputActions.Interact);
        await Frames(3);
        Place(World.ClampToFloor(chest.GlobalPosition + new Vector2(-70f * S, 30f * S)), snap: false);
        World.Player.Face(Vector2.Right);
        await Frames(12);
        await host.CaptureNow("c34_13_bau_aberto");
        await Frames(100);
        await host.CaptureNow("c34_14_fenda_descer");
        await Leave();
    }

    private async Task Arena()
    {
        await WaitRoom();
        if (World.Director?.Boss is not { } boss)
        {
            return;
        }

        Place(World.ClampToFloor(boss.GlobalPosition + new Vector2(0, 150f * S)));
        await Frames(20);
        await host.CaptureNow("c34_15_chefe_entrada");
        await Until(() => boss.State == EnemyState.Telegraph, 400);
        for (var i = 0; i < 8; i++)
        {
            await host.CaptureNow($"c34_16_chefe_aviso_pulo_{i:00}");
            await Frames(5);
        }

        await Frames(60);
        await Lineup(boss);
        boss.SetHealth(boss.MaxHp);
        boss.TakeDirect(boss.MaxHp * 0.45);
        await Until(() => World.Enemies.OfType<LodoMae>().Any(b => b.State == EnemyState.Telegraph), 600);
        await Frames(12);
        await host.CaptureNow("c34_18_chefe_aviso_engolir");
        await Frames(40);
        await host.CaptureNow("c34_19_chefe_investida");
        await Frames(140);
        boss.TakeDirect(boss.Hp - (boss.MaxHp * 0.3));
        await Frames(30);
        await host.CaptureNow("c34_20_chefe_divide");
        await Frames(80);
        await host.CaptureNow("c34_21_chefe_dividida");
        await Weapons();
        await ClearAndExit("c34_24_chefe_derrotada", portalWait: true);
    }

    private async Task Lineup(LodoMae boss)
    {
        var p = World.ClampToFloor(boss.GlobalPosition + new Vector2(-110f * S, 70f * S));
        Place(p);
        var lodo = new Lodo(World) { Position = World.ClampToFloor(p + new Vector2(-55f * S, 6f * S)), StartDelay = 9f };
        var ghost = new Espectro(World) { Position = World.ClampToFloor(p + new Vector2(-120f * S, 0)) };
        World.AddEnemy(lodo);
        World.AddEnemy(ghost);
        await Frames(10);
        await host.CaptureNow("c34_17_proporcao_cacador_lodo_espectro_mae");
        lodo.Die();
        ghost.Die();
    }

    private async Task Weapons()
    {
        RiftSession.Cheat = true;
        World.Player.SetHunter(HunterClass.Arco);
        var target = World.Enemies.Where(e => !e.Dead).OrderBy(e => e.GlobalPosition.DistanceTo(World.Player.GlobalPosition)).FirstOrDefault();
        if (target is not null)
        {
            Place(World.ClampToFloor(target.GlobalPosition + new Vector2(-170f * S, 0)));
            World.Player.Face(Vector2.Right);
        }

        _input.Hold(InputActions.Heavy, true);
        for (var i = 0; i < 8; i++)
        {
            await Frames(6);
            await host.CaptureNow($"c34_22_arco_carregado_{i:00}");
        }

        _input.Hold(InputActions.Heavy, false);
        for (var i = 0; i < 5; i++)
        {
            await Frames(3);
            await host.CaptureNow($"c34_22b_arco_solta_{i:00}");
        }

        World.Player.SetHunter(HunterClass.Martelo);
        _input.Hold(InputActions.Heavy, true);
        _input.Tap(InputActions.Heavy);
        await Frames(40);
        _input.Hold(InputActions.Heavy, false);
        for (var i = 0; i < 10; i++)
        {
            await Frames(3);
            await host.CaptureNow($"c34_23_martelo_onda_{i:00}");
        }

        World.Player.SetHunter(HunterClass.Lamina);
    }

    private async Task Fall()
    {
        await Until(() => !level.Switching && World.Data.Id == Level.HubLevelId, 900);
        RiftSession.Cheat = false;
        level.EnterRift(HunterClass.Lamina);
        await WaitRoom();
        Progression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        Place(World.ClampToFloor(World.SpawnPoint + new Vector2(220f * S, 0)));
        World.Player.Combat.Digest(99999);
        await Frames(40);
        await host.CaptureNow("c34_25_queda");
        await Until(() => !level.Switching && World.Data.Id == Level.HubLevelId, 900);
        await Frames(50);
        await host.CaptureNow("c34_26_praca_lanterna_perdida");
        World.Player.Combat.RefreshStats(fullHeal: true);
    }

    /// <summary>Limpa a sala com o caçador em modo de teste (dano ×10), fotografa e sai.</summary>
    private async Task ClearAndExit(string shot, bool portalWait = false)
    {
        RiftSession.Cheat = true;
        var director = World.Director!;
        for (var frame = 0; frame < 60 * 90 && !director.Cleared; frame++)
        {
            var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(World.Player.GlobalPosition)).FirstOrDefault();
            if (target is not null && frame % 30 == 0)
            {
                Place(World.ClampToFloor(target.GlobalPosition + new Vector2(-34f * S, 4f * S)), snap: false);
            }

            if (frame % 8 == 0)
            {
                _input.Tap(InputActions.Attack);
            }

            await Frames(1);
        }

        await Frames(portalWait ? 140 : 30);
        await host.CaptureNow(shot);
        await Leave();
        RiftSession.Cheat = false;
    }

    private async Task Leave()
    {
        var director = World.Director!;
        if (director.ExitPortal is { } portal)
        {
            Place(World.ClampToFloor(portal.GlobalPosition + new Vector2(0, 40f * S)));
            await Frames(4);
            _input.Tap(InputActions.Interact);
        }
        else if (World.Data.Rift?.Exit is { } exit)
        {
            var t = exit.Trigger;
            Place(new Vector2(t[0] + (t[2] / 2f), t[1] + (t[3] / 2f)));
        }

        await Frames(6);
    }

    private async Task<T?> WaitFor<T>(Func<T, bool> condition, int frames, bool chase = false)
        where T : Enemy
    {
        for (var i = 0; i < frames; i++)
        {
            var found = World.Enemies.OfType<T>().FirstOrDefault(e => !e.Dead && condition(e));
            if (found is not null)
            {
                return found;
            }

            if (chase && i % 60 == 0 && World.Enemies.OfType<T>().FirstOrDefault(e => !e.Dead) is { } near)
            {
                Place(World.ClampToFloor(near.GlobalPosition + new Vector2(-60f * S, 30f * S)), snap: false);
            }

            await Frames(1);
        }

        return null;
    }

    private async Task WaitRoom()
    {
        await Until(() => !level.Switching && World.Director is not null, 900);
        Bind();
    }

    private async Task Until(Func<bool> condition, int frames)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            await Frames(1);
        }
    }

    private void Bind()
    {
        World.Player.Input = _input;
        _input.Move = Vector2.Zero;
    }

    private void Place(Vector2 feet, bool snap = true)
    {
        World.Player.Teleport(feet);
        if (snap)
        {
            World.Camera.Snap();
        }
    }

    private Task Frames(int count) => host.PhysicsFrames(count);
}
