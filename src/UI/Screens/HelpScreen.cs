using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>"Como jogar" (<c>screenHelp</c>, <c>extra.js:184-193</c>): 7 páginas; ↑/↓ troca de página.</summary>
public partial class HelpScreen : FrameScreen
{
    private readonly List<SelectableRow> _tabs = [];
    private readonly Label _pageTitle;
    private readonly VBoxContainer _paragraphs;
    private int _page;

    public HelpScreen()
        : base(UiText.MenuHelp)
    {
        var tabs = Ui.Column(1);
        tabs.CustomMinimumSize = new Vector2(86, 0);
        for (var i = 0; i < UiText.Help.Length; i++)
        {
            var index = i;
            var row = new SelectableRow { OnFocused = () => ShowPage(index), ClickSound = false };
            row.AddChild(Ui.Text(UiText.Help[i].Tab, UiFonts.Body, UiPalette.Sub));
            tabs.AddChild(row);
            _tabs.Add(row);
        }

        _pageTitle = Ui.Title(string.Empty, UiFonts.Large, UiPalette.Ambar);
        _paragraphs = Ui.Column(4);
        var content = Ui.Column(6, _pageTitle, _paragraphs);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Body.AddChild(Ui.Row(12, tabs, content));
    }

    protected override Control? DefaultFocus => _tabs[_page];

    private void ShowPage(int index)
    {
        _page = index;
        var (_, title, paragraphs) = UiText.Help[index];
        _pageTitle.Text = title;
        Ui.Clear(_paragraphs);
        IReadOnlyList<string> texts = paragraphs.Length > 0 ? paragraphs : ClassParagraphs();
        foreach (var text in texts)
        {
            _paragraphs.AddChild(Ui.Text(KeyText.Expand(text), UiFonts.Small, UiPalette.Body, wrap: true)); // POLI-4
        }

        for (var i = 0; i < _tabs.Count; i++)
        {
            ((Label)_tabs[i].GetChild(0)).AddThemeColorOverride("font_color", i == index ? UiPalette.Creme : UiPalette.Sub);
        }
    }

    /// <summary>Página "Classes": "Lâmina · Fenda Rubra: …" para cada classe (<c>extra.js:175</c>).</summary>
    private static List<string> ClassParagraphs()
    {
        var db = Database.Db;
        var list = new List<string>();
        foreach (var cls in db.ClassList)
        {
            if (db.Ultimates.TryGetValue(cls.Id, out var ult))
            {
                list.Add($"{cls.Name} · {ult.Name}: {ult.Desc}");
            }
        }

        return list;
    }
}
