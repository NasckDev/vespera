using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>Pausa sobre o jogo: Continuar, Opções, Controles, Ajuda e Voltar ao título. Pausar a árvore é papel de quem abre.</summary>
public partial class PauseScreen : UiScreen
{
    private const int MenuWidth = 600;

    private Button _resume = null!;

    protected override Control? DefaultFocus() => _resume;

    protected override void Back() => Resume();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.Pause) && IsVisibleInTree() && SceneRouter.Instance.IsActive(this))
        {
            GetViewport().SetInputAsHandled();
            Resume();
            return;
        }

        base._UnhandledInput(@event);
    }

    protected override void Build()
    {
        AddChild(UiKit.MakeBackdrop(showArt: false));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);
        var column = UiKit.MakeColumn(12);
        column.AddChild(UiKit.MakeLabel("Pausa", UiTheme.TitleVariation, HorizontalAlignment.Center));

        panel.AddChild(UiKit.PadForGlow(column));
        var buttons = new List<Control>();
        _resume = Add(column, buttons, "Continuar", Resume);
        if (Progression.IsActive)
        {
            Add(column, buttons, "Inventário", () => InventoryScreen.Open());
            Add(column, buttons, "Caçador", () => CharacterScreen.Open());
        }

        Add(column, buttons, "Opções", () => SceneRouter.Instance.Push(ScreenKeys.Options));
        Add(column, buttons, "Controles", () => SceneRouter.Instance.Push(ScreenKeys.Controls));
        Add(column, buttons, "Ajuda", () => SceneRouter.Instance.Push(ScreenKeys.Help));
        Add(column, buttons, "Voltar ao título", ReturnToTitle);
        UiKit.WrapFocus(buttons);
    }

    private static Button Add(Container parent, List<Control> all, string text, System.Action onPressed)
    {
        var button = UiKit.MakeButton(text, onPressed, MenuWidth, UiTheme.MenuButtonVariation);
        parent.AddChild(button);
        all.Add(button);
        return button;
    }

    private void Resume()
    {
        GetTree().Paused = false;
        SceneRouter.Instance.Pop();
    }

    private void ReturnToTitle()
    {
        GameSession.Autosave();
        GameSession.End();
        SceneRouter.Instance.GoTo(ScreenKeys.Title);
    }
}
