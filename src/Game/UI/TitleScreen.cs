using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>Tela de título: Nova história, Co-op local, Online, Continuar, Opções, Controles, Ajuda, Créditos e Sair.</summary>
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
        AddMenuButton(menu, buttons, "Co-op local", () => SceneRouter.Instance.Push(new CoopJoinScreen()));
        AddMenuButton(menu, buttons, "Online", () => SceneRouter.Instance.Push(new OnlineScreen(inGame: false)));
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

        // Volta de uma partida online que caiu: o aviso fica no rodapé até a próxima tela.
        if (Net.NetSession.PendingNotice is { Length: > 0 } notice)
        {
            Net.NetSession.PendingNotice = null;
            LastNotice = notice;
            var label = UiKit.MakeWrapped(notice);
            label.Position = new Vector2(36, 940);
            label.Size = new Vector2(1060, 60);
            label.Modulate = new Color("f2a541");
            AddChild(label);
        }
    }

    /// <summary>Último aviso mostrado no título (queda do online), para os bots.</summary>
    public static string LastNotice { get; private set; } = string.Empty;

    private static Button AddMenuButton(Container parent, List<Control> all, string text, System.Action onPressed)
    {
        var button = UiKit.MakeButton(text, onPressed, 0, UiTheme.MenuButtonVariation);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(button);
        all.Add(button);
        return button;
    }
}
