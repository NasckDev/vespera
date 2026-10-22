using System;
using Godot;

namespace Vespera.Game.UI;

/// <summary>Caixa de confirmação sobreposta. O foco inicial fica em "Não" para evitar apagar algo por engano.</summary>
public partial class ConfirmScreen(string message, Action onYes) : UiScreen
{
    private Button _no = null!;

    protected override Control? DefaultFocus() => _no;

    protected override void Build()
    {
        AddChild(UiKit.MakeBackdrop(showArt: false));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(960, 0) };
        center.AddChild(panel);
        var column = UiKit.MakeColumn(30);
        panel.AddChild(column);
        column.AddChild(UiKit.MakeWrapped(message));

        var row = UiKit.MakeRow(30);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(row);
        var yes = UiKit.MakeButton("Sim", () =>
        {
            Back();
            onYes();
        }, 260);
        _no = UiKit.MakeButton("Não", Back, 260);
        row.AddChild(yes);
        row.AddChild(_no);
        UiKit.WrapFocus([yes, _no]);
    }
}
