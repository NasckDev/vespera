using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Core.Rift;
using Vespera.Core.Save;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Estado da caçada em curso (fora das cenas, para sobreviver à troca de sala): a Brecha e suas regras de combate, a sala
/// atual, a classe escolhida e o sorteio. Fragmentos, saque, XP e a Lanterna são da <see cref="Progression"/> (fonte
/// única): entrar chama <see cref="Progression.BeginRun"/> e sair, <see cref="Progression.EndRun"/>. Sem save ativo
/// (teste direto da fase) usa um save temporário de jogo novo e a progressão fica de fora.
/// </summary>
public static class RiftSession
{
    public const string BrechaFile = "res://src/Data/brecha_esgotos.json";

    /// <summary>Brechas jogáveis por id (zona "brecha" da fase com <c>"brecha": "&lt;id&gt;"</c>; sem id = esgotos).</summary>
    public static readonly System.Collections.Generic.Dictionary<string, string> BrechaFiles = new()
    {
        ["esgotos_do_mercado"] = BrechaFile,
        ["pomar_podre"] = "res://src/Data/brecha_pomar.json",
        ["ruas_invertidas"] = "res://src/Data/brecha_ruas.json",
    };

    private static readonly System.Collections.Generic.Dictionary<string, BrechaDef> Loaded = [];
    private static string _selected = "esgotos_do_mercado";
    private static RiftRules? _rules;
    private static SaveData? _fallback;

    public static GameDatabase Db => SaveService.Database;

    /// <summary>A Brecha escolhida (a última em que o caçador entrou ou vai entrar).</summary>
    public static BrechaDef Brecha => Load(_selected);

    public static RiftRules Rules => _rules ??= new RiftRules(Db, Brecha);

    /// <summary>Regras da Brecha que tem este monstro (a escolhida, se tiver; senão a primeira que tiver — capturas de escala).</summary>
    public static RiftRules RulesFor(string monsterId)
    {
        if (Brecha.Monsters.ContainsKey(monsterId))
        {
            return Rules;
        }

        foreach (var id in BrechaFiles.Keys)
        {
            var def = Load(id);
            if (def.Monsters.ContainsKey(monsterId))
            {
                return new RiftRules(Db, def);
            }
        }

        return Rules;
    }

    /// <summary>Escolhe a Brecha da próxima caçada (fora da Brecha). Id vazio ou desconhecido = esgotos.</summary>
    public static void Select(string? brechaId)
    {
        var id = !string.IsNullOrEmpty(brechaId) && BrechaFiles.ContainsKey(brechaId) ? brechaId : "esgotos_do_mercado";
        if (id != _selected)
        {
            _selected = id;
            _rules = null;
        }
    }

    public static BrechaDef Load(string id)
    {
        if (!Loaded.TryGetValue(id, out var def))
        {
            var file = BrechaFiles[id];
            def = BrechaDef.Parse(UserFile.ReadText(file) ?? throw new System.InvalidOperationException($"Dado do jogo ausente: {file}"));
            Loaded[id] = def;
        }

        return def;
    }

    public static SaveData Save => GameSession.Data ?? (_fallback ??= SaveData.NewGame(Db, null, SaveService.DefaultClassId));

    /// <summary>Rank da Brecha (para o saque da progressão).</summary>
    public static Rank BreachRank => System.Enum.TryParse<Rank>(Brecha.Rank, out var rank) ? rank : Rank.E;

    public static IRandom Random { get; } = new SeededRandom(20261022);

    /// <summary>Dentro da Brecha (sala índice <see cref="RoomIndex"/>); fora = Praça.</summary>
    public static bool InRift { get; private set; }

    public static int RoomIndex { get; private set; }

    /// <summary>Chão (fase) da sala atual: o da caçada sorteada; sem caçada, a lista fixa do JSON da Brecha.</summary>
    public static string CurrentRoom => HuntService.Plan is { } plan ? plan.Rooms[RoomIndex].Ground : Brecha.Rooms[RoomIndex];

    /// <summary>Salas desta caçada (5-7, sorteadas a cada entrada).</summary>
    public static int RoomCount => HuntService.Plan?.Rooms.Count ?? Brecha.Rooms.Count;

    public static bool IsLastRoom => RoomIndex >= RoomCount - 1;

    /// <summary>Bot de combate: caçador invulnerável e dano ×10 para a caçada automática caber no teste.</summary>
    public static bool Cheat { get; set; }

    /// <summary>Classe do caçador, guardada no save como arma atual.</summary>
    public static HunterClass Hunter
    {
        get => HunterFor(Save.CurrentWeapon);
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

    /// <summary>Fragmentos desta caçada (ficam na Brecha se o caçador cair).</summary>
    public static int Fragments => Progression.IsActive ? Progression.Engine.RunFragmentos : 0;

    /// <summary>Fragmentos na carteira (já trazidos para a cidade).</summary>
    public static int WalletFragments => Progression.IsActive ? Progression.State.Fragmentos : 0;

    /// <summary>Fragmentos esperando na Lanterna perdida no bairro desta Brecha.</summary>
    public static int LanternFragments => Progression.IsActive ? Progression.Engine.LanternFragmentos(Brecha.NeighborhoodId) : 0;

    public static HunterClass HunterFor(string weaponId) => weaponId switch
    {
        "bow" => HunterClass.Arco,
        "hammer" => HunterClass.Martelo,
        _ => HunterClass.Lamina,
    };

    /// <summary>
    /// Entra na Brecha: monta a caçada (salas sorteadas, ver <see cref="HuntService"/>), começa a caçada na progressão e
    /// devolve os Fragmentos recuperados da Lanterna do bairro.
    /// </summary>
    public static int BeginRun()
    {
        RoomIndex = 0;
        HuntService.BeginRun();
        InRift = true;
        return Progression.IsActive ? Progression.BeginRun(Brecha.NeighborhoodId) : 0;
    }

    public static void AdvanceRoom()
    {
        if (!IsLastRoom)
        {
            RoomIndex++;
        }
    }

    /// <summary>Volta à Praça: vivo leva os Fragmentos para a carteira; caído, eles ficam na Lanterna do bairro.</summary>
    public static Core.Advancement.RunEnd? EndRun(bool survived) => EndRunWithSummary(survived, completed: survived).End;

    /// <summary>Como <see cref="EndRun"/>, com o resumo da caçada (contratos pagos, XP, rank) para a tela de resultados.</summary>
    public static (Core.Advancement.RunEnd? End, RunSummary? Summary) EndRunWithSummary(bool survived, bool completed)
    {
        var end = Progression.IsActive ? Progression.EndRun(survived) : null;
        var summary = HuntService.EndRun(survived, completed, end);
        InRift = false;
        RoomIndex = 0;
        return (end, summary);
    }

    /// <summary>Esquece o save temporário e a caçada (troca de sessão ou de cena).</summary>
    public static void Reset()
    {
        _fallback = null;
        InRift = false;
        RoomIndex = 0;
        HuntService.Reset();
    }
}
