using System;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Escolha da arma antes de entrar na Brecha: três cartões (Lâmina, Arco, Martelo) com o caçador parado animado, o
/// estilo e os comandos. Esquerda/direita escolhe, interagir/ataque confirma, Esc/esquiva cancela.
/// </summary>
public partial class ClassPicker : Control
{
    private static readonly HunterClass[] Order = [HunterClass.Lamina, HunterClass.Arco, HunterClass.Martelo];

    private static readonly string[] Blurbs =
    [
        "Equilibrada. Combo de 3 golpes;\nsegure o pesado: corte giratório.",
        "Longo alcance. Toque: flecha.\nSegure: flecha de luz que atravessa.",
        "Lento e brutal. Quebra partes em dobro;\nsegure o pesado: martelada no chão.",
    ];

    private readonly PanelContainer[] _cards = new PanelContainer[3];
    private int _index;

    public ClassPicker(HunterClass current) => _index = Array.IndexOf(Order, current);

    public event Action<HunterClass>? Chosen;

    public event Action? Cancelled;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(new ColorRect { Color = new Color(Palette.Indigo, 0.72f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        var column = UiKit.MakeColumn(26);
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        column.GrowHorizontal = GrowDirection.Both;
        column.GrowVertical = GrowDirection.Both;
        AddChild(column);
        column.AddChild(UiKit.MakeLabel("Esgotos do Mercado · Brecha E", UiTheme.HeadingVariation, HorizontalAlignment.Center));
        column.AddChild(UiKit.MakeLabel("Escolha a arma da caçada", null, HorizontalAlignment.Center));
        var row = UiKit.MakeRow(28);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(row);
        for (var i = 0; i < Order.Length; i++)
        {
            _cards[i] = MakeCard(i);
            row.AddChild(_cards[i]);
        }

        var keys = InputSetup.Table;
        column.AddChild(UiKit.MakeLabel(
            $"Ataque {Key(InputActions.Attack)} · Pesado {Key(InputActions.Heavy)} (segure) · Esquiva {Key(InputActions.Dodge)} · Interagir {Key(InputActions.Interact)}",
            UiTheme.DimVariation, HorizontalAlignment.Center));
        column.AddChild(UiKit.MakeLabel("←/→ escolher   ·   Interagir ou Ataque: entrar   ·   Esc: voltar", UiTheme.DimVariation, HorizontalAlignment.Center));
        Refresh();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(InputActions.MoveLeft) || e.IsActionPressed("ui_left"))
        {
            Move(-1);
        }
        else if (e.IsActionPressed(InputActions.MoveRight) || e.IsActionPressed("ui_right"))
        {
            Move(1);
        }
        else if (e.IsActionPressed(InputActions.Interact) || e.IsActionPressed(InputActions.Attack) || e.IsActionPressed("ui_accept"))
        {
            UiSfx.Play(UiSound.Confirm);
            Chosen?.Invoke(Order[_index]);
        }
        else if (e.IsActionPressed(InputActions.Pause) || e.IsActionPressed(InputActions.Dodge) || e.IsActionPressed("ui_cancel"))
        {
            UiSfx.Play(UiSound.Back);
            Cancelled?.Invoke();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    private static string Key(string action) =>
        GlyphNames.Describe(InputSetup.Table.Get(action, BindingSlot.Primary), GlyphNames.DetectFamily());

    private void Move(int step)
    {
        _index = (_index + step + Order.Length) % Order.Length;
        UiSfx.Play(UiSound.Move);
        Refresh();
    }

    private void Refresh()
    {
        for (var i = 0; i < _cards.Length; i++)
        {
            var selected = i == _index;
            _cards[i].AddThemeStyleboxOverride("panel", UiTheme.RowFrame(selected));
            _cards[i].Modulate = selected ? Colors.White : new Color(0.72f, 0.72f, 0.8f);
            _cards[i].Scale = selected ? new Vector2(1.04f, 1.04f) : Vector2.One;
        }
    }

    private PanelContainer MakeCard(int i)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(420, 470), PivotOffset = new Vector2(210, 235) };
        var inner = UiKit.MakeColumn(10);
        card.AddChild(inner);
        var stage = new Control { CustomMinimumSize = new Vector2(380, 280) };
        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = HunterSprites.Build(Order[i]),
            Position = new Vector2(190, 268),
            Offset = HunterSprites.FootOffset,
            Scale = new Vector2(2.1f, 2.1f),
        };
        stage.AddChild(sprite);
        sprite.Play(HunterSprites.IdleAnimation(Facing.Down));
        inner.AddChild(stage);
        inner.AddChild(UiKit.MakeLabel(HunterSprites.DisplayName(Order[i]), UiTheme.HeadingVariation, HorizontalAlignment.Center));
        inner.AddChild(UiKit.MakeLabel(Blurbs[i], null, HorizontalAlignment.Center));
        return card;
    }
}
