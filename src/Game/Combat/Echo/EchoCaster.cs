using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>
/// Dispara o efeito de combate de cada Eco de Guardião (menos o Viscoso, que é o avanço do próprio caçador e fica em
/// <see cref="HunterCombat"/>). Números (recarga, duração, força, alcance) vêm de <c>echoes.json</c> via <see cref="EchoDef"/>.
/// </summary>
public static class EchoCaster
{
    public const string Viscoso = "eco_viscoso";

    /// <summary>Solta o Eco <paramref name="def"/>; false se não deu (sem mundo ou Eco sem efeito próprio).</summary>
    public static bool Cast(HunterCombat caster, EchoDef def, Vector2 aim)
    {
        if (caster.Player.World is not { } world)
        {
            return false;
        }

        var feet = caster.Player.GlobalPosition;
        var scale = caster.Scale;
        switch (def.Id)
        {
            case "eco_semente":
                world.AddGroundFx(new EchoSeed(world, feet + (aim * 70f * scale), def, caster.Stats.Atk, scale));
                break;
            case "eco_farol":
                world.AddFx(new EchoBeam(world, caster.HitCenter, aim, def, scale));
                break;
            case "eco_vela":
                caster.StartVela(def);
                break;
            case "eco_sino":
                Bell(world, caster, def);
                break;
            case "eco_lar":
                world.AddGroundFx(new EchoShelter(world, feet, def, scale));
                break;
            case "eco_avesso":
                world.AddFx(new EchoClone(world, caster.Player, def, caster.Stats.Atk));
                break;
            default:
                return false;
        }

        CombatSfx.Play(world, CombatSound.Slam, feet, -4f);
        return true;
    }

    private static void Bell(LevelWorld world, HunterCombat caster, EchoDef def)
    {
        var radius = (float)def.Radius * caster.Scale;
        var origin = caster.HitCenter;
        foreach (var enemy in world.Enemies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Dead || enemy.HitCenter.DistanceTo(origin) > radius + enemy.HitRadius)
            {
                continue;
            }

            enemy.Stun((float)EchoRules.StunSeconds(def, enemy.Def.Boss));
        }

        world.AddFx(new EchoRing(origin, radius, new Color(1f, 0.85f, 0.45f)));
        world.Feel.Shake(0.6f);
    }
}
