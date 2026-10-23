using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Combat;

public enum Team
{
    Hunters,
    Monsters,
}

/// <summary>Algo que pode levar golpe: corpo de monstro, núcleo do Guardião ou caçador.</summary>
public interface IHittable
{
    Team Team { get; }

    /// <summary>Centro do corpo no mundo (não o pé): é contra ele que os golpes medem distância.</summary>
    Vector2 HitCenter { get; }

    float HitRadius { get; }

    bool CanBeHit { get; }

    HitOutcome ReceiveHit(in HitInfo hit);
}

/// <summary>
/// Um golpe já calculado pelo atacante: dano base (ataque × multiplicador do golpe), origem, empurrão (vetor em px/s),
/// pausa no impacto pedida, multiplicador em partes e chance de crítico (pontos percentuais).
/// </summary>
public readonly record struct HitInfo(double Damage, Vector2 From, Vector2 Push, double HitStop, double PartMult, bool Heavy, int CritChance);

/// <summary>Resultado de um golpe: dano final, se matou/quebrou e se foi crítico.</summary>
public readonly record struct HitOutcome(int Damage, bool Killed, bool Crit, bool Landed)
{
    public static HitOutcome Miss => default;
}

/// <summary>
/// Registro dos alvos de uma sala e consultas geométricas dos golpes (setor e círculo). Sem física: os golpes medem
/// a distância ao centro do corpo, o que funciona igual com janela e headless.
/// </summary>
public sealed class CombatSpace
{
    private readonly List<IHittable> _targets = [];

    public IReadOnlyList<IHittable> Targets => _targets;

    public void Register(IHittable target)
    {
        if (!_targets.Contains(target))
        {
            _targets.Add(target);
        }
    }

    public void Unregister(IHittable target) => _targets.Remove(target);

    /// <summary>Alvos do outro time cujo círculo toca o setor (abertura <paramref name="arc"/> rad; 0 = círculo cheio).</summary>
    public List<IHittable> InSector(Vector2 origin, Vector2 dir, float range, float arc, Team attacker)
    {
        var hits = new List<IHittable>();
        foreach (var target in _targets.ToArray())
        {
            if (target.Team == attacker || !target.CanBeHit || !Touches(target, origin, dir, range, arc))
            {
                continue;
            }

            hits.Add(target);
        }

        return hits;
    }

    public List<IHittable> InCircle(Vector2 center, float radius, Team attacker) => InSector(center, Vector2.Right, radius, 0f, attacker);

    /// <summary>O alvo vivo do outro time mais próximo de <paramref name="from"/> dentro de <paramref name="maxDistance"/>.</summary>
    public IHittable? Nearest(Vector2 from, Team attacker, float maxDistance)
    {
        IHittable? best = null;
        var bestDistance = maxDistance;
        foreach (var target in _targets)
        {
            if (target.Team == attacker || !target.CanBeHit)
            {
                continue;
            }

            var d = target.HitCenter.DistanceTo(from) - target.HitRadius;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = target;
            }
        }

        return best;
    }

    public static bool Touches(IHittable target, Vector2 origin, Vector2 dir, float range, float arc)
    {
        var v = target.HitCenter - origin;
        var d = v.Length();
        var r = target.HitRadius;
        if (d - r > range)
        {
            return false;
        }

        if (arc <= 0f || d <= r || dir == Vector2.Zero)
        {
            return true;
        }

        var allowed = (arc / 2f) + Mathf.Asin(Mathf.Min(1f, r / d));
        return Mathf.Abs(v.AngleTo(dir)) <= allowed;
    }
}
