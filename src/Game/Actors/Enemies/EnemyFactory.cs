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
        "afogado" => new Afogado(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "ancoreiro" => new Ancoreiro(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "lanterna_afogada" => new LanternaAfogada(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
        "carpideira" => new Carpideira(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "cao_de_vigilia" => new CaoDeVigilia(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "velinha" => new Velinha(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
        "sineiro" => new Sineiro(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "casca" => new Casca(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "traca_de_arquivo" => new TracaDeArquivo(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
        "varal" => new Varal(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "despejado" => new Despejado(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "rasgo" => new Rasgo(world) { StartDelay = (float)GD.RandRange(0.0, 0.8) },
        "ceifador_pomar" => new CeifadorPomar(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
        "sombra_cacador" => new SombraCacador(world, Vespera.Core.Rift.AvessoMoves.ShadowFor(World.Rift.RiftSession.Save.CurrentWeapon)) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
        _ when type.StartsWith("sombra_cacador_", System.StringComparison.Ordinal) => new SombraCacador(world, type),
        _ => new Lodo(world) { StartDelay = (float)GD.RandRange(0.0, 0.6) },
    };

    /// <summary>Guardiões que já existem no jogo (as duplas do Ato VII só chamam quem existe).</summary>
    public static bool HasGuardian(string type) => type is "lodo_mae" or "macieira_oca" or "farol_afogado" or "velora" or "sineiro_mudo" or "inquilino";

    public static Enemy Guardian(string type, LevelWorld world) => type switch
    {
        "macieira_oca" => new MacieiraOca(world),
        "farol_afogado" => new FarolAfogado(world),
        "inquilino" => new Inquilino(world),
        "reflexo" => new Reflexo(world),
        "velora" => new Velora(world),
        "sineiro_mudo" => new SineiroMudo(world),
        _ => new LodoMae(world),
    };
}
