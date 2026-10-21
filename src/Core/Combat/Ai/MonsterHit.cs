using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Combat.Ai;

/// <summary>Opções de um golpe no monstro (<c>o</c> de <c>hitMob</c>): multiplicador em partes, pelas costas, empurrão, elemento.</summary>
public readonly record struct HitOptions(double Part = 1, double Backstab = 0, double Knockback = 0, string? Element = null);

/// <summary>Resultado de um golpe: dano, lado, parte quebrada (−1 = nenhuma) e se matou.</summary>
public readonly record struct HitOutcome(MonsterHit Hit, PartSide Side, int BrokenPart, bool Killed);

/// <summary>
/// <c>hitMob</c> sem efeitos visuais (<c>combat.js:164-183</c>; spec 02 §3.4): lado, dano com crítico/elemento/costas,
/// flash, dano na primeira parte não quebrada do lado (ou <c>any</c>), empurrão (chefe 0, elite ×0.35), idle → chase.
/// Bonecos de treino só calculam o dano.
/// </summary>
public static class MonsterHitResolver
{
    public static HitOutcome Apply(MonsterBody m, double baseDamage, double srcX, double srcY, in HitOptions o, int critChance, DamageCalculator calc, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(calc);
        var side = DamageCalculator.HitSide(srcX, m.X, m.Face);
        var ctx = new MonsterHitContext(side, o.Backstab, critChance, o.Element, m.Def.Weak, m.Def.Res);
        var hit = calc.ToMonster(baseDamage, ctx, random);
        m.Flash = MonsterBody.FlashSeconds;
        if (m.IsDummy)
        {
            return new HitOutcome(hit, side, -1, false);
        }

        m.Hp -= hit.Damage;
        var part = DamageCalculator.SelectPart(m.Def.Parts, m.Broken, side);
        var broke = part >= 0 ? m.DamagePart(part, DamageCalculator.PartDamage(hit.Damage, o.Part)) : -1;

        if (o.Knockback > 0 && !m.IsBoss)
        {
            var dx = m.X - srcX;
            var dy = m.Y - srcY;
            var d = CombatMath.Hypot(dx, dy);
            if (d == 0)
            {
                d = 1;
            }

            var k = o.Knockback * calc.KnockbackScale(m.IsElite, m.IsBoss);
            m.Kx = dx / d * k;
            m.Ky = dy / d * k;
        }

        if (m.State == MonsterState.Idle)
        {
            m.ForceState(MonsterState.Chase);
        }

        var killed = m.Hp <= 0;
        if (killed)
        {
            m.Kill();
        }

        return new HitOutcome(hit, side, broke, killed);
    }
}
