using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Combat;

public enum ElementOutcome
{
    None,
    Weak,
    Resist,
}

/// <summary>Entrada de um golpe no monstro (<c>hitMob</c>, <c>combat.js:164-183</c>).</summary>
/// <param name="Side">Lado atingido (<see cref="DamageCalculator.HitSide"/>).</param>
/// <param name="Backstab">Multiplicador pelas costas da fonte (adagas 2, Dança das Sombras 1.5); 0 = nenhum.</param>
/// <param name="CritChance">Chance de crítico em pontos percentuais (<c>stats().crit</c>).</param>
/// <param name="Element">Elemento do golpe (infusão), ou null. Ecos comuns não passam elemento.</param>
/// <param name="Weak">Fraquezas do monstro.</param>
/// <param name="Resist">Resistências do monstro.</param>
public readonly record struct MonsterHitContext(PartSide Side, double Backstab, int CritChance, string? Element, IReadOnlyList<string> Weak, IReadOnlyList<string> Resist);

/// <summary>Resultado de um golpe no monstro: dano final e o que o modificou (para o texto flutuante).</summary>
public readonly record struct MonsterHit(int Damage, bool Crit, ElementOutcome Element, double Mult);

/// <summary>
/// Fórmulas de dano da v2 (spec 02 §3.4-3.5). O sorteio vem de um <see cref="IRandom"/> injetado (D8), na mesma
/// ordem do protótipo: primeiro o crítico, depois a variação.
/// </summary>
public sealed class DamageCalculator(CombatRules rules)
{
    /// <summary>
    /// Lado do monstro atingido: <c>(src.x - m.x) * m.face &gt;= 0</c> é frente (a igualdade conta como frente).
    /// <paramref name="face"/> é 1 (olhando para a direita) ou -1.
    /// </summary>
    public static PartSide HitSide(double sourceX, double monsterX, int face) => (sourceX - monsterX) * face >= 0 ? PartSide.Front : PartSide.Back;

    /// <summary>
    /// <c>max(1, round(base * mult * R(0.92, 1.08)))</c>, com <c>mult</c> = costas × crítico (1.6) × fraqueza (1.5) ou
    /// resistência (0.6). Fraqueza tem prioridade sobre resistência.
    /// </summary>
    public MonsterHit ToMonster(double baseDamage, in MonsterHitContext ctx, IRandom random)
    {
        var mult = 1.0;
        if (ctx.Backstab > 0 && ctx.Side == PartSide.Back)
        {
            mult *= ctx.Backstab;
        }

        var crit = random.NextDouble() * 100 < ctx.CritChance;
        if (crit)
        {
            mult *= rules.CritMult;
        }

        var element = ElementOutcome.None;
        if (ctx.Element is { } el)
        {
            if (ctx.Weak.Contains(el))
            {
                mult *= rules.WeakMult;
                element = ElementOutcome.Weak;
            }
            else if (ctx.Resist.Contains(el))
            {
                mult *= rules.ResistMult;
                element = ElementOutcome.Resist;
            }
        }

        var damage = Math.Max(1, JsMath.Round(baseDamage * mult * random.Range(rules.Variance)));
        return new MonsterHit(damage, crit, element, mult);
    }

    /// <summary>Dano recebido: <c>max(1, round(dmg * (1 - min(0.6, def/100)) * R(0.9, 1.1)))</c> (<c>combat.js:16</c>).</summary>
    public int ToPlayer(double damage, int defense, IRandom random)
    {
        var reduction = Math.Min(rules.DefenseCap, defense / 100.0);
        return Math.Max(1, JsMath.Round(damage * (1 - reduction) * random.Range(rules.PlayerVariance)));
    }

    /// <summary>Dano na parte: dano do golpe × multiplicador de parte da fonte (<c>o.part || 1</c>).</summary>
    public static double PartDamage(int damage, double partMult) => damage * (partMult > 0 ? partMult : 1);

    /// <summary>
    /// Parte que recebe o golpe: a primeira, na ordem dos dados, não quebrada e com lado igual ou <c>any</c>.
    /// Devolve o índice ou -1. <paramref name="broken"/> tem um item por parte.
    /// </summary>
    public static int SelectPart(IReadOnlyList<PartDef> parts, IReadOnlyList<bool> broken, PartSide side)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (!broken[i] && (parts[i].Side == side || parts[i].Side == PartSide.Any))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Parte com este id existe e está quebrada. Corrige o <c>broken()</c> do protótipo, que devolvia true para
    /// partes inexistentes (<c>combat.js:257</c>; DECISIONS D6).
    /// </summary>
    public static bool IsPartBroken(IReadOnlyList<PartDef> parts, IReadOnlyList<bool> broken, string partId)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Id == partId)
            {
                return broken[i];
            }
        }

        return false;
    }

    /// <summary>Escala do knockback no monstro: chefe 0, elite 0.35, demais 1.</summary>
    public double KnockbackScale(bool elite, bool boss) => boss ? 0 : elite ? rules.EliteKnockback : 1;

    /// <summary>Essência após acertar: <c>min(100, ess + 3.2 * mult)</c>. Bonecos de treino não dão Essência.</summary>
    public double EssenceAfterHit(double current, double essenceMult, bool dummy = false) =>
        dummy ? current : Math.Min(rules.EssenceMax, current + (rules.EssencePerHit * essenceMult));

    /// <summary>HP depois do dano, nunca abaixo de zero.</summary>
    public static int ApplyDamage(int currentHp, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        return Math.Max(0, currentHp - damage);
    }
}
