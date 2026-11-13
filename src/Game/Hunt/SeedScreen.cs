using System;
using Godot;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Hunt;

/// <summary>
/// Escolha da semente para um canteiro vazio da Horta: as três sementes de Celeste com o tempo de crescimento e o rendimento.
/// Teclado, controle e mouse (fecha com Voltar, Esc ou o botão direito).
/// </summary>
public partial class SeedScreen : HunterScreen
{
    private readonly int _plot;
    private readonly Action<string> _done;
    private Button _first = null!;

    public SeedScreen(int plot, Action<string> done)
    {
        _plot = plot;
        _done = done;
    }

    public static SeedScreen? Active { get; private set; }

    public static void Show(int plot, Action<string> done) => Open(new SeedScreen(plot, done));

    /// <summary>Planta a semente <paramref name="seedId"/> como se o jogador tivesse apertado o botão (testes).</summary>
    public void Pick(string seedId) => Plant(seedId);

    protected override Control? DefaultFocus() => _first;

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
        var panel = BuildPage($"Canteiro {_plot + 1}", out var column, sideMargin: 300);
        var list = UiKit.MakeColumn(22);
        panel.AddChild(UiKit.PadForGlow(list));
        list.AddChild(UiKit.MakeWrapped("O que plantar? Cada volta da Brecha conta um dia. Depois de colhida, a Nilza cozinha na feira.", UiTheme.DimVariation));
        foreach (var seed in HuntService.Horta.Def.Seeds)
        {
            var card = UiKit.MakeColumn(4);
            var id = seed.Id;
            var button = UiKit.MakeButton($"{seed.Name}  ·  {seed.Days} {(seed.Days == 1 ? "volta" : "voltas")}  ·  rende {seed.Yield}", () => Plant(id), 760);
            _first ??= button;
            card.AddChild(button);
            card.AddChild(UiKit.MakeWrapped(seed.Desc, UiTheme.ReadingVariation));
            list.AddChild(card);
        }

        var footer = MakeFooter();
        footer.AddChild(UiKit.MakeButton("Voltar", Close, 300));
        column.AddChild(footer);
    }

    private void Plant(string seedId)
    {
        var (_, message) = HortaService.Plant(_plot, seedId);
        Close();
        _done(message);
    }
}
