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
/// (sprite × câmera × viewport, em px de uma tela 1920×1080) e exige 95–125 px (regra: ~110 px em todo mapa). Na arena,
/// mede também durante a apresentação da Lodo-Mãe (a câmera pode se afastar até 0,8×) e depois dela. Confere ainda que a
/// câmera não mostra nada fora do chão pintado quando o mapa é maior que a tela.
/// </summary>
public sealed class CameraCheck(Level level, SmokeTest host)
{
    public const float MinOnScreen = 95f;
    public const float MaxOnScreen = 125f;
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
        }

        await CheckBossIntro();
        await level.GoToRoom(Level.HubLevelId);
        await host.PhysicsFrames(4);
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
        for (var i = 0; i < 60 * 5 && !boss.Engaged; i++)
        {
            await host.PhysicsFrames(1);
        }

        await host.PhysicsFrames(90);
        Expect($"{arena} (luta)", MinOnScreen, MaxOnScreen);
        RiftSession.Cheat = cheat;
    }
}
