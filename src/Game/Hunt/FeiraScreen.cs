using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Horta;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Hunt;

/// <summary>
/// Feira da Nilza na Praça (Ato II): compra as receitas com Lumes e cozinha o prato com os ingredientes da Horta. O prato
/// pronto vale na próxima caçada (é comido ao entrar na Brecha). Teclado, controle e mouse.
/// </summary>
public partial class FeiraScreen : HunterScreen
{
    private VBoxContainer _list = null!;
    private Label _header = null!;
    private Label _status = null!;
    private Button _close = null!;
    private readonly List<(string DishId, Button Button)> _buttons = [];

    public static FeiraScreen? Active { get; private set; }

    public static new void Show() => Open(new FeiraScreen());

    /// <summary>Última frase mostrada ao jogador (testes e capturas).</summary>
    public string StatusText => _status.Text;

    /// <summary>Texto do botão do prato (testes): "Comprar receita (20 Lumes)", "Cozinhar"...</summary>
    public string ButtonTextOf(string dishId) => _buttons.FirstOrDefault(b => b.DishId == dishId).Button?.Text ?? string.Empty;

    public void Press(string dishId) => _buttons.FirstOrDefault(b => b.DishId == dishId).Button?.EmitSignal(BaseButton.SignalName.Pressed);

    protected override Control? DefaultFocus() => _buttons.Count > 0 ? _buttons[0].Button : _close;

    public override void _EnterTree() => Active = this;

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Active == this)
        {
            Active = null;
        }
    }

    protected override void OnProgressionChanged()
    {
        if (IsInsideTree() && _list is not null)
        {
            Refresh(null);
        }
    }

    protected override void Build()
    {
        var panel = BuildPage("Feira da Nilza", out var column, sideMargin: 230);
        var content = UiKit.MakeColumn(14);
        panel.AddChild(UiKit.PadForGlow(content));
        _header = UiKit.MakeWrapped(string.Empty, UiTheme.ReadingVariation);
        content.AddChild(_header);
        _status = UiKit.MakeWrapped(string.Empty, UiTheme.HeadingVariation);
        content.AddChild(_status);
        var scroll = UiKit.MakeScroll();
        content.AddChild(scroll);
        _list = UiKit.MakeColumn(22);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(_list));

        var footer = MakeFooter();
        _close = UiKit.MakeButton("Voltar", Close, 300);
        footer.AddChild(_close);
        column.AddChild(footer);
        Refresh(null);
    }

    private void Refresh(string? message)
    {
        var state = HuntService.HortaState;
        var rules = HuntService.Horta;
        if (message is not null)
        {
            _status.Text = message;
        }

        var meal = rules.Def.Dish(state.Meal);
        _header.Text = $"Lumes: {(Progression.IsActive ? Progression.State.Lumes : 0)}   ·   Despensa: {rules.PantryText(state)}\n"
            + (meal is null ? "Nenhum prato pronto." : $"Prato pronto para a próxima caçada: {meal.Name}");
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        _buttons.Clear();
        foreach (var dish in rules.Def.Dishes)
        {
            _list.AddChild(BuildDish(dish, state, rules));
        }

        if (!HasFocusInside())
        {
            CallDeferred(nameof(FocusDefault));
        }
    }

    private bool HasFocusInside() => GetViewport()?.GuiGetFocusOwner() is { } owner && IsAncestorOf(owner);

    private Control BuildDish(DishDef dish, HortaState state, HortaRules rules)
    {
        var card = UiKit.MakeColumn(4);
        var learned = state.Recipes.Contains(dish.Id);
        var text = learned ? "Cozinhar" : $"Comprar receita ({dish.Price} Lumes)";
        var id = dish.Id;
        var button = UiKit.MakeButton($"{dish.Name}  ·  {text}", () => Act(id), 900);
        card.AddChild(button);
        _buttons.Add((dish.Id, button));
        card.AddChild(UiKit.MakeWrapped($"{dish.Desc} {dish.Label}", UiTheme.ReadingVariation));
        card.AddChild(UiKit.MakeWrapped($"Ingredientes: {rules.IngredientsText(state, dish)}", UiTheme.DimVariation));
        return card;
    }

    private void Act(string dishId)
    {
        var learned = HuntService.HortaState.Recipes.Contains(dishId);
        var (_, message) = learned ? HortaService.Cook(dishId) : HortaService.Learn(dishId);
        Refresh(message);
    }
}
