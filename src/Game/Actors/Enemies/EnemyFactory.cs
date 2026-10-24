using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>Cria o monstro pelo id do plano de caçada (data/cacada) ou do Guardião da sala (rift.boss.type).</summary>
public static class EnemyFactory
{
    public static Enemy Create(string type, LevelWorld world) => type switch
    {
        "espectro" => new Espectro(world),
        "carrapicho" => new Carrapicho(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "rato_lanterna" => new RatoLanterna(world) { StartDelay = (float)GD.RandRange(0.0, 0.5) },
        _ => new Lodo(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
    };

    public static Enemy Guardian(string type, LevelWorld world) => type switch
    {
        "macieira_oca" => new MacieiraOca(world),
        _ => new LodoMae(world),
    };
}
