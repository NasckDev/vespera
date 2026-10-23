using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Tamanho do caçador NA TELA (<c>--camera</c>): em cada mapa do Ato I, mede a altura opaca do caçador pelas transformações
/// (sprite × câmera × viewport, em px de uma tela 1920×1080) e exige 130–150 px (regra: ~140 px em todo mapa). Na arena,
/// mede também durante a apresentação da Lodo-Mãe (a câmera pode se afastar até 0,8×) e depois dela. Confere ainda que a
/// câmera não mostra nada fora do chão pintado quando o mapa é maior que a tela.
/// </summary>
public sealed class CameraCheck(Level level, SmokeTest host, bool shots = false)
{
    public const float MinOnScreen = 130f;
    public const float MaxOnScreen = 150f;
    private static readonly string[] CityLevels = ["praca", "forjas", "forja_interior"];

    private readonly List<string> _failures = [];

    private LevelWorld World => level.World;

    public async Task<List<string>> Run()
    {
        var rooms = new List<string>(CityLevels);
        rooms.AddRange(RiftSession.Brecha.Rooms);
        foreach (var room in rooms)
        {
            await level.GoToRoom(room, withDirector: false);
            await host.PhysicsFrames(4);
            World.Camera.Snap();
            await host.PhysicsFrames(2);
            Expect(room, MinOnScreen, MaxOnScreen);
            CheckFraming(room);
            if (shots && System.Array.IndexOf(CityLevels, room) >= 0)
            {
                await Shoot(room);
            }
        }

        if (shots)
        {
            // Sala com monstros de verdade (ondas fixas do JSON), para ver caçador × Lodo × Espectro × arcos da parede.
            var cheat = RiftSession.Cheat;
            RiftSession.Cheat = true;
            await level.GoToRoom("esgoto_corredor");
            await host.PhysicsFrames(150);
            await Shoot("esgoto_corredor");
            RiftSession.Cheat = cheat;
        }

        await CheckBossIntro();
        await level.GoToRoom(Level.HubLevelId);
        await host.PhysicsFrames(4);
        await MeasurePracaCrossing();
        return _failures;
    }

    /// <summary>Altura do caçador em px da tela 1080p, pelas transformações (o mesmo cálculo da fila de escala, × câmera).</summary>
    public static float OnScreenHeight(Level level)
    {
        var player = level.Player;
        var world = ScaleCheck.Measure(player.Sprite);
        var feet = level.WorldToScreen(player.GlobalPosition);
        var head = level.WorldToScreen(player.GlobalPosition - new Vector2(0, world));
        var toWindow = 1080f / level.GetViewportRect().Size.Y;
        return feet.DistanceTo(head) * toWindow;
    }

    private void Expect(string where, float min, float max)
    {
        var h = OnScreenHeight(level);
        var ok = h >= min && h <= max;
        Log.Info($"câmera {where}: caçador com {h:0.0} px na tela 1080p (zoom {World.Camera.Zoom.X:0.000}, aceito {min:0}–{max:0}) {(ok ? "ok" : "FORA")}");
        if (!ok)
        {
            _failures.Add($"câmera: caçador com {h:0.0} px na tela em {where} (aceito {min:0}–{max:0})");
        }
    }

    /// <summary>Com o mapa maior que a tela, a câmera presa ao chão não mostra a moldura; menor, centraliza o mapa.</summary>
    private void CheckFraming(string room)
    {
        var view = World.GetViewportRect().Size / World.Camera.Zoom;
        var center = World.Camera.GetScreenCenterPosition();
        var shown = new Rect2(center - (view / 2f), view);
        var b = World.Bounds;
        for (var axis = 0; axis < 2; axis++)
        {
            var size = axis == 0 ? b.Size.X : b.Size.Y;
            var lo = axis == 0 ? shown.Position.X - b.Position.X : shown.Position.Y - b.Position.Y;
            var hi = axis == 0 ? b.End.X - shown.End.X : b.End.Y - shown.End.Y;
            var fits = size <= (axis == 0 ? view.X : view.Y);
            var ok = fits ? Mathf.Abs(lo - hi) <= 2f : lo >= -1f && hi >= -1f;
            if (!ok)
            {
                _failures.Add($"câmera: enquadramento em {room} no eixo {(axis == 0 ? "x" : "y")} mostra fora do mapa ({lo:0}/{hi:0})");
            }
        }
    }

    private async Task Shoot(string name)
    {
        World.Camera.Snap();
        await host.PhysicsFrames(20);
        Log.Info($"qa-tela {name}: caçador {OnScreenHeight(level):0.0} px");
        await host.CaptureNow($"escala_{name}");
    }

    /// <summary>Segundos máximos para atravessar a Praça de uma ponte à outra (playtest 23/10).</summary>
    public const float MaxPracaCrossing = 6f;

    /// <summary>
    /// Atravessa a Praça andando (input de verdade, sem correr de ponte a ponte em linha reta pela lanterna): da cabeceira
    /// oeste (x 240) à leste (x 1432) pela faixa livre y 505. Mede o tempo e falha acima de <see cref="MaxPracaCrossing"/>.
    /// </summary>
    private async Task MeasurePracaCrossing()
    {
        const float startX = 240f, endX = 1432f, laneY = 505f;
        var player = level.Player;
        var input = new ScriptedInput();
        var original = player.Input;
        player.Input = input;
        player.Teleport(new Vector2(startX, laneY));
        await host.PhysicsFrames(10);
        input.Move = Vector2.Right;
        var seconds = 0f;
        while (player.GlobalPosition.X < endX && seconds < 15f)
        {
            await host.PhysicsFrames(1);
            seconds += (float)player.GetPhysicsProcessDeltaTime();
        }

        input.Move = Vector2.Zero;
        player.Input = original;
        var ok = player.GlobalPosition.X >= endX && seconds <= MaxPracaCrossing;
        Log.Info($"travessia da Praça: {endX - startX:0} px de mundo em {seconds:0.00} s ({player.MaxSpeed:0} px/s, {player.MaxSpeed / World.Data.HumanHeight:0.00} alturas/s) {(ok ? "ok" : "FORA")}");
        if (!ok)
        {
            _failures.Add($"travessia da Praça em {seconds:0.00} s (máx. {MaxPracaCrossing} s; parou em x {player.GlobalPosition.X:0})");
        }
    }

    private async Task CheckBossIntro()
    {
        var cheat = RiftSession.Cheat;
        RiftSession.Cheat = true;
        var arena = RiftSession.Brecha.Rooms[^1];
        await level.GoToRoom(arena);
        await host.PhysicsFrames(40);
        var boss = World.Director?.Boss;
        if (boss is null)
        {
            _failures.Add("câmera: a arena deveria ter a Lodo-Mãe");
            RiftSession.Cheat = cheat;
            return;
        }

        Expect($"{arena} (apresentação da Lodo-Mãe)", MinOnScreen * CameraRig.MinPullBack, MaxOnScreen);
        if (shots)
        {
            Log.Info($"qa-tela {arena}_lodo_mae: caçador {OnScreenHeight(level):0.0} px (câmera afastada na apresentação)");
            await host.CaptureNow($"escala_{arena}_lodo_mae");
        }

        for (var i = 0; i < 60 * 5 && !boss.Engaged; i++)
        {
            await host.PhysicsFrames(1);
        }

        await host.PhysicsFrames(90);
        Expect($"{arena} (luta)", MinOnScreen, MaxOnScreen);
        RiftSession.Cheat = cheat;
    }
}
