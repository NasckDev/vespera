using Godot;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Moldura das sub-telas da v2 (<c>frameBox</c>, <c>extra.js:179-183</c>): fundo escurecido, painel central com título em
/// Cinzel, linha dourada e "[Esc] Voltar" no canto. As filhas montam o conteúdo em <see cref="Body"/>.
/// </summary>
public abstract partial class FrameScreen : Screen
{
    protected FrameScreen(string title, float width = 490, float height = 300)
    {
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.78f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        Frame = Ui.Ornate();
        Frame.CustomMinimumSize = new Vector2(width, height);
        center.AddChild(Frame);

        var header = Ui.Row(6, TitleLabel = Ui.Title(title, UiFonts.Heading), Ui.Spacer(), Ui.Hint("Esc", "B", UiText.Back));
        Body = Ui.Column(6);
        Body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var column = Ui.Column(5, header, Ui.Rule(), Body);
        Frame.AddChild(Ui.Margin(6, 2, 6, 2, column));
    }

    public override bool HidesBelow => true;

    public override bool HidesHud => true;

    protected OrnatePanel Frame { get; }

    protected Label TitleLabel { get; }

    /// <summary>Área de conteúdo abaixo do título.</summary>
    protected VBoxContainer Body { get; }

    public override bool HandleInput(InputEvent @event)
    {
        // Backspace também volta (extra.js:183).
        if (InputReady && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Backspace })
        {
            UiSound();
            Close();
            return true;
        }

        return base.HandleInput(@event);
    }
}
