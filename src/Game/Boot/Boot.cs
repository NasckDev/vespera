using System;
using Godot;
using Vespera.Game.Testing;
using Vespera.Game.UI;

namespace Vespera.Game.Boot;

/// <summary>
/// Cena principal: carrega opções e atalhos, aplica o tema, registra as telas no <see cref="SceneRouter"/> e abre o título.
/// Com <c>-- --smoke-test</c> usa uma pasta de usuário isolada e roda o roteiro automático.
/// </summary>
public partial class Boot : Node
{
    public const string SmokeFlag = "--smoke-test";

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var smoke = Array.IndexOf(args, SmokeFlag) >= 0;
        if (smoke)
        {
            UserPaths.UseIsolatedRoot("smoke");
            GameSettings.SuppressWindowChanges = true;
        }

        GameSettings.Load();
        InputSetup.Initialize();
        GameSettings.Apply(GetTree());
        ApplyTheme();
        GameSettings.Changed += ApplyTheme;

        var router = GetNode<SceneRouter>("/root/SceneRouter");
        RegisterScreens(router);
        router.GoTo(ScreenKeys.Title);

        if (smoke)
        {
            AddChild(new SmokeTest(args));
        }
    }

    public override void _ExitTree() => GameSettings.Changed -= ApplyTheme;

    private void ApplyTheme() => GetNode<SceneRouter>("/root/SceneRouter").ApplyTheme(UiTheme.Build(GameSettings.Current.TextSize));

    private static void RegisterScreens(SceneRouter router)
    {
        router.Register(ScreenKeys.Title, () => new TitleScreen());
        router.Register(ScreenKeys.Options, () => new OptionsScreen());
        router.Register(ScreenKeys.Controls, () => new ControlsScreen());
        router.Register(ScreenKeys.Help, () => new HelpScreen());
        router.Register(ScreenKeys.Credits, () => new CreditsScreen());
        router.Register(ScreenKeys.Pause, () => new PauseScreen());
        router.Register(ScreenKeys.SlotsNew, () => new SlotScreen(SlotScreen.Mode.NewGame));
        router.Register(ScreenKeys.SlotsContinue, () => new SlotScreen(SlotScreen.Mode.Continue));
        router.Register(ScreenKeys.Game, () => new GameRoom());
    }
}
