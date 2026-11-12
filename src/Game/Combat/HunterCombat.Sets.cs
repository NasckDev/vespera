using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>Efeitos dos conjuntos de Guardião no combate do caçador (regra em <see cref="SetEffects"/>).</summary>
public sealed partial class HunterCombat
{
    private ActiveSetEffects _setFx = ActiveSetEffects.None;
    private double _burstCooldown;
    private bool _lastStandUsed;
    private int _hitCount;
    private bool _gelDash;

    /// <summary>Efeitos de conjunto ativos para este jogador (lidos da progressão dele em <see cref="RefreshStats"/>).</summary>
    public ActiveSetEffects SetFx => _setFx;

    /// <summary>Conjunto da Lodo-Mãe (2 peças): lentidão não pega.</summary>
    public bool SlowImmune => _setFx.Has(SetEffects.SlowImmune);

    private void RefreshSetEffects()
    {
        var engine = Hunter.Progression.IsActive ? Coop.PartyProgression.EngineFor(_player.Slot) ?? Hunter.Progression.Engine : null;
        _setFx = engine?.Equipment().Effects ?? ActiveSetEffects.None;
    }

    private void TickSetEffects(float dt) => _burstCooldown = System.Math.Max(0, _burstCooldown - dt);

    /// <summary>Dano antes da defesa: o Teto Firme (Inquilino) tira 20%.</summary>
    private double IncomingDamage(double damage) => SetEffects.ReduceIncoming(damage, _setFx.Has(SetEffects.DamageReduction));

    /// <summary>Velória (4): uma vez por sala, o golpe fatal deixa 1 de vida e invencível por 2 s.</summary>
    private bool TryLastStand()
    {
        if (_lastStandUsed || !_setFx.Has(SetEffects.LastStand))
        {
            return false;
        }

        _lastStandUsed = true;
        Hp = 1;
        _invulnerable = Mathf.Max(_invulnerable, (float)SetEffects.LastStandInvulnerableSeconds);
        _player.Feel?.Toast("Última Chama: você resiste de pé.");
        _player.Flash.Pulse(new Color(1f, 0.8f, 0.4f), 0.1f, 0.5f, 0.8f);
        return true;
    }

    /// <summary>Macieira (espinhos), Farol (clarão) e Reflexo (dano devolvido): reação a ferimento, com recarga.</summary>
    private void HurtBurst(int damageTaken)
    {
        var thorns = _setFx.Has(SetEffects.HurtThorns);
        var flash = _setFx.Has(SetEffects.HurtFlash);
        var reflect = _setFx.Has(SetEffects.HurtReflect);
        if (!(thorns || flash || reflect) || _burstCooldown > 0 || _player.World is not { } world)
        {
            return;
        }

        _burstCooldown = SetEffects.BurstCooldownSeconds;
        var radius = (thorns ? SetEffects.ThornsRadius : flash ? SetEffects.FlashRadius : SetEffects.ReflectRadius) * Scale;
        var damage = thorns ? SetEffects.ThornsDamage(Stats.Atk) : reflect ? SetEffects.ReflectDamage(damageTaken) : 0;
        var color = thorns ? new Color("8bd46e") : flash ? new Color("ffe9a8") : new Color("b9d4ff");
        world.Feel.Spark(HitCenter, color, 18, 340f);
        foreach (var target in world.Combat.InCircle(HitCenter, radius, Team.Hunters))
        {
            if (damage > 0)
            {
                target.ReceiveHit(new HitInfo(damage, HitCenter, Vector2.Zero, 0, 1, false, 0));
            }

            if (flash && target is Enemy enemy)
            {
                enemy.Root(SetEffects.FlashRootSeconds);
            }
        }
    }

    /// <summary>Chamado por golpes (corpo a corpo e flechas) que acertaram: cura por abate, eco do golpe e badalada.</summary>
    public void OnHitLanded(IHittable target, in HitOutcome outcome, in HitInfo hit)
    {
        if (!outcome.Landed || target is not Enemy enemy)
        {
            return;
        }

        if (outcome.Killed && _setFx.Has(SetEffects.KillHeal))
        {
            Heal(SetEffects.KillHealAmount(MaxHp));
        }

        if (!outcome.Killed && _setFx.Has(SetEffects.HitEcho) && SetEffects.EchoProcs(RiftSession.Random.NextDouble()))
        {
            var echo = enemy.ReceiveHit(hit with { Damage = hit.Damage * SetEffects.EchoDamageFactor, Push = Vector2.Zero, HitStop = 0 });
            if (echo.Killed && _setFx.Has(SetEffects.KillHeal))
            {
                Heal(SetEffects.KillHealAmount(MaxHp));
            }
        }

        if (_setFx.Has(SetEffects.HitBell) && SetEffects.BellRings(++_hitCount) && _player.World is { } world)
        {
            world.Feel.Spark(HitCenter, new Color("d9c7a0"), 22, 380f);
            world.Feel.Shake(0.25f);
            foreach (var other in world.Enemies)
            {
                if (IsInstanceValid(other) && !other.Dead && other.GlobalPosition.DistanceTo(_player.GlobalPosition) < SetEffects.BellRadius * Scale)
                {
                    other.Root(SetEffects.BellRootSeconds);
                }
            }
        }
    }
}
