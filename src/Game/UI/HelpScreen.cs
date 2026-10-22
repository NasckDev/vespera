using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>"Como jogar": objetivo, resumo dos controles atuais, ranks e a regra da Lanterna (PLANO-V2 §5–§6 e GDD).</summary>
public partial class HelpScreen : UiScreen
{
    // Cada página cabe em ~6 linhas de 46 caracteres (corpo de 20 px na grade de 480x270).
    private const string ObjectiveOne =
        "Há vinte anos o sol se pôs sobre Lumeira e não voltou por um ano inteiro: o Ano Sem Alvor. " +
        "Foi quando as Brechas se abriram para o Avesso, uma Lumeira invertida.";

    private const string ObjectiveTwo =
        "Você chegou com um selo E no pulso e uma lanterna velha. Toda véspera uma Brecha abre num bairro: " +
        "entre, derrote os monstros e o Guardião, traga Fragmentos e forje armas melhores. " +
        "No fim do caminho está o Alvor, quando o sol volta de vez.";

    private const string RanksText =
        "E, D, C, B, A, S, SS, SSS e Alvor. Você sobe por XP e por conquista: vença o Guardião do rank e, " +
        "em C, A, S e SSS, passe no exame da Ordem. Cada rank dá mais vida e um novo selo.";

    private const string LootText =
        "Cada Brecha mostra o poder recomendado. O loot vem do rank dela (80%), do rank seguinte (15%) e raros (5%). " +
        "Fragmentos vêm do Avesso (forja e Ecos); Lumes, da cidade (comida, poções, mapas).";

    private const string LanternText =
        "Se você cair numa Brecha, os Fragmentos daquela caçada ficam lá, na sua Lanterna caída. Volte e recupere-a. " +
        "Seu equipamento nunca é perdido. No co-op, um aliado pode recuperar a Lanterna por você.";

    private const string MenuTip = "Menus: setas e Enter, ou direcional e A. Esc ou B volta.";

    private Label _heading = null!;
    private PanelContainer _panel = null!;
    private MarginContainer _body = null!;
    private Button _next = null!;
    private int _page;

    private IReadOnlyList<(string Title, Func<Control> Body)> Pages =>
    [
        ("Objetivo", () => UiKit.MakeWrapped(ObjectiveOne, UiTheme.ReadingVariation)),
        ("Objetivo", () => UiKit.MakeWrapped(ObjectiveTwo, UiTheme.ReadingVariation)),
        ("Controles", BuildControlsPage),
        ("Ranks, E até Alvor", () => UiKit.MakeWrapped(RanksText, UiTheme.ReadingVariation)),
        ("Brechas e loot", () => UiKit.MakeWrapped(LootText, UiTheme.ReadingVariation)),
        ("A Lanterna", () => UiKit.MakeWrapped(LanternText, UiTheme.ReadingVariation)),
    ];

    protected override Control? DefaultFocus() => _next;

    protected override void Build()
    {
        _panel = BuildPage("Como jogar", out var column, sideMargin: 150);
        var inner = UiKit.MakeColumn(10);
        _panel.AddChild(inner);
        _heading = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        inner.AddChild(_heading);
        _body = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        inner.AddChild(_body);

        var footer = UiKit.MakeRow(24);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(footer);
        var previous = UiKit.MakeButton("Anterior", () => ShowPage(_page - 1), 330);
        _next = UiKit.MakeButton("Próxima", () => ShowPage(_page + 1), 330);
        var back = UiKit.MakeButton("Voltar", Back, 330);
        footer.AddChild(previous);
        footer.AddChild(_next);
        footer.AddChild(back);
        UiKit.WrapFocus([previous, _next, back]);
        ShowPage(0);
    }

    private static Control BuildControlsPage()
    {
        var family = GlyphNames.DetectFamily();
        var column = UiKit.MakeColumn(24);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 10);
        column.AddChild(grid);

        AddEntry(grid, "Mover", "WASD", "analógico");
        foreach (var id in new[] { InputActions.Attack, InputActions.Heavy, InputActions.Dodge, InputActions.Skill, InputActions.Interact, InputActions.Lantern, InputActions.Inventory, InputActions.Pause })
        {
            var keyboard = GlyphNames.Describe(InputSetup.Table.Get(id, BindingSlot.Primary), family);
            var pad = GlyphNames.Describe(InputSetup.Table.Get(id, BindingSlot.Gamepad), family);
            AddEntry(grid, InputActions.LabelOf(id), keyboard, pad);
        }

        column.AddChild(UiKit.MakeWrapped(MenuTip, UiTheme.DimVariation));
        return column;
    }

    private static void AddEntry(GridContainer grid, string label, string keyboard, string pad)
    {
        var text = UiKit.MakeLabel($"{label}: {keyboard} / {pad}", UiTheme.ReadingVariation);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(text);
    }

    private void ShowPage(int index)
    {
        var pages = Pages;
        _page = (index + pages.Count) % pages.Count;
        foreach (var child in _body.GetChildren())
        {
            child.QueueFree();
        }

        _body.AddChild(pages[_page].Body());
        _heading.Text = $"{pages[_page].Title} ({_page + 1}/{pages.Count})";
    }
}
