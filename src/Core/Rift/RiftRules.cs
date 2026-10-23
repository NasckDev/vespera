using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.Save;

namespace Vespera.Core.Rift;

/// <summary>Monstro da Brecha já escalado para o rank (HP e dano).</summary>
public readonly record struct RiftMonsterStats(string Id, double Hp, double Damage, RiftMonsterDef Def);

/// <summary>
/// Regras de combate de uma Brecha (PLANO-V2 §6, GDD §4) sobre o Core existente: escala por rank (<c>rules.scaling</c>),
/// fases e divisões do Guardião, XP por morte e pausa no impacto. Fragmentos, saque e a Lanterna NÃO moram aqui: são da
/// progressão (<c>Vespera.Core.Advancement.ProgressionEngine</c>: GrantLoot, BeginRun/EndRun), a fonte única.
/// </summary>
public sealed class RiftRules(GameDatabase db, BrechaDef brecha)
{
    public BrechaDef Brecha => brecha;

    /// <summary>XP de uma morte do monstro (0 se não estiver na Brecha).</summary>
    public int Xp(string monsterId) => brecha.Monsters.TryGetValue(monsterId, out var def) ? Math.Max(0, def.Xp) : 0;

    /// <summary>XP de uma caçada completa: cada monstro das ondas (por sala) mais o Guardião.</summary>
    public int FullRunXp(IEnumerable<string> waveMonsters) =>
        waveMonsters.Sum(Xp) + brecha.Monsters.Where(m => m.Value.Boss).Sum(m => Math.Max(0, m.Value.Xp));

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

    /// <summary>Saiu vivo pela fenda depois do Guardião: a Brecha conta como fechada no save.</summary>
    public static void MarkClosed(SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        save.BrechasClosed++;
        save.FirstBrechaDone = true;
    }

    /// <summary>Pausa no impacto: a do golpe, maior se matou (peso do golpe final), entre 40 e 120 ms (PLANO-V2 §2).</summary>
    public static double HitStop(double moveHitStop, bool killed) => Math.Clamp(moveHitStop + (killed ? 0.03 : 0), 0.04, 0.12);
}
