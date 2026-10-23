using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Base das telas de menu: ocupa a viewport inteira, funciona com a árvore pausada, volta com <c>ui_cancel</c>
/// e devolve o foco ao controle certo quando outra tela sobreposta fecha. Navegação por teclado, controle e mouse.
/// </summary>
public abstract partial class UiScreen : Control
{
    private const ulong QuietMilliseconds = 300;
    private static ulong _quietUntil;

    private Control? _savedFocus;

    /// <summary>Se falso, <c>ui_cancel</c> não faz nada (ex.: o título).</summary>
    protected virtual bool CanGoBack => true;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        _quietUntil = Time.GetTicksMsec() + QuietMilliseconds;
        Build();
        CallDeferred(nameof(FocusDefault));
    }

    /// <summary>
    /// Botão direito do mouse volta, em qualquer ponto da tela. Fica em <c>_Input</c> porque os painéis (que param o mouse)
    /// engolem o clique antes do <c>_UnhandledInput</c>. Telas que capturam botões (Controles) sobrescrevem <c>_Input</c>.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }
            || !CanGoBack || !IsVisibleInTree() || !SceneRouter.Instance.IsActive(this))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        UiSfx.Play(UiSound.Back);
        Back();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!CanGoBack || !@event.IsActionPressed("ui_cancel") || !IsVisibleInTree() || !SceneRouter.Instance.IsActive(this))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        UiSfx.Play(UiSound.Back);
        Back();
    }

    /// <summary>Som de mover o foco; calado logo após abrir uma tela, para o foco inicial não "clicar".</summary>
    public static void PlayMoveSound()
    {
        if (Time.GetTicksMsec() >= _quietUntil)
        {
            UiSfx.Play(UiSound.Move);
        }
    }

    public void RememberFocus(Control? focus) => _savedFocus = focus;

    public void RestoreFocus()
    {
        if (_savedFocus is { } saved && IsInstanceValid(saved) && saved.IsVisibleInTree() && IsAncestorOf(saved))
        {
            saved.GrabFocus();
            return;
        }

        FocusDefault();
    }

    /// <summary>
    /// Esqueleto das telas de página: fundo com a key art, título grande, painel ornamentado que ocupa o resto
    /// e a coluna onde o rodapé (botões) deve ser acrescentado depois do painel.
    /// </summary>
    protected PanelContainer BuildPage(string title, out VBoxContainer column, int sideMargin = 220)
    {
        AddChild(UiKit.MakeBackdrop(showArt: true));
        var root = UiKit.MakeMargin(sideMargin, 20, sideMargin, 28);
        AddChild(root);
        column = UiKit.MakeColumn(14);
        root.AddChild(column);
        column.AddChild(UiKit.MakeLabel(title, UiTheme.TitleVariation, HorizontalAlignment.Center));
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(panel);
        return panel;
    }

    /// <summary>Monta os controles da tela (chamado uma vez em <c>_Ready</c>).</summary>
    protected abstract void Build();

    /// <summary>Controle que recebe o foco ao abrir a tela.</summary>
    protected abstract Control? DefaultFocus();

    protected virtual void Back() => SceneRouter.Instance.Pop();

    protected void FocusDefault() => DefaultFocus()?.GrabFocus();
}
