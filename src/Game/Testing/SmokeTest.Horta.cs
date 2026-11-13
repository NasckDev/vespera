using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Horta;
using Vespera.Game.Boot;
using Vespera.Game.Components;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro da Horta (<c>--horta</c>): sem a Celeste não há canteiros; depois dela os 6 aparecem no Pomar Alto; planta as três
/// sementes pelo canteiro (tela de escolha), avança duas voltas da Brecha, colhe, na Praça a Nilza abre a feira (comprar a
/// receita, cozinhar, um prato por vez) e o prato é comido ao entrar na Brecha, com o bônus do dia.
/// </summary>
public partial class SmokeTest
{
    public const string HortaFlag = "--horta";

    private const int HortaLumes = 100;

    private async Task RunHorta(Level start)
    {
        var engine = StoryService.Engine;
        foreach (var flag in DoneBeforeAto4.Where(f => f != HortaRules.UnlockFlag))
        {
            engine.SetFlag(flag);
        }

        engine.Dawn();
        Progression.AddLumes(HortaLumes);
        var level = start;

        // Antes da Celeste: nenhum canteiro.
        await level.GoToRoom("pomar_alto", withDirector: false);
        await PhysicsFrames(3);
        Check(level.World.Zones.All(z => z.Kind != CanteiroMark.ZoneKind), "antes de conhecer a Celeste o Pomar Alto não deveria ter canteiros");

        engine.SetFlag(HortaRules.UnlockFlag);
        await level.GoToRoom("pomar_alto", withDirector: false);
        await PhysicsFrames(3);
        var plots = level.World.Zones.Where(z => z.Kind == CanteiroMark.ZoneKind).OrderBy(z => z.Index).ToList();
        Check(plots.Count == 6, $"o Pomar Alto deveria ter 6 canteiros, tem {plots.Count}");
        if (plots.Count != 6)
        {
            return;
        }

        await StandAt(level, plots[0].GlobalPosition + new Vector2(0, -20));
        await Shot("horta_01_canteiros", 0.5);

        // Plantar as três sementes (cada uma num canteiro, pela tela de escolha).
        await PlantAt(level, plots, 0, "abobora_roxa");
        await PlantAt(level, plots, 1, "raiz_doce");
        await PlantAt(level, plots, 2, "erva_de_lanterna");
        Check(HuntService.HortaState.Plots.Count(p => p.Seed is not null) == 3, "três canteiros deveriam estar plantados");
        await StandAt(level, level.World.ClampToFloor(plots[0].GlobalPosition));
        Check(level.Player.Focus?.Prompt.Contains("falta 1 volta", System.StringComparison.Ordinal) == true,
            $"canteiro plantado deveria avisar o que falta, avisa '{level.Player.Focus?.Prompt}'");
        await Shot("horta_02_plantado", 0.5);

        // Volta da Brecha 1: abóbora e erva ficam prontas, a raiz-doce (2 voltas) ainda não.
        await HortaRun();
        Check(HortaService.StatusOf(0) == PlotStatus.Ready && HortaService.StatusOf(2) == PlotStatus.Ready, "abóbora e erva deveriam estar prontas na 1ª volta");
        Check(HortaService.StatusOf(1) == PlotStatus.Growing, "a raiz-doce deveria ainda estar crescendo na 1ª volta");
        await Frames(3);
        await Shot("horta_03_pronto", 0.5);
        await HarvestAt(level, plots, 0);
        await HarvestAt(level, plots, 2);
        Check(HuntService.HortaState.Count("abobora_roxa") == 2 && HuntService.HortaState.Count("erva_de_lanterna") == 2, "a colheita deveria render 2 abóboras e 2 ervas");

        await HortaRun();
        await HarvestAt(level, plots, 1);
        Check(HuntService.HortaState.Count("raiz_doce") == 3, "a raiz-doce deveria render 3 na 2ª volta");

        await RunFeira(level);
        await RunEatDish();
        Log.Info("horta OK: 6 canteiros, plantar, crescer por volta da Brecha, colher, feira da Nilza (comprar e cozinhar) e prato comido ao entrar");
    }

    private async Task PlantAt(Level level, List<Interactable> plots, int index, string seedId)
    {
        await StandAt(level, level.World.ClampToFloor(plots[index].GlobalPosition));
        Check(level.Player.Focus?.Index == index, $"perto do canteiro {index + 1} o foco deveria ser ele, foi {level.Player.Focus?.Index}");
        await PressInWorld(Key.E);
        await WaitFor(() => SeedScreen.Active is not null, "escolha da semente");
        if (index == 0)
        {
            await Shot("horta_01b_sementes", 0.3);
        }

        SeedScreen.Active?.Pick(seedId);
        await Frames(4);
        Check(HuntService.HortaState.Plots[index].Seed == seedId, $"o canteiro {index + 1} deveria ter '{seedId}', tem '{HuntService.HortaState.Plots[index].Seed}'");
        Check(!GetTree().Paused, "depois de plantar o jogo deveria voltar a correr");
    }

    private async Task HarvestAt(Level level, List<Interactable> plots, int index)
    {
        await StandAt(level, level.World.ClampToFloor(plots[index].GlobalPosition));
        await PressInWorld(Key.E);
        Check(HortaService.StatusOf(index) == PlotStatus.Empty, $"E no canteiro {index + 1} pronto deveria colher e esvaziar");
    }

    /// <summary>Uma caçada inteira (entra e fecha viva): o prato é comido na entrada e cada plantação ganha um dia na volta.</summary>
    private async Task HortaRun()
    {
        RiftSession.BeginRun();
        await Frames(2);
        RiftSession.EndRunWithSummary(survived: true, completed: true);
        await Frames(2);
    }

    private async Task RunFeira(Level start)
    {
        await start.GoToRoom(Level.HubLevelId);
        await PhysicsFrames(4);
        var level = (Level)_router.Current!;
        var lumes = Progression.State.Lumes;
        await OpenTalk(level, "nilza");
        await Choose(level, "Ver a feira");
        await SkipDialogue(level);
        await WaitFor(() => FeiraScreen.Active is not null, "feira da Nilza");
        var feira = FeiraScreen.Active!;
        await Shot("horta_04_feira", 0.4);

        feira.Press("sopa_de_abobora");
        Check(HuntService.HortaState.Recipes.Contains("sopa_de_abobora"), "comprar a receita da sopa deveria aprendê-la");
        Check(Progression.State.Lumes == lumes - 20, $"a receita deveria custar 20 Lumes, sobraram {Progression.State.Lumes} de {lumes}");
        Check(feira.ButtonTextOf("sopa_de_abobora").Contains("Cozinhar", System.StringComparison.Ordinal), "depois de comprada o botão deveria virar Cozinhar");
        feira.Press("sopa_de_abobora");
        Check(HuntService.HortaState.Meal == "sopa_de_abobora", $"cozinhar deveria deixar a sopa pronta, o prato é '{HuntService.HortaState.Meal}'");
        Check(HuntService.HortaState.Count("abobora_roxa") == 0, "a sopa deveria gastar as 2 abóboras");
        feira.Press("cha_de_lanterna");
        feira.Press("cha_de_lanterna");
        Check(HuntService.HortaState.Meal == "sopa_de_abobora", "com um prato pronto a Nilza não deveria cozinhar outro");
        await Shot("horta_05_feira_pronto", 0.4);
        feira.Close();
        await Frames(4);
        Check(!GetTree().Paused, "fechar a feira deveria devolver o jogo");
    }

    private async Task RunEatDish()
    {
        var damageBefore = HuntService.Bless.DamagePct;
        RiftSession.BeginRun();
        await Frames(2);
        Check(HuntService.Dish?.Id == "sopa_de_abobora", $"ao entrar na Brecha o prato deveria ser comido, o prato é '{HuntService.Dish?.Id}'");
        Check(HuntService.HortaState.Meal is null, "o prato comido deveria sair da mesa");
        Check(HuntService.Bless.DamagePct >= damageBefore + 8, $"a sopa deveria dar +8% de dano, o dano é {HuntService.Bless.DamagePct}%");
        RiftSession.EndRunWithSummary(survived: true, completed: true);
        await Frames(2);
        Check(HuntService.Dish is null, "fora da Brecha nenhum prato deveria valer");
    }
}
