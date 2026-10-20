using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Core.Rules;

/// <summary>
/// Mecânica da Lanterna (spec 02 §3.13; <c>combat.js:29-41</c>, <c>world.js:66-72,207-210</c>): ao morrer dentro de
/// uma Brecha a bolsa fica na Lanterna, o HP máximo cai para 60% e a luz para 45% até ela voltar.
/// </summary>
public sealed class LanternRules(GameDatabase db)
{
    private readonly LanternRulesDef _rules = db.Rules.Lantern;

    /// <summary>HP máximo sem Lanterna: <c>round(hp * 0.6)</c>.</summary>
    public int PenalizedHp(int maxHp) => JsMath.Round(maxHp * _rules.HpMult);

    public double PenalizedLight(double light) => light * _rules.LightMult;

    /// <summary>Um caçador traz a Lanterna em <c>R(55, 85)</c> s.</summary>
    public double RollRescueDelay(IRandom random) => random.Range(_rules.RescueDelay);

    /// <summary>Resgate pago com a Odete chega em 12 s.</summary>
    public double OdeteRescueDelay => _rules.OdeteRescueDelay;

    /// <summary>Ao carregar um save sem Lanterna, o resgate chega em 20 s.</summary>
    public double RescueOnLoadDelay => _rules.RescueOnLoadDelay;

    /// <summary>Entregar lanternas de outros caçadores à Odete: +35 Lumes e +20 XP cada.</summary>
    public (int Lumes, int Xp) DeliveryReward(int lanterns) => (_rules.DeliveryLumes * lanterns, _rules.DeliveryXp * lanterns);

    /// <summary>45% das Brechas guardam a Lanterna de outro caçador.</summary>
    public bool RollFoundLantern(IRandom random) => random.Chance(_rules.FoundChance);

    /// <summary>HP ao acordar na estalagem depois de cair na cidade: <c>round(max * 0.5)</c>.</summary>
    public int CityRespawnHp(int maxHp) => JsMath.Round(maxHp * db.Rules.Death.CityHpFraction);

    /// <summary>Lumes depois de cair na cidade: −10, nunca negativo.</summary>
    public int LumesAfterCityDeath(int lumes) => Math.Max(0, lumes - db.Rules.Death.CityLumesLoss);

    /// <summary>A Lanterna perdida está nesta Brecha e nesta sala (aparece o pick "Recuperar sua Lanterna").</summary>
    public static bool IsLanternHere(SaveData save, int brechaId, string room) =>
        save.LostLantern is { } lost && lost.BrechaId == brechaId && lost.Room == room;

    /// <summary><c>mergeBag</c>: tudo da bolsa entra no save (Lumes, materiais, equipamentos e lanternas achadas).</summary>
    public static void MergeBag(SaveData save, RunBag bag)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(bag);
        save.Lumes += bag.Lumes;
        foreach (var (id, amount) in bag.Materials)
        {
            if (amount > 0)
            {
                save.Materials[id] = save.MaterialCount(id) + amount;
            }
        }

        save.Backpack.AddRange(bag.Gear);
        save.CarriedLanterns.AddRange(bag.Lanterns);
    }
}
