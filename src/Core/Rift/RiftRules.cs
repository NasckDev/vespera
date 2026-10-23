using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.Save;

namespace Vespera.Core.Rift;

/// <summary>Monstro da Brecha já escalado para o rank (HP e dano).</summary>
public readonly record struct RiftMonsterStats(string Id, double Hp, double Damage, RiftMonsterDef Def);

/// <summary>
/// Regras de uma caçada numa Brecha (PLANO-V2 §6, GDD §4 e §5.5) sobre o Core existente: escala por rank
/// (<c>rules.scaling</c>), fases e divisões do Guardião, Fragmentos na bolsa da caçada e a Lanterna na morte.
/// </summary>
public sealed class RiftRules(GameDatabase db, BrechaDef brecha)
{
    public BrechaDef Brecha => brecha;

    /// <summary>Material que conta como Fragmento (o Estilhaço de Brecha do save).</summary>
    public string FragmentMaterial => db.Rules.Economy.ShardMaterial;

    public int RankIndex => Math.Max(0, db.RankIndexOf(brecha.Rank));

    /// <summary>HP e dano do monstro no rank da Brecha: <c>hp × (1 + 0,4·rk)</c>, <c>dano × (1 + 0,25·rk)</c>.</summary>
    public RiftMonsterStats Stats(string monsterId)
    {
        if (!brecha.Monsters.TryGetValue(monsterId, out var def))
        {
            throw new ArgumentException($"Monstro '{monsterId}' não existe na Brecha '{brecha.Id}'.", nameof(monsterId));
        }

        var rk = RankIndex;
        var hp = def.Hp * (1 + (db.Rules.Scaling.HpPerRank * rk));
        var damage = def.Damage * (1 + (db.Rules.Scaling.DamagePerRank * rk));
        return new RiftMonsterStats(monsterId, hp, damage, def);
    }

    /// <summary>Fase do Guardião (1, 2 ou 3) pela fração de HP restante.</summary>
    public int BossPhase(double hp, double maxHp)
    {
        var fraction = maxHp <= 0 ? 0 : hp / maxHp;
        var phases = brecha.Boss.Phases;
        if (fraction > phases[0])
        {
            return 1;
        }

        return fraction > phases[1] ? 2 : 3;
    }

    /// <summary>Em quantos a mãe se divide na fase 3: cada núcleo quebrado tira uma divisão (GDD §4).</summary>
    public int SplitCount(int coresBroken) => Math.Max(0, brecha.Boss.SplitBase - Math.Clamp(coresBroken, 0, brecha.Boss.Cores));

    public int RollFragments(string monsterId, IRandom random) => random.RangeInt(brecha.Monsters[monsterId].Fragments);

    public int RollChestFragments(IRandom random) => random.RangeInt(brecha.ChestFragments);

    public static void AddMaterial(RunBag bag, string materialId, int amount)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (amount <= 0)
        {
            return;
        }

        bag.Materials[materialId] = (bag.Materials.TryGetValue(materialId, out var n) ? n : 0) + amount;
    }

    public void AddFragments(RunBag bag, int amount) => AddMaterial(bag, FragmentMaterial, amount);

    public int Fragments(RunBag bag) => bag.Materials.TryGetValue(FragmentMaterial, out var n) ? n : 0;

    /// <summary>Guardião vencido: garante o material dele na bolsa.</summary>
    public void AddGuardianLoot(RunBag bag) => AddMaterial(bag, brecha.GuardianMaterial, brecha.GuardianMaterialCount);

    /// <summary>
    /// Morte dentro da Brecha (GDD §5.5): a bolsa da caçada fica na Lanterna, na sala e no ponto onde caiu.
    /// Se já havia uma Lanterna perdida, ela some para o Avesso e esta toma o lugar.
    /// </summary>
    public LostLantern DropLantern(SaveData save, RunBag bag, string room, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(bag);
        var lantern = new LostLantern
        {
            BrechaId = brecha.LanternId,
            Bairro = brecha.Bairro,
            ThemeId = brecha.Id,
            Bag = bag,
            MapId = brecha.Id,
            Room = room,
            X = x,
            Y = y,
        };
        save.LostLantern = bag.IsEmpty ? null : lantern;
        return lantern;
    }

    /// <summary>A Lanterna perdida está nesta sala desta Brecha.</summary>
    public bool IsLanternHere(SaveData save, string room) => LanternRules.IsLanternHere(save, brecha.LanternId, room);

    /// <summary>Recolheu a Lanterna: a bolsa dela volta para a bolsa da caçada atual e o save fica sem Lanterna perdida.</summary>
    public static void RecoverLantern(SaveData save, RunBag bag)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(bag);
        if (save.LostLantern is not { } lost)
        {
            return;
        }

        foreach (var (id, amount) in lost.Bag.Materials)
        {
            AddMaterial(bag, id, amount);
        }

        bag.Lumes += lost.Bag.Lumes;
        bag.Gear.AddRange(lost.Bag.Gear);
        bag.Lanterns.AddRange(lost.Bag.Lanterns);
        save.LostLantern = null;
    }

    /// <summary>Saiu pelo portal depois do Guardião: a bolsa entra no save e a Brecha conta como fechada.</summary>
    public static void CloseBrecha(SaveData save, RunBag bag)
    {
        LanternRules.MergeBag(save, bag);
        save.BrechasClosed++;
        save.FirstBrechaDone = true;
    }

    /// <summary>Pausa no impacto: a do golpe, maior se matou (peso do golpe final), entre 40 e 120 ms (PLANO-V2 §2).</summary>
    public static double HitStop(double moveHitStop, bool killed) => Math.Clamp(moveHitStop + (killed ? 0.03 : 0), 0.04, 0.12);
}
