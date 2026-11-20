using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Testing;

/// <summary>Telas que precisam caber na viewport no pior caso, em todos os tamanhos de texto (issues #78 e #82).</summary>
public partial class SmokeTest
{
    private const int WorstCaseItems = 14;

    /// <summary>Resultado da caçada com todos os materiais, peças demais, bênçãos demais e contratos cumpridos: o botão tem de ficar à vista.</summary>
    private async Task VisitResultWorstCase()
    {
        var summary = BuildWorstCaseSummary();
        for (var size = GameSettings.TextNormal; size <= GameSettings.TextHuge; size++)
        {
            _router.ApplyTheme(UiTheme.Build(size));
            foreach (var survived in new[] { true, false })
            {
                summary.Report.Survived = survived;
                RunResultScreen.Show(summary, brechaClosed: survived);
                await WaitFor(() => RunResultScreen.Active is not null && !_router.IsBusy, "tela de resultado (pior caso)");
                await Frames(4);
                var screen = RunResultScreen.Active!;
                var view = GetTree().Root.GetVisibleRect();
                var button = new Rect2(screen.ContinueButton.GlobalPosition, screen.ContinueButton.Size);
                Check(view.Encloses(button), $"resultado (texto {size}, sobreviveu {survived}): botão {button} fora da viewport {view}");
                Check(screen.ContinueButton.HasFocus(), $"resultado (texto {size}): o foco deveria estar em 'Voltar à Praça'");
                await Shot($"ui_resultado_pior_caso_{size}_{(survived ? "vivo" : "caiu")}");
                screen.Close();
                await Frames(4);
            }
        }

        _router.ApplyTheme(UiTheme.Build(GameSettings.Current.TextSize));
    }

    private static RunSummary BuildWorstCaseSummary()
    {
        var ids = Progression.Catalog.Materials.Keys.OrderBy(k => k).Take(WorstCaseItems).ToList();
        var report = new RunReport
        {
            Survived = true,
            RoomCount = 5,
            RoomsCleared = 5,
            Kills = 88,
            EliteKills = 6,
            DamageTaken = 340,
            HitsTaken = 17,
            Fragmentos = 120,
            Xp = 480,
            Seconds = 1325,
            Materials = ids.ToDictionary(id => id, _ => 7),
            Blessings = [],
            Items = [.. Enumerable.Range(0, WorstCaseItems).Select(i => new LootLine($"Botas Remendadas do Ferrão {i + 1}", "E", i % 2 == 0))],
        };
        var contract = new Contract { Kind = "colheita", Target = 5 };
        var reward = new ContractKindDef { Title = "Contrato", Xp = 40, Fragmentos = 10, Lumes = 5 };
        var contracts = Enumerable.Range(0, 3)
            .Select(i => new ContractProgress(contract, $"Contrato de teste {i + 1}", "texto", 5, 5, true, reward))
            .ToList();
        var status = new RankStatus(Rank.E, Rank.D, 400, 400, 400, [new RankRequirement(RequirementKind.Guardian, "x", "Forjar a Lâmina do rank D e vencer o Guardião da Brecha de rank E", false)]);
        var blessings = HuntService.BlessingCatalog.Blessings.Take(8).Select(b => b.Id).ToList();
        return new RunSummary(report, contracts, status, status, 12, true, new CharacterLine("Ravi", "Você demorou, mas trouxe o bastante para a semana inteira, e isso conta."), 3, 2, blessings);
    }

    /// <summary>Página de controles da Ajuda (3ª) no tamanho de texto <paramref name="size"/>: toda linha, inclusive o Golpe Supremo, acima dos botões e dentro da tela.</summary>
    private async Task VisitHelpControlsAt(int size)
    {
        await OpenFromTitle("Ajuda");
        await Tap(Key.Enter);
        await Tap(Key.Enter);
        await Frames(4);
        await Shot($"05b_ajuda_controles_texto_{size}");
        var where = $"ajuda (controles, texto {size})";
        var help = FindInTree<HelpScreen>(GetTree().Root);
        var next = help is null ? null : FindInTree<Button>(help, b => b.Text == "Próxima");
        if (help is null || next is null)
        {
            Check(false, $"{where}: tela ou botão 'Próxima' não encontrado");
        }
        else
        {
            Check(help.BodyText.Contains("Golpe Supremo", System.StringComparison.Ordinal), $"{where}: falta 'Golpe Supremo'");
            var view = GetTree().Root.GetVisibleRect();
            var lowest = 0f;
            var stack = new Stack<Node>();
            stack.Push(help);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node is Label { Text.Length: > 0 } label && label.IsVisibleInTree())
                {
                    var rect = new Rect2(label.GlobalPosition, label.Size);
                    lowest = Mathf.Max(lowest, rect.End.Y);
                    Check(view.Encloses(rect) && rect.End.Y <= next.GlobalPosition.Y, $"{where}: '{label.Text}' {rect} cortado (tela {view}, botões em y {next.GlobalPosition.Y})");
                }

                foreach (var child in node.GetChildren())
                {
                    stack.Push(child);
                }
            }

            Log.Info($"{where}: texto vai até y {lowest:0}, botões em y {next.GlobalPosition.Y:0}, tela {view.Size.Y:0}");
        }

        await CloseWithEscape("Ajuda");
    }

    /// <summary>Primeiro nó do tipo <typeparamref name="T"/> sob <paramref name="root"/> (inclusive sobreposições, que não são o <c>_router.Current</c>).</summary>
    private static T? FindInTree<T>(Node root, System.Func<T, bool>? match = null)
        where T : Node
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is T found && (match is null || match(found)))
            {
                return found;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return null;
    }
}
