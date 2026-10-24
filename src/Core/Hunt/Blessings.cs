using System.Text.Json;
using System.Text.Json.Serialization;
using Vespera.Core.Common;

namespace Vespera.Core.Hunt;

/// <summary>Família de bênçãos: nome, personagem e cor (o jogador aprende a ler pela cor).</summary>
public sealed class BlessingFamily
{
    public string Name { get; set; } = string.Empty;

    public string Who { get; set; } = string.Empty;

    public string Color { get; set; } = "#ffffff";
}

/// <summary>
/// Uma bênção da Lanterna (ROTEIRO-V2 §5.1) ou a soma de várias (<see cref="BlessingCatalog.Total"/>). Valores em
/// pontos percentuais, segundos ou vida.
/// </summary>
public sealed class BlessingDef
{
    public string Id { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public double DamagePct { get; set; }

    public double CritPct { get; set; }

    public double PartPct { get; set; }

    public double DodgeDistancePct { get; set; }

    public double DodgeInvulnerable { get; set; }

    public double DodgeCostPct { get; set; }

    public double HurtInvulnerable { get; set; }

    public int MaxHp { get; set; }

    public double HealOnClearPct { get; set; }

    public int HealOnKill { get; set; }

    public double PotionPct { get; set; }

    public double FragmentPct { get; set; }

    public int LumesPerBreakable { get; set; }

    public double SpeedPct { get; set; }

    public double LowHpDamagePct { get; set; }

    /// <summary>Multiplicador de dano (com <paramref name="lowHp"/> = menos de metade da vida).</summary>
    public double DamageMultiplier(bool lowHp) =>
        Math.Max(0.1, 1 + (DamagePct / 100.0) + (lowHp ? LowHpDamagePct / 100.0 : 0));
}

/// <summary>Catálogo das bênçãos (data/cacada/bencaos.json): sorteio das 3 cartas e soma dos efeitos.</summary>
public sealed class BlessingCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public const int OfferSize = 3;

    public Dictionary<string, BlessingFamily> Families { get; set; } = [];

    public List<BlessingDef> Blessings { get; set; } = [];

    /// <summary>Bênção dada ao recuperar a Lanterna (ROTEIRO-V2 §6.1).</summary>
    public BlessingDef Teimosia { get; set; } = new();

    public static BlessingCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<BlessingCatalog>(json, Options) ?? throw new FormatException("bencaos.json vazio");
        foreach (var b in catalog.Blessings.Where(b => !catalog.Families.ContainsKey(b.Family)))
        {
            throw new FormatException($"bênção '{b.Id}' com família desconhecida '{b.Family}'");
        }

        return catalog;
    }

    public BlessingDef? Find(string id) => id == Teimosia.Id ? Teimosia : Blessings.FirstOrDefault(b => b.Id == id);

    /// <summary>Três cartas que o caçador ainda não tem, de famílias diferentes sempre que possível.</summary>
    public List<BlessingDef> Offer(IRandom random, IReadOnlyCollection<string> owned)
    {
        var pool = Blessings.Where(b => !owned.Contains(b.Id)).ToList();
        var offer = new List<BlessingDef>(OfferSize);
        while (offer.Count < OfferSize && pool.Count > 0)
        {
            var fresh = pool.Where(b => offer.All(o => o.Family != b.Family)).ToList();
            var from = fresh.Count > 0 ? fresh : pool;
            var pick = from[random.RangeInt(0, from.Count - 1)];
            offer.Add(pick);
            pool.Remove(pick);
        }

        return offer;
    }

    /// <summary>Soma dos efeitos das bênçãos <paramref name="ids"/> (ids desconhecidos são ignorados).</summary>
    public BlessingDef Total(IEnumerable<string> ids)
    {
        var t = new BlessingDef { Id = "total" };
        foreach (var b in ids.Select(Find).OfType<BlessingDef>())
        {
            t.DamagePct += b.DamagePct;
            t.CritPct += b.CritPct;
            t.PartPct += b.PartPct;
            t.DodgeDistancePct += b.DodgeDistancePct;
            t.DodgeInvulnerable += b.DodgeInvulnerable;
            t.DodgeCostPct += b.DodgeCostPct;
            t.HurtInvulnerable += b.HurtInvulnerable;
            t.MaxHp += b.MaxHp;
            t.HealOnClearPct += b.HealOnClearPct;
            t.HealOnKill += b.HealOnKill;
            t.PotionPct += b.PotionPct;
            t.FragmentPct += b.FragmentPct;
            t.LumesPerBreakable += b.LumesPerBreakable;
            t.SpeedPct += b.SpeedPct;
            t.LowHpDamagePct += b.LowHpDamagePct;
        }

        return t;
    }
}

/// <summary>Uma linha de personagem (tela de resultado e falas de morte).</summary>
public sealed record CharacterLine(string Who, string Text);

/// <summary>Falas curtas da volta da Brecha (data/cacada/falas.json): morte em ordem e depois sorteio (ROTEIRO-V2 §6.2).</summary>
public sealed class ReturnLines
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public List<CharacterLine> Death { get; set; } = [];

    public List<CharacterLine> Survive { get; set; } = [];

    public List<CharacterLine> Closed { get; set; } = [];

    public static ReturnLines Parse(string json) => JsonSerializer.Deserialize<ReturnLines>(json, Options) ?? new ReturnLines();

    /// <summary>Fala da <paramref name="deathNumber"/>-ésima morte (1 = primeira): em ordem, depois sorteada.</summary>
    public CharacterLine? ForDeath(int deathNumber, IRandom random) =>
        Death.Count == 0 ? null : deathNumber >= 1 && deathNumber <= Death.Count ? Death[deathNumber - 1] : Death[random.RangeInt(0, Death.Count - 1)];

    public CharacterLine? ForReturn(bool closed, IRandom random)
    {
        var list = closed && Closed.Count > 0 ? Closed : Survive;
        return list.Count == 0 ? null : list[random.RangeInt(0, list.Count - 1)];
    }
}
