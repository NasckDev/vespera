using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rift;
using Vespera.Core.Save;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Estado da caçada em curso (fora das cenas, para sobreviver à troca de sala): a Brecha e suas regras do Core,
/// a bolsa da caçada, a sala atual, a classe escolhida e o sorteio. Sem save ativo (teste direto da fase) usa um
/// save temporário de jogo novo.
/// </summary>
public static class RiftSession
{
    public const string BrechaFile = "res://src/Data/brecha_esgotos.json";

    private static BrechaDef? _brecha;
    private static RiftRules? _rules;
    private static SaveData? _fallback;

    public static GameDatabase Db => SaveService.Database;

    public static BrechaDef Brecha => _brecha ??= BrechaDef.Parse(UserFile.ReadText(BrechaFile)
        ?? throw new System.InvalidOperationException($"Dado do jogo ausente: {BrechaFile}"));

    public static RiftRules Rules => _rules ??= new RiftRules(Db, Brecha);

    public static SaveData Save => GameSession.Data ?? (_fallback ??= SaveData.NewGame(Db, null, SaveService.DefaultClassId));

    public static RunBag Bag { get; private set; } = new();

    public static IRandom Random { get; } = new SeededRandom(20261022);

    /// <summary>Dentro da Brecha (sala índice <see cref="RoomIndex"/>); fora = Praça.</summary>
    public static bool InRift { get; private set; }

    public static int RoomIndex { get; private set; }

    public static string CurrentRoom => Brecha.Rooms[RoomIndex];

    public static bool IsLastRoom => RoomIndex >= Brecha.Rooms.Count - 1;

    /// <summary>Bot de combate: caçador invulnerável e dano ×10 para a caçada automática caber no teste.</summary>
    public static bool Cheat { get; set; }

    /// <summary>Classe do caçador, guardada no save como arma atual.</summary>
    public static HunterClass Hunter
    {
        get => Save.CurrentWeapon switch
        {
            "bow" => HunterClass.Arco,
            "hammer" => HunterClass.Martelo,
            _ => HunterClass.Lamina,
        };
        set
        {
            var weapon = HunterSprites.WeaponId(value);
            Save.CurrentWeapon = weapon;
            if (Save.WeaponLevel(weapon) < 1)
            {
                Save.Weapons[weapon] = 1;
            }
        }
    }

    public static int Fragments => Rules.Fragments(Bag);

    public static void BeginRun()
    {
        Bag = new RunBag();
        RoomIndex = 0;
        InRift = true;
    }

    public static void AdvanceRoom()
    {
        if (!IsLastRoom)
        {
            RoomIndex++;
        }
    }

    /// <summary>Volta à Praça (vitória ou morte): a bolsa já foi tratada por quem chamou.</summary>
    public static void EndRun()
    {
        InRift = false;
        RoomIndex = 0;
        Bag = new RunBag();
    }

    /// <summary>Esquece o save temporário (troca de sessão).</summary>
    public static void Reset()
    {
        _fallback = null;
        EndRun();
    }
}
