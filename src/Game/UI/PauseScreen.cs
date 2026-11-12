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

        AddCoopButton(column, buttons);
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

    /// <summary>
    /// Co-op local no meio do jogo (fora da Brecha): "Chamar J2" abre a tela de entrada com o J1 já dentro; com o J2 em
    /// jogo, "J2 sai do co-op" volta ao solo. Dentro da Brecha o botão não aparece (o grupo entra e sai junto).
    /// </summary>
    private void AddCoopButton(Container column, List<Control> buttons)
    {
        if (SceneRouter.Instance.Current is not World.Level level || !level.CanChangeParty)
        {
            return;
        }

        if (Coop.CoopSession.Active)
        {
            Add(column, buttons, "J2 sai do co-op", () =>
            {
                Resume();
                level.LeaveParty();
            });
            return;
        }

        Add(column, buttons, "Chamar J2 (co-op)", () =>
        {
            var input = level.Player.Input;
            var deviceId = (input as Actors.MergedInput)?.LastUsed.DeviceId ?? input.DeviceId;
            var lead = deviceId == LocalInputSource.KeyboardMouse ? Vespera.Core.Coop.InputDevice.Keyboard : Vespera.Core.Coop.InputDevice.Gamepad(deviceId);
            SceneRouter.Instance.Push(new CoopJoinScreen(level, lead, level.Player.Hunter));
        });
    }

    private void ReturnToTitle()
    {
        GameSession.Autosave();
        Game.Coop.CoopSession.End();
        GameSession.End();
        SceneRouter.Instance.GoTo(ScreenKeys.Title);
    }
}
