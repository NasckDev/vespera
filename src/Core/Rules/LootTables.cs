using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Rules;

public enum LootKind
{
    Lumes,
    Material,
    Gear,
    Potion,
}

/// <summary>Um item que cai no chão (<c>dropPick</c>): tipo, id (material/equipamento) e quantidade.</summary>
public readonly record struct LootDrop(LootKind Kind, string? Id, int Amount)
{
    public static LootDrop Lumes(int amount) => new(LootKind.Lumes, null, amount);

    public static LootDrop Material(string id, int amount) => new(LootKind.Material, id, amount);

    public static LootDrop Gear(string id) => new(LootKind.Gear, id, 1);

    public static LootDrop Potion(int amount) => new(LootKind.Potion, null, amount);
}

/// <summary>
/// Saque da v2 (spec 02 §3.12; <c>combat.js:194-213,476-530</c>). Cada método consome o <see cref="IRandom"/> na
/// mesma ordem do protótipo, para testes determinísticos.
/// </summary>
public sealed class LootTables
{
    private readonly GameDatabase _db;
    private readonly LootRules _rules;
    private readonly IReadOnlyList<GearDef> _gearPool;

    public LootTables(GameDatabase db)
    {
        _db = db;
        _rules = db.Rules.Loot;
        _gearPool = [.. db.GearData.Gear.Where(g => !_rules.GearExclude.Contains(g.Id))];
    }

    /// <summary>
    /// <c>rollGear(boss)</c>: sorteio ponderado sobre todo equipamento menos a Lanterna de Herança. Pesos por raridade:
    /// comum 3, incomum 2, raro 0.6 (3 se chefe ou baú roxo).
    /// </summary>
    public string RollGear(bool boss, IRandom random)
    {
        var weights = boss ? _rules.GearWeightsBoss : _rules.GearWeights;
        var total = 0.0;
        foreach (var g in _gearPool)
        {
            total += weights[g.Rarity];
        }

        var x = random.NextDouble() * total;
        foreach (var g in _gearPool)
        {
            x -= weights[g.Rarity];
            if (x <= 0)
            {
                return g.Id;
            }
        }

        return _gearPool[0].Id;
    }

    /// <summary>
    /// <c>killMob</c>: Estilhaço (chefe ×4 e mob de invasão ×1 sempre; demais 70%), montes de Lumes
    /// (chefe <c>RI(6,9)</c> de <c>RI(4,7)</c>; comum <c>RI(1,3)</c> de <c>RI(1,3)</c>) e, se chefe, 1 equipamento.
    /// O Eco é sorteado à parte (<see cref="RollEco"/>).
    /// </summary>
    public IReadOnlyList<LootDrop> KillDrops(MonsterDef monster, bool invasion, IRandom random)
    {
        var drops = new List<LootDrop>();
        if (invasion || monster.Boss || random.Chance(_rules.KillShardChance))
        {
            drops.Add(LootDrop.Material(_db.Rules.Economy.ShardMaterial, monster.Boss ? _rules.BossShards : _rules.KillShards));
        }

        var piles = random.RangeInt(monster.Boss ? _rules.BossCoinPiles : _rules.CoinPiles);
        for (var i = 0; i < piles; i++)
        {
            drops.Add(LootDrop.Lumes(random.RangeInt(monster.Boss ? _rules.BossCoinPileSize : _rules.CoinPileSize)));
        }

        if (monster.Boss)
        {
            drops.Add(LootDrop.Gear(RollGear(boss: true, random)));
        }

        return drops;
    }

    /// <summary>Chance de o monstro deixar seu Eco: 0 se já possui; chefe 0.75; demais o <c>ecoChance</c> do dado.</summary>
    public double EcoChance(MonsterDef monster, bool alreadyOwned)
    {
        if (monster.Eco is null || alreadyOwned)
        {
            return 0;
        }

        return monster.Boss ? _rules.BossEcoChance : monster.EcoChance;
    }

    /// <summary>Sorteia o Eco do monstro. Consome um sorteio sempre que o monstro tem Eco (como o protótipo).</summary>
    public string? RollEco(MonsterDef monster, bool alreadyOwned, IRandom random)
    {
        if (monster.Eco is null)
        {
            return null;
        }

        return random.NextDouble() < EcoChance(monster, alreadyOwned) ? monster.Eco : null;
    }

    /// <summary><c>breakPart</c>: <c>Count</c> drops de 1 do material da parte.</summary>
    public IReadOnlyList<LootDrop> PartDrops(PartDef part)
    {
        var drops = new LootDrop[part.Count];
        Array.Fill(drops, LootDrop.Material(part.Drop, 1));
        return drops;
    }

    /// <summary>Quantos baús numa sala de Brecha: <c>1 + (Primavera ? 1 : 0) + (30% ? 1 : 0)</c>.</summary>
    public int ChestCount(SeasonDef season, IRandom random) =>
        _rules.Chest.BaseCount + season.ExtraChests + (random.Chance(_rules.Chest.ExtraChance) ? 1 : 0);

    /// <summary>Baú roxo (25%): mais chance de equipamento e pesos de chefe.</summary>
    public bool RollPurpleChest(IRandom random) => random.Chance(_rules.Chest.PurpleChance);

    /// <summary>
    /// <c>openChest</c>: <c>RI(3,5)</c> montes de <c>RI(2,5)</c> Lumes, <c>RI(1,2)</c> Estilhaços, 1 material básico,
    /// equipamento 30% (roxo 85%) e poção 35%. O número de montes é sorteado uma vez (o laço do protótipo
    /// re-sorteava o limite a cada volta, <c>combat.js:525</c>; DECISIONS D6).
    /// </summary>
    public IReadOnlyList<LootDrop> ChestDrops(bool purple, IRandom random)
    {
        var chest = _rules.Chest;
        var drops = new List<LootDrop>();
        var piles = random.RangeInt(chest.CoinPiles);
        for (var i = 0; i < piles; i++)
        {
            drops.Add(LootDrop.Lumes(random.RangeInt(chest.CoinPileSize)));
        }

        drops.Add(LootDrop.Material(_db.Rules.Economy.ShardMaterial, random.RangeInt(chest.Shards)));
        drops.Add(LootDrop.Material(random.Pick(chest.BasicMaterials), 1));
        if (random.Chance(purple ? chest.PurpleGearChance : chest.GearChance))
        {
            drops.Add(LootDrop.Gear(RollGear(purple, random)));
        }

        if (random.Chance(chest.PotionChance))
        {
            drops.Add(LootDrop.Potion(1));
        }

        return drops;
    }

    /// <summary>
    /// <c>breakProp</c>: um sorteio <c>r</c>: &lt;0.45 Lumes <c>RI(1,3)</c>; &lt;0.75 material (&lt;0.6 o do tema, senão erva);
    /// &lt;0.82 poção; senão nada.
    /// </summary>
    public LootDrop? PropDrop(ThemeDef theme, IRandom random)
    {
        var prop = _rules.Prop;
        var r = random.NextDouble();
        if (r < prop.LumesBelow)
        {
            return LootDrop.Lumes(random.RangeInt(prop.Lumes));
        }

        if (r < prop.MaterialBelow)
        {
            return LootDrop.Material(r < prop.ThemeMaterialBelow ? theme.PropMaterial : prop.FallbackMaterial, 1);
        }

        return r < prop.PotionBelow ? LootDrop.Potion(1) : null;
    }
}
