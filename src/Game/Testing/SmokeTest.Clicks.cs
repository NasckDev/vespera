using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Sonda de cliques (<c>--cliques</c>): mede quantos apertos de "interagir"/"confirmar" o jogo aceita com eventos reais
/// (<c>Input.ParseInputEvent</c>, o mesmo caminho do teclado e do mouse). Rode com <c>--fixed-fps</c> diferente do tique
/// de física (60): 144 deixa quadros sem tique; 30 põe dois tiques por quadro. Toda medida tem de dar 100%.
/// </summary>
public partial class SmokeTest
{
    public const string ClicksFlag = "--cliques";
    private const int ShortTapTrials = 90;
    private const int BusyTrials = 20;
    private const int DialogueTrials = 10;

    private async Task RunClickProbe(Level level)
    {
        // Sem falas automáticas da história no meio da medição (elas abririam por cima das falas de teste).
        StoryService.SuppressAutoplay = true;
        await CloseAnyDialogue(level);
        var k = level.World.Data.ActorScale;
        var lamp = level.World.Props.First(p => p.Interaction?.Kind == "lamp");
        var spot = lamp.GlobalPosition + new Vector2(0, 50 * k);
        await StandAt(level, spot);
        Check(level.Player.Focus?.Kind == "lamp", $"cliques: o foco deveria ser o lampião, foi '{level.Player.Focus?.Kind}'");

        // A: toques curtos (solta 0, 1 ou 2 quadros depois de apertar), em fases variadas do tique de física.
        var accepted = new int[3];
        for (var i = 0; i < ShortTapTrials; i++)
        {
            var hold = i % 3;
            await Frames(1 + (i % 2));
            var before = lamp.LightOn;
            Input.ParseInputEvent(MakeKey(Key.E, pressed: true));
            await Frames(hold);
            Input.ParseInputEvent(MakeKey(Key.E, pressed: false));
            await PhysicsFrames(4);
            await Frames(2);
            accepted[hold] += lamp.LightOn != before ? 1 : 0;
        }

        var tapTotal = accepted.Sum();
        Log.Info($"cliques A (toque curto): {tapTotal}/{ShortTapTrials} aceitos (solta em 0 quadro {accepted[0]}/30, 1 quadro {accepted[1]}/30, 2 quadros {accepted[2]}/30)");
        Check(tapTotal == ShortTapTrials, $"cliques A: toque curto aceito {tapTotal}/{ShortTapTrials}");

        // B: Interagir logo depois de um golpe (o golpe ainda em curso).
        var busyOk = 0;
        for (var i = 0; i < BusyTrials; i++)
        {
            level.Player.Teleport(spot);
            await PhysicsFrames(30);
            var before = lamp.LightOn;
            Input.ParseInputEvent(MakeKey(Key.J, pressed: true));
            await PhysicsFrames(2);
            Input.ParseInputEvent(MakeKey(Key.J, pressed: false));
            await PhysicsFrames(1 + (i % 4));
            Input.ParseInputEvent(MakeKey(Key.E, pressed: true));
            await Frames(3);
            Input.ParseInputEvent(MakeKey(Key.E, pressed: false));
            await PhysicsFrames(60);
            busyOk += lamp.LightOn != before ? 1 : 0;
        }

        Log.Info($"cliques B (E logo após um golpe): {busyOk}/{BusyTrials} aceitos");
        Check(busyOk == BusyTrials, $"cliques B: E logo após golpe aceito {busyOk}/{BusyTrials}");

        // C: o clique que fecha a fala não pode virar golpe (nem roubar o próximo Interagir).
        var ghosts = 0;
        for (var i = 0; i < DialogueTrials; i++)
        {
            level.Player.Teleport(spot);
            await CloseAnyDialogue(level);
            await PhysicsFrames(30);
            Check(level.StartDialogue("pomar_cerca"), "cliques C: a fala de teste deveria abrir");
            for (var guard = 0; guard < 12 && level.DialogueOpen; guard++)
            {
                await Frames(2);
                await ClickAt(new Vector2(960, 900), hold: 2);
            }

            var swung = false;
            for (var f = 0; f < 12; f++)
            {
                await PhysicsFrames(1);
                swung |= level.Player.Combat.Moves.Busy;
            }

            ghosts += swung ? 1 : 0;
            await PhysicsFrames(40);
        }

        Log.Info($"cliques C (clique fecha a fala): {ghosts}/{DialogueTrials} viraram golpe");
        Check(ghosts == 0, $"cliques C: o clique que fecha a fala virou golpe {ghosts}/{DialogueTrials}");

        // D: Enter (ui_accept) escolhe a resposta focada, como E e o clique.
        var enterOk = 0;
        StoryService.SetFlag("met_nilza");
        for (var i = 0; i < DialogueTrials; i++)
        {
            await PhysicsFrames(20);
            await CloseAnyDialogue(level);
            Check(level.StartDialogue("nilza"), "cliques D: a fala da Nilza deveria abrir");
            level.Dialogue.Complete();
            await Frames(4);
            var node = level.Dialogue.CurrentNodeId;
            var hadChoices = level.Dialogue.HasChoices;
            await Tap(Key.Enter);
            enterOk += hadChoices && (!level.DialogueOpen || level.Dialogue.CurrentNodeId != node) ? 1 : 0;
            for (var guard = 0; guard < 8 && level.DialogueOpen; guard++)
            {
                level.Dialogue.SkipAll();
                if (level.Dialogue.HasChoices)
                {
                    await ClickAt(CenterOf(level.Dialogue.ChoiceButtons[0]), hold: 2);
                }

                await Frames(2);
            }

            await Seconds(0.4);
        }

        Log.Info($"cliques D (Enter na resposta): {enterOk}/{DialogueTrials} aceitos");
        Check(enterOk == DialogueTrials, $"cliques D: Enter na resposta aceito {enterOk}/{DialogueTrials}");
    }

    private async Task CloseAnyDialogue(Level level)
    {
        for (var guard = 0; guard < 20 && level.DialogueOpen; guard++)
        {
            level.Dialogue.SkipAll();
            if (level.Dialogue.HasChoices)
            {
                level.Dialogue.ChoiceButtons[^1].EmitSignal(BaseButton.SignalName.Pressed);
            }

            await Frames(2);
        }

        await Seconds(0.4);
    }

    private async Task ClickAt(Vector2 point, int hold)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left,
        });
        await Frames(hold);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false,
        });
        await Frames(2);
    }
}
