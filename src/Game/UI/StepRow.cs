using System;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Linha de opção focável: rótulo à esquerda e valor com setas à direita. Esquerda/direita (teclado, direcional)
/// diminuem/aumentam; confirmar avança; o mouse usa as setas. O valor é lido de volta após cada mudança.
/// </summary>
public partial class StepRow : PanelContainer
{
    private const int ValueWidth = 360;
    private const int ArrowWidth = 72;

    private static StyleBoxTexture? _idle;
    private static StyleBoxTexture? _focused;

    private readonly string _caption;
    private readonly Func<string> _display;
    private readonly Action<int> _step;
    private Label _value = null!;

    public StepRow(string caption, Func<string> display, Action<int> step)
    {
        _caption = caption;
        _display = display;
        _step = step;
    }

    public string Caption => _caption;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ProcessMode = ProcessModeEnum.Always;
        _idle ??= UiTheme.RowFrame(highlighted: false);
        _focused ??= UiTheme.RowFrame(highlighted: true);
        AddThemeStyleboxOverride("panel", _idle);
        FocusEntered += () =>
        {
            AddThemeStyleboxOverride("panel", _focused);
            UiScreen.PlayMoveSound();
        };
        FocusExited += () => AddThemeStyleboxOverride("panel", _idle);
        MouseEntered += () => GrabFocus();
        AddChild(new FocusGlow { Outset = 14 });

        var row = UiKit.MakeRow(10);
        AddChild(row);
        var caption = UiKit.MakeLabel(_caption);
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(caption);
        row.AddChild(MakeArrow("<", -1));
        _value = UiKit.MakeLabel(_display(), null, HorizontalAlignment.Center);
        _value.CustomMinimumSize = new Vector2(ValueWidth, 0);
        row.AddChild(_value);
        row.AddChild(MakeArrow(">", 1));
    }

    public override void _GuiInput(InputEvent @event)
    {
        var direction = 0;
        if (@event.IsActionPressed("ui_left"))
        {
            direction = -1;
        }
        else if (@event.IsActionPressed("ui_right") || @event.IsActionPressed("ui_accept"))
        {
            direction = 1;
        }

        if (direction == 0)
        {
            return;
        }

        AcceptEvent();
        Step(direction);
    }

    /// <summary>Atualiza o texto do valor (use quando outra coisa mudou o dado).</summary>
    public void Refresh() => _value.Text = _display();

    private void Step(int direction)
    {
        UiSfx.Play(UiSound.Confirm);
        _step(direction);
        Refresh();
    }

    private Button MakeArrow(string text, int direction)
    {
        var arrow = UiKit.MakeButton(text, () => { GrabFocus(); _step(direction); Refresh(); }, ArrowWidth);
        arrow.FocusMode = FocusModeEnum.None;
        return arrow;
    }
}
