using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>Tela de título: Nova história, Co-op local (em breve), Continuar, Opções, Controles, Ajuda, Créditos e Sair.</summary>
public partial class TitleScreen : UiScreen
{
    private const string Version = "v0.2.0-dev";
    private const float MenuFadeSeconds = 0.7f;
    private const float MenuFadeDelay = 0.25f;
    private static readonly Rect2 MenuRect = new(1150, 340, 730, 680);

    private Button _newGame = null!;
    private Button _continue = null!;

    protected override bool CanGoBack => false;

    protected override Control? DefaultFocus() => _continue.Disabled ? _newGame : _continue;

    protected override void Build()
    {
        AddChild(new TitleBackground());

        var panel = new PanelContainer { Position = MenuRect.Position, Size = MenuRect.Size, Modulate = Colors.Transparent };
        AddChild(panel);
        CreateTween().TweenProperty(panel, "modulate:a", 1f, MenuFadeSeconds).SetDelay(MenuFadeDelay);
        var scroll = UiKit.MakeScroll();
        panel.AddChild(scroll);
        var menu = UiKit.MakeColumn(10);
        menu.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(menu));

        var buttons = new List<Control>();
        _newGame = AddMenuButton(menu, buttons, "Nova história", () => SceneRouter.Instance.Push(ScreenKeys.SlotsNew));
        var coop = AddMenuButton(menu, buttons, "Co-op local (em breve)", () => { });
        coop.Disabled = true;
        _continue = AddMenuButton(menu, buttons, "Continuar", () => SceneRouter.Instance.Push(ScreenKeys.SlotsContinue));
        _continue.Disabled = !SaveService.HasAnySave();
        AddMenuButton(menu, buttons, "Opções", () => SceneRouter.Instance.Push(ScreenKeys.Options));
        AddMenuButton(menu, buttons, "Controles", () => SceneRouter.Instance.Push(ScreenKeys.Controls));
        AddMenuButton(menu, buttons, "Ajuda", () => SceneRouter.Instance.Push(ScreenKeys.Help));
        AddMenuButton(menu, buttons, "Créditos", () => SceneRouter.Instance.Push(ScreenKeys.Credits));
        AddMenuButton(menu, buttons, "Sair", () => SceneRouter.Instance.QuitGame());
        UiKit.WrapFocus(buttons);

        var version = UiKit.MakeLabel(Version, UiTheme.DimVariation);
        version.Position = new Vector2(36, 1010);
        AddChild(version);
    }

    private static Button AddMenuButton(Container parent, List<Control> all, string text, System.Action onPressed)
    {
        var button = UiKit.MakeButton(text, onPressed, 0, UiTheme.MenuButtonVariation);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(button);
        all.Add(button);
        return button;
    }
}
