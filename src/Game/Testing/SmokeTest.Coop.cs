using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Coop;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Co-op local (<c>--coop-bot</c>): pelo caminho real da interface, o J2 entra no meio do jogo (pausa → "Chamar J2" →
/// controle 2 aperta A, troca de classe, confirma), depois as duas fontes de input simuladas caçam juntas na Brecha
/// (<see cref="CoopBot"/>) e, no fim, o J2 sai pela pausa. Com <c>--shots</c> e janela, fotografa a tela de entrada.
/// </summary>
public partial class SmokeTest
{
    public const string CoopBotFlag = "--coop-bot";

    private async Task RunCoop(Level level)
    {
        CheckNumpadCodes();
        await CaptureFreshJoinScreen();
        var bot = new CoopBot(level, this) { Shots = _shotsDir is not null && DisplayServer.GetName() != "headless" };
        CoopSession.SourceOverride = bot.SourceFor;
        await JoinGuestThroughPause(level);
        _failures.AddRange(await bot.Run());
        await LeaveThroughPause();
        CoopSession.SourceOverride = null;
    }

    /// <summary>As teclas do J2 (números do Godot.Key guardados como inteiros na tabela pura) batem com o motor.</summary>
    private void CheckNumpadCodes()
    {
        Check(BindingTable.KeyKp0 == (long)Key.Kp0 && BindingTable.KeyKp1 == (long)Key.Kp1 && BindingTable.KeyKp2 == (long)Key.Kp2
            && BindingTable.KeyKp3 == (long)Key.Kp3 && BindingTable.KeyKpEnter == (long)Key.KpEnter && BindingTable.KeyEnter == (long)Key.Enter
            && BindingTable.KeyKpAdd == (long)Key.KpAdd && BindingTable.KeyKpSubtract == (long)Key.KpSubtract
            && BindingTable.KeyKpMultiply == (long)Key.KpMultiply && BindingTable.KeyKpPeriod == (long)Key.KpPeriod,
            "códigos do teclado numérico do J2 não batem com Godot.Key");
    }

    /// <summary>Tela de entrada como vem do título: J1 no teclado (esquerda), J2 na metade direita, classes iguais.</summary>
    private async Task CaptureFreshJoinScreen()
    {
        var screen = new CoopJoinScreen();
        GetTree().Paused = true;
        _router.Push(screen);
        await Frames(4);
        await Shot("coop_entrada_vazia", 0.6);
        await SendKey(Key.J);
        await SendKey(Key.Kp1);
        Check(screen.Roster.Count == 2 && screen.Roster.KeyboardSplit, "J e Num 1 deveriam pôr dois caçadores no teclado dividido");
        await SendKey(Key.Left);
        Check(screen.Roster.Slots.Count == 2 && screen.Roster.Slots[1].ClassId == "sword", "Seta esq. deveria trocar a classe do J2 para a Lâmina");
        await SendKey(Key.J);
        Check(screen.Roster.Slots[0].Ready && !screen.Roster.Slots[1].Ready, "J deveria confirmar só o J1");
        await Shot("coop_entrada_teclado_dividido", 0.6);
        await SendKey(Key.Escape);
        Check(!screen.Roster.Slots[0].Ready, "Esc deveria desfazer a confirmação do J1");
        await SendKey(Key.Escape);
        await SendKey(Key.Kp0);
        await SendKey(Key.Escape);
        await Frames(3);
        Check(FindOverlay<CoopJoinScreen>() is null, "Esc sem ninguém no teclado deveria fechar a tela de entrada");
        GetTree().Paused = false;
        await Frames(2);
    }

    private async Task JoinGuestThroughPause(Level level)
    {
        await Tap(Key.Escape);
        Check(_router.OverlayDepth == 1 && GetTree().Paused, "Esc deveria abrir a pausa");
        await FocusButton("Chamar J2 (co-op)");
        await Tap(Key.Enter);
        await WaitFor(() => FindOverlay<CoopJoinScreen>() is not null, "tela de entrada do co-op");
        var screen = FindOverlay<CoopJoinScreen>()!;
        Check(screen.HotJoin && screen.Roster.Count == 1 && screen.Roster.Slots[0].Locked, "na pausa o J1 já deveria estar dentro e travado");

        // Controle 2 (índice 1): A entra, direcional troca a classe (Arco → Martelo → Arco), A confirma, A de novo começa.
        await PadButton(1, JoyButton.A);
        Check(screen.Roster.Count == 2 && screen.Roster.Slots[1].Device == InputDevice.Gamepad(1), "A no controle 2 deveria pôr o J2 no grupo");
        await PadButton(1, JoyButton.DpadRight);
        Check(screen.Roster.Slots[1].ClassId == "hammer", $"direcional deveria trocar a classe do J2 (está em {screen.Roster.Slots[1].ClassId})");
        await PadButton(1, JoyButton.DpadLeft);
        await PadButton(1, JoyButton.A);
        Check(screen.Roster.CanStart, "com o J2 confirmado o grupo deveria poder começar");
        await Shot("coop_entrada_pausa", 0.5);
        await PadButton(1, JoyButton.A);
        await WaitFor(() => _router.OverlayDepth == 0 && !GetTree().Paused, "jogo de volta depois de o J2 entrar");
        Check(CoopSession.Active && level.Players.Count == 2, $"o J2 deveria nascer na Praça (caçadores: {level.Players.Count})");
        Check(level.CombatHud.GuestPanelVisible, "a HUD deveria mostrar o painel do J2");
        Check(level.Players.Count == 2 && level.Players[1].Hunter == HunterClass.Arco, "o J2 deveria entrar com o Arco");
        Log.Info("co-op: J2 entrou pela pausa com o controle 2 (Arco)");
    }

    private async Task LeaveThroughPause()
    {
        await WaitFor(() => _router.Current is Level { Switching: false } && !GetTree().Paused, "Praça antes de o J2 sair");
        var level = (Level)_router.Current!;
        await Tap(Key.Escape);
        await FocusButton("J2 sai do co-op");
        await Tap(Key.Enter);
        await WaitFor(() => _router.OverlayDepth == 0 && !GetTree().Paused, "jogo de volta depois de o J2 sair");
        Check(!CoopSession.Active && level.Players.Count == 1, $"depois de sair o jogo deveria voltar ao solo ({level.Players.Count} caçadores)");
        Check(level.Player.Input is MergedInput, "o J1 deveria voltar a ler todos os dispositivos");
        Check(!level.CombatHud.GuestPanelVisible, "o painel do J2 deveria sumir");
        Log.Info("co-op: J2 saiu pela pausa; jogo solo de novo");
    }

    private async Task PadButton(int device, JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { Device = device, ButtonIndex = button, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventJoypadButton { Device = device, ButtonIndex = button, Pressed = false });
        await Frames(2);
    }
}
