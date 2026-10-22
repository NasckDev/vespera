using System;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>Construtores pequenos de widgets para as telas (tudo em código, sem .tscn). Medidas em pixels de 1920x1080.</summary>
public static class UiKit
{
    public static Button MakeButton(string text, Action onPressed, int minWidth = 0, string? variation = null)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(minWidth, 0),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        if (variation is not null)
        {
            button.ThemeTypeVariation = variation;
        }

        button.AddChild(new FocusGlow());
        button.Pressed += () =>
        {
            UiSfx.Play(UiSound.Confirm);
            onPressed();
        };
        button.FocusEntered += UiScreen.PlayMoveSound;
        button.MouseEntered += () =>
        {
            if (!button.Disabled && button.IsVisibleInTree() && button.FocusMode != Control.FocusModeEnum.None)
            {
                button.GrabFocus();
            }
        };
        return button;
    }

    public static Label MakeLabel(string text, string? variation = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, AutowrapMode = TextServer.AutowrapMode.Off };
        if (variation is not null)
        {
            label.ThemeTypeVariation = variation;
        }

        return label;
    }

    public static Label MakeWrapped(string text, string? variation = null)
    {
        var label = MakeLabel(text, variation);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    public static ScreenBackdrop MakeBackdrop(bool showArt) => new(showArt);

    public static MarginContainer MakeMargin(int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    public static VBoxContainer MakeColumn(int separation)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static HBoxContainer MakeRow(int separation)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    /// <summary>Lista rolável que segue o foco.</summary>
    public static ScrollContainer MakeScroll() => new()
    {
        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        FollowFocus = true,
    };

    /// <summary>Envolve o conteúdo de uma lista rolável numa margem que acomoda o brilho do foco (que sai do controle).</summary>
    public static MarginContainer PadForGlow(Control content)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        margin.AddChild(content);
        return margin;
    }

    /// <summary>Liga a navegação cíclica (cima no primeiro vai ao último e vice-versa) numa lista de controles.</summary>
    public static void WrapFocus(System.Collections.Generic.IReadOnlyList<Control> items)
    {
        if (items.Count < 2)
        {
            return;
        }

        var first = items[0];
        var last = items[^1];
        first.FocusNeighborTop = first.GetPathTo(last);
        last.FocusNeighborBottom = last.GetPathTo(first);
    }
}
