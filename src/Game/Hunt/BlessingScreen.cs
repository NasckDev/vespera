using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Hunt;

/// <summary>
/// Bênçãos da Lanterna (ROTEIRO-V2 §5.1): ao limpar a sala, 3 cartas com a cor da família (Chama âmbar, Vigília creme,
/// Sopa verde, Mapa azul, Aposta magenta); o caçador escolhe 1. Teclado, controle e mouse; o jogo fica pausado.
/// Sem "voltar": é preciso escolher.
/// </summary>
public partial class BlessingScreen(IReadOnlyList<BlessingDef> offer, Action<string> onPick) : HunterScreen
{
    private const int CardWidth = 440;
    private const int IconSize = 96;
    private const int MaxPendingLines = 2;
    private const int PanelWidth = 1300;
    private static readonly Color ComboColor = new("ffd36a");
    private const string IconFolder = "res://assets/v2/interface/bencaos/";
    private readonly List<Button> _cards = [];

    /// <summary>Ícone pintado da família (issue #43: chama, vigilia, sopa, mapa, aposta); null se faltar a arte.</summary>
    public static Texture2D? FamilyIcon(string family)
    {
        var path = $"{IconFolder}{family}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    public static BlessingScreen? Active { get; private set; }

    public IReadOnlyList<BlessingDef> Offer => offer;

    protected override bool CanGoBack => false;

    public static void Show(IReadOnlyList<BlessingDef> offer, Action<string> onPick)
    {
        if (offer.Count == 0 || Active is not null)
        {
            return;
        }

        Open(new BlessingScreen(offer, onPick));
    }

    protected override Control? DefaultFocus() => _cards.Count > 0 ? _cards[0] : null;

    public override void _EnterTree() => Active = this;

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Active == this)
        {
            Active = null;
        }
    }

    /// <summary>Escolhe a carta <paramref name="index"/> (testes e bots).</summary>
    public void Choose(int index)
    {
        if (index < 0 || index >= offer.Count)
        {
            return;
        }

        var id = offer[index].Id;
        Close();
        onPick(id);
    }

    protected override void Build()
    {
        var veil = new ColorRect { Color = new Color(0.03f, 0.03f, 0.1f, 0.94f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = UiKit.MakeColumn(28);
        center.AddChild(column);
        column.AddChild(UiKit.MakeLabel("A lanterna brilha. Escolha uma bênção.", UiTheme.TitleVariation, HorizontalAlignment.Center));
        column.AddChild(UiKit.MakeLabel("Vale até o fim desta caçada.", UiTheme.DimVariation, HorizontalAlignment.Center));
        var row = UiKit.MakeRow(36);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(row);
        var families = HuntService.BlessingCatalog.Families;
        for (var i = 0; i < offer.Count; i++)
        {
            var b = offer[i];
            var family = families.TryGetValue(b.Family, out var f) ? f : new BlessingFamily { Name = b.Family };
            row.AddChild(Card(b, family, i));
        }

        AddComboPanel(column);
        UiKit.WrapFocus(_cards);
    }

    /// <summary>Combinações em vigor e as que ainda faltam uma família (ROTEIRO-V2 §5.1), em português, sob as cartas.</summary>
    private static void AddComboPanel(Control column)
    {
        var catalog = HuntService.BlessingCatalog;
        var owned = HuntService.Blessings.ToList();
        var lines = new List<string>();
        foreach (var combo in catalog.ActiveCombos(owned))
        {
            lines.Add($"Combinação ativa: {combo.Name} ({FamilyNames(catalog, combo.Families)})");
        }

        foreach (var combo in catalog.PendingCombos(owned).Take(MaxPendingLines))
        {
            var missing = FamilyNames(catalog, catalog.MissingFamilies(combo, owned));
            lines.Add($"Falta {missing} para {combo.Name} ({FamilyNames(catalog, combo.Families)}).");
        }

        if (lines.Count == 0)
        {
            column.AddChild(UiKit.MakeLabel("Bênçãos de famílias diferentes se combinam e dão um bônus extra.", UiTheme.DimVariation, HorizontalAlignment.Center));
            return;
        }

        foreach (var text in lines)
        {
            var label = UiKit.MakeWrapped(text, UiTheme.DimVariation);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(PanelWidth, 0);
            label.AddThemeColorOverride("font_color", text.StartsWith("Combinação ativa", StringComparison.Ordinal) ? ComboColor : Palette.Cream);
            column.AddChild(label);
        }
    }

    private static string FamilyNames(BlessingCatalog catalog, IEnumerable<string> families) =>
        string.Join(" + ", families.Select(f => catalog.Families.TryGetValue(f, out var family) ? family.Name : f));

    private Button Card(BlessingDef b, BlessingFamily family, int index)
    {
        var color = new Color(family.Color);
        var card = UiKit.MakeButton(string.Empty, () => Choose(index), CardWidth);
        card.CustomMinimumSize = new Vector2(CardWidth, 540);
        var style = new StyleBoxFlat { BgColor = new Color(Palette.Indigo, 0.96f), BorderColor = color, AntiAliasing = true };
        style.SetBorderWidthAll(5);
        style.SetCornerRadiusAll(14);
        card.AddThemeStyleboxOverride("normal", style);
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = color.Darkened(0.65f);
        card.AddThemeStyleboxOverride("hover", hover);
        card.AddThemeStyleboxOverride("focus", hover);
        card.AddThemeStyleboxOverride("pressed", hover);
        var margin = UiKit.MakeMargin(26, 24, 26, 24);
        margin.MouseFilter = MouseFilterEnum.Ignore;
        card.AddChild(margin);
        var box = UiKit.MakeColumn(14);
        box.MouseFilter = MouseFilterEnum.Ignore;
        margin.AddChild(box);
        if (FamilyIcon(b.Family) is { } icon)
        {
            box.AddChild(new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, IconSize),
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        var tag = UiKit.MakeLabel($"{family.Name} · {family.Who}", UiTheme.DimVariation, HorizontalAlignment.Center);
        tag.AddThemeColorOverride("font_color", color);
        var name = UiKit.MakeLabel(b.Name, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        name.AddThemeColorOverride("font_color", color.Lerp(Palette.Cream, 0.3f));
        var text = UiKit.MakeWrapped(b.Text, UiTheme.ReadingVariation);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var label in new[] { tag, name, text })
        {
            label.MouseFilter = MouseFilterEnum.Ignore;
            box.AddChild(label);
        }

        foreach (var combo in HuntService.BlessingCatalog.CompletedBy(HuntService.Blessings.ToList(), b.Id))
        {
            var line = UiKit.MakeWrapped($"Combina! {combo.Name}: {combo.Text}", UiTheme.ReadingVariation);
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.AddThemeColorOverride("font_color", ComboColor);
            line.MouseFilter = MouseFilterEnum.Ignore;
            box.AddChild(line);
        }

        _cards.Add(card);
        return card;
    }
}
