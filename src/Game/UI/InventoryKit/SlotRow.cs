using System;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Linha focável de inventário: ícone em moldura (ou selo de rank), nome na cor do rank e um valor à direita. Confirmar (teclado, controle ou clique)
/// aciona; focar (ou passar o mouse) avisa a tela para mostrar os detalhes. Segue a moldura de linha do tema.
/// </summary>
public partial class SlotRow : PanelContainer
{
    private const int LeadSize = 80;
    private const int MinHeight = 90;

    private static StyleBoxTexture? _idle;
    private static StyleBoxTexture? _focused;

    private readonly Action? _activate;
    private readonly Label _name;
    private readonly Label _caption;
    private readonly Label _value;
    private readonly Control _lead;
    private readonly bool _dimmed;

    public SlotRow(string caption, string name, Color nameColor, string value = "", Control? lead = null, Action? activate = null, bool dimmed = false)
    {
        _activate = activate;
        _dimmed = dimmed;
        Caption = caption;
        FocusMode = FocusModeEnum.All;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, MinHeight);
        MouseDefaultCursorShape = CursorShape.PointingHand;

        var row = UiKit.MakeRow(14);
        row.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddChild(row);

        _lead = lead ?? new ItemIconSlot(LeadSize, null, null);
        _lead.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_lead);

        var text = UiKit.MakeColumn(0);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _caption = UiKit.MakeLabel(caption, UiTheme.DimVariation);
        _caption.AddThemeFontSizeOverride("font_size", 26);
        _name = UiKit.MakeLabel(name);
        _name.AddThemeColorOverride("font_color", nameColor);
        _name.ClipText = true;
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AddChild(_caption);
        text.AddChild(_name);
        row.AddChild(text);

        _value = UiKit.MakeLabel(value, UiTheme.HeadingVariation, HorizontalAlignment.Right);
        _value.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_value);
        ApplyDim();
    }

    /// <summary>Linha de cabeçalho do slot ("Cabeça", "Arma"...).</summary>
    public string Caption { get; }

    /// <summary>Avisa a tela de que esta linha ganhou o foco (atualizar o painel de detalhes).</summary>
    public event Action<SlotRow>? Highlighted;

    /// <summary>Dado livre que a tela amarra à linha (peça, material, Eco...).</summary>
    public object? Tag { get; set; }

    public Control Lead => _lead;

    public string NameText => _name.Text;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _idle ??= UiTheme.RowFrame(highlighted: false);
        _focused ??= UiTheme.RowFrame(highlighted: true);
        AddThemeStyleboxOverride("panel", _idle);
        FocusEntered += () =>
        {
            AddThemeStyleboxOverride("panel", _focused);
            UiScreen.PlayMoveSound();
            SetLeadSelected(true);
            Highlighted?.Invoke(this);
        };
        FocusExited += () =>
        {
            AddThemeStyleboxOverride("panel", _idle);
            SetLeadSelected(false);
        };
        MouseEntered += () =>
        {
            if (IsVisibleInTree())
            {
                GrabFocus();
            }
        };
        AddChild(new FocusGlow { Outset = 6 });
    }

    public override void _GuiInput(InputEvent @event)
    {
        var accepted = @event.IsActionPressed("ui_accept")
            || (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left });
        if (!accepted)
        {
            return;
        }

        AcceptEvent();
        if (_activate is null)
        {
            UiSfx.Play(UiSound.Deny);
            return;
        }

        UiSfx.Play(UiSound.Confirm);
        _activate();
    }

    public void SetValue(string value) => _value.Text = value;

    private void SetLeadSelected(bool selected)
    {
        if (_lead is ItemIconSlot slot)
        {
            slot.Selected = selected;
        }
    }

    private void ApplyDim() => Modulate = _dimmed ? new Color(1f, 1f, 1f, 0.62f) : Colors.White;
}
