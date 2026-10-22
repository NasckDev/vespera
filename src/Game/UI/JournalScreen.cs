using Godot;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Story;

namespace Vespera.Game.UI;

/// <summary>
/// Diário: o objetivo atual em destaque, as missões concluídas e o que vem adiante ("em breve"). Abre sobre o jogo
/// pausado (quem abre pausa a árvore, como na Pausa) e fecha com Voltar, Esc/B ou a própria tecla do Diário.
/// </summary>
public partial class JournalScreen : UiScreen
{
    private Button _back = null!;

    protected override Control? DefaultFocus() => _back;

    protected override void Back()
    {
        GetTree().Paused = false;
        SceneRouter.Instance.Pop();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if ((@event.IsActionPressed(InputActions.Journal) || @event.IsActionPressed(InputActions.Pause))
            && IsVisibleInTree() && SceneRouter.Instance.IsActive(this))
        {
            GetViewport().SetInputAsHandled();
            Back();
            return;
        }

        base._UnhandledInput(@event);
    }

    protected override void Build()
    {
        var panel = BuildPage("Diário", out var column, sideMargin: 260);
        var scroll = UiKit.MakeScroll();
        panel.AddChild(scroll);
        var list = UiKit.MakeColumn(14);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(list));
        FillEntries(list);

        var footer = UiKit.MakeRow(24);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(footer);
        _back = UiKit.MakeButton("Voltar", Back, 360);
        footer.AddChild(_back);
        UiKit.WrapFocus([_back]);
    }

    private static void FillEntries(VBoxContainer list)
    {
        var engine = StoryService.Engine;
        var objectives = engine.Script.Objectives;
        var current = engine.Script.ObjectiveIndex(engine.ObjectiveId);

        list.AddChild(UiKit.MakeLabel("Objetivo atual", UiTheme.HeadingVariation));
        if (engine.CurrentObjective is { } now)
        {
            list.AddChild(UiKit.MakeLabel(now.Title, UiTheme.ReadingVariation));
            list.AddChild(UiKit.MakeWrapped(now.Text, UiTheme.DimVariation));
        }
        else
        {
            list.AddChild(UiKit.MakeWrapped("Nenhum por enquanto. Converse com as pessoas de Lumeira.", UiTheme.DimVariation));
        }

        var done = 0;
        for (var i = 0; i < objectives.Count && i < current; i++)
        {
            if (objectives[i].Locked)
            {
                continue;
            }

            if (done++ == 0)
            {
                list.AddChild(UiKit.MakeLabel("Concluídos", UiTheme.HeadingVariation));
            }

            list.AddChild(UiKit.MakeLabel($"[x] {objectives[i].Title}", UiTheme.DimVariation));
        }

        var ahead = 0;
        for (var i = current + 1; i < objectives.Count; i++)
        {
            if (!objectives[i].Locked)
            {
                continue;
            }

            if (ahead++ == 0)
            {
                list.AddChild(UiKit.MakeLabel("Mais adiante", UiTheme.HeadingVariation));
            }

            list.AddChild(UiKit.MakeLabel($"[ ] {objectives[i].Title} (em breve)", UiTheme.DimVariation));
        }
    }
}
