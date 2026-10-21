using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.Combat;

/// <summary>Golpe corpo a corpo pronto para resolver (setor, dano e empurrão), já com o combo da Lâmina aplicado.</summary>
/// <param name="Arc">Abertura do setor em rad.</param>
/// <param name="Range">Alcance em u.</param>
/// <param name="DamageMult">Multiplicador sobre o ataque (golpe 3 da Lâmina: 1.6).</param>
/// <param name="Knockback">Empurrão (90; 160 no golpe 3; 220 no martelo).</param>
/// <param name="Big">Golpe forte (3º da Lâmina): arco visual mais grosso.</param>
/// <param name="ComboStep">0, 1 ou 2 (só a Lâmina muda).</param>
public readonly record struct MeleeSwing(double Arc, double Range, double DamageMult, double Knockback, bool Big, int ComboStep);

/// <summary>Flecha disparada (<c>fireArrow</c>, <c>combat.js:138-143</c>).</summary>
/// <param name="Charge">Carga de 0 a 0.9.</param>
public readonly record struct ArrowShot(double Charge, double Speed, int Damage, bool Pierce, double Knockback)
{
    public const double Radius = 3;
    public const double Life = 0.9;

    /// <summary>Velocidade <c>280 + 240c</c>, dano <c>round(atk × (0.8 + 1.6c))</c>, cheia (<c>c ≥ 0.85</c>) perfura e empurra 120 (senão 60).</summary>
    public static ArrowShot From(double charge, int attack)
    {
        var full = charge >= PlayerCombatState.FullChargeAt;
        return new ArrowShot(charge, 280 + (240 * charge), JsMath.Round(attack * (0.8 + (1.6 * charge))), full, full ? 120 : 60);
    }
}

/// <summary>
/// Estado de combate do jogador da v2 (<c>makePlayer</c>/<c>updPlayer</c>, <c>combat.js:2-110</c>; spec 02 §3.1-3.3, §3.6):
/// energia e esquiva, i-frames, recargas, combo da Lâmina, carga do Arco, windup do Martelo, Essência, recarga de Eco e
/// do Golpe Supremo. Não move nada: quem chama aplica deslocamentos e acertos. Ordem de uso por frame:
/// <see cref="TickTimers"/> → <see cref="TryStartDash"/>/<see cref="AdvanceDash"/> → <see cref="TickAttackTimers"/> → ataques.
/// </summary>
public sealed class PlayerCombatState
{
    public const double EnergyMax = 100;
    public const double EnergyRegen = 30;
    public const double DashCost = 22;
    public const double DashSeconds = 0.17;
    public const double DashSpeed = 270;
    public const double DashCooldown = 0.32;
    public const double HurtInvulnerability = 0.75;
    public const double HurtFlashSeconds = 0.25;
    public const double HurtKnockback = 170;
    public const double ComboWindow = 0.6;
    public const double MeleeAttackSeconds = 0.2;
    public const double DaggerAttackSeconds = 0.14;
    public const double SmashWindup = 0.24;
    public const double SmashAttackSeconds = 0.28;
    public const double SmashKnockback = 220;
    public const double OrbAttackSeconds = 0.26;
    public const double BowAttackSeconds = 0.18;
    public const double MaxCharge = 0.9;
    public const double FullChargeAt = 0.85;
    public const double EcoCooldown = 0.8;
    public const double RegenDelay = 4;
    public const double ChillSpeedMult = 0.65;

    public PlayerCombatState(double initialUltimateCooldown) => UltCooldown = initialUltimateCooldown;

    public double Energy { get; private set; } = EnergyMax;

    public double Essence { get; private set; }

    /// <summary>Tempo restante da esquiva (invulnerável enquanto &gt; 0).</summary>
    public double DashTime { get; private set; }

    public double DashCd { get; private set; }

    /// <summary>i-frames restantes (ferido, respawn, Ecos, Supremos).</summary>
    public double Invulnerable { get; private set; }

    public double HurtTime { get; private set; }

    /// <summary>Recarga do ataque.</summary>
    public double Cooldown { get; private set; }

    /// <summary>Animação de ataque (pose <c>c_*_atk</c>, velocidade ×0.55).</summary>
    public double AttackTime { get; private set; }

    public int Combo { get; private set; }

    public double ComboTime { get; private set; }

    /// <summary>Carga do Arco; −1 = não carregando.</summary>
    public double Charge { get; private set; } = -1;

    /// <summary>Windup do Martelo.</summary>
    public double WindTime { get; private set; }

    public double EcoCd { get; private set; }

    public double UltCooldown { get; private set; }

    /// <summary>Inverno: lento por 1.6 s depois de ferido (só em Brecha).</summary>
    public double Chill { get; private set; }

    /// <summary>Segundos desde o último dano (regeneração só depois de 4 s).</summary>
    public double SinceHurt { get; private set; } = 99;

    public bool IsDashing => DashTime > 0;

    public bool IsCharging => Charge >= 0;

    public bool IsWindingUp => WindTime > 0;

    /// <summary>Não leva dano: i-frames ou esquiva (<c>hurtPlayer</c>, <c>combat.js:15</c>).</summary>
    public bool IsInvulnerable => Invulnerable > 0 || DashTime > 0;

    public bool CanAttack => Cooldown <= 0 && DashTime <= 0;

    public bool CanRegenerate => SinceHurt > RegenDelay;

    public bool EcoReady => EcoCd <= 0;

    public bool UltimateReady => UltCooldown <= 0;

    /// <summary>Recargas de início de frame (<c>combat.js:53</c>) e a do Supremo.</summary>
    public void TickTimers(double dt)
    {
        Invulnerable = Math.Max(0, Invulnerable - dt);
        HurtTime = Math.Max(0, HurtTime - dt);
        Chill = Math.Max(0, Chill - dt);
        EcoCd = Math.Max(0, EcoCd - dt);
        UltCooldown = Math.Max(0, UltCooldown - dt);
        DashCd = Math.Max(0, DashCd - dt);
        SinceHurt += dt;
    }

    /// <summary>Esquiva: precisa de <c>dashT ≤ 0</c>, <c>dashCd ≤ 0</c> e 22 de energia. Cancela carga e windup.</summary>
    public bool TryStartDash()
    {
        if (DashTime > 0 || DashCd > 0 || Energy < DashCost)
        {
            return false;
        }

        Energy -= DashCost;
        DashTime = DashSeconds;
        DashCd = DashCooldown;
        Charge = -1;
        WindTime = 0;
        return true;
    }

    /// <summary>
    /// Avança a esquiva ou regenera energia. Devolve a distância a andar neste frame na direção da esquiva
    /// (0 quando não está esquivando).
    /// </summary>
    public double AdvanceDash(double dt)
    {
        if (DashTime > 0)
        {
            DashTime -= dt;
            return DashSpeed * dt;
        }

        Energy = Math.Min(EnergyMax, Energy + (EnergyRegen * dt));
        return 0;
    }

    /// <summary>Recargas de ataque (<c>combat.js:94</c>).</summary>
    public void TickAttackTimers(double dt)
    {
        Cooldown = Math.Max(0, Cooldown - dt);
        AttackTime = Math.Max(0, AttackTime - dt);
        ComboTime = Math.Max(0, ComboTime - dt);
    }

    /// <summary>
    /// Golpe corpo a corpo (<c>playerAttack</c>): Lâmina faz combo de 3 dentro de 0.6 s; o 3º golpe tem dano ×1.6, arco
    /// ×1.15, alcance ×1.2, empurrão 160 e recarga ×1.5. Adagas têm animação de 0.14 s.
    /// </summary>
    public MeleeSwing StartMelee(WeaponDef weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var arc = weapon.Arc ?? 0;
        var range = weapon.Range ?? 0;
        var mult = 1.0;
        var big = false;
        var step = 0;
        if (weapon.Id == "sword")
        {
            Combo = ComboTime > 0 ? (Combo + 1) % 3 : 0;
            ComboTime = ComboWindow;
            step = Combo;
            if (Combo == 2)
            {
                mult = 1.6;
                arc *= 1.15;
                range *= 1.2;
                big = true;
            }
        }

        Cooldown = weapon.Cooldown * (big ? 1.5 : 1);
        AttackTime = weapon.Id == "dag" ? DaggerAttackSeconds : MeleeAttackSeconds;
        return new MeleeSwing(arc, range, mult, big ? 160 : 90, big, step);
    }

    /// <summary>Martelo: inicia o windup de 0.24 s e a recarga já (<c>combat.js:132</c>).</summary>
    public void StartSmash(WeaponDef weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        WindTime = SmashWindup;
        Cooldown = weapon.Cooldown;
    }

    /// <summary>Avança o windup; devolve true no frame em que o golpe sai (<c>smash()</c>, animação 0.28 s).</summary>
    public bool TickWindup(double dt)
    {
        if (WindTime <= 0)
        {
            return false;
        }

        WindTime -= dt;
        if (WindTime > 0)
        {
            return false;
        }

        WindTime = 0;
        AttackTime = SmashAttackSeconds;
        return true;
    }

    /// <summary>Golpe do Martelo como setor: arco 2.9 rad, alcance 36, empurrão 220.</summary>
    public static MeleeSwing SmashSwing(WeaponDef weapon) => new(weapon.Arc ?? 0, weapon.Range ?? 0, 1, SmashKnockback, true, 0);

    /// <summary>Cajado: orbe sai agora (<c>combat.js:133-135</c>).</summary>
    public void StartOrb(WeaponDef weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        Cooldown = weapon.Cooldown;
        AttackTime = OrbAttackSeconds;
    }

    /// <summary>
    /// Arco (<c>combat.js:97-99</c>): segurar com recarga pronta começa a carga (0 → 0.9 a 1/s); soltar dispara.
    /// Devolve a flecha no frame em que soltou, senão null.
    /// </summary>
    public ArrowShot? UpdateBow(WeaponDef weapon, double dt, bool held, int attack)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (held && Cooldown <= 0 && Charge < 0 && DashTime <= 0)
        {
            Charge = 0;
        }

        if (Charge < 0)
        {
            return null;
        }

        if (held)
        {
            Charge = Math.Min(MaxCharge, Charge + dt);
            return null;
        }

        var shot = ArrowShot.From(Charge, attack);
        Charge = -1;
        Cooldown = weapon.Cooldown;
        AttackTime = BowAttackSeconds;
        return shot;
    }

    /// <summary>Cancela carga e windup (troca de mapa, morte).</summary>
    public void CancelActions()
    {
        Charge = -1;
        WindTime = 0;
    }

    /// <summary>
    /// Leva um golpe: falso se invulnerável. Senão 0.75 s de i-frames e 0.25 s de flash (<c>hurtPlayer</c>).
    /// <paramref name="chillSeconds"/> &gt; 0 no Inverno dentro de Brecha.
    /// </summary>
    public bool TryHurt(double chillSeconds = 0)
    {
        if (IsInvulnerable)
        {
            return false;
        }

        Invulnerable = HurtInvulnerability;
        HurtTime = HurtFlashSeconds;
        SinceHurt = 0;
        if (chillSeconds > 0)
        {
            Chill = chillSeconds;
        }

        return true;
    }

    /// <summary>Garante i-frames por pelo menos <paramref name="seconds"/> (respawn 1.5, Investida 0.4, Supremos).</summary>
    public void GrantInvulnerability(double seconds) => Invulnerable = Math.Max(Invulnerable, seconds);

    /// <summary>Multiplicador de velocidade de andar: atributo × frio 0.65 × ataque 0.55 × carga 0.45 × windup 0.2.</summary>
    public double SpeedMultiplier(double statSpeed)
    {
        var m = statSpeed;
        if (Chill > 0)
        {
            m *= ChillSpeedMult;
        }

        if (AttackTime > 0)
        {
            m *= 0.55;
        }

        if (Charge >= 0)
        {
            m *= 0.45;
        }

        if (WindTime > 0)
        {
            m *= 0.2;
        }

        return m;
    }

    /// <summary>Essência ganha ao acertar um monstro (<see cref="DamageCalculator.EssenceAfterHit"/>).</summary>
    public void SetEssence(double value) => Essence = Math.Clamp(value, 0, 100);

    /// <summary>Gasta Essência se houver. False e nada gasto se faltar.</summary>
    public bool TrySpendEssence(double cost)
    {
        if (Essence < cost)
        {
            return false;
        }

        Essence -= cost;
        return true;
    }

    public void StartEcoCooldown() => EcoCd = EcoCooldown;

    public void StartUltimateCooldown(double seconds) => UltCooldown = seconds;

    /// <summary>Volta da morte: i-frames, Essência 0, sem esquiva/carga em curso (<c>respawnAfterDeath</c>).</summary>
    public void Respawn(double invulnerability)
    {
        Invulnerable = invulnerability;
        Essence = 0;
        DashTime = 0;
        Charge = -1;
        WindTime = 0;
        AttackTime = 0;
        HurtTime = 0;
        Chill = 0;
    }
}
