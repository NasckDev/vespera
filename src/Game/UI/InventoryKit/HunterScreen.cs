using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>
/// Base das telas de progressão (Inventário, Forja, Personagem): abre sobreposta ao jogo pausando a árvore (e despausando só se foi
/// ela quem pausou), volta com cancelar e se atualiza quando a progressão muda.
/// </summary>
public abstract partial class HunterScreen : UiScreen
{
    private bool _pausedByThisScreen;

    /// <summary>Abre a tela sobre a cena atual. Pausa o jogo se ainda não estiver pausado.</summary>
    public static void Open(HunterScreen screen)
    {
        if (!Progression.IsActive)
        {
            Log.Warn($"{screen.GetType().Name}: sem sessão ativa, nada a mostrar.");
            screen.Free();
            return;
        }

        var tree = SceneRouter.Instance.GetTree();
        screen._pausedByThisScreen = !tree.Paused;
        tree.Paused = true;
        SceneRouter.Instance.Push(screen);
    }

    public override void _Ready()
    {
        Progression.Changed += OnProgressionChanged;
        base._Ready();
    }

    public override void _ExitTree() => Progression.Changed -= OnProgressionChanged;

    protected override void Back() => Close();

    /// <summary>Fecha a tela e devolve o jogo ao estado de antes.</summary>
    public void Close()
    {
        if (_pausedByThisScreen)
        {
            GetTree().Paused = false;
        }

        SceneRouter.Instance.Pop();
    }

    /// <summary>A progressão mudou enquanto a tela estava aberta: refaça o que for preciso.</summary>
    protected virtual void OnProgressionChanged()
    {
    }

    /// <summary>Contornos reutilizados: painel de rodapé com botões centralizados.</summary>
    protected static HBoxContainer MakeFooter()
    {
        var footer = UiKit.MakeRow(30);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        return footer;
    }
}
