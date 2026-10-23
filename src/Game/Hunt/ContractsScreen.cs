using Godot;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Hunt;

/// <summary>
/// Mural de contratos do Benício: os três contratos da próxima caçada, com o que pedem, o andamento e o pagamento.
/// Teclado, controle e mouse (fecha com o botão, Esc ou o botão direito).
/// </summary>
public partial class ContractsScreen : HunterScreen
{
    private Button _close = null!;

    public static ContractsScreen? Active { get; private set; }

    public static void Show() => Open(new ContractsScreen());

    protected override Control? DefaultFocus() => _close;

    public override void _EnterTree() => Active = this;

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Active == this)
        {
            Active = null;
        }
    }

    protected override void Build()
    {
        var panel = BuildPage("Mural de contratos", out var column, sideMargin: 300);
        var list = UiKit.MakeColumn(26);
        panel.AddChild(UiKit.PadForGlow(list));
        list.AddChild(UiKit.MakeWrapped("Valem para a próxima caçada. A cada volta da Brecha, o mural troca os contratos.", UiTheme.DimVariation));
        var board = HuntService.Board;
        foreach (var contract in HuntService.Contracts)
        {
            var p = board.Evaluate(contract, null);
            var card = UiKit.MakeColumn(4);
            card.AddChild(UiKit.MakeLabel(p.Title, UiTheme.HeadingVariation));
            card.AddChild(UiKit.MakeWrapped(p.Text, UiTheme.ReadingVariation));
            var reward = UiKit.MakeLabel($"Paga: +{p.Reward.Xp} XP  +{p.Reward.Fragmentos} Fragmentos  +{p.Reward.Lumes} Lumes", UiTheme.ReadingVariation);
            reward.AddThemeColorOverride("font_color", Palette.Amber);
            card.AddChild(reward);
            list.AddChild(card);
        }

        _close = UiKit.MakeButton("Fechar", Close, 420);
        _close.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_close);
    }
}
