using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Title;

namespace Vespera.Testing;

/// <summary>Cenários da interface (frente B3): título → classe → mundo, telas e lojas com input simulado.</summary>
public partial class SmokeTest
{
    [SmokeScenario("interface: título → história → classe → mundo", order: 5)]
    private async Task TitleToWorld()
    {
        DirAccess.RemoveAbsolute(SaveService.Instance.SavePath);
        await WaitForScene("World", 10); // boot do smoke vai para o World; o título abre daqui
        GameManager.Instance.ChangeScene(TitleScreen.ScenePath);
        await WaitForScene("Title", 10);
        var title = (TitleScreen)GetTree().CurrentScene;
        Require(title.ContinueRow.Disabled, "sem save, Continuar deveria estar desabilitado");
        Require(title.NewGameRow.HasFocus(), "sem save, o foco deveria começar em Novo jogo");
        await Shot("ui-titulo");

        await UiPress("ui_accept");
        await WaitUntil(() => title.Stack.Top == title.Story, 2, "história abrir");
        await WaitSeconds(0.2);
        await Shot("ui-historia");
        for (var i = 0; i < 5; i++)
        {
            await UiPress("ui_accept");
            await WaitSeconds(0.05);
        }

        await WaitUntil(() => title.Stack.Top == title.ClassSelect, 2, "escolha de classe abrir");
        await WaitSeconds(0.2);
        await UiKey(Key.Backspace);
        await UiKey(Key.Backspace);
        await UiKey(Key.Backspace);
        title.ClassSelect.Type("Lia");
        await UiPress("ui_right");
        await UiPress("ui_right");
        Require(title.ClassSelect.SelectedClass == "staff", $"duas vezes → deveria escolher o Cajado, está {title.ClassSelect.SelectedClass}");
        Require(title.ClassSelect.PlayerName == "Lia", $"nome digitado: '{title.ClassSelect.PlayerName}'");
        await Shot("ui-classe");

        await UiPress("ui_accept");
        await WaitForScene("World", 10);
        var state = GameManager.Instance.State;
        Require(state.Save.Name == "Lia" && state.Save.ClassId == "staff", $"jogo novo: {state.Save.Name} ({state.Save.ClassId})");
        Require(SaveService.Instance.HasSave(), "o jogo novo deveria ser salvo no início (spec 01 §1.11)");
        var world = FindWorld();
        Require(world.MapId == "guild", $"jogo novo deveria começar na Guilda, está em {world.MapId}");
        Require(FindPlayer()!.GlobalPosition.DistanceTo(new Vector2(250, 560)) < 2, $"posição inicial {FindPlayer()!.GlobalPosition}");
        await WaitSeconds(0.3);
        await Shot("ui-mundo-inicio");
    }

    /// <summary>Aperta e solta uma ação como evento (passa pela GUI: foco, botões, telas).</summary>
    private async Task UiPress(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await NextPhysicsFrame();
    }

    private async Task UiKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await NextPhysicsFrame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await NextPhysicsFrame();
    }
}
