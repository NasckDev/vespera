using System.Collections.Generic;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;

namespace Vespera.Game.Hunter;

/// <summary>Forja de armas (<c>Progression.Forge</c>): saber se dá para subir a arma e subir.</summary>
public sealed class ForgeApi
{
    internal ForgeApi()
    {
    }

    /// <summary>Atributos da arma da classe no nível atual.</summary>
    public WeaponStats Weapon => Progression.Engine.WeaponStats;

    /// <summary>Custo, atributos antes/depois e o que falta, para a tela mostrar. Não gasta nada.</summary>
    public ForgeCheck CanUpgrade() => Progression.Engine.CanUpgrade();

    /// <summary>Paga e sobe a arma um nível se possível (veja <see cref="ForgeCheck.CanUpgrade"/> no resultado). Salva.</summary>
    public ForgeResult Upgrade()
    {
        var result = Progression.Engine.Upgrade();
        if (result.Done)
        {
            Progression.Save();
            Progression.Notify();
        }

        return result;
    }
}

/// <summary>Equipamento e Ecos (<c>Progression.Equipment</c>).</summary>
public sealed class EquipmentApi
{
    internal EquipmentApi()
    {
    }

    public IReadOnlyList<ItemInstance> Items => Progression.Engine.Items;

    public ItemInstance? In(string slot) => Progression.Engine.EquippedIn(slot);

    /// <summary>Veste a peça no slot dela (troca a atual). Falha se o rank do caçador for menor que o da peça.</summary>
    public EquipResult Equip(int itemId) => Apply(Progression.Engine.Equip(itemId));

    public bool Unequip(string slot)
    {
        var done = Progression.Engine.Unequip(slot);
        if (done)
        {
            Apply(EquipResult.Success);
        }

        return done;
    }

    /// <summary>Põe um Eco absorvido num dos 2 slots (0 ou 1).</summary>
    public EquipResult EquipEcho(int slot, string echoId) => Apply(Progression.Engine.EquipEcho(slot, echoId));

    public bool UnequipEcho(int slot)
    {
        var done = Progression.Engine.UnequipEcho(slot);
        if (done)
        {
            Apply(EquipResult.Success);
        }

        return done;
    }

    /// <summary>Atributos somados do que está vestido, com os bônus de conjunto.</summary>
    public EquipmentSummary Summary() => Progression.Engine.Equipment();

    private static EquipResult Apply(EquipResult result)
    {
        if (result.Ok)
        {
            Progression.Save();
            Progression.Notify();
        }

        return result;
    }
}
