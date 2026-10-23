using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Hunter;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>Aparência do golpe num nível de arma: cor e largura do rastro, faíscas do impacto e números de dano.</summary>
public sealed record WeaponTier(int Level, string Name, Color Trail, Color Core, Color Spark, Color Number, float TrailScale, int SparkBonus, float NumberScale);

/// <summary>
/// A arma forjada se vê no golpe (playtest 23/10: "quero evoluir"): nível 1 aço frio, nível 2 âmbar, nível 3+ âmbar vivo
/// com brilho. O rastro do corte cresce, o impacto solta mais faíscas e os números de dano ficam maiores a cada nível.
/// </summary>
public static class WeaponLook
{
    private static readonly IReadOnlyList<WeaponTier> Tiers =
    [
        new(1, "aço", new Color("c9d4e3"), new Color("f4f8ff"), new Color("dfe8f5"), new Color("f4e7c6"), 1.00f, 0, 1.00f),
        new(2, "âmbar", new Color("f2a541"), new Color("ffe2a8"), new Color("ffc46b"), new Color("ffd58a"), 1.18f, 5, 1.18f),
        new(3, "âmbar vivo", new Color("ffcf4a"), new Color("fff6c8"), new Color("ffe27a"), new Color("ffe9a0"), 1.36f, 10, 1.34f),
    ];

    /// <summary>Nível forjado da arma do caçador (1 sem sessão).</summary>
    public static int Level
    {
        get
        {
            if (!Progression.IsActive)
            {
                return 1;
            }

            var weapon = HunterSprites.WeaponId(RiftSession.Hunter);
            return Progression.State.WeaponLevels.TryGetValue(weapon, out var level) ? level : 1;
        }
    }

    public static WeaponTier Current => For(Level);

    public static float NumberScale => Current.NumberScale;

    public static WeaponTier For(int level) => Tiers[Mathf.Clamp(level, 1, Tiers.Count) - 1];
}
