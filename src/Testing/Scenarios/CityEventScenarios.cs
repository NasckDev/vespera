using System;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World.City;

namespace Vespera.Testing;

/// <summary>
/// A véspera como evento (B2-D51): a mesma esquina da Rua da Forja de dia, na véspera com o portal aberto e na noite
/// fechada, com prints para olhar em 1080p.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("cidade: véspera é um evento — dia, véspera e noite na Rua da Forja", order: 64)]
    private async Task CityVesperaIsAnEvent()
    {
        var spot = World.World.Catalog["forge"].PortalSpots[0];
        var stand = new Vector2(spot.X + 40, spot.Y + 70);
        var (world, city) = await StartCity("forge", stand, 16 * 60 + 30);
        try
        {
            city.TimeScale = 0f;
            var state = GameManager.Instance.State;
            var player = FindPlayer()!;
            player.Teleport(world.CorrectToWalkable(stand));
            world.Camera.Snap();
            await ProcessFrames(4);
            var day = city.Lighting.CurrentDarkness;
            await Shot("evento-1-dia");

            state.Save.ClockMinutes = 18 * 60;
            city.Brechas.OpenSoon(0);
            city.TimeScale = 1f;
            await WaitUntil(() => city.Brechas.OpenAt(0) is not null, 3, "Brecha aberta no portal 0");
            city.TimeScale = 0f;
            await WaitSeconds(0.45);
            await Shot("evento-2-abrindo"); // estouro de luz e onda de choque
            await WaitSeconds(1.0); // termina a abertura
            var vespera = city.Lighting.CurrentDarkness;
            var warm = city.Lighting.CurrentGrade;
            await Shot("evento-3-vespera");

            state.Save.ClockMinutes = 22 * 60;
            await ProcessFrames(4);
            var night = city.Lighting.CurrentDarkness;
            var cold = city.Lighting.CurrentGrade;
            await Shot("evento-4-noite");

            Log.Info($"cidade: escuridão na Rua da Forja — dia {day:0.000}, véspera {vespera:0.000}, noite {night:0.000}; cor véspera {warm}, noite {cold}");
            Require(day < 0.12f, $"de dia a rua fica clara como na v2 ({day:0.000})");
            Require(vespera > day + 0.1f, $"às 18:00 a rua já escureceu bem mais que de dia ({vespera:0.000})");
            Require(night >= 0.68f, $"noite fechada na rua escurece de verdade ({night:0.000}, a v2 parava em 0,372)");
            Require(warm.R > warm.B + 0.1f, $"véspera tem luz quente ({warm})");
            Require(cold.B > cold.R + 0.1f, $"noite tem luz fria ({cold})");
            var portal = FindAll<PortalVisual>()[0];
            var light = portal.GetNode<LightEmitter>("Light");
            Require(light.Radius >= PortalVisual.LightRadius * 0.9f && light.Intensity > 0.9f, $"luz do portal aberto: raio {light.Radius:0}, intensidade {light.Intensity:0.00}");
        }
        finally
        {
            StopCity(city);
        }
    }
}
