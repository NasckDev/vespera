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
        "Há vinte anos o sol não nasceu sobre Lumeira por um ano inteiro: o Ano Sem Sol. " +
        "Foi quando as Brechas se abriram para o Avesso, uma Lumeira invertida.";

    private const string ObjectiveTwo =
        "Você chegou com um selo E no pulso e uma lanterna velha. Toda véspera uma Brecha abre num bairro: " +
        "entre, derrote os monstros e o Guardião, traga Fragmentos e forje armas melhores. " +
        "No fim do caminho está o Alvor, o selo mais alto da Ordem.";

    private const string RanksText =
        "E, D, C, B, A, S, SS, SSS e Alvor. Você sobe por XP e por conquista: vença o Guardião do rank " +
        "com a Ordem de olho (o exame é a própria Brecha). Cada rank dá mais vida e um novo selo.";

    private const string LootText =
        "Cada Brecha mostra o poder recomendado. O loot vem do rank dela (80%), do rank seguinte (15%) e raros (5%). " +
        "Fragmentos vêm do Avesso (forja e Ecos); Lumes, da cidade (comida, poções, mapas).";

    private const string LanternText =
        "Se você cair numa Brecha, os Fragmentos daquela caçada ficam lá, na sua Lanterna caída. Volte e recupere-a. " +
        "Seu equipamento nunca é perdido. No co-op, um aliado pode recuperar a Lanterna por você.";

    private const string EchoCoopText =
        "Ecos são habilidades deixadas pelos Guardiões. Cada caçador leva dois: o Eco 1 e o Eco 2, cada um com a sua recarga. " +
        "Para jogar em dupla, abra a pausa e escolha Chamar J2 (co-op). O J2 joga com setas e teclado numérico, ou num segundo controle, " +
        "e a câmera acompanha os dois.";

    private const string OnlineText =
        "Online: um jogador escolhe Hospedar e mostra o IP e a porta; o amigo escolhe Entrar, digita o endereço e a classe. " +
        "O amigo entra ao seu lado na cidade, quando vocês estiverem fora da Brecha. Se a conexão cair, o título avisa.";

    private const string HortaText =
        "Horta: são 6 canteiros. Cada volta da Brecha conta um dia: o que você planta vira ingrediente. " +
        "Depois de colhido, a Nilza cozinha na feira.";

    private const string BlessingsText =
        "Ao limpar uma sala, a Lanterna oferece 3 bênçãos e você escolhe 1; ela vale até o fim da caçada. " +
        "As famílias são Chama (Odete), Vigília (Benício), Sopa (Nilza), Mapa (Lis) e Aposta (Ravi). " +
        "Famílias diferentes se combinam e dão um bônus extra.";

    private const string MenuTip = "Menus: setas e Enter, ou direcional e A. Esc ou B volta.";

    private Label _heading = null!;
    private PanelContainer _panel = null!;
    private MarginContainer _body = null!;
    private Button _next = null!;
    private int _page;

    private IReadOnlyList<(string Title, Func<Control> Body)> Pages =>
    [
        ("Objetivo", () => TopText(ObjectiveOne)),
        ("Objetivo", () => TopText(ObjectiveTwo)),
        ("Controles", BuildControlsPage),
        ("Ranks, E até Alvor", () => TopText(RanksText)),
        ("Brechas e loot", () => TopText(LootText)),
        ("A Lanterna", () => TopText(LanternText)),
        ("Ecos e co-op", () => TopText(EchoCoopText)),
        ("Online", () => TopText(OnlineText)),
        ("Horta", () => TopText(HortaText)),
        ("Bênçãos", () => TopText(BlessingsText)),
    ];

    /// <summary>Texto da página mostrada (testes).</summary>
    public string BodyText { get; private set; } = string.Empty;

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

    private static Label TopText(string text)
    {
        var label = UiKit.MakeWrapped(text, UiTheme.ReadingVariation);
        label.VerticalAlignment = VerticalAlignment.Top;
        return label;
    }

    /// <summary>Tabela gerada de <see cref="InputActions.Remappable"/>: toda ação remapeável aparece, com os nomes do controle em uso.</summary>
    private static Control BuildControlsPage()
    {
        var family = GlyphNames.DetectFamily();
        var column = UiKit.MakeColumn(14);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 4);
        column.AddChild(grid);

        AddEntry(grid, "Mover", "WASD", "analógico");
        foreach (var info in InputActions.Remappable)
        {
            if (info.GamepadFixed)
            {
                continue;
            }

            var keyboard = GlyphNames.Describe(InputSetup.Table.Get(info.Id, BindingSlot.Primary), family);
            var pad = GlyphNames.Describe(InputSetup.Table.Get(info.Id, BindingSlot.Gamepad), family);
            AddEntry(grid, info.Label, keyboard, pad);
        }

        column.AddChild(UiKit.MakeWrapped(MenuTip, UiTheme.DimVariation));
        return column;
    }

    private static void AddEntry(GridContainer grid, string label, string keyboard, string pad)
    {
        var text = UiKit.MakeWrapped($"{label}: {keyboard} / {pad}", UiTheme.ReadingVariation);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(text);
    }

    private static string CollectText(Node root)
    {
        var parts = new List<string>();
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Label label)
            {
                parts.Add(label.Text);
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return string.Join(System.Environment.NewLine, parts);
    }

    private void ShowPage(int index)
    {
        var pages = Pages;
        _page = (index + pages.Count) % pages.Count;
        foreach (var child in _body.GetChildren())
        {
            child.QueueFree();
        }

        var page = pages[_page].Body();
        page.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _body.AddChild(page);
        BodyText = CollectText(page);
        _heading.Text = $"{pages[_page].Title} ({_page + 1}/{pages.Count})";
    }
}
