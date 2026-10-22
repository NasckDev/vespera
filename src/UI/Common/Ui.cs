using Godot;
using Vespera.UI.Widgets;

namespace Vespera.UI.Common;

/// <summary>Atalhos para montar telas em código com o estilo do tema (rótulos, caixas, painéis, títulos de seção).</summary>
public static class Ui
{
    /// <summary>Rótulo de texto (Pixelify Sans) no tamanho e cor dados.</summary>
    public static Label Text(string text, int size = UiFonts.Body, Color? color = null, bool wrap = false, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
        };
        if (size != UiFonts.Body)
        {
            label.AddThemeFontSizeOverride("font_size", size);
        }

        if (color is { } c)
        {
            label.AddThemeColorOverride("font_color", c);
        }

        return label;
    }

    /// <summary>Rótulo em negrito (Pixelify 600).</summary>
    public static Label Bold(string text, int size = UiFonts.Body, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = Text(text, size, color, false, align);
        label.AddThemeFontOverride("font", UiFonts.TextBold);
        return label;
    }

    /// <summary>Título em Cinzel 700.</summary>
    public static Label Title(string text, int size = UiFonts.Heading, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = Text(text, size, color, false, align);
        label.AddThemeFontOverride("font", UiFonts.Title700);
        return label;
    }

    /// <summary>Números grandes em VT323.</summary>
    public static Label Number(string text, int size = 12, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = Text(text, size, color, false, align);
        label.AddThemeFontOverride("font", UiFonts.Numbers);
        return label;
    }

    public static HBoxContainer Row(int separation = 4, params Control[] children)
    {
        var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", separation);
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    public static VBoxContainer Column(int separation = 2, params Control[] children)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", separation);
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    /// <summary>Espaço que cresce para empurrar os vizinhos.</summary>
    public static Control Spacer(bool horizontal = true) => new()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore,
        SizeFlagsHorizontal = horizontal ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        SizeFlagsVertical = horizontal ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
    };

    /// <summary>Espaço fixo.</summary>
    public static Control Gap(float width, float height = 0) => new()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore,
        CustomMinimumSize = new Vector2(width, height),
    };

    public static MarginContainer Margin(int left, int top, int right, int bottom, Control? child = null)
    {
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        if (child is not null)
        {
            margin.AddChild(child);
        }

        return margin;
    }

    /// <summary>Painel do tema por variação (<c>SoftPanel</c>, <c>InsetPanel</c>, <c>SlotPanel</c>…).</summary>
    public static PanelContainer Panel(StringName variation, Control? child = null)
    {
        var panel = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (child is not null)
        {
            panel.AddChild(child);
        }

        return panel;
    }

    /// <summary>Painel ornamentado (borda dourada e losangos).</summary>
    public static OrnatePanel Ornate(Control? child = null, StringName? variation = null)
    {
        var panel = new OrnatePanel();
        if (variation is not null)
        {
            panel.ThemeTypeVariation = variation;
        }

        if (child is not null)
        {
            panel.AddChild(child);
        }

        return panel;
    }

    /// <summary>Linha dourada fina (separador da v2, <c>rgba(201,150,62,.35)</c>).</summary>
    public static ColorRect Rule(float width = 0, float alpha = 0.35f) => new()
    {
        Color = UiPalette.Gold with { A = alpha },
        CustomMinimumSize = new Vector2(width, 1),
        MouseFilter = Control.MouseFilterEnum.Ignore,
        SizeFlagsHorizontal = width > 0 ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
    };

    /// <summary>Título de seção da v2 (<c>sectionTitle</c>, <c>ui.js:132</c>): losango, CAIXA ALTA em Cinzel e linha.</summary>
    public static VBoxContainer SectionTitle(string text, int size = UiFonts.Heading)
    {
        var header = Row(5, Diamond.Create(7, UiPalette.Gold, hollow: true), Title(text.ToUpperInvariant(), size));
        return Column(3, header, Rule());
    }

    /// <summary>Tecla + legenda ("[Esc] Fechar").</summary>
    public static HBoxContainer Hint(string keyboard, string gamepad, string label, int size = UiFonts.Small, Color? color = null) =>
        Row(3, KeyCap.Create(keyboard, gamepad), Text(label, size, color ?? UiPalette.SubLight));

    /// <summary>Tecla da ação do Input Map + legenda: segue o dispositivo em uso e o remapeamento.</summary>
    public static HBoxContainer ActionHint(string action, string label, int size = UiFonts.Small, Color? color = null) =>
        Row(3, KeyCap.ForAction(action), Text(label, size, color ?? UiPalette.SubLight));

    /// <summary>Remove e libera todos os filhos.</summary>
    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Formato pt-BR com ponto de milhar (<c>toLocaleString('pt-BR')</c>).</summary>
    public static string Thousands(int value) => value.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
