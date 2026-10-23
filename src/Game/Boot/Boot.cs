using System;
using Godot;
using Vespera.Game.Testing;
using Vespera.Game.UI;

namespace Vespera.Game.Boot;

/// <summary>
/// Cena principal: carrega opções e atalhos, aplica o tema, registra as telas no <see cref="SceneRouter"/> e abre o título.
/// Com <c>-- --smoke-test</c> usa uma pasta de usuário isolada por execução (<c>--user-dir=nome</c>) e roda o roteiro automático.
/// </summary>
public partial class Boot : Node
{
    public const string SmokeFlag = "--smoke-test";
    public const string SkipIntroFlag = "--skip-intro";
    public const string WithIntroFlag = "--with-intro";

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var smoke = Array.IndexOf(args, SmokeFlag) >= 0;
        if (smoke)
        {
            UserPaths.UseTestRoot(args);
            GameSettings.SuppressWindowChanges = true;
        }

        World.ScaleRegistry.ReadText = UserFile.ReadText;
        GameSettings.Load();
        InputSetup.Initialize();
        AddChild(new InputDevices { Name = "InputDevices" });
        GameSettings.Apply(GetTree());
        ApplyTheme();
        UiAssets.InstallCursor();
        GameSettings.Changed += ApplyTheme;

        var router = GetNode<SceneRouter>("/root/SceneRouter");
        RegisterScreens(router);
        var skipIntro = Array.IndexOf(args, SkipIntroFlag) >= 0 || (smoke && Array.IndexOf(args, WithIntroFlag) < 0);
        router.GoTo(skipIntro ? ScreenKeys.Title : ScreenKeys.Intro);

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
        router.Register(ScreenKeys.Journal, () => new JournalScreen());
        router.Register(ScreenKeys.SlotsNew, () => new SlotScreen(SlotScreen.Mode.NewGame));
        router.Register(ScreenKeys.SlotsContinue, () => new SlotScreen(SlotScreen.Mode.Continue));
        router.Register(ScreenKeys.Game, World.GameEntry.CreateStartScene);
        router.Register(ScreenKeys.Intro, () => new IntroScreen());
        router.Register(ScreenKeys.Prologue, () => new PrologueScreen());
    }
}
