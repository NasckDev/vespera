using Vespera.Core.Data;

namespace Vespera.Core.Rules;

/// <summary>Monstro escalado para um rank de Brecha: HP (float, sem arredondar), dano e HP de cada parte.</summary>
public sealed record ScaledMonster(MonsterDef Def, double Hp, double Damage, IReadOnlyList<double> PartHp);

/// <summary>
/// Escala de monstros por rank da Brecha (<c>spawnMob</c>, <c>combat.js:244-253</c>; spec 02 §3.9):
/// <c>hm = 1 + 0.4*rk</c> no HP e nas partes; <c>dm = (1 + 0.25*rk) × (Verão ? 1.15 : 1)</c> no dano. Velocidade não escala.
/// </summary>
public sealed class MonsterScaling(GameDatabase db)
{
    public double HpMult(int brechaRank) => 1 + (db.Rules.Scaling.HpPerRank * brechaRank);

    /// <summary>
    /// <paramref name="seasonId"/> só vale dentro de Brecha (o protótipo aplica o Verão via <c>G.run</c>);
    /// para invasão na cidade passe null.
    /// </summary>
    public double DamageMult(int brechaRank, string? seasonId)
    {
        var season = seasonId is null ? null : db.SeasonData.Seasons.FirstOrDefault(s => s.Id == seasonId);
        return (1 + (db.Rules.Scaling.DamagePerRank * brechaRank)) * (season?.MonsterDamageMult ?? 1);
    }

    public ScaledMonster Scale(MonsterDef monster, int brechaRank, string? seasonId)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var hm = HpMult(brechaRank);
        var parts = new double[monster.Parts.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = monster.Parts[i].Hp * hm;
        }

        return new ScaledMonster(monster, monster.Hp * hm, monster.Damage * DamageMult(brechaRank, seasonId), parts);
    }
}
