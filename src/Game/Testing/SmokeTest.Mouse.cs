using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Verificação do mouse com eventos sintéticos (sem teclado): passar o mouse destaca, clique esquerdo ativa, clique direito
/// volta, a linha de opção reage ao clique, a tela de arma escolhe por hover e clique, e a fala avança com o clique.
/// </summary>
public partial class SmokeTest
{
    private async Task MouseMoveTo(Vector2 point)
    {
        var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One };
        GetViewport().PushInput(motion, inLocalCoords: true);
        await Frames(2);
    }

    private async Task MouseClick(Vector2 point, MouseButton button = MouseButton.Left)
    {
        await MouseMoveTo(point);
        var down = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true, ButtonMask = ButtonMaskFor(button) };
        GetViewport().PushInput(down, inLocalCoords: true);
        await Frames(1);
        var up = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false };
        GetViewport().PushInput(up, inLocalCoords: true);
        await Frames(3);
    }

    private static MouseButtonMask ButtonMaskFor(MouseButton button) => button switch
    {
        MouseButton.Right => MouseButtonMask.Right,
        MouseButton.Middle => MouseButtonMask.Middle,
        _ => MouseButtonMask.Left,
    };

    private static Vector2 CenterOf(Control control) => control.GetGlobalRect().GetCenter();

    private Control? FindFocusable(string text)
    {
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(_router.OverlayDepth > 0 ? (Node)GetTree().Root.FindChild("OverlayRoot", true, false)! : _router.Current!);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Button b && b.Text == text && b.IsVisibleInTree())
            {
                return b;
            }

            if (node is StepRow row && row.Caption == text && row.IsVisibleInTree())
            {
                return row;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return null;
    }

    /// <summary>Título → Opções → clique numa linha → botão direito volta; tudo só com o mouse.</summary>
    private async Task VisitMouseTitleAndOptions()
    {
        var options = FindButton("Opções") ?? throw new System.InvalidOperationException("sem botão Opções");
        await MouseMoveTo(CenterOf(options));
        Check(GetViewport().GuiGetFocusOwner() == options, $"mouse: passar sobre 'Opções' deveria dar o foco (foco em '{FocusText()}')");
        Check(InputDevices.Current == LastDevice.Mouse, "mouse: o último dispositivo deveria ser o mouse");
        await MouseClick(CenterOf(options));
        await Frames(4);
        Check(_router.OverlayDepth == 1, $"mouse: clicar em 'Opções' deveria abrir a tela (profundidade {_router.OverlayDepth})");

        var flashes = FindFocusable("Reduzir flashes") as StepRow ?? throw new System.InvalidOperationException("sem linha Reduzir flashes");
        var before = GameSettings.Current.ReduceFlashes;
        await MouseClick(CenterOf(flashes));
        Check(GameSettings.Current.ReduceFlashes != before, "mouse: clicar numa linha de opção deveria mudar o valor");
        await MouseClick(CenterOf(flashes));
        Check(GameSettings.Current.ReduceFlashes == before, "mouse: clicar de novo deveria voltar o valor");

        var music = FindFocusable("Música") as StepRow ?? throw new System.InvalidOperationException("sem linha Música");
        var volume = GameSettings.Current.MusicVolume;
        var arrowRight = FindArrow(music, ">") ?? throw new System.InvalidOperationException("sem seta >");
        await MouseClick(CenterOf(arrowRight));
        Check(GameSettings.Current.MusicVolume != volume, "mouse: a seta > deveria mudar o volume");
        var arrowLeft = FindArrow(music, "<") ?? throw new System.InvalidOperationException("sem seta <");
        await MouseClick(CenterOf(arrowLeft));
        Check(GameSettings.Current.MusicVolume == volume, "mouse: a seta < deveria desfazer");

        await MouseClick(new Vector2(960, 540), MouseButton.Right);
        await Frames(3);
        Check(_router.OverlayDepth == 0, $"mouse: botão direito deveria voltar ao título (profundidade {_router.OverlayDepth})");
    }

    private static Button? FindArrow(Node row, string text)
    {
        foreach (var child in row.GetChildren())
        {
            if (child is Button b && b.Text == text)
            {
                return b;
            }

            if (FindArrow(child, text) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>Tela de arma: hover seleciona, clique no botão confirma (sem teclado).</summary>
    private async Task ChooseClassWithMouse()
    {
        await WaitFor(() => FindClassScreen() is not null, "escolha da arma");
        var screen = FindClassScreen()!;
        Check(screen.Selected == HunterClass.Lamina, $"a Lâmina deveria começar escolhida, está {screen.Selected}");
        await Shot("07c_escolha_arma");
        await MouseMoveTo(CenterOf(screen.Cards[1]));
        Check(screen.Selected == HunterClass.Arco, $"mouse: passar sobre o 2º cartão deveria destacar o Arco, está {screen.Selected}");
        await MouseClick(CenterOf(screen.Cards[2]));
        Check(screen.Selected == HunterClass.Martelo, $"mouse: clicar no 3º cartão deveria escolher o Martelo, está {screen.Selected}");
        await MouseClick(CenterOf(screen.Cards[0]));
        Check(screen.Selected == HunterClass.Lamina, "mouse: clicar no 1º cartão deveria voltar à Lâmina");
        await Shot("07d_escolha_lamina");
        await MouseClick(CenterOf(screen.ConfirmButton));
    }

    private ClassSelectScreen? FindClassScreen()
    {
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(GetTree().Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is ClassSelectScreen screen)
            {
                return screen;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return null;
    }

    /// <summary>Uma conversa avança com o clique do mouse e a segunda caixa responde a uma escolha clicada.</summary>
    private async Task VisitMouseDialogue(Level level)
    {
        Check(level.StartDialogue("lis"), "mouse: a conversa com a Lis deveria abrir");
        await Frames(4);
        level.Dialogue.Complete();
        var first = level.Dialogue.CurrentNodeId;
        await MouseClick(new Vector2(960, 900));
        Check(level.Dialogue.CurrentNodeId != first, $"mouse: clicar deveria avançar a fala (continua em '{first}')");
        await WaitFor(() => level.Dialogue.HasChoices, "escolhas da Lis");
        var choice = level.Dialogue.ChoiceButtons[0];
        await Frames(2);
        await MouseClick(CenterOf(choice));
        Check(level.Dialogue.CurrentNodeId == "l3", $"mouse: clicar na escolha deveria ir ao nó l3 (está em '{level.Dialogue.CurrentNodeId}')");
        level.Dialogue.Complete();
        await MouseClick(new Vector2(960, 900));
        Check(!level.DialogueOpen, "mouse: o último clique deveria fechar a conversa");
        await Seconds(0.4);
    }
}
