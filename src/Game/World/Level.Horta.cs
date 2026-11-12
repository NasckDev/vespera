using Vespera.Game.Components;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Core.Horta;

namespace Vespera.Game.World;

/// <summary>
/// Horta do Ato II no mundo: os 6 canteiros do Pomar Alto (zonas "canteiro") e a feira da Nilza na Praça. Canteiro vazio abre a
/// escolha da semente, em crescimento mostra quanto falta e pronto colhe na hora; a feira abre pela conversa com a Nilza.
/// </summary>
public partial class Level
{
    private void OpenCanteiro(Interactable plot)
    {
        switch (HortaService.StatusOf(plot.Index))
        {
            case PlotStatus.Ready:
                var (_, harvested) = HortaService.Harvest(plot.Index);
                WorldSfx.Play(World, WorldSound.Chime, plot.GlobalPosition);
                CombatHud.Toast(harvested);
                return;
            case PlotStatus.Growing:
                ShowNotice(HortaService.PlotPrompt(plot.Index));
                return;
            default:
                WorldSfx.Play(World, WorldSound.Paper, plot.GlobalPosition);
                SeedScreen.Show(plot.Index, CombatHud.Toast);
                return;
        }
    }

    /// <summary>A feira da manhã: a Nilza vende receitas e cozinha. Antes da Horta abrir (Ato II) ela só desconversa.</summary>
    private void OpenFeira()
    {
        if (!HortaService.Unlocked)
        {
            ShowNotice("A Nilza ainda não tem o que vender: a feira abre quando a Horta do Pomar Alto der colheita.");
            return;
        }

        FeiraScreen.Show();
    }
}
