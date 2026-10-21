using Vespera.Core.Common;
using Vespera.Core.Save;

namespace Vespera.Core.Combat;

/// <summary>Números dos Ecos (<c>useEco</c>, <c>combat.js:226-241</c>, <c>57-59</c>, <c>422</c>, <c>464-469</c>; spec 02 §3.7).</summary>
public static class EcoNumbers
{
    // Lanternas Votivas (arauto)
    public const double VotiveDuration = 7;
    public const double VotiveFirstShot = 0.2;
    public const double VotiveInterval = 0.55;
    public const double VotiveRange = 110;
    public const double VotiveBoltSpeed = 260;
    public const double VotiveBoltRadius = 4;
    public const double VotiveBoltLife = 0.8;
    public const double VotivePart = 0.8;

    // Isca Hipnótica (abissal)
    public const double LureThrow = 110;
    public const double LureCursorMax = 140;
    public const double LureFlightSpeed = 320;
    public const double LureDuration = 1.4;
    public const double LureAttractRadius = 95;
    public const double LureExplodeRadius = 50;
    public const double LurePart = 1.3;
    public const double LureKnockback = 60;
    public const double LureStun = 1.2;
    public const double LureStunBoss = 0.6;

    // Corrente Votiva (ceifador)
    public const double ChainSpeed = 420;
    public const double ChainRadius = 6;
    public const double ChainLife = 0.45;
    public const double ChainPullSeconds = 0.3;
    public const double ChainStun = 1.3;
    public const double ChainStunBoss = 0.5;

    // Investida Rubra (uivo)
    public const double ChargeSeconds = 0.32;
    public const double ChargeSpeed = 330;
    public const double ChargeHitRadius = 14;
    public const double ChargePart = 1.2;
    public const double ChargeKnockback = 140;
    public const double ChargeInvulnerability = 0.4;

    public static int VotiveDamage(int atk) => JsMath.Round((atk * 0.6) + 4);

    public static double LureDamage(int atk) => (atk * 2.2) + 12;

    public static double ChainDamage(int atk) => (atk * 1.5) + 8;

    public static double ChargeDamage(int atk) => (atk * 1.6) + 14;
}

/// <summary>Números dos Golpes Supremos (<c>tryUlt</c>/<c>updUlt</c>, <c>extra.js:14-65</c>; spec 02 §3.8).</summary>
public static class UltimateNumbers
{
    public const double CursorRecentSeconds = 2.5;
    public const double CursorMax = 150;
    public const double AimDistanceX = 90;
    public const double AimDistanceY = 70;
    public const double Hitstop = 0.12;
    public const double Shake = 6;

    // Lâmina — Fenda Rubra
    public const double SwordSegment = 0.16;
    public const int SwordDashes = 3;
    public const double SwordSpeed = 420;
    public const double SwordSwing = 0.35;
    public const double SwordHitRadius = 30;
    public const double SwordEnd = 0.5;
    public const double SwordInvulnerability = 0.2;

    // Arco — Chuva de Vésperas
    public const double BowRainSeconds = 2.5;
    public const double BowInterval = 0.07;
    public const double BowSpreadX = 52;
    public const double BowSpreadY = 38;
    public const double BowImpact = 0.18;
    public const double BowHitRadius = 12;
    public const double BowEnd = 2.8;

    // Cajado — Lanterna Estelar
    public const double StaffPullSeconds = 1.6;
    public const double StaffPullRadius = 140;
    public const double StaffPullSpeed = 110;
    public const double StaffPullSpeedBoss = 20;
    public const double StaffBlastRadius = 80;
    public const double StaffStun = 1.6;
    public const double StaffStunBoss = 0.8;
    public const double StaffEnd = 2;

    // Martelo — Terremoto da Forja
    public const int HammerWaves = 3;
    public const double HammerWaveEvery = 0.35;
    public const double HammerWaveGrow = 0.45;
    public const double HammerWaveRadius = 90;
    public const double HammerWaveMax = 95;
    public const double HammerBand = 16;
    public const double HammerStun = 1;
    public const double HammerEnd = 1.3;

    // Adagas — Dança das Sombras
    public const int DagTargets = 6;
    public const double DagFirst = 0.15;
    public const double DagEvery = 0.2;
    public const double DagBehind = 16;
    public const double DagBackstab = 1.5;
    public const double DagInvulnerability = 2.4;
    public const double DagEnd = 2.4;

    public static double SwordDamage(int atk) => (atk * 2.4) + 10;

    public static double BowDamage(int atk) => (atk * 0.75) + 3;

    public static double StaffDamage(int atk) => (atk * 4) + 20;

    public static double HammerDamage(int atk) => (atk * 1.6) + 8;

    public static double DagDamage(int atk) => (atk * 2.8) + 6;

    /// <summary>Raio da onda <paramref name="wave"/> do Terremoto no tempo <paramref name="t"/>; null fora de 0–95 u.</summary>
    public static double? HammerWaveRadiusAt(int wave, double t)
    {
        var r = (t - (wave * HammerWaveEvery)) / HammerWaveGrow * HammerWaveRadius;
        return r < 0 || r > HammerWaveMax ? null : r;
    }
}

/// <summary>
/// Dois slots de Eco no combate (DECISIONS B1-D41): o 1º é o Eco equipado (<c>s.ecoEq</c>, tecla Q da v2); o 2º é o
/// primeiro outro Eco absorvido, na ordem em que foi obtido. Sem campo novo no save.
/// </summary>
public static class EcoSlots
{
    public const int Count = 2;

    public static string? Slot(SaveData save, int index)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (index == 0)
        {
            return save.EquippedEco;
        }

        if (index != 1)
        {
            return null;
        }

        foreach (var id in save.Ecos)
        {
            if (id != save.EquippedEco)
            {
                return id;
            }
        }

        return null;
    }
}

/// <summary>Onde a Lanterna perdida aparece (spec 02 §3.13).</summary>
public static class LanternPlacement
{
    /// <summary>
    /// A Lanterna está nesta sala: mesma Brecha, ou (DECISIONS B1-D40) a próxima Brecha do mesmo tema, já que as Brechas
    /// não são salvas e o id muda entre sessões.
    /// </summary>
    public static bool IsHere(SaveData save, int brechaId, string themeId, string room)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save.LostLantern is { } lost && lost.Room == room && (lost.BrechaId == brechaId || lost.ThemeId == themeId);
    }
}
