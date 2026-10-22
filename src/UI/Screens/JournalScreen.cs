using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>Diário de missões (<c>screenJournal</c>, <c>extra.js:236-241</c>): cumpridas, a atual com progresso e a próxima como "???".</summary>
public partial class JournalScreen : FrameScreen
{
    private readonly VBoxContainer _list = Ui.Column(3);
    private readonly Control _focusAnchor;

    public JournalScreen()
        : base(UiText.PauseJournal)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        Body.AddChild(scroll);
        _focusAnchor = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_focusAnchor);
    }

    protected override Control? DefaultFocus => _focusAnchor;

    protected override void OnOpened()
    {
        Ui.Clear(_list);
        var state = GameManager.Instance.State;
        var quests = Database.Db.Quests;
        var current = state.Save.Quests.Index;
        for (var i = 0; i < quests.Count && i <= current + 1; i++)
        {
            var quest = quests[i];
            var done = i < current;
            var isCurrent = i == current;
            var name = Ui.Title(i > current ? UiText.JournalUnknown : quest.Name, UiFonts.Body, done ? UiPalette.Dim : isCurrent ? UiPalette.Creme : UiPalette.Disabled);
            var header = Ui.Row(5, Diamond.Create(6, done ? UiPalette.Done : isCurrent ? UiPalette.Ambar : new Color("#4a3d52")), name, Ui.Spacer());
            if (done)
            {
                header.AddChild(Ui.Text(UiText.JournalDone, UiFonts.Small, UiPalette.Done));
            }
            else if (isCurrent && state.QuestRules.ProgressText(quest, state.Save) is { } progress)
            {
                header.AddChild(Ui.Bold(progress, UiFonts.Small, UiPalette.Ambar));
            }

            var column = Ui.Column(2, header);
            if (isCurrent)
            {
                column.AddChild(Ui.Text(quest.Desc, UiFonts.Small, UiPalette.Body, wrap: true));
            }

            var card = Ui.Panel(isCurrent ? "TabFocus" : "SoftPanel", column);
            _list.AddChild(card);
        }
    }
}
