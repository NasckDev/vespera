using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro automático (<c>-- --smoke-test [--shots=pasta]</c>): abre cada tela de menu com teclas simuladas, remapeia,
/// cria um save, entra no jogo, pausa, volta ao título, continua e sai. Termina com exit code 0 se tudo passou.
/// Com renderização real e <c>--shots</c>, salva capturas 1920x1080 em PNG para revisão visual.
/// </summary>
public partial class SmokeTest : Node
{
    private const string ShotsPrefix = "--shots=";
    private const double WatchdogSeconds = 90;
    private const double BotWatchdogSeconds = 900;
    private const int HelpPages = 6;
    private const double ShotSettleSeconds = 1.4;
    private const int ExitFailed = 1;
    private const int ExitTimeout = 2;

    private readonly List<string> _failures = [];
    private readonly string? _shotsDir;
    private readonly bool _withIntro;
    private readonly string[] _args;
    private SceneRouter _router = null!;

    public SmokeTest(string[] args)
    {
        _withIntro = Array.IndexOf(args, Boot.Boot.WithIntroFlag) >= 0;
        _args = args;
        ConfigureStory();
        foreach (var arg in args)
        {
            if (arg.StartsWith(ShotsPrefix, StringComparison.Ordinal))
            {
                _shotsDir = arg[ShotsPrefix.Length..];
            }
        }
    }

    public override void _Ready()
    {
        _router = SceneRouter.Instance;
        var watchdog = GetTree().CreateTimer(Array.IndexOf(_args, BotFlag) >= 0 || Array.IndexOf(_args, QaFlag) >= 0 || Array.IndexOf(_args, CombatBotFlag) >= 0 || Array.IndexOf(_args, CombatQaFlag) >= 0 || Array.IndexOf(_args, ScaleFlag) >= 0 || Array.IndexOf(_args, StoryFlag) >= 0 ? BotWatchdogSeconds : WatchdogSeconds);
        watchdog.Timeout += () =>
        {
            Log.Error("smoke: tempo esgotado");
            GetTree().Quit(ExitTimeout);
        };
        CallDeferred(nameof(StartRun));
    }

    private async void StartRun()
    {
        try
        {
            await Run();
        }
        catch (Exception ex)
        {
            _failures.Add($"exceção: {ex}");
        }

        Finish();
    }

    private void Finish()
    {
        foreach (var failure in _failures)
        {
            Log.Error($"smoke FALHOU: {failure}");
        }

        Log.Info(_failures.Count == 0 ? "smoke OK" : $"smoke com {_failures.Count} falha(s)");
        GetTree().Quit(_failures.Count == 0 ? 0 : ExitFailed);
    }

    private async Task Run()
    {
        ResetUserFiles();
        if (_withIntro)
        {
            await VisitIntro();
        }

        if (Array.IndexOf(_args, ProgressionFlag) >= 0)
        {
            await RunProgressionSmoke();
            return;
        }

        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy && FocusText() is not null, "título");
        Check(FocusText() == "Nova história", $"foco inicial no título deveria ser 'Nova história', foi '{FocusText()}'");
        Check(FindButton("Continuar")?.Disabled == true, "Continuar deveria estar desabilitado sem save");
        Check(FindButton("Co-op local (em breve)")?.Disabled == true, "Co-op local deveria estar desabilitado");
        await Shot("01_titulo");

        await VisitOptions();
        await VisitLargeText();
        await VisitControls();
        await VisitHelp();
        await VisitCredits();
        await StartNewGameAndPause();
        await ContinueAndQuit();
    }

    private async Task VisitOptions()
    {
        await OpenFromTitle("Opções");
        await Shot("02_opcoes");
        await Tap(Key.Down);
        await Tap(Key.Right);
        Check(GameSettings.Current.MusicVolume == 90, $"Música deveria ir a 90, foi {GameSettings.Current.MusicVolume}");
        await Tap(Key.Left);
        Check(GameSettings.Current.MusicVolume == 80, $"Música deveria voltar a 80, foi {GameSettings.Current.MusicVolume} (foco em '{FocusText()}')");
        await CloseWithEscape("Opções");
    }

    /// <summary>Liga "Tamanho do texto: Grande", fotografa título e controles (os mais apertados) e desliga de novo.</summary>
    private async Task VisitLargeText()
    {
        await ToggleLargeText();
        await Shot("02b_titulo_texto_grande");
        await OpenFromTitle("Controles");
        await Shot("03b_controles_texto_grande");
        await CloseWithEscape("Controles");
        await ToggleLargeText();
        Check(GameSettings.Current.TextSize == GameSettings.TextNormal, "texto deveria voltar ao normal");
    }

    private async Task ToggleLargeText()
    {
        await OpenFromTitle("Opções");
        await FocusButton("Tamanho do texto");
        await Tap(Key.Enter);
        await Shot($"02c_opcoes_texto_{GameSettings.Current.TextSize}");
        await CloseWithEscape("Opções");
    }

    private async Task VisitControls()
    {
        await OpenFromTitle("Controles");
        await Shot("03_controles");
        for (var i = 0; i < 4; i++)
        {
            await Tap(Key.Down);
        }

        await Tap(Key.Enter);
        await SendKey(Key.H);
        Check(InputSetup.Table.Get(InputActions.Attack, BindingSlot.Primary) == "key:72", "Ataque deveria estar em H");
        Check(HasKeyEvent(InputActions.Attack, Key.H), "InputMap deveria ter H no ataque");

        await Tap(Key.Down);
        await Tap(Key.Enter);
        await SendKey(Key.H);
        Check(InputSetup.Table.Get(InputActions.Heavy, BindingSlot.Primary) == "key:72", "Pesado deveria estar em H");
        Check(InputSetup.Table.Get(InputActions.Attack, BindingSlot.Primary) == "key:75", "Conflito deveria trocar: ataque vai para K");
        await Shot("04_controles_conflito");

        await FocusButton("Restaurar padrão");
        await Tap(Key.Enter);
        Check(InputSetup.Table.Get(InputActions.Attack, BindingSlot.Primary) == "key:74", "Restaurar padrão deveria devolver o ataque ao J");
        await CloseWithEscape("Controles");
    }

    private async Task VisitHelp()
    {
        await OpenFromTitle("Ajuda");
        for (var page = 1; page <= HelpPages; page++)
        {
            await Shot($"05_ajuda_{page}");
            await Tap(Key.Enter);
        }

        await CloseWithEscape("Ajuda");
    }

    private async Task VisitCredits()
    {
        await OpenFromTitle("Créditos");
        await Shot("06_creditos");
        await CloseWithEscape("Créditos");
    }

    private async Task VisitIntro()
    {
        await WaitFor(() => _router.Current is IntroScreen, "abertura");
        await Seconds(0.3);
        await Shot("00a_abertura", 0);
        await Seconds(0.6);
        await Shot("00b_abertura", 0);
        await WaitFor(() => _router.Current is TitleScreen, "título (fundido)");
        await Seconds(0.45);
        await Shot("00c_fundido", 0);
    }

    private async Task StartNewGameAndPause()
    {
        await OpenFromTitle("Nova história");
        await Shot("07_espacos");
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is PrologueScreen && !_router.IsBusy, "prólogo");
        Check(GameSession.IsActive, "sessão deveria estar ativa durante o prólogo");
        await Seconds(1.2);
        await Shot("07b_prologo");
        await Tap(Key.Space);
        await WaitFor(() => _router.Current is Level && !_router.IsBusy, "jogo");
        Check(GameSession.IsActive, "sessão deveria estar ativa");
        Check(Godot.FileAccess.FileExists(UserPaths.SlotFile(1)), "save do espaço 1 deveria existir");
        Check(SaveService.Load(1)?.Name == SaveService.DefaultName, "save deveria carregar de volta com o nome padrão");

        var level = (Level)_router.Current!;
        var startX = level.Player.GlobalPosition.X;
        Input.ParseInputEvent(MakeKey(Key.D, pressed: true));
        await PhysicsFrames(20);
        Input.ParseInputEvent(MakeKey(Key.D, pressed: false));
        Check(level.Player.GlobalPosition.X > startX, "o caçador deveria andar para a direita com a fonte de input");
        Check(level.Player.CurrentAnimation.StartsWith("andar", StringComparison.Ordinal) || level.Player.CurrentAnimation.StartsWith("parado", StringComparison.Ordinal),
            $"animação inesperada: {level.Player.CurrentAnimation}");
        await Shot("08_jogo");
        await RunLevelExtras(level);

        await Tap(Key.Escape);
        Check(_router.OverlayDepth == 1 && GetTree().Paused, "Esc deveria abrir a pausa e pausar a árvore");
        await Shot("09_pausa");
        await FocusButton("Opções");
        await Tap(Key.Enter);
        Check(_router.Current is Level && _router.OverlayDepth == 2, "Opções deveria abrir sobre a pausa");
        await Tap(Key.Escape);
        Check(FocusText() == "Opções", $"foco deveria voltar a 'Opções' na pausa, foi '{FocusText()}'");
        await Tap(Key.Escape);
        Check(_router.OverlayDepth == 0 && !GetTree().Paused, "Esc na pausa deveria continuar o jogo");

        await Tap(Key.Escape);
        await FocusButton("Voltar ao título");
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy && FocusText() is not null, "título (volta)");
        Check(FindButton("Continuar")?.Disabled == false, "Continuar deveria estar habilitado após salvar");
        await Shot("10_titulo_com_save");
    }

    private async Task ContinueAndQuit()
    {
        await OpenFromTitle("Continuar");
        await Shot("11_continuar");
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is Level && !_router.IsBusy, "jogo (continuar)");
        await Tap(Key.Escape);
        await FocusButton("Voltar ao título");
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy && FocusText() is not null, "título (final)");
        await FocusButton("Sair");
        if (_failures.Count > 0)
        {
            return;
        }

        Log.Info("smoke OK: pressionando Sair, o jogo deve encerrar com código 0");
        await Tap(Key.Enter);
        await Frames(10);
        Check(false, "Sair deveria ter encerrado o jogo");
    }

    private async Task OpenFromTitle(string buttonText)
    {
        await FocusButton(buttonText);
        await Tap(Key.Enter);
        await Frames(4);
        Check(_router.OverlayDepth == 1, $"'{buttonText}' deveria abrir uma tela (profundidade {_router.OverlayDepth})");
        Check(GetViewport().GuiGetFocusOwner() is not null, $"'{buttonText}' deveria ter foco inicial");
    }

    private async Task CloseWithEscape(string expectedFocusText)
    {
        await Tap(Key.Escape);
        await Frames(3);
        Check(_router.OverlayDepth == 0, "Esc deveria fechar a tela");
        Check(FocusText() == expectedFocusText, $"foco deveria voltar a '{expectedFocusText}', foi '{FocusText()}'");
    }

    private async Task FocusButton(string text)
    {
        for (var i = 0; i < 16 && FocusText() != text; i++)
        {
            await Tap(Key.Down);
        }

        Check(FocusText() == text, $"não alcancei o botão '{text}' (foco em '{FocusText()}')");
    }

    private string? FocusText() => GetViewport().GuiGetFocusOwner() switch
    {
        Button b => b.Text,
        StepRow row => row.Caption,
        _ => null,
    };

    private Button? FindButton(string text)
    {
        var stack = new Stack<Node>();
        stack.Push(_router.Current!);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Button b && b.Text == text)
            {
                return b;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return null;
    }

    private static bool HasKeyEvent(string action, Key key)
    {
        foreach (var ev in InputMap.ActionGetEvents(action))
        {
            if (ev is InputEventKey k && k.PhysicalKeycode == key)
            {
                return true;
            }
        }

        return false;
    }

    private static InputEventKey MakeKey(Key key, bool pressed) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = pressed };

    private async Task Tap(Key key)
    {
        Input.ParseInputEvent(MakeKey(key, pressed: true));
        await Frames(2);
        Input.ParseInputEvent(MakeKey(key, pressed: false));
        await Frames(2);
    }

    private async Task SendKey(Key key) => await Tap(key);

    private async Task Seconds(double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WaitFor(Func<bool> condition, string what)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Frames(1);
        }

        Check(condition(), $"tempo esgotado esperando: {what}");
        await Frames(2);
    }

    private void Check(bool condition, string failure)
    {
        if (!condition)
        {
            _failures.Add(failure);
        }
    }

    private async Task Shot(string name, double settle = ShotSettleSeconds)
    {
        if (_shotsDir is null || DisplayServer.GetName() == "headless")
        {
            return;
        }

        await Seconds(settle);
        var image =GetTree().Root.GetTexture().GetImage();
        var folder = ProjectSettings.GlobalizePath(_shotsDir);
        DirAccess.MakeDirRecursiveAbsolute(folder);
        var error = image.SavePng($"{folder}/{name}.png");
        Check(error == Error.Ok, $"não salvou captura {name}: {error}");
    }

    private static void ResetUserFiles()
    {
        UserFile.Delete(UserPaths.SettingsFile);
        UserFile.Delete(UserPaths.BindingsFile);
        for (var slot = 1; slot <= SaveService.SlotCount; slot++)
        {
            SaveService.Delete(slot);
        }

        GameSettings.Load();
        InputSetup.Initialize();
    }
}
